import { NextResponse } from "next/server";
import { requireActor } from "@/server/actor";
import { findTicketByScan } from "@/server/services/search.service";
import { toPublicError } from "@/server/errors";

export async function GET(req: Request) {
  try {
    const actor = await requireActor();
    const code = new URL(req.url).searchParams.get("code") ?? "";
    const ticket = await findTicketByScan(actor, code);
    return NextResponse.json(ticket ? { id: ticket.id } : { id: null });
  } catch (error) {
    const pub = toPublicError(error);
    return NextResponse.json(pub, { status: pub.status });
  }
}
