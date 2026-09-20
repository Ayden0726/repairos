import { assertCan, type Actor } from "../actor";
import { prisma } from "../db";

export async function listBookings(actor: Actor, from: Date, to: Date) {
  assertCan(actor, "bookings.view");
  return prisma.booking.findMany({
    where: { startsAt: { gte: from, lte: to } },
    include: { customer: true, type: true, staff: { select: { id: true, name: true } } },
    orderBy: { startsAt: "asc" },
  });
}

export async function createBooking(
  actor: Actor,
  input: { customerId: string; typeId: string; staffId?: string; startsAt: string; endsAt: string; notes?: string },
) {
  assertCan(actor, "bookings.manage");
  return prisma.booking.create({
    data: {
      customerId: input.customerId,
      typeId: input.typeId,
      staffId: input.staffId,
      startsAt: new Date(input.startsAt),
      endsAt: new Date(input.endsAt),
      notes: input.notes,
    },
  });
}

export async function calendarEvents(actor: Actor, from: Date, to: Date) {
  assertCan(actor, "bookings.view");
  const [bookings, shifts, ticketDues, builds, pos] = await Promise.all([
    prisma.booking.findMany({
      where: { startsAt: { gte: from, lte: to } },
      include: { customer: true, type: true, staff: true },
    }),
    prisma.rosterShift.findMany({
      where: { startsAt: { gte: from, lte: to } },
      include: { user: true },
    }),
    prisma.ticket.findMany({
      where: { dueAt: { gte: from, lte: to }, archivedAt: null },
      include: { customer: true, type: true },
    }),
    prisma.pcBuild.findMany({
      where: { dueAt: { gte: from, lte: to }, archivedAt: null },
      include: { customer: true },
    }),
    prisma.purchaseOrder.findMany({
      where: { expectedAt: { gte: from, lte: to } },
      include: { supplier: true },
    }),
  ]);
  return [
    ...bookings.map((b) => ({
      id: b.id,
      kind: "booking" as const,
      title: `${b.type.name} · ${b.customer.displayName}`,
      start: b.startsAt,
      end: b.endsAt,
      colour: b.type.colour,
      href: `/bookings/${b.id}`,
    })),
    ...shifts.map((s) => ({
      id: s.id,
      kind: "shift" as const,
      title: `Shift · ${s.user.name}`,
      start: s.startsAt,
      end: s.endsAt,
      colour: "#475569",
      href: "/roster",
    })),
    ...ticketDues.map((t) => ({
      id: t.id,
      kind: "ticket-due" as const,
      title: `Due · ${t.ticketNumber}`,
      start: t.dueAt!,
      end: t.dueAt!,
      colour: "#b45309",
      href: `/tickets/${t.id}`,
    })),
    ...builds.map((b) => ({
      id: b.id,
      kind: "build-due" as const,
      title: `PC build · ${b.number}`,
      start: b.dueAt!,
      end: b.dueAt!,
      colour: "#7c3aed",
      href: `/builds/${b.id}`,
    })),
    ...pos.map((p) => ({
      id: p.id,
      kind: "delivery" as const,
      title: `Delivery · ${p.supplier.name}`,
      start: p.expectedAt!,
      end: p.expectedAt!,
      colour: "#0f766e",
      href: `/purchase-orders/${p.id}`,
    })),
  ];
}

export async function upsertShift(actor: Actor, input: { id?: string; userId: string; startsAt: string; endsAt: string; notes?: string }) {
  assertCan(actor, "roster.manage");
  if (input.id) {
    return prisma.rosterShift.update({
      where: { id: input.id },
      data: { userId: input.userId, startsAt: new Date(input.startsAt), endsAt: new Date(input.endsAt), notes: input.notes },
    });
  }
  return prisma.rosterShift.create({
    data: { userId: input.userId, startsAt: new Date(input.startsAt), endsAt: new Date(input.endsAt), notes: input.notes },
  });
}

export async function technicianWorkload() {
  const rows = await prisma.ticket.groupBy({
    by: ["assignedToId"],
    where: { archivedAt: null, assignedToId: { not: null }, status: { isCompleted: false, isCancelled: false } },
    _count: true,
  });
  return rows;
}
