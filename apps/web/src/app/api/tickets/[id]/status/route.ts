import { NextResponse } from "next/server";
import { prisma } from "@/lib/db";
import { requireUser } from "@/lib/session";
import { statusChangeSchema } from "@/lib/validations";
import { createStatusHistory } from "@/lib/tickets";

export async function POST(
  req: Request,
  ctx: { params: Promise<{ id: string }> },
) {
  const session = await requireUser();
  if (!session?.userId) {
    return NextResponse.json({ error: "Unauthorised" }, { status: 401 });
  }
  const { id } = await ctx.params;
  const body = await req.json().catch(() => null);
  const parsed = statusChangeSchema.safeParse(body);
  if (!parsed.success) {
    return NextResponse.json({ error: "Invalid status" }, { status: 400 });
  }

  const ticket = await prisma.ticket.findFirst({
    where: { id, archivedAt: null },
  });
  if (!ticket) {
    return NextResponse.json({ error: "Not found" }, { status: 404 });
  }
  if (ticket.status === parsed.data.status) {
    return NextResponse.json({ error: "Status is unchanged" }, { status: 400 });
  }

  const updated = await prisma.$transaction(async (tx) => {
    const t = await tx.ticket.update({
      where: { id },
      data: { status: parsed.data.status },
    });
    await createStatusHistory(tx, {
      ticketId: id,
      actorId: session.userId!,
      fromStatus: ticket.status,
      toStatus: parsed.data.status,
      comment: parsed.data.comment,
    });
    return t;
  });

  return NextResponse.json({
    ticket: {
      ...updated,
      labourFeeCents: updated.labourFeeCents.toString(),
      estimateCents: updated.estimateCents?.toString() ?? null,
    },
  });
}
