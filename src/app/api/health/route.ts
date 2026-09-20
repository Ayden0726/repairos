import { NextRequest, NextResponse } from "next/server";
import { toPublicError } from "@/server/errors";
import { runHealthChecks } from "@/server/services/health.service";

export async function GET(_req: NextRequest) {
  try {
    const health = await runHealthChecks();
    const status = health.overall === "CRITICAL" ? 503 : 200;
    return NextResponse.json(health, { status });
  } catch (error) {
    const pub = toPublicError(error);
    return NextResponse.json(pub, { status: pub.status });
  }
}
