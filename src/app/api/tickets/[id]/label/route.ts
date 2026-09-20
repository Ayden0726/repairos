import { NextResponse } from "next/server";
import { requireActor } from "@/server/actor";
import { labelPng } from "@/server/services/document.service";
import { prisma } from "@/server/db";
import { toPublicError } from "@/server/errors";

export async function GET(_req: Request, ctx: { params: Promise<{ id: string }> }) {
  try {
    await requireActor();
    const { id } = await ctx.params;
    const ticket = await prisma.ticket.findUnique({
      where: { id },
      include: { customer: true, customerDevice: true, priority: true },
    });
    if (!ticket) return NextResponse.json({ error: "Not found" }, { status: 404 });
    const png = await labelPng(id);
    const html = `<!doctype html><html><body style="font-family:sans-serif;padding:16px">
      <h1 style="font-size:20px">${ticket.ticketNumber}</h1>
      <p>${ticket.customer.displayName}<br/>${ticket.customerDevice?.modelName ?? ""} · ${ticket.priority.name}</p>
      <img src="data:image/png;base64,${png.toString("base64")}" width="180" alt="QR" />
      <script>window.print()</script>
    </body></html>`;
    return new NextResponse(html, { headers: { "content-type": "text/html" } });
  } catch (error) {
    const pub = toPublicError(error);
    return NextResponse.json(pub, { status: pub.status });
  }
}
