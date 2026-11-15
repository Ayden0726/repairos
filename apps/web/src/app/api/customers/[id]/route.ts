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
  const customer = await prisma.customer.findFirst({
    where: { id, archivedAt: null },
    include: {
      devices: { where: { archivedAt: null }, orderBy: { updatedAt: "desc" } },
      tickets: {
        where: { archivedAt: null },
        orderBy: { updatedAt: "desc" },
        take: 20,
      },
    },
  });
  if (!customer) {
    return NextResponse.json({ error: "Not found" }, { status: 404 });
  }
  return NextResponse.json({
    customer: {
      ...customer,
      tickets: customer.tickets.map((t) => ({
        ...t,
        labourFeeCents: t.labourFeeCents.toString(),
        estimateCents: t.estimateCents?.toString() ?? null,
      })),
    },
  });
}
