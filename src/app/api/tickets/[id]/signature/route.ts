import { NextResponse } from "next/server";
import { requireActor } from "@/server/actor";
import { prisma } from "@/server/db";
import { saveUpload } from "@/server/storage";
import { toPublicError } from "@/server/errors";

export async function POST(req: Request, ctx: { params: Promise<{ id: string }> }) {
  try {
    const actor = await requireActor();
    const { id } = await ctx.params;
    const body = (await req.json()) as { image: string; purpose?: string };
    const raw = body.image.split(",")[1] ?? "";
    const buffer = Buffer.from(raw, "base64");
    const stored = await saveUpload(buffer, `signature-${id}.png`, "image/png");
    const sig = await prisma.signature.create({
      data: {
        purpose: (body.purpose as "PICKUP") ?? "PICKUP",
        ticketId: id,
        staffId: actor.id,
        imageKey: stored.key,
        documentVersion: "1",
      },
    });
    await prisma.ticketEvent.create({
      data: {
        ticketId: id,
        actorId: actor.id,
        type: "signature",
        summary: "Pickup signature captured",
        visibility: "CUSTOMER",
      },
    });
    return NextResponse.json({ id: sig.id });
  } catch (error) {
    const pub = toPublicError(error);
    return NextResponse.json(pub, { status: pub.status });
  }
}
