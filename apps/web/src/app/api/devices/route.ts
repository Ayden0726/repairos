import { NextResponse } from "next/server";
import { prisma } from "@/lib/db";
import { requireUser } from "@/lib/session";
import { deviceSchema } from "@/lib/validations";

export async function POST(req: Request) {
  const session = await requireUser();
  if (!session?.userId) {
    return NextResponse.json({ error: "Unauthorised" }, { status: 401 });
  }

  const body = await req.json().catch(() => null);
  const parsed = deviceSchema.safeParse(body);
  if (!parsed.success) {
    return NextResponse.json(
      { error: "Validation failed", details: parsed.error.flatten() },
      { status: 400 },
    );
  }

  const customer = await prisma.customer.findFirst({
    where: { id: parsed.data.customerId, archivedAt: null },
  });
  if (!customer) {
    return NextResponse.json({ error: "Customer not found" }, { status: 404 });
  }

  const device = await prisma.device.create({
    data: {
      customerId: parsed.data.customerId,
      category: parsed.data.category,
      brand: parsed.data.brand,
      model: parsed.data.model,
      colour: parsed.data.colour || null,
      storage: parsed.data.storage || null,
      imei: parsed.data.imei || null,
      serial: parsed.data.serial || null,
      notes: parsed.data.notes || null,
    },
  });

  return NextResponse.json({ device }, { status: 201 });
}
