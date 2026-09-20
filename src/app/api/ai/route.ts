import { NextResponse } from "next/server";
import { requireActor } from "@/server/actor";
import { aiAssist } from "@/server/services/ai.service";
import { toPublicError } from "@/server/errors";

export async function POST(req: Request) {
  try {
    const actor = await requireActor();
    const body = (await req.json()) as { intent: "diagnose" | "price" | "summary" | "sms" | "kb"; ticketId?: string; prompt?: string };
    const result = await aiAssist(actor, body);
    return NextResponse.json(result);
  } catch (error) {
    const pub = toPublicError(error);
    return NextResponse.json(pub, { status: pub.status });
  }
}
