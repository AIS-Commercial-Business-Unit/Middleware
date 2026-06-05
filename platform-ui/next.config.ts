import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  output: "standalone", // Required for Docker — produces self-contained server.js

  // Proxy internal observability tools through Next.js in cloud deployments.
  // In local dev these paths are never hit (env vars default to localhost URLs).
  // In cloud, set NEXT_PUBLIC_GRAFANA_URL=/proxy/grafana and NEXT_PUBLIC_KAFDROP_URL=/proxy/kafdrop.
  async rewrites() {
    return [
      {
        source: "/proxy/grafana/:path*",
        destination: `${process.env.GRAFANA_URL || "http://grafana:3000"}/:path*`,
      },
      {
        source: "/proxy/kafdrop/:path*",
        destination: `${process.env.KAFDROP_URL || "http://kafdrop:9000"}/:path*`,
      },
      {
        source: "/proxy/prometheus/:path*",
        destination: `${process.env.PROMETHEUS_URL || "http://prometheus:9090"}/:path*`,
      },
    ];
  },
};

export default nextConfig;
