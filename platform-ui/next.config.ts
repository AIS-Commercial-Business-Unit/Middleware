import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  output: "standalone", // Required for Docker — produces self-contained server.js
  // Note: Proxying to internal observability tools is handled by API routes
  // at /proxy/grafana/* and /proxy/kafdrop/* (see src/app/proxy/).
  // Rewrites can't be used because env var interpolation in next.config.ts
  // is resolved at build time, not runtime.
};

export default nextConfig;
