import { NextRequest, NextResponse } from "next/server";

const APPRAISAL_URL =
  process.env.PRS_APPRAISAL_SERVICE_URL ?? "http://dotnet-prs-appraisal:8189";

export async function GET(req: NextRequest) {
  const requestId = req.nextUrl.searchParams.get("requestId");
  if (!requestId) {
    return NextResponse.json({ error: "requestId required" }, { status: 400 });
  }
  try {
    const res = await fetch(
      `${APPRAISAL_URL}/api/appraisals/flow-sagas/${encodeURIComponent(requestId)}`,
      { signal: AbortSignal.timeout(5000), cache: "no-store" }
    );
    if (!res.ok) {
      const text = await res.text();
      return NextResponse.json({ error: text }, { status: res.status });
    }
    return NextResponse.json(await res.json());
  } catch (err) {
    return NextResponse.json({ error: String(err) }, { status: 503 });
  }
}
