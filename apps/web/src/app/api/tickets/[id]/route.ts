import { NextResponse } from "next/server";
import { prisma } from "@/lib/db";
import { requireUser } from "@/lib/session";

export async function GET(
  _req: Request,
  ctx: { params: Promise<{ id: string }> },
) {
  const session = await requireUser();
  if (!session?.userId) {
    return NextResponse.json({ error: "Unauthorised" }, { status: 401 });
  }
  const { id } = await ctx.params;

  const ticket = await prisma.ticket.findFirst({
    where: { id, archivedAt: null },
    include: {
      customer: true,
      device: true,
      createdBy: { select: { id: true, displayName: true, email: true } },
      notes: {
        orderBy: { createdAt: "desc" },
        include: { author: { select: { displayName: true } } },
      },
      statusHistory: {
        orderBy: { createdAt: "desc" },
        include: { actor: { select: { displayName: true } } },
      },
      photos: { orderBy: { createdAt: "desc" } },
    },
  });

  if (!ticket) {
    return NextResponse.json({ error: "Not found" }, { status: 404 });
  }

  return NextResponse.json({
    ticket: {
      ...ticket,
      labourFeeCents: ticket.labourFeeCents.toString(),
      estimateCents: ticket.estimateCents?.toString() ?? null,
    },
  });
}
