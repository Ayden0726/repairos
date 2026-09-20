import { assertCan, type Actor } from "../actor";
import { prisma } from "../db";
import { NotFoundError, ValidationError } from "../errors";

export async function startTimer(actor: Actor, ticketId: string) {
  assertCan(actor, "tickets.edit");
  const existing = await prisma.technicianTimeEntry.findFirst({
    where: { technicianId: actor.id, state: { in: ["RUNNING", "PAUSED"] } },
  });
  if (existing) throw new ValidationError("Stop or pause your current timer first.");
  const ticket = await prisma.ticket.findUnique({ where: { id: ticketId } });
  if (!ticket) throw new NotFoundError("Ticket");
  const entry = await prisma.technicianTimeEntry.create({
    data: {
      ticketId,
      technicianId: actor.id,
      labourRateId: ticket.labourRateId,
      state: "RUNNING",
      startedAt: new Date(),
    },
  });
  await prisma.ticketEvent.create({
    data: { ticketId, actorId: actor.id, type: "timer", summary: `${actor.name} started work`, visibility: "INTERNAL" },
  });
  return entry;
}

export async function pauseTimer(actor: Actor, entryId: string) {
  const entry = await prisma.technicianTimeEntry.findUnique({ where: { id: entryId } });
  if (!entry || entry.technicianId !== actor.id) throw new NotFoundError("Timer");
  if (entry.state !== "RUNNING") throw new ValidationError("Timer is not running.");
  return prisma.technicianTimeEntry.update({
    where: { id: entryId },
    data: { state: "PAUSED", pauseStartedAt: new Date() },
  });
}

export async function resumeTimer(actor: Actor, entryId: string) {
  const entry = await prisma.technicianTimeEntry.findUnique({ where: { id: entryId } });
  if (!entry || entry.technicianId !== actor.id) throw new NotFoundError("Timer");
  const extra = entry.pauseStartedAt ? Math.round((Date.now() - entry.pauseStartedAt.getTime()) / 1000) : 0;
  return prisma.technicianTimeEntry.update({
    where: { id: entryId },
    data: { state: "RUNNING", pausedSeconds: entry.pausedSeconds + extra, pauseStartedAt: null },
  });
}

export async function stopTimer(actor: Actor, entryId: string) {
  const entry = await prisma.technicianTimeEntry.findUnique({ where: { id: entryId } });
  if (!entry || entry.technicianId !== actor.id) throw new NotFoundError("Timer");
  const extra = entry.pauseStartedAt ? Math.round((Date.now() - entry.pauseStartedAt.getTime()) / 1000) : 0;
  const updated = await prisma.technicianTimeEntry.update({
    where: { id: entryId },
    data: { state: "STOPPED", endedAt: new Date(), pausedSeconds: entry.pausedSeconds + extra, pauseStartedAt: null },
  });
  await prisma.ticketEvent.create({
    data: { ticketId: entry.ticketId, actorId: actor.id, type: "timer", summary: `${actor.name} stopped work`, visibility: "INTERNAL" },
  });
  return updated;
}

export async function activeTimer(userId: string) {
  return prisma.technicianTimeEntry.findFirst({
    where: { technicianId: userId, state: { in: ["RUNNING", "PAUSED"] } },
    include: { ticket: { select: { id: true, ticketNumber: true } } },
  });
}

export async function startDiagnostic(actor: Actor, ticketId: string, templateId: string, phase: string) {
  assertCan(actor, "tickets.edit");
  const template = await prisma.diagnosticTemplate.findUnique({
    where: { id: templateId },
    include: { items: { orderBy: { sortOrder: "asc" } } },
  });
  if (!template) throw new NotFoundError("Checklist template");
  return prisma.diagnosticRun.create({
    data: {
      ticketId,
      templateId,
      phase,
      technicianId: actor.id,
      results: {
        create: template.items.map((item) => ({ itemId: item.id, value: "NOT_TESTED" })),
      },
    },
    include: { results: { include: { item: true } }, template: true },
  });
}

export async function saveDiagnosticResult(actor: Actor, resultId: string, value: "PASS" | "FAIL" | "NOT_TESTED" | "NOT_APPLICABLE", notes?: string) {
  assertCan(actor, "tickets.edit");
  return prisma.diagnosticResult.update({
    where: { id: resultId },
    data: { value, notes },
  });
}
