import { Prisma } from "@prisma/client";
import { assertCan, type Actor } from "../actor";
import { audit } from "../audit";
import { prisma } from "../db";
import { getSetting } from "../config/settings";
import { add, applyGst, gstSplit, roundMoney, toNumber } from "../money";
import { nextNumber } from "../numbering";
import { NotFoundError, ValidationError } from "../errors";
import { getEmailProvider } from "../integrations/email";
import { manualPayments, squarePayments } from "../integrations/payments";
import { notifyUsers } from "./notification.service";

type LineInput = {
  type: "PARTS" | "LABOUR" | "DIAGNOSTIC" | "SERVICE" | "DISCOUNT" | "OTHER";
  description: string;
  quantity: number;
  unitPrice: number;
  gstTreatment?: "INCLUSIVE" | "EXCLUSIVE" | "GST_FREE";
  itemId?: string;
};

function totals(lines: LineInput[], gstRegistered: boolean, rate: number) {
  let subtotal = new Prisma.Decimal(0);
  let gst = new Prisma.Decimal(0);
  for (const line of lines) {
    const qty = line.quantity;
    const lineTotal = roundMoney(line.unitPrice * qty);
    const treatment = line.gstTreatment ?? "INCLUSIVE";
    if (!gstRegistered || treatment === "GST_FREE") {
      subtotal = add(subtotal, lineTotal);
      continue;
    }
    if (treatment === "EXCLUSIVE") {
      const split = applyGst(lineTotal, rate);
      subtotal = add(subtotal, split.exclusive);
      gst = add(gst, split.gst);
    } else {
      const split = gstSplit(lineTotal, rate);
      subtotal = add(subtotal, split.exclusive);
      gst = add(gst, split.gst);
    }
  }
  const discountLines = lines.filter((l) => l.type === "DISCOUNT");
  const discount = discountLines.reduce((s, l) => add(s, Math.abs(l.unitPrice * l.quantity)), new Prisma.Decimal(0));
  const total = add(subtotal, gst);
  return { subtotal, gst, discount, total };
}

export async function createQuote(actor: Actor, input: { customerId: string; issue?: string; deviceSummary?: string; lines: LineInput[]; expiryDate?: string; notes?: string }) {
  assertCan(actor, "quotes.manage");
  const gst = await getSetting("gst", { registered: true, rate: 0.1, inclusiveDefault: true });
  const t = totals(input.lines, gst.registered, gst.rate);
  const number = await nextNumber("quote", (await getSetting("numbering", { quotePrefix: "QTE" })).quotePrefix);
  const quote = await prisma.quote.create({
    data: {
      number,
      customerId: input.customerId,
      issue: input.issue,
      deviceSummary: input.deviceSummary,
      notes: input.notes,
      expiryDate: input.expiryDate ? new Date(input.expiryDate) : undefined,
      subtotal: t.subtotal,
      gst: t.gst,
      discount: t.discount,
      total: t.total,
      createdById: actor.id,
      lines: {
        create: input.lines.map((line, i) => ({
          type: line.type,
          description: line.description,
          quantity: line.quantity,
          unitPrice: line.unitPrice,
          gstTreatment: line.gstTreatment ?? "INCLUSIVE",
          itemId: line.itemId,
          sortOrder: i,
        })),
      },
    },
    include: { lines: true, customer: true },
  });
  await audit({ actor, action: "quote.create", entityType: "Quote", entityId: quote.id });
  return quote;
}

export async function recordQuoteApproval(actor: Actor, quoteId: string, status: "approved" | "declined" | "pending", notes?: string) {
  assertCan(actor, "quotes.manage");
  return prisma.quote.update({
    where: { id: quoteId },
    data: {
      approvalStatus: status,
      approvalNotes: notes,
      status: status === "approved" ? "APPROVED" : status === "declined" ? "DECLINED" : "PENDING",
      approvedAt: status === "approved" ? new Date() : null,
    },
  });
}

export async function convertQuoteToTicket(actor: Actor, quoteId: string, typeId: string) {
  assertCan(actor, "tickets.create");
  const quote = await prisma.quote.findUnique({ where: { id: quoteId }, include: { lines: true } });
  if (!quote) throw new NotFoundError("Quote");
  const { quickCreateTicket } = await import("./ticket.service");
  const ticket = await quickCreateTicket(actor, {
    customerId: quote.customerId,
    typeId,
    reportedIssue: quote.issue ?? "Quoted work",
    estimatedPrice: toNumber(quote.total),
  });
  await prisma.quote.update({
    where: { id: quoteId },
    data: { status: "CONVERTED", convertedTicketId: ticket.id },
  });
  await prisma.ticket.update({ where: { id: ticket.id }, data: { quoteId } });
  return ticket;
}

export async function createInvoiceFromTicket(actor: Actor, ticketId: string) {
  assertCan(actor, "invoices.manage");
  const ticket = await prisma.ticket.findUnique({
    where: { id: ticketId },
    include: { parts: true, timeEntries: { include: { labourRate: true } }, type: true, customer: true },
  });
  if (!ticket) throw new NotFoundError("Ticket");
  const gst = await getSetting("gst", { registered: true, rate: 0.1 });
  const lines: LineInput[] = [];
  for (const part of ticket.parts.filter((p) => p.status !== "RETURNED")) {
    lines.push({
      type: "PARTS",
      description: part.name,
      quantity: part.quantity,
      unitPrice: toNumber(part.unitPrice),
      itemId: part.itemId ?? undefined,
    });
  }
  const minutes = ticket.timeEntries.reduce((sum, e) => {
    if (!e.endedAt) return sum;
    return sum + Math.max(0, (e.endedAt.getTime() - e.startedAt.getTime()) / 60000 - e.pausedSeconds / 60);
  }, 0);
  const rate = ticket.timeEntries[0]?.labourRate?.hourlyRate ?? (await prisma.labourRate.findFirst({ where: { isDefault: true } }))?.hourlyRate ?? 110;
  if (minutes > 0) {
    lines.push({
      type: "LABOUR",
      description: `${ticket.type.name} labour (${Math.round(minutes)} min)`,
      quantity: Math.round((minutes / 60) * 100) / 100,
      unitPrice: toNumber(rate),
    });
  }
  if (ticket.diagnosticFeeCents && !ticket.diagnosticFeeWaived) {
    const fee = ticket.diagnosticFeeCents / 100;
    const credited = ticket.parts.length > 0 || minutes > 0;
    if (!credited || !ticket.diagnosticFeeCredited) {
      lines.push({ type: "DIAGNOSTIC", description: "Diagnostic fee", quantity: 1, unitPrice: fee });
    }
  }
  const t = totals(lines, gst.registered, gst.rate);
  const number = await nextNumber("invoice", (await getSetting("numbering", { invoicePrefix: "INV" })).invoicePrefix);
  const invoice = await prisma.invoice.create({
    data: {
      number,
      customerId: ticket.customerId,
      ticketId,
      status: "DRAFT",
      subtotal: t.subtotal,
      gst: t.gst,
      discount: t.discount,
      total: t.total,
      createdById: actor.id,
      lines: {
        create: lines.map((line, i) => ({
          type: line.type,
          description: line.description,
          quantity: line.quantity,
          unitPrice: line.unitPrice,
          sortOrder: i,
        })),
      },
    },
    include: { lines: true, customer: true },
  });
  await audit({ actor, action: "invoice.create", entityType: "Invoice", entityId: invoice.id });
  return invoice;
}

export async function issueInvoice(actor: Actor, invoiceId: string) {
  assertCan(actor, "invoices.manage");
  const invoice = await prisma.invoice.findUnique({ where: { id: invoiceId } });
  if (!invoice || invoice.status !== "DRAFT") throw new ValidationError("Only draft invoices can be issued.");
  return prisma.invoice.update({
    where: { id: invoiceId },
    data: { status: "ISSUED", issuedAt: new Date() },
  });
}

export async function recordPayment(
  actor: Actor,
  input: {
    invoiceId: string;
    methodKey: string;
    amount: number;
    isDeposit?: boolean;
    notes?: string;
    confirmSquare?: boolean;
  },
) {
  assertCan(actor, "payments.record");
  const invoice = await prisma.invoice.findUnique({ where: { id: input.invoiceId }, include: { customer: true } });
  if (!invoice) throw new NotFoundError("Invoice");
  if (invoice.status === "VOID" || invoice.status === "DRAFT") {
    throw new ValidationError("This invoice cannot accept payment yet.");
  }
  const method = await prisma.paymentMethod.findUnique({ where: { key: input.methodKey } });
  if (!method?.isActive) throw new ValidationError("Payment method is not available.");

  let status: "PENDING" | "SUCCEEDED" | "FAILED" = "PENDING";
  let providerRef: string | undefined;
  let failureReason: string | undefined;
  let provider = "manual";

  if (method.provider === "square") {
    const cfg = await getSetting("integrations.square", {
      enabled: false,
      accessToken: "",
      locationId: "",
      environment: "sandbox" as const,
    });
    if (!cfg.enabled) throw new ValidationError("Square is not configured. Record this as Manual Card Entry instead.");
    const result = await squarePayments(cfg.accessToken, cfg.locationId, cfg.environment).charge({
      amount: input.amount,
      currency: "AUD",
      reference: invoice.number,
      description: `Invoice ${invoice.number}`,
    });
    provider = "square";
    providerRef = result.providerRef;
    if (result.ok) status = "SUCCEEDED";
    else {
      status = result.pending ? "PENDING" : "FAILED";
      failureReason = result.error;
    }
  } else {
    const result = await manualPayments().charge({
      amount: input.amount,
      currency: "AUD",
      reference: invoice.number,
      description: method.name,
    });
    status = result.ok ? "SUCCEEDED" : "FAILED";
    providerRef = result.providerRef;
  }

  const payment = await prisma.payment.create({
    data: {
      invoiceId: invoice.id,
      methodId: method.id,
      amount: input.amount,
      status,
      provider,
      providerRef,
      failureReason,
      notes: input.notes,
      isDeposit: input.isDeposit ?? false,
      recordedById: actor.id,
    },
  });

  if (status === "SUCCEEDED") {
    const amountPaid = add(invoice.amountPaid, input.amount);
    const remaining = Number(invoice.total) - Number(amountPaid) - Number(invoice.amountRefunded);
    const nextStatus = remaining <= 0.009 ? "PAID" : "PART_PAID";
    await prisma.invoice.update({
      where: { id: invoice.id },
      data: { amountPaid, status: nextStatus },
    });
    await notifyUsers(
      (await prisma.user.findMany({ where: { isOwner: true, status: "ACTIVE" }, select: { id: true } })).map((u) => u.id),
      { title: "Payment received", body: `${invoice.number} · $${input.amount.toFixed(2)}`, href: `/invoices/${invoice.id}` },
    );
  }
  await audit({ actor, action: "payment.record", entityType: "Payment", entityId: payment.id, newValue: { status, amount: input.amount } });
  if (invoice.ticketId) {
    await prisma.ticketEvent.create({
      data: {
        ticketId: invoice.ticketId,
        actorId: actor.id,
        type: "payment",
        summary: status === "SUCCEEDED" ? `Payment of $${input.amount.toFixed(2)} recorded` : `Payment not confirmed (${status})`,
        visibility: "INTERNAL",
      },
    });
  }
  return payment;
}

export async function issueRefund(actor: Actor, input: { invoiceId: string; paymentId?: string; amount: number; reason: string; method?: string }) {
  assertCan(actor, "payments.refund");
  if (!input.reason.trim()) throw new ValidationError("A refund reason is required.");
  const invoice = await prisma.invoice.findUnique({ where: { id: input.invoiceId } });
  if (!invoice) throw new NotFoundError("Invoice");
  const refund = await prisma.refund.create({
    data: {
      invoiceId: invoice.id,
      paymentId: input.paymentId,
      amount: input.amount,
      reason: input.reason,
      method: input.method,
      issuedById: actor.id,
    },
  });
  const amountRefunded = add(invoice.amountRefunded, input.amount);
  const stillPaid = Number(invoice.amountPaid) - Number(amountRefunded);
  await prisma.invoice.update({
    where: { id: invoice.id },
    data: {
      amountRefunded,
      status: stillPaid <= 0.009 ? "REFUNDED" : "PART_PAID",
    },
  });
  await audit({ actor, action: "refund.issue", entityType: "Refund", entityId: refund.id, newValue: input });
  return refund;
}

export async function emailDocument(actor: Actor, to: string, subject: string, text: string) {
  const provider = await getEmailProvider();
  return provider.send({ to, subject, text });
}

export async function suggestRepairPrice(actor: Actor, input: { typeKey: string; deviceKey?: string; partCost: number; labourMinutes: number; labourRate: number }) {
  assertCan(actor, "pricing.view");
  const observations = await prisma.pricingObservation.findMany({
    where: {
      repairType: input.typeKey,
      ...(input.deviceKey ? { deviceKey: input.deviceKey } : {}),
    },
    orderBy: { createdAt: "desc" },
    take: 25,
  });
  const diagnostic = await getSetting("finance.diagnosticFee", { amount: "89.00" });
  const labour = (input.labourMinutes / 60) * input.labourRate;
  const partsMarked = input.partCost * 1.35;
  let historical = 0;
  if (observations.length) {
    historical = observations.reduce((s, o) => s + Number(o.salePrice), 0) / observations.length;
  }
  const base = partsMarked + labour + Number(diagnostic.amount);
  const recommended = historical ? Math.round(((base + historical) / 2) * 100) / 100 : Math.round(base * 100) / 100;
  return {
    recommended,
    basis: {
      partCost: input.partCost,
      estimatedLabour: labour,
      diagnosticFee: Number(diagnostic.amount),
      markup: partsMarked - input.partCost,
      similarJobs: observations.length,
      historicalAverage: historical || null,
    },
    disclaimer: "Recommendation only. Staff must approve the customer price.",
  };
}
