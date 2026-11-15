import { NextResponse } from "next/server";
import { prisma } from "@/lib/db";
import { requireUser } from "@/lib/session";
import {
  MAX_PHOTOS_PER_TICKET,
  saveTicketPhoto,
} from "@/lib/uploads";

export async function POST(
  req: Request,
  ctx: { params: Promise<{ id: string }> },
) {
  const session = await requireUser();
  if (!session?.userId) {
    return NextResponse.json({ error: "Unauthorised" }, { status: 401 });
  }
  const { id } = await ctx.params;

  const ticket = await prisma.ticket.findFirst({
    where: { id, archivedAt: null },
    include: { _count: { select: { photos: true } } },
  });
  if (!ticket) {
    return NextResponse.json({ error: "Not found" }, { status: 404 });
  }
  if (ticket._count.photos >= MAX_PHOTOS_PER_TICKET) {
    return NextResponse.json(
      { error: `Maximum ${MAX_PHOTOS_PER_TICKET} photos per ticket` },
      { status: 400 },
    );
  }

  const form = await req.formData().catch(() => null);
  const file = form?.get("file");
  if (!(file instanceof File)) {
    return NextResponse.json({ error: "file is required" }, { status: 400 });
  }

  try {
    const saved = await saveTicketPhoto(file);
    const photo = await prisma.ticketPhoto.create({
      data: {
        ticketId: id,
        storedName: saved.storedName,
        originalName: saved.originalName,
        contentType: saved.contentType,
        sizeBytes: saved.sizeBytes,
        uploadedById: session.userId,
      },
    });
    return NextResponse.json({ photo }, { status: 201 });
  } catch (e) {
    return NextResponse.json(
      { error: e instanceof Error ? e.message : "Upload failed" },
      { status: 400 },
    );
  }
}
