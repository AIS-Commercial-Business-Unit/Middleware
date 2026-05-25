import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  output: "standalone", // Required for Docker — produces self-contained server.js
};

export default nextConfig;
