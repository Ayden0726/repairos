import { NextResponse } from "next/server";
import { prisma } from "@/lib/db";
import { requireUser } from "@/lib/session";
import { ticketIntakeSchema } from "@/lib/validations";
import { dollarsToCents } from "@/lib/money";
import {
  createStatusHistory,
  ensureBusinessSettings,
  nextTicketNumber,
} from "@/lib/tickets";
import type { TicketStatus } from "@prisma/client";

function serializeTicket<T extends { labourFeeCents: bigint; estimateCents: bigint | null }>(
  t: T,
) {
  return {
    ...t,
    labourFeeCents: t.labourFeeCents.toString(),
    estimateCents: t.estimateCents?.toString() ?? null,
  };
}

export async function GET(req: Request) {
  const session = await requireUser();
  if (!session?.userId) {
    return NextResponse.json({ error: "Unauthorised" }, { status: 401 });
  }

  const { searchParams } = new URL(req.url);
  const q = (searchParams.get("q") || "").trim();
  const status = searchParams.get("status") as TicketStatus | null;

  const tickets = await prisma.ticket.findMany({
    where: {
      archivedAt: null,
      ...(status ? { status } : {}),
      ...(q
        ? {
            OR: [
              { ticketNumber: { contains: q, mode: "insensitive" } },
              { reportedFault: { contains: q, mode: "insensitive" } },
              { customer: { firstName: { contains: q, mode: "insensitive" } } },
              { customer: { lastName: { contains: q, mode: "insensitive" } } },
              { customer: { phone: { contains: q } } },
              { device: { imei: { contains: q } } },
              { device: { serial: { contains: q, mode: "insensitive" } } },
            ],
          }
        : {}),
    },
    include: {
      customer: true,
      device: true,
    },
    orderBy: { updatedAt: "desc" },
    take: 100,
  });

  return NextResponse.json({ tickets: tickets.map(serializeTicket) });
}

export async function POST(req: Request) {
  const session = await requireUser();
  if (!session?.userId) {
    return NextResponse.json({ error: "Unauthorised" }, { status: 401 });
  }

  await ensureBusinessSettings();
  const body = await req.json().catch(() => null);
  const parsed = ticketIntakeSchema.safeParse(body);
  if (!parsed.success) {
    return NextResponse.json(
      { error: "Validation failed", details: parsed.error.flatten() },
      { status: 400 },
    );
  }

  let labourFeeCents: bigint;
  let estimateCents: bigint | null = null;
  try {
    labourFeeCents = dollarsToCents(parsed.data.labourFeeAud);
    if (labourFeeCents < 0n) throw new Error("Labour fee cannot be negative");
    if (parsed.data.estimateAud) {
      estimateCents = dollarsToCents(parsed.data.estimateAud);
      if (estimateCents < 0n) throw new Error("Estimate cannot be negative");
    }
  } catch (e) {
    return NextResponse.json(
      { error: e instanceof Error ? e.message : "Invalid money" },
      { status: 400 },
    );
  }

  const customer = await prisma.customer.findFirst({
    where: { id: parsed.data.customerId, archivedAt: null },
  });
  if (!customer) {
    return NextResponse.json({ error: "Customer not found" }, { status: 404 });
  }

  try {
    const ticket = await prisma.$transaction(async (tx) => {
      let deviceId = parsed.data.deviceId;
      if (!deviceId) {
        if (!parsed.data.deviceBrand || !parsed.data.deviceModel || !parsed.data.deviceCategory) {
          throw new Error("Device brand, model and category are required when creating a device");
        }
        const device = await tx.device.create({
          data: {
            customerId: customer.id,
            category: parsed.data.deviceCategory,
            brand: parsed.data.deviceBrand,
            model: parsed.data.deviceModel,
            imei: parsed.data.deviceImei || null,
            serial: parsed.data.deviceSerial || null,
          },
        });
        deviceId = device.id;
      } else {
        const device = await tx.device.findFirst({
          where: { id: deviceId, customerId: customer.id, archivedAt: null },
        });
        if (!device) throw new Error("Device not found for this customer");
      }

      const ticketNumber = await nextTicketNumber(tx);
      const created = await tx.ticket.create({
        data: {
          ticketNumber,
          customerId: customer.id,
          deviceId,
          status: "received",
          reportedFault: parsed.data.reportedFault,
          conditionPowersOn: parsed.data.conditionPowersOn ?? null,
          conditionCracks: parsed.data.conditionCracks ?? null,
          conditionScratches: parsed.data.conditionScratches ?? null,
          conditionLiquid: parsed.data.conditionLiquid ?? null,
          conditionNotes: parsed.data.conditionNotes || null,
          accessoryCharger: parsed.data.accessoryCharger,
          accessorySim: parsed.data.accessorySim,
          accessoryCase: parsed.data.accessoryCase,
          accessoryOther: parsed.data.accessoryOther || null,
          labourFeeCents,
          estimateCents,
          createdById: session.userId!,
        },
        include: { customer: true, device: true },
      });

      await createStatusHistory(tx, {
        ticketId: created.id,
        actorId: session.userId!,
        fromStatus: null,
        toStatus: "received",
        comment: "Ticket received",
      });

      return created;
    });

    return NextResponse.json({ ticket: serializeTicket(ticket) }, { status: 201 });
  } catch (e) {
    return NextResponse.json(
      { error: e instanceof Error ? e.message : "Could not create ticket" },
      { status: 400 },
    );
  }
}
