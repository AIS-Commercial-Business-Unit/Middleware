import { NextRequest, NextResponse } from "next/server";
import { getActiveBackendFromRequest, getPolicyIssuanceServiceUrl } from "@/lib/backend";

export async function POST(req: NextRequest) {
  const body = await req.text();
  try {
    const backend = getActiveBackendFromRequest(req);
    const policyIssuanceUrl = getPolicyIssuanceServiceUrl(backend);
    const res = await fetch(`${policyIssuanceUrl}/api/v1/policies/issue`, {
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
