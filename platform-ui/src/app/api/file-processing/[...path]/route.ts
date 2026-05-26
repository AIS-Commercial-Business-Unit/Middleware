import { NextRequest, NextResponse } from "next/server";
import { getFileProcessingServiceUrl } from "@/lib/backend";

export async function GET(req: NextRequest, { params }: { params: Promise<{ path: string[] }> }) {
  const { path } = await params;
  const joined = path.join("/");
  const search = req.nextUrl.search;
  try {
    const fileProcessingUrl = getFileProcessingServiceUrl();
    const upstream = await fetch(`${fileProcessingUrl}/api/v1/${joined}${search}`, {
      headers: { "Content-Type": "application/json" },
      next: { revalidate: 0 },
    });
    const data = await upstream.json();
    return NextResponse.json(data, { status: upstream.status });
  } catch {
    return NextResponse.json({ error: "File processing service unavailable" }, { status: 503 });
  }
}

export async function POST(req: NextRequest, { params }: { params: Promise<{ path: string[] }> }) {
  const { path } = await params;
  const joined = path.join("/");
  const search = req.nextUrl.search;
  const body = await req.text();
  try {
    const fileProcessingUrl = getFileProcessingServiceUrl();
    const upstream = await fetch(`${fileProcessingUrl}/api/v1/${joined}${search}`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body,
    });
    const data = await upstream.json();
    return NextResponse.json(data, { status: upstream.status });
  } catch {
    return NextResponse.json({ error: "File processing service unavailable" }, { status: 503 });
  }
}
