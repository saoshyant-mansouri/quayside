import type { NextConfig } from "next";

const apiUrl = (process.env.API_URL ?? "http://localhost:8080").replace(/\/+$/, "");

if (process.env.VERCEL && !process.env.API_URL) {
  throw new Error("API_URL must be set in the Vercel project environment");
}

const nextConfig: NextConfig = {
  compress: false,
  async rewrites() {
    return [{ source: "/api/:path*", destination: `${apiUrl}/api/:path*` }];
  },
};

export default nextConfig;
