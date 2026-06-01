import { NextRequest, NextResponse } from "next/server";
import { getEventDescription, type FlowEvent } from "@/types/eda-flow";

const LOKI_URL = process.env.LOKI_URL ?? "http://loki:3100";

export async function GET(
  _req: NextRequest,
  { params }: { params: Promise<{ issuanceId: string }> }
) {
  const { issuanceId } = await params;
  const end = Date.now() * 1_000_000;
  const start = (Date.now() - 2 * 60 * 60 * 1000) * 1_000_000;

  const query = `{service_name=~".+"} |= \`EDA_FLOW\` |= \`${issuanceId}\``;
  const qs = new URLSearchParams({
    query,
    start: String(start),
    end: String(end),
    limit: "200",
    direction: "forward",
  });

  try {
    const res = await fetch(`${LOKI_URL}/loki/api/v1/query_range?${qs}`, {
      cache: "no-store",
      headers: { Accept: "application/json" },
    });

    if (!res.ok) {
      const body = await res.text();
      return NextResponse.json({ error: `Loki ${res.status}: ${body}` }, { status: res.status });
    }

    const raw = await res.json();
    const events: FlowEvent[] = [];

    for (const stream of raw?.data?.result ?? []) {
      for (const [tsNs, line] of stream?.values ?? []) {
        const tsMs = parseInt(String(tsNs).slice(0, 13), 10);
        const ts = new Date(tsMs).toISOString();

        try {
          const parsed = JSON.parse(line as string);
          const edaEvent = parsed["EDA_Event"] ?? parsed?.Properties?.EDA_Event;
          if (edaEvent !== "EDA_FLOW") continue;

          const messageType = parsed["EDA_MessageType"] ?? parsed?.Properties?.EDA_MessageType;
          const from = parsed["EDA_From"] ?? parsed?.Properties?.EDA_From;
          const to = parsed["EDA_To"] ?? parsed?.Properties?.EDA_To;
          const topic = parsed["EDA_Topic"] ?? parsed?.Properties?.EDA_Topic;
          const direction = parsed["EDA_Direction"] ?? parsed?.Properties?.EDA_Direction;
          const stack = parsed["EDA_Stack"] ?? parsed?.Properties?.EDA_Stack;
          const iid = parsed["EDA_IssuanceId"] ?? parsed?.Properties?.EDA_IssuanceId;
          const handler = parsed["EDA_Handler"] ?? parsed?.Properties?.EDA_Handler;
          const rawPayload = parsed["EDA_Payload"] ?? parsed?.Properties?.EDA_Payload;
          let payload: Record<string, unknown> | undefined;
          try {
            if (rawPayload && typeof rawPayload === "string") {
              payload = JSON.parse(rawPayload);
            } else if (rawPayload && typeof rawPayload === "object") {
              payload = rawPayload as Record<string, unknown>;
            }
          } catch {
            payload = undefined;
          }

          if (!messageType || !from || !to) continue;

          const normalizedTopic = String(topic ?? "");
          const normalizedDirection =
            direction === "handled" ? "handled" : direction === "consumed" ? "consumed" : "published";
          const normalizedStack = stack === "dotnet" ? "dotnet" : "java";
          const normalizedMessageType = String(messageType);
          const normalizedHandler = handler ? String(handler) : undefined;
          // EDA_To already contains the resolved participant ID set by the behavior.
          // Do NOT override it with the raw handler class name — that caused duplicate
          // lifelines (e.g. both "MainframeListAggregatorSaga" and "MainframeListAggregator").
          const normalizedTo = String(to);

          events.push({
            messageType: normalizedMessageType,
            from: String(from),
            to: normalizedTo,
            topic: normalizedTopic,
            direction: normalizedDirection,
            stack: normalizedStack,
            timestamp: ts,
            issuanceId: String(iid ?? issuanceId),
            handler: normalizedHandler,
            payload,
            details: {
              topic: normalizedTopic,
              direction: normalizedDirection,
              stack: normalizedStack,
              timestamp: ts,
              description: getEventDescription(normalizedMessageType),
            },
          });
        } catch {
          // skip non-JSON or non-EDA lines
        }
      }
    }

    // NServiceBus routing artefacts — must never appear as diagram participants.
    const SUPPRESSED_PARTICIPANTS = new Set(["broadcast", "allsubscribers"]);
    const isSuppressed = (id: string) => SUPPRESSED_PARTICIPANTS.has(id.toLowerCase());

    // Internal saga scheduling artefacts — timeout messages are an implementation
    // detail of NServiceBus saga timeouts and must never appear as diagram steps.
    const SUPPRESSED_MESSAGE_TYPES = new Set([
      "DocumentListSagaTimeoutMessage",
      "DocumentRetrievalSagaTimeoutMessage",
      "MainframeListAggregatorTimeoutMessage",
      "MainframeDocumentAggregatorTimeoutMessage",
    ]);
    const isMessageSuppressed = (mt: string) => SUPPRESSED_MESSAGE_TYPES.has(mt);

    // Remove any events where either endpoint is a suppressed routing artefact
    // or where the message type is an internal saga timeout.
    const cleanEvents = events.filter(
      (event) => !isSuppressed(event.from) && !isSuppressed(event.to) && !isMessageSuppressed(event.messageType)
    );

    // Build the set of messageType|from keys that have real handled entries so
    // we can drop redundant consumed/published duplicates.
    const handledKeys = new Set(
      cleanEvents
        .filter((event) => event.direction === "handled")
        .map((event) => `${event.messageType}|${event.from}`)
    );

    // These message types fire once per part/chunk so all instances must appear
    // on the diagram — deduplication by messageType|from|to would collapse them.
    const MULTI_INSTANCE_MESSAGE_TYPES = new Set([
      "MainframeAppraisalListPartReceivedEvent",
      "MainframeDocumentChunkReceivedEvent",
      "MqDocumentChunk",
    ]);

    const seen = new Set<string>();
    const deduped = cleanEvents.filter((event) => {
      const baseKey = `${event.messageType}|${event.from}`;

      // Multi-instance events: each occurrence is meaningful — never collapse them.
      if (MULTI_INSTANCE_MESSAGE_TYPES.has(event.messageType)) return true;

      if (event.direction === "handled") {
        const key = `${baseKey}|${event.to}|handled`;
        if (seen.has(key)) return false;
        seen.add(key);
        return true;
      }

      if (event.direction === "published") {
        // Suppress when a concrete handled entry already shows the same message.
        if (handledKeys.has(baseKey)) return false;
        const key = `${baseKey}|published`;
        if (seen.has(key)) return false;
        seen.add(key);
        return true;
      }

      // consumed — skip when a handled entry covers the same message/sender
      if (handledKeys.has(baseKey)) return false;
      if (seen.has(`${baseKey}|published`)) return false;

      const key = `${baseKey}|${event.to}|consumed`;
      if (seen.has(key)) return false;
      seen.add(key);
      return true;
    });

    deduped.sort((a, b) => a.timestamp.localeCompare(b.timestamp));

    return NextResponse.json({ events: deduped });
  } catch (err: unknown) {
    const message = err instanceof Error ? err.message : "Unknown error";
    return NextResponse.json({ error: `Loki unreachable: ${message}` }, { status: 503 });
  }
}
