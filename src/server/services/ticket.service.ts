import { Prisma } from "@prisma/client";
import { addMinutes, isBefore } from "date-fns";
import { assertCan, type Actor } from "../actor";
import { audit } from "../audit";
import { prisma } from "../db";
import { encryptString } from "../crypto";
import { NotFoundError, ValidationError } from "../errors";
import { sendTicketSms } from "../integrations/sms";
import { nextTicketNumber } from "../numbering";
import { getSetting } from "../config/settings";
import { notifyUsers } from "./notification.service";

const ticketInclude = {
  customer: true,
  customerDevice: true,
  deviceModel: { include: { family: { include: { manufacturer: true } } } },
  type: true,
  status: true,
  priority: true,
  assignedTo: { select: { id: true, name: true, email: true } },
  createdBy: { select: { id: true, name: true } },
  parts: { include: { item: true } },
  notes: { include: { author: { select: { name: true } } }, orderBy: { createdAt: "desc" as const } },
  events: { orderBy: { createdAt: "desc" as const }, take: 80 },
  timeEntries: { include: { technician: { select: { name: true } } }, orderBy: { startedAt: "desc" as const } },
  communications: { orderBy: { createdAt: "desc" as const } },
  diagnosticRuns: { include: { results: { include: { item: true } }, template: true } },
  conditions: true,
  credentials: { select: { id: true, label: true, deletedAt: true } },
  signatures: true,
  invoices: true,
  warranty: true,
} satisfies Prisma.TicketInclude;

export async function listTickets(
  actor: Actor,
  query: {
    q?: string;
    statusId?: string;
    technicianId?: string;
    priorityId?: string;
    typeId?: string;
    customerId?: string;
    waitingForParts?: boolean;
    overdue?: boolean;
    sla?: string;
    warranty?: boolean;
    view?: "active" | "all";
    page?: number;
    pageSize?: number;
    sort?: string;
  },
) {
  assertCan(actor, "tickets.view");
  const page = Math.max(1, query.page ?? 1);
  const pageSize = Math.min(100, query.pageSize ?? 30);
  const where: Prisma.TicketWhereInput = { archivedAt: null };
  if (query.view !== "all") {
    where.status = { isCompleted: false, isCancelled: false };
  }
  if (query.statusId) where.statusId = query.statusId;
  if (query.technicianId) where.assignedToId = query.technicianId;
  if (query.priorityId) where.priorityId = query.priorityId;
  if (query.typeId) where.typeId = query.typeId;
  if (query.customerId) where.customerId = query.customerId;
  if (query.waitingForParts) where.waitingForParts = true;
  if (query.warranty) where.isWarrantyReturn = true;
  if (query.overdue) where.dueAt = { lt: new Date() };
  if (query.sla === "breached") where.slaState = "BREACHED";
  if (query.sla === "approaching") where.slaState = "APPROACHING";
  if (query.q) {
    where.OR = [
      { ticketNumber: { contains: query.q, mode: "insensitive" } },
      { reportedIssue: { contains: query.q, mode: "insensitive" } },
      { customer: { displayName: { contains: query.q, mode: "insensitive" } } },
      { customer: { phone: { contains: query.q, mode: "insensitive" } } },
      { customerDevice: { serial: { contains: query.q, mode: "insensitive" } } },
      { customerDevice: { imei: { contains: query.q, mode: "insensitive" } } },
    ];
  }

  const [total, rows, statuses] = await Promise.all([
    prisma.ticket.count({ where }),
    prisma.ticket.findMany({
      where,
      include: {
        customer: true,
        type: true,
        status: true,
        priority: true,
        assignedTo: { select: { id: true, name: true } },
        customerDevice: true,
      },
      orderBy: query.sort === "due" ? { dueAt: "asc" } : { createdAt: "desc" },
      skip: (page - 1) * pageSize,
      take: pageSize,
    }),
    prisma.ticketStatus.findMany({ where: { archivedAt: null }, orderBy: { sortOrder: "asc" } }),
  ]);

  return { total, page, pageSize, rows, statuses };
}

export async function boardTickets(actor: Actor) {
  assertCan(actor, "tickets.view");
  const statuses = await prisma.ticketStatus.findMany({
    where: { archivedAt: null, isCompleted: false, isCancelled: false },
    orderBy: { sortOrder: "asc" },
  });
  const tickets = await prisma.ticket.findMany({
    where: { archivedAt: null, status: { isCompleted: false, isCancelled: false } },
    include: {
      customer: true,
      type: true,
      status: true,
      priority: true,
      assignedTo: { select: { id: true, name: true } },
    },
    orderBy: [{ priority: { sortOrder: "desc" } }, { dueAt: "asc" }],
    take: 500,
  });
  return { statuses, tickets };
}

export async function getTicket(actor: Actor, id: string) {
  assertCan(actor, "tickets.view");
  const ticket = await prisma.ticket.findFirst({
    where: { id, archivedAt: null },
    include: ticketInclude,
  });
  if (!ticket) throw new NotFoundError("Ticket");
  const notes = canSeeInternal(actor)
    ? ticket.notes
    : ticket.notes.filter((n) => !n.isInternal);
  return { ...ticket, notes };
}

function canSeeInternal(actor: Actor) {
  return actor.isOwner || actor.permissions.has("tickets.internal_notes");
}

export async function quickCreateTicket(
  actor: Actor,
  input: {
    customerId: string;
    typeId: string;
    reportedIssue: string;
    priorityId?: string;
    assignedToId?: string;
    estimatedPrice?: number;
    dueAt?: string;
    deviceModelId?: string;
    customerDeviceId?: string;
    serviceId?: string;
  },
) {
  assertCan(actor, "tickets.create");
  if (!input.reportedIssue?.trim()) throw new ValidationError("Reported issue is required.");
  const type = await prisma.ticketType.findUnique({ where: { id: input.typeId } });
  if (!type) throw new ValidationError("Select a job type.");
  const status = await prisma.ticketStatus.findUnique({ where: { key: "checked_in" } });
  const priority =
    (input.priorityId && (await prisma.ticketPriority.findUnique({ where: { id: input.priorityId } }))) ||
    (await prisma.ticketPriority.findFirst({ where: { isDefault: true } }));
  if (!status || !priority) throw new ValidationError("Workshop defaults are missing. Run setup again.");

  const ticketNumber = await nextTicketNumber(type.prefix);
  const sla = computeSla(priority.slaResponseMinutes, priority.slaCompletionMinutes);
  const diagnostic = await getSetting("finance.diagnosticFee", { amount: "89.00" });

  let labourRateId = type.defaultLabourRateId;
  let estimatedLabourMinutes: number | undefined;
  if (input.serviceId) {
    const service = await prisma.serviceCatalogue.findUnique({ where: { id: input.serviceId } });
    if (service) {
      labourRateId = service.labourRateId ?? labourRateId;
      estimatedLabourMinutes = service.expectedLabourMinutes;
    }
  }

  const ticket = await prisma.ticket.create({
    data: {
      ticketNumber,
      customerId: input.customerId,
      typeId: type.id,
      statusId: status.id,
      priorityId: priority.id,
      reportedIssue: input.reportedIssue.trim(),
      assignedToId: input.assignedToId,
      createdById: actor.id,
      estimatedPrice: input.estimatedPrice,
      dueAt: input.dueAt ? new Date(input.dueAt) : undefined,
      deviceModelId: input.deviceModelId,
      customerDeviceId: input.customerDeviceId,
      labourRateId,
      estimatedLabourMinutes,
      diagnosticFeeCents: Math.round(Number(diagnostic.amount) * 100),
      slaResponseDue: sla.responseDue,
      slaCompletionDue: sla.completionDue,
      slaState: sla.responseDue ? "ON_TRACK" : "NONE",
    },
    include: { customer: true, type: true, status: true, priority: true },
  });

  await prisma.ticketEvent.create({
    data: {
      ticketId: ticket.id,
      actorId: actor.id,
      type: "created",
      summary: `Ticket ${ticket.ticketNumber} created`,
      visibility: "CUSTOMER",
    },
  });
  await audit({ actor, action: "ticket.create", entityType: "Ticket", entityId: ticket.id, newValue: { ticketNumber } });
  if (ticket.assignedToId) {
    await notifyUsers([ticket.assignedToId], {
      title: "Job assigned",
      body: `${ticket.ticketNumber} was assigned to you.`,
      href: `/tickets/${ticket.id}`,
      ticketId: ticket.id,
    });
  }
  return ticket;
}

function computeSla(response?: number | null, completion?: number | null) {
  const now = new Date();
  return {
    responseDue: response ? addMinutes(now, response) : null,
    completionDue: completion ? addMinutes(now, completion) : null,
  };
}

export async function changeStatus(actor: Actor, ticketId: string, statusId: string) {
  assertCan(actor, "tickets.status");
  const ticket = await prisma.ticket.findUnique({
    where: { id: ticketId },
    include: { status: true, assignedTo: true },
  });
  if (!ticket) throw new NotFoundError("Ticket");
  const next = await prisma.ticketStatus.findUnique({ where: { id: statusId } });
  if (!next) throw new ValidationError("Unknown status.");
  const updated = await prisma.ticket.update({
    where: { id: ticketId },
    data: {
      statusId,
      waitingForParts: next.countsAsWaitingForParts,
      closedAt: next.isCompleted || next.isCancelled ? new Date() : null,
      closedById: next.isCompleted || next.isCancelled ? actor.id : null,
      firstResponseAt: ticket.firstResponseAt ?? new Date(),
    },
    include: { status: true, customer: true, type: true },
  });
  await prisma.ticketEvent.create({
    data: {
      ticketId,
      actorId: actor.id,
      type: "status",
      summary: `Status changed from ${ticket.status.name} to ${next.name}`,
      visibility: "CUSTOMER",
    },
  });
  await audit({
    actor,
    action: "ticket.status",
    entityType: "Ticket",
    entityId: ticketId,
    oldValue: { status: ticket.status.key },
    newValue: { status: next.key },
  });
  if (next.autoSmsTemplateId || next.key) {
    const tpl = await prisma.smsTemplate.findFirst({ where: { triggerStatusKey: next.key, enabled: true } });
    if (tpl) await sendTicketSms(ticketId, tpl.key).catch((err) => console.error("sms", err));
  }
  if (next.isCompleted) {
    await completeJobSideEffects(actor, ticketId);
  }
  return updated;
}

export async function completeJobSideEffects(actor: Actor, ticketId: string) {
  const ticket = await prisma.ticket.findUnique({
    where: { id: ticketId },
    include: { type: true, timeEntries: true, parts: true, invoices: true, customer: true },
  });
  if (!ticket) return;
  await prisma.deviceCredential.updateMany({
    where: { ticketId, deletedAt: null },
    data: { ciphertext: encryptString("DELETED"), deletedAt: new Date(), deletedReason: "job_completed" },
  });
  await prisma.deviceCredential.deleteMany({ where: { ticketId } });
  const warrantyDays = ticket.type.defaultWarrantyDays;
  await prisma.warranty.upsert({
    where: { ticketId },
    update: {},
    create: {
      ticketId,
      customerId: ticket.customerId,
      durationDays: warrantyDays,
      startsAt: new Date(),
      expiresAt: addMinutes(new Date(), warrantyDays * 24 * 60),
      terms: "Repaired fault only. Accidental damage and unrelated faults are excluded.",
    },
  });
  const actualMin = ticket.timeEntries.reduce((sum, entry) => {
    if (!entry.endedAt) return sum;
    return sum + Math.max(0, (entry.endedAt.getTime() - entry.startedAt.getTime()) / 60000 - entry.pausedSeconds / 60);
  }, 0);
  const sale = Number(ticket.invoices[0]?.total ?? ticket.estimatedPrice ?? 0);
  const partCost = ticket.parts.reduce((s, p) => s + Number(p.unitCost) * p.quantity, 0);
  if (sale > 0) {
    const profit = sale - partCost;
    await prisma.pricingObservation.create({
      data: {
        deviceKey: ticket.deviceModelId ?? ticket.type.key,
        repairType: ticket.type.key,
        partCost,
        estimatedMin: ticket.estimatedLabourMinutes ?? 0,
        actualMin: Math.round(actualMin),
        salePrice: sale,
        profit,
        margin: sale === 0 ? 0 : profit / sale,
      },
    });
  }
  await prisma.ticketEvent.create({
    data: {
      ticketId,
      actorId: actor.id,
      type: "completed",
      summary: "Job completed. Device credentials permanently deleted. Warranty started.",
      visibility: "INTERNAL",
    },
  });
}

export async function assignTicket(actor: Actor, ticketId: string, assignedToId: string | null) {
  assertCan(actor, "tickets.assign");
  const ticket = await prisma.ticket.update({
    where: { id: ticketId },
    data: { assignedToId },
    include: { assignedTo: true },
  });
  await prisma.ticketEvent.create({
    data: {
      ticketId,
      actorId: actor.id,
      type: "assignment",
      summary: assignedToId ? `Assigned to ${ticket.assignedTo?.name}` : "Unassigned",
      visibility: "INTERNAL",
    },
  });
  if (assignedToId) {
    await notifyUsers([assignedToId], {
      title: "Job assigned",
      body: `${ticket.ticketNumber} was assigned to you.`,
      href: `/tickets/${ticketId}`,
      ticketId,
    });
  }
  return ticket;
}

export async function addNote(actor: Actor, ticketId: string, body: string, isInternal: boolean) {
  if (isInternal) assertCan(actor, "tickets.internal_notes");
  else assertCan(actor, "tickets.edit");
  const note = await prisma.ticketNote.create({
    data: { ticketId, authorId: actor.id, body, isInternal },
  });
  await prisma.ticketEvent.create({
    data: {
      ticketId,
      actorId: actor.id,
      type: "note",
      summary: isInternal ? "Internal note added" : "Customer-visible note added",
      visibility: isInternal ? "INTERNAL" : "CUSTOMER",
    },
  });
  return note;
}

export async function storeCredential(actor: Actor, ticketId: string, secret: string, label?: string) {
  assertCan(actor, "tickets.credentials.manage");
  const ticket = await prisma.ticket.findUnique({ where: { id: ticketId }, include: { status: true } });
  if (!ticket || ticket.status.isCompleted) throw new ValidationError("Credentials cannot be stored on a completed job.");
  return prisma.deviceCredential.upsert({
    where: { ticketId },
    update: { ciphertext: encryptString(secret), label: label ?? "Device PIN / password", deletedAt: null },
    create: { ticketId, ciphertext: encryptString(secret), label: label ?? "Device PIN / password" },
  });
}

export async function revealCredential(actor: Actor, ticketId: string) {
  assertCan(actor, "tickets.credentials.view");
  const row = await prisma.deviceCredential.findUnique({ where: { ticketId } });
  if (!row || row.deletedAt) throw new NotFoundError("Device credential");
  const { decryptString } = await import("../crypto");
  const secret = decryptString(row.ciphertext);
  await audit({ actor, action: "credential.reveal", entityType: "DeviceCredential", entityId: row.id });
  await prisma.ticketEvent.create({
    data: {
      ticketId,
      actorId: actor.id,
      type: "credential_reveal",
      summary: `${actor.name} revealed the device password`,
      visibility: "INTERNAL",
    },
  });
  return { secret, label: row.label };
}

export async function refreshSlaStates() {
  const open = await prisma.ticket.findMany({
    where: { archivedAt: null, status: { isCompleted: false, isCancelled: false } },
    select: { id: true, slaResponseDue: true, slaCompletionDue: true, slaState: true },
  });
  const now = new Date();
  for (const ticket of open) {
    const due = ticket.slaCompletionDue ?? ticket.slaResponseDue;
    if (!due) continue;
    const minutes = (due.getTime() - now.getTime()) / 60000;
    let slaState: "ON_TRACK" | "APPROACHING" | "BREACHED" = "ON_TRACK";
    if (isBefore(due, now)) slaState = "BREACHED";
    else if (minutes <= 60) slaState = "APPROACHING";
    if (slaState !== ticket.slaState) {
      await prisma.ticket.update({ where: { id: ticket.id }, data: { slaState } });
    }
  }
}

export async function archiveTicket(actor: Actor, ticketId: string) {
  assertCan(actor, "tickets.delete");
  await prisma.ticket.update({ where: { id: ticketId }, data: { archivedAt: new Date() } });
  await audit({ actor, action: "ticket.archive", entityType: "Ticket", entityId: ticketId });
}
