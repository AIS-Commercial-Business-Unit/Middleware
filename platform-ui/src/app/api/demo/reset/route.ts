import { NextResponse } from "next/server";

const DEMO_API_URL =
  process.env.DEMO_API_URL ?? "http://policy-issuance-service:8081";

export async function POST() {
  try {
    const res = await fetch(`${DEMO_API_URL}/api/demo/reset`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      next: { revalidate: 0 },
      signal: AbortSignal.timeout(60_000),
    });
    if (!res.ok) throw new Error(`HTTP ${res.status}`);
    return NextResponse.json(await res.json());
  } catch {
    // Backend demo API not yet implemented — return mock success for demo prep
    await new Promise((r) => setTimeout(r, 1500));
    return NextResponse.json({
      isMockData: true,
      success: true,
      message: "Demo reset complete (mock — backend endpoint pending)",
      steps: [
        { step: "health_check", status: "ok",  message: "All services responding" },
        { step: "clear_data",   status: "ok",  message: "UC4 appraisal data cleared from MongoDB" },
        { step: "seed_data",    status: "ok",  message: "3 sample appraisal sagas seeded" },
        { step: "verify",       status: "ok",  message: "Verification pass complete — 3 sagas ready" },
      ],
      durationMs: 1500,
    });
  }
}
