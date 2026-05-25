import { NextRequest, NextResponse } from "next/server";

const LOKI_URL = process.env.LOKI_URL ?? "http://loki:3100";

export interface LogEntry {
  service: string;
  ts: string;        // ISO-8601 timestamp
  level: string;
  message: string;
}

export async function GET(req: NextRequest) {
  const { searchParams } = new URL(req.url);
  const issuanceId = searchParams.get("issuanceId");
  const limit = parseInt(searchParams.get("limit") ?? "60", 10);

  if (!issuanceId) {
    return NextResponse.json({ error: "issuanceId required" }, { status: 400 });
  }

  // Loki expects nanosecond timestamps
  const end = Date.now() * 1_000_000;
  const start = (Date.now() - 60 * 60 * 1000) * 1_000_000; // last 60 minutes

  // Query all streams with a service_name label (Promtail-scraped), filtering for any line containing the issuanceId
  const query = `{service_name=~".+"} |= \`${issuanceId}\``;
  const params = new URLSearchParams({
    query,
    start: String(start),
    end: String(end),
    limit: String(limit),
    direction: "forward",
  });

  try {
    const res = await fetch(`${LOKI_URL}/loki/api/v1/query_range?${params}`, {
      cache: "no-store",
      headers: { Accept: "application/json" },
    });

    if (!res.ok) {
      const body = await res.text();
      return NextResponse.json({ error: `Loki ${res.status}: ${body}` }, { status: res.status });
    }

    const raw = await res.json();
    const entries: LogEntry[] = [];

    for (const stream of (raw?.data?.result ?? [])) {
      const service = stream?.stream?.service_name ?? stream?.stream?.job ?? "unknown";
      for (const [tsNs, line] of (stream?.values ?? [])) {
        // Timestamp: Loki provides nanoseconds since epoch as a string.
        // Slice to 13 digits (milliseconds) to avoid BigInt requirement.
        const tsMs = parseInt(String(tsNs).slice(0, 13), 10);
        const ts = new Date(tsMs).toISOString();
        let message = line as string;
        let level = "INFO";

        // Best-effort JSON parse to extract structured fields
        try {
          const parsed = JSON.parse(message);
          message = parsed.message ?? parsed["@m"] ?? parsed.msg ?? message;
          level = parsed.level ?? parsed["@l"] ?? "INFO";
        } catch {
          // raw text line — leave as-is
        }

        entries.push({ service, ts, level, message });
      }
    }

    // Sort by timestamp ascending
    entries.sort((a, b) => a.ts.localeCompare(b.ts));
    return NextResponse.json({ entries });
  } catch (err: unknown) {
    const message = err instanceof Error ? err.message : "Unknown error";
    return NextResponse.json(
      { error: `Loki unreachable: ${message}` },
      { status: 503 }
    );
  }
}
