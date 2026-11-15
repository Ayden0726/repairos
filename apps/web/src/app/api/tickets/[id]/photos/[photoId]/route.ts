import { NextResponse } from "next/server";
import { readFile } from "fs/promises";
import path from "path";
import { prisma } from "@/lib/db";
import { requireUser } from "@/lib/session";
import { uploadRoot } from "@/lib/uploads";

export async function GET(
  _req: Request,
  ctx: { params: Promise<{ id: string; photoId: string }> },
) {
  const session = await requireUser();
  if (!session?.userId) {
    return NextResponse.json({ error: "Unauthorised" }, { status: 401 });
  }
  const { id, photoId } = await ctx.params;
  const photo = await prisma.ticketPhoto.findFirst({
    where: { id: photoId, ticketId: id },
  });
  if (!photo) {
    return NextResponse.json({ error: "Not found" }, { status: 404 });
  }

  // Prevent path traversal — storedName is UUID + ext only
  if (photo.storedName.includes("..") || photo.storedName.includes("/") || photo.storedName.includes("\\")) {
    return NextResponse.json({ error: "Invalid file" }, { status: 400 });
  }

  const filePath = path.join(uploadRoot(), "tickets", photo.storedName);
  try {
    const buf = await readFile(filePath);
    return new NextResponse(buf, {
      headers: {
        "Content-Type": photo.contentType,
        "Content-Length": String(buf.length),
        "Cache-Control": "private, max-age=3600",
        "X-Content-Type-Options": "nosniff",
      },
    });
  } catch {
    return NextResponse.json({ error: "File missing" }, { status: 404 });
  }
}
