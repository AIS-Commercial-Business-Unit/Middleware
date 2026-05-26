import { NextRequest, NextResponse } from "next/server";

// ACTIVE_BACKEND env var controls which stack handles requests.
// java (default) → Java/Camel on port 8081
// dotnet         → .NET/NServiceBus on port 8181
const ACTIVE_BACKEND = process.env.ACTIVE_BACKEND ?? "java";

const POLICY_ISSUANCE_URL =
  ACTIVE_BACKEND === "dotnet"
    ? (process.env.DOTNET_POLICY_ISSUANCE_URL ?? "http://localhost:8181")
    : (process.env.POLICY_ISSUANCE_SERVICE_URL ?? "http://localhost:8081");

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
      headers: {
        "Content-Type": "application/json",
        "X-Active-Backend": ACTIVE_BACKEND,
      },
    });
  } catch (err: unknown) {
    const message = err instanceof Error ? err.message : "Unknown error";
    return NextResponse.json(
      { error: `Backend unavailable (${ACTIVE_BACKEND}): ${message}` },
      { status: 503 }
    );
  }
}
