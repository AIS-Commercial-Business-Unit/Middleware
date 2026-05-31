import { NextRequest, NextResponse } from "next/server";

const INTEGRATION_SERVICE_URL =
  process.env.INTEGRATION_SERVICE_URL ?? "http://platform-integration-service:8084";

type Params = { path: string[] };

export async function GET(req: NextRequest, { params }: { params: Promise<Params> }) {
  const { path } = await params;
  return proxy(req, "GET", path, null);
}

export async function POST(req: NextRequest, { params }: { params: Promise<Params> }) {
  const { path } = await params;
  const body = await req.text();
  return proxy(req, "POST", path, body);
}

async function proxy(
  _req: NextRequest,
  method: string,
  pathSegments: string[],
  body: string | null
): Promise<NextResponse> {
  const upstream = `${INTEGRATION_SERVICE_URL}/api/demo/${pathSegments.join("/")}`;
  try {
    const res = await fetch(upstream, {
      method,
      headers: { "Content-Type": "application/json" },
      ...(body ? { body } : {}),
      signal: AbortSignal.timeout(30_000), // resets can take a few seconds
    });
    const data = await res.text();
    return new NextResponse(data, {
      status: res.status,
      headers: { "Content-Type": "application/json" },
    });
  } catch (err) {
    const message = err instanceof Error ? err.message : String(err);
    return NextResponse.json(
      { error: "Demo reset service unavailable", detail: message },
      { status: 503 }
    );
  }
}
