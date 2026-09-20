import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  output: "standalone",
  serverExternalPackages: ["argon2", "@prisma/client", "pdfkit", "ioredis", "bullmq", "sharp"],
  typedRoutes: true,
};

export default nextConfig;
