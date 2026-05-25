import { NextRequest, NextResponse } from "next/server";

const POLICY_ISSUANCE_URL =
  process.env.POLICY_ISSUANCE_SERVICE_URL ?? "http://localhost:8081";

export async function POST(req: NextRequest) {
  const body = await req.text();
  try {
    const res = await fetch(`${POLICY_ISSUANCE_URL}/api/v1/policies/issue`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body,
    });
    const data = await res.text();
    return new NextResponse(data, {
      status: res.status,
      headers: { "Content-Type": "application/json" },
    });
  } catch (err: unknown) {
    const message = err instanceof Error ? err.message : "Unknown error";
    return NextResponse.json({ error: `Backend unavailable: ${message}` }, { status: 503 });
  }
}
