import { assertCan, type Actor } from "../actor";
import { prisma } from "../db";
import { toNumber } from "../money";

export async function dashboard(actor: Actor, from: Date, to: Date) {
  const openWhere = { archivedAt: null, status: { isCompleted: false, isCancelled: false } };
  const [open, overdue, waitingParts, ready, bookings, lowStock, payments, ticketsCompleted] = await Promise.all([
    prisma.ticket.count({ where: openWhere }),
    prisma.ticket.count({ where: { ...openWhere, dueAt: { lt: new Date() } } }),
    prisma.ticket.count({ where: { ...openWhere, waitingForParts: true } }),
    prisma.ticket.count({ where: { archivedAt: null, status: { key: "ready_pickup" } } }),
    prisma.booking.count({
      where: {
        startsAt: {
          gte: new Date(new Date().setHours(0, 0, 0, 0)),
          lt: new Date(new Date().setHours(23, 59, 59, 999)),
        },
      },
    }),
    prisma.$queryRaw<Array<{ count: bigint }>>`SELECT COUNT(*)::bigint as count FROM "InventoryItem" WHERE "archivedAt" IS NULL AND "minStock" > 0 AND "onHand" <= "minStock"`.then((r) => Number(r[0]?.count ?? 0)),
    actor.permissions.has("pricing.view")
      ? prisma.payment.findMany({
          where: { status: "SUCCEEDED", receivedAt: { gte: from, lte: to } },
          select: { amount: true },
        })
      : [],
    prisma.ticket.findMany({
      where: { closedAt: { gte: from, lte: to }, status: { isCompleted: true } },
      include: { timeEntries: true, invoices: true, type: true },
    }),
  ]);

  const revenue = Array.isArray(payments) ? payments.reduce((s, p) => s + toNumber(p.amount), 0) : 0;
  const avgRepair =
    ticketsCompleted.length === 0
      ? 0
      : ticketsCompleted.reduce((s, t) => {
          if (!t.closedAt) return s;
          return s + (t.closedAt.getTime() - t.createdAt.getTime()) / 36e5;
        }, 0) / ticketsCompleted.length;

  const myJobs = await prisma.ticket.findMany({
    where: { assignedToId: actor.id, archivedAt: null, status: { isCompleted: false, isCancelled: false } },
    include: { customer: true, status: true, priority: true, type: true },
    orderBy: { updatedAt: "desc" },
    take: 20,
  });

  const urgent = await prisma.ticket.findMany({
    where: { archivedAt: null, status: { isCompleted: false }, priority: { key: "urgent" } },
    include: { customer: true, status: true, priority: true },
    take: 10,
  });

  const sla = await prisma.ticket.findMany({
    where: { archivedAt: null, slaState: { in: ["APPROACHING", "BREACHED"] }, status: { isCompleted: false } },
    include: { customer: true, status: true, priority: true },
    take: 10,
  });

  const workload = await prisma.ticket.groupBy({
    by: ["assignedToId"],
    where: { archivedAt: null, assignedToId: { not: null }, status: { isCompleted: false, isCancelled: false } },
    _count: true,
  });

  return {
    open,
    overdue,
    waitingParts,
    ready,
    bookings,
    lowStock,
    revenue: actor.permissions.has("pricing.view") ? revenue : null,
    avgRepairHours: Math.round(avgRepair * 10) / 10,
    completedCount: ticketsCompleted.length,
    myJobs,
    urgent,
    sla,
    workload,
  };
}

export async function reportSummary(actor: Actor, from: Date, to: Date) {
  assertCan(actor, "reports.view");
  const invoices = await prisma.invoice.findMany({
    where: { issuedAt: { gte: from, lte: to }, status: { in: ["ISSUED", "PART_PAID", "PAID"] } },
    include: { lines: true, payments: true },
  });
  const tickets = await prisma.ticket.findMany({
    where: { createdAt: { gte: from, lte: to } },
    include: { type: true, timeEntries: true, parts: true, customer: true },
  });
  const refunds = await prisma.refund.findMany({ where: { createdAt: { gte: from, lte: to } } });
  const builds = await prisma.pcBuild.findMany({ where: { createdAt: { gte: from, lte: to } } });
  const used = await prisma.usedDevicePurchase.findMany({ where: { createdAt: { gte: from, lte: to } } });
  const lowStock = await prisma.$queryRaw<Array<{ id: string; name: string; sku: string; onHand: number; minStock: number }>>`
    SELECT id, name, sku, "onHand", "minStock" FROM "InventoryItem"
    WHERE "archivedAt" IS NULL AND "minStock" > 0 AND "onHand" <= "minStock"
  `;

  const revenue = invoices.reduce((s, i) => s + toNumber(i.amountPaid), 0);
  const gst = invoices.reduce((s, i) => s + toNumber(i.gst), 0);
  const partsCost = tickets.reduce((s, t) => s + t.parts.reduce((p, part) => p + toNumber(part.unitCost) * part.quantity, 0), 0);
  const profit = revenue - partsCost;
  const byCategory = new Map<string, number>();
  for (const t of tickets) byCategory.set(t.type.name, (byCategory.get(t.type.name) ?? 0) + 1);

  const financial = actor.permissions.has("reports.financial");
  return {
    from,
    to,
    repairCount: tickets.length,
    categories: [...byCategory.entries()].map(([name, count]) => ({ name, count })),
    averageRepairHours:
      tickets.filter((t) => t.closedAt).reduce((s, t) => s + (t.closedAt!.getTime() - t.createdAt.getTime()) / 36e5, 0) /
      Math.max(1, tickets.filter((t) => t.closedAt).length),
    lowStock,
    warrantyReturns: tickets.filter((t) => t.isWarrantyReturn).length,
    refunds: financial ? refunds.reduce((s, r) => s + toNumber(r.amount), 0) : null,
    revenue: financial ? revenue : null,
    gst: financial ? gst : null,
    partsCost: financial ? partsCost : null,
    profit: financial ? profit : null,
    pcBuildCount: builds.length,
    usedPurchases: used.length,
    businessShare:
      tickets.filter((t) => t.customer.type === "BUSINESS").length / Math.max(1, tickets.length),
  };
}
