import { assertCan, type Actor } from "../actor";
import { prisma } from "../db";

export async function globalSearch(actor: Actor, q: string) {
  const term = q.trim();
  if (term.length < 2) return { tickets: [], customers: [], invoices: [], parts: [], builds: [], suppliers: [] };

  const tickets = actor.permissions.has("tickets.view")
    ? await prisma.ticket.findMany({
        where: {
          archivedAt: null,
          OR: [
            { ticketNumber: { contains: term, mode: "insensitive" } },
            { reportedIssue: { contains: term, mode: "insensitive" } },
            { customerDevice: { serial: { contains: term, mode: "insensitive" } } },
            { customerDevice: { imei: { contains: term, mode: "insensitive" } } },
          ],
        },
        include: { customer: true, status: true },
        take: 8,
      })
    : [];

  const customers = actor.permissions.has("customers.view")
    ? await prisma.customer.findMany({
        where: {
          archivedAt: null,
          OR: [
            { displayName: { contains: term, mode: "insensitive" } },
            { phone: { contains: term, mode: "insensitive" } },
            { email: { contains: term, mode: "insensitive" } },
            { companyName: { contains: term, mode: "insensitive" } },
          ],
        },
        take: 8,
      })
    : [];

  const invoices = actor.permissions.has("invoices.view")
    ? await prisma.invoice.findMany({ where: { number: { contains: term, mode: "insensitive" } }, take: 5 })
    : [];

  const parts = actor.permissions.has("inventory.view")
    ? await prisma.inventoryItem.findMany({
        where: {
          archivedAt: null,
          OR: [
            { name: { contains: term, mode: "insensitive" } },
            { sku: { contains: term, mode: "insensitive" } },
            { barcode: { contains: term, mode: "insensitive" } },
          ],
        },
        take: 8,
      })
    : [];

  const builds = actor.permissions.has("builds.view")
    ? await prisma.pcBuild.findMany({ where: { number: { contains: term, mode: "insensitive" } }, include: { customer: true }, take: 5 })
    : [];

  const suppliers = actor.permissions.has("inventory.view")
    ? await prisma.supplier.findMany({ where: { name: { contains: term, mode: "insensitive" }, archivedAt: null }, take: 5 })
    : [];

  const technicians = actor.permissions.has("staff.view")
    ? await prisma.user.findMany({ where: { name: { contains: term, mode: "insensitive" }, archivedAt: null }, take: 5 })
    : [];

  return { tickets, customers, invoices, parts, builds, suppliers, technicians };
}

export async function findTicketByScan(actor: Actor, code: string) {
  assertCan(actor, "tickets.view");
  return prisma.ticket.findFirst({
    where: { archivedAt: null, OR: [{ id: code }, { ticketNumber: code }] },
  });
}
