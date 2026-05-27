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

          if (!messageType || !from || !to) continue;

          const normalizedTopic = String(topic ?? "");
          const normalizedDirection = direction === "consumed" ? "consumed" : "published";
          const normalizedStack = stack === "dotnet" ? "dotnet" : "java";
          const normalizedMessageType = String(messageType);

          events.push({
            messageType: normalizedMessageType,
            from: String(from),
            to: String(to),
            topic: normalizedTopic,
            direction: normalizedDirection,
            stack: normalizedStack,
            timestamp: ts,
            issuanceId: String(iid ?? issuanceId),
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

    // Dedup by messageType+from+to only — each Kafka hop is logged twice
    // (once as "published" by the sender, once as "consumed" by the receiver).
    // Both carry the same logical edge; we only want one arrow in the diagram.
    const seen = new Set<string>();
    const deduped = events.filter((event) => {
      const key = `${event.messageType}|${event.from}|${event.to}`;
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
