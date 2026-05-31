import { NextRequest, NextResponse } from "next/server";

const APPRAISAL_URL =
  process.env.PRS_APPRAISAL_SERVICE_URL ?? "http://dotnet-prs-appraisal:8189";

export async function GET(req: NextRequest) {
  const policyNumber = req.nextUrl.searchParams.get("policyNumber");
  if (!policyNumber) {
    return NextResponse.json({ error: "policyNumber required" }, { status: 400 });
  }
  try {
    const res = await fetch(
      `${APPRAISAL_URL}/api/policies/${encodeURIComponent(policyNumber)}/appraisals/documents`,
      { signal: AbortSignal.timeout(35_000) }
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
