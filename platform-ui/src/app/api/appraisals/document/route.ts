import { NextRequest, NextResponse } from "next/server";

const APPRAISAL_URL =
  process.env.PRS_APPRAISAL_SERVICE_URL ?? "http://dotnet-prs-appraisal:8189";

export async function GET(req: NextRequest) {
  const documentKey = req.nextUrl.searchParams.get("documentKey");
  const sourceSystem = req.nextUrl.searchParams.get("sourceSystem") ?? "Mainframe";
  if (!documentKey) {
    return NextResponse.json({ error: "documentKey required" }, { status: 400 });
  }
  try {
    const res = await fetch(
      `${APPRAISAL_URL}/api/appraisals/documents/${encodeURIComponent(documentKey)}?sourceSystem=${sourceSystem}`,
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
