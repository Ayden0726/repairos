import { NextResponse } from "next/server";
import { prisma } from "@/lib/db";
import { requireUser } from "@/lib/session";
import { customerSchema } from "@/lib/validations";

export async function GET(req: Request) {
  const session = await requireUser();
  if (!session?.userId) {
    return NextResponse.json({ error: "Unauthorised" }, { status: 401 });
  }

  const { searchParams } = new URL(req.url);
  const q = (searchParams.get("q") || "").trim();

  const customers = await prisma.customer.findMany({
    where: {
      archivedAt: null,
      ...(q
        ? {
            OR: [
              { firstName: { contains: q, mode: "insensitive" } },
              { lastName: { contains: q, mode: "insensitive" } },
              { phone: { contains: q } },
              { email: { contains: q, mode: "insensitive" } },
            ],
          }
        : {}),
    },
    orderBy: [{ updatedAt: "desc" }],
    take: 50,
    include: { _count: { select: { devices: true, tickets: true } } },
  });

  return NextResponse.json({ customers });
}

export async function POST(req: Request) {
  const session = await requireUser();
  if (!session?.userId) {
    return NextResponse.json({ error: "Unauthorised" }, { status: 401 });
  }

  const body = await req.json().catch(() => null);
  const parsed = customerSchema.safeParse(body);
  if (!parsed.success) {
    return NextResponse.json(
      { error: "Validation failed", details: parsed.error.flatten() },
      { status: 400 },
    );
  }

  const customer = await prisma.customer.create({
    data: {
      firstName: parsed.data.firstName,
      lastName: parsed.data.lastName || "",
      phone: parsed.data.phone,
      email: parsed.data.email,
      notes: parsed.data.notes,
    },
  });

  return NextResponse.json({ customer }, { status: 201 });
}
