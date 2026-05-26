import { NextResponse } from "next/server";

// Returns the active backend stack so the UI can show a badge.
export async function GET() {
  const backend = process.env.ACTIVE_BACKEND ?? "java";
  return NextResponse.json({ backend });
}
