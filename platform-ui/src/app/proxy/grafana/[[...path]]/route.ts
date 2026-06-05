import { NextRequest } from "next/server";

export const dynamic = "force-dynamic";

async function proxy(req: NextRequest, path: string[]) {
  const target = process.env.GRAFANA_URL || "http://middleware-grafana:80";
  const url = `${target}/${path.join("/")}${req.nextUrl.search}`;

  const headers = new Headers(req.headers);
  headers.delete("host");
  headers.delete("connection");

  const init: RequestInit = {
    method: req.method,
    headers,
    redirect: "manual",
  };

  if (req.method !== "GET" && req.method !== "HEAD") {
    init.body = await req.arrayBuffer();
  }

  try {
    const upstream = await fetch(url, init);
    const respHeaders = new Headers(upstream.headers);
    // Strip headers that block embedding
    respHeaders.delete("x-frame-options");
    respHeaders.delete("content-security-policy");
    return new Response(upstream.body, {
      status: upstream.status,
      statusText: upstream.statusText,
      headers: respHeaders,
    });
  } catch (err) {
    return new Response(`Proxy error: ${err instanceof Error ? err.message : String(err)}`, {
      status: 502,
    });
  }
}

export async function GET(req: NextRequest, ctx: { params: Promise<{ path?: string[] }> }) {
  const { path } = await ctx.params;
  return proxy(req, path || []);
}

export async function POST(req: NextRequest, ctx: { params: Promise<{ path?: string[] }> }) {
  const { path } = await ctx.params;
  return proxy(req, path || []);
}

export async function PUT(req: NextRequest, ctx: { params: Promise<{ path?: string[] }> }) {
  const { path } = await ctx.params;
  return proxy(req, path || []);
}

export async function DELETE(req: NextRequest, ctx: { params: Promise<{ path?: string[] }> }) {
  const { path } = await ctx.params;
  return proxy(req, path || []);
}

export async function PATCH(req: NextRequest, ctx: { params: Promise<{ path?: string[] }> }) {
  const { path } = await ctx.params;
  return proxy(req, path || []);
}
