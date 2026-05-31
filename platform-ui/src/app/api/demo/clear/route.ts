import { NextResponse } from "next/server";

const DEMO_API_URL =
  process.env.DEMO_API_URL ?? "http://policy-issuance-service:8081";

export async function POST() {
  try {
    const res = await fetch(`${DEMO_API_URL}/api/demo/clear`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      next: { revalidate: 0 },
      signal: AbortSignal.timeout(30_000),
    });
    if (!res.ok) throw new Error(`HTTP ${res.status}`);
    return NextResponse.json(await res.json());
  } catch {
    await new Promise((r) => setTimeout(r, 600));
    return NextResponse.json({
      isMockData: true,
      success: true,
      message: "UC4 data cleared (mock — backend endpoint pending)",
      cleared: { appraisalSagas: 3, issuanceSagas: 0 },
    });
  }
}
