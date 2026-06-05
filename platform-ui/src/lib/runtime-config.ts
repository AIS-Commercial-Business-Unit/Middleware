"use client";

/**
 * Runtime configuration injected by the server layout into window.__RUNTIME_CONFIG__.
 * This allows NEXT_PUBLIC_* env vars to be read at runtime (not just build time)
 * which is required for client components in Next.js standalone mode.
 */

interface RuntimeConfig {
  grafanaUrl: string;
  kafdropUrl: string;
}

const DEFAULTS: RuntimeConfig = {
  grafanaUrl: "http://localhost:3001",
  kafdropUrl: "http://localhost:9000",
};

export function getRuntimeConfig(): RuntimeConfig {
  if (typeof window !== "undefined" && (window as any).__RUNTIME_CONFIG__) {
    return (window as any).__RUNTIME_CONFIG__;
  }
  return DEFAULTS;
}
