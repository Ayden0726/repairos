import { NextResponse } from "next/server";
import { requireActor } from "@/server/actor";
import { changeStatus } from "@/server/services/ticket.service";
import { toPublicError } from "@/server/errors";

export async function POST(req: Request, ctx: { params: Promise<{ id: string }> }) {
  try {
    const actor = await requireActor();
    const { id } = await ctx.params;
    const body = (await req.json()) as { statusId: string };
    const ticket = await changeStatus(actor, id, body.statusId);
    return NextResponse.json({ ok: true, statusId: ticket.statusId });
  } catch (error) {
    const pub = toPublicError(error);
    return NextResponse.json(pub, { status: pub.status });
  }
}
