import { NextRequest, NextResponse } from "next/server";

const INTEGRATION_SERVICE_URL =
  process.env.INTEGRATION_SERVICE_URL ?? "http://platform-integration-service:8084";

export async function POST(req: NextRequest) {
  const body = await req.text();

  try {
    const res = await fetch(`${INTEGRATION_SERVICE_URL}/api/appraisal/status-update`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body,
      signal: AbortSignal.timeout(5000),
    });

    const data = await res.text();
    return new NextResponse(data, {
      status: res.status,
      headers: { "Content-Type": "application/json" },
    });
  } catch {
    // Integration service not yet implemented — return a stub saga for demo
    const payload = JSON.parse(body) as {
      inspectionId?: string;
      policyNumber?: string;
      statusCode?: number;
      inspectionTypeCode?: string;
    };

    const sagaId = crypto.randomUUID();
    const now = new Date().toISOString();

    const stub = {
      sagaId,
      correlationId: sagaId,
      inspectionId: payload.inspectionId ?? "INS-DEMO",
      policyNumber: payload.policyNumber ?? "POL-DEMO",
      statusCode: payload.statusCode ?? 6,
      inspectionTypeCode: payload.inspectionTypeCode ?? "A",
      status: "Initiated",
      isMockData: true,
      receivedAt: now,
    };

    return NextResponse.json(stub, { status: 202 });
  }
}
