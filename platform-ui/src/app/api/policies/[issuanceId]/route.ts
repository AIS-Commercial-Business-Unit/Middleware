import { NextRequest, NextResponse } from "next/server";

const POLICY_ISSUANCE_URL =
  process.env.POLICY_ISSUANCE_SERVICE_URL ?? "http://localhost:8081";

export async function GET(
  _req: NextRequest,
  { params }: { params: Promise<{ issuanceId: string }> }
) {
  const { issuanceId } = await params;
  try {
    const res = await fetch(
      `${POLICY_ISSUANCE_URL}/api/v1/policies/issue/${issuanceId}`,
      { cache: "no-store" }
    );
    if (res.status === 404) {
      return NextResponse.json({ error: "Saga not found" }, { status: 404 });
    }
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
