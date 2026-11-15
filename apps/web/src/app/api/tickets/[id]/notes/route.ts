import { NextResponse } from "next/server";
import { prisma } from "@/lib/db";
import { requireUser } from "@/lib/session";
import { noteSchema } from "@/lib/validations";

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
  const parsed = noteSchema.safeParse(body);
  if (!parsed.success) {
    return NextResponse.json({ error: "Note body is required" }, { status: 400 });
  }

  const ticket = await prisma.ticket.findFirst({
    where: { id, archivedAt: null },
  });
  if (!ticket) {
    return NextResponse.json({ error: "Not found" }, { status: 404 });
  }

  const note = await prisma.ticketNote.create({
    data: {
      ticketId: id,
      authorId: session.userId,
      body: parsed.data.body,
    },
    include: { author: { select: { displayName: true } } },
  });

  await prisma.ticket.update({
    where: { id },
    data: { updatedAt: new Date() },
  });

  return NextResponse.json({ note }, { status: 201 });
}
