import { NextResponse } from "next/server";
import { requireActor } from "@/server/actor";
import { revealCredential } from "@/server/services/ticket.service";
import { toPublicError } from "@/server/errors";

export async function POST(_req: Request, ctx: { params: Promise<{ id: string }> }) {
  try {
    const actor = await requireActor();
    const { id } = await ctx.params;
    const result = await revealCredential(actor, id);
    return NextResponse.json(result);
  } catch (error) {
    const pub = toPublicError(error);
    return NextResponse.json(pub, { status: pub.status });
  }
}
