import { NextRequest, NextResponse } from "next/server";

const VALID_BACKENDS = ["java", "dotnet"] as const;
type Backend = (typeof VALID_BACKENDS)[number];

export async function GET(req: NextRequest) {
  const cookie = req.cookies.get("active-backend")?.value;
  const backend: Backend =
    (VALID_BACKENDS.includes(cookie as Backend) ? (cookie as Backend) : null) ??
    (process.env.ACTIVE_BACKEND === "dotnet" ? "dotnet" : "java");
  return NextResponse.json({
    backend,
    label: backend === "dotnet" ? ".NET Stack" : "Java Stack",
  });
}

export async function POST(req: NextRequest) {
  const body = await req.json();
  const backend: string = body.backend;
  if (!VALID_BACKENDS.includes(backend as Backend)) {
    return NextResponse.json({ error: "Invalid backend" }, { status: 400 });
  }
  const res = NextResponse.json({
    backend,
    label: backend === "dotnet" ? ".NET Stack" : "Java Stack",
  });
  res.cookies.set("active-backend", backend, {
    httpOnly: false,
    maxAge: 60 * 60 * 24 * 30,
    sameSite: "lax",
    path: "/",
  });
  return res;
}
