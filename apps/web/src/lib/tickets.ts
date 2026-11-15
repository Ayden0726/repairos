import { prisma } from "@/lib/db";
import { brisbaneYear } from "@/lib/time";
import { DEFAULT_LABOUR_FEE_CENTS } from "@/lib/money";
import type { Prisma, TicketStatus } from "@prisma/client";

export async function ensureBusinessSettings() {
  const year = brisbaneYear();
  const existing = await prisma.businessSettings.findUnique({ where: { id: 1 } });
  if (existing) {
    if (existing.ticketSeqYear !== year) {
      return prisma.businessSettings.update({
        where: { id: 1 },
        data: { ticketSeqYear: year, ticketSeqNext: 1 },
      });
    }
    return existing;
  }
  return prisma.businessSettings.create({
    data: {
      id: 1,
      businessName: "TechBench NQ",
      timezone: "Australia/Brisbane",
      currency: "AUD",
      gstRegistered: false,
      gstRateBp: 1000,
      defaultLabourFeeCents: DEFAULT_LABOUR_FEE_CENTS,
      ticketSeqYear: year,
      ticketSeqNext: 1,
    },
  });
}

export async function nextTicketNumber(tx: Prisma.TransactionClient): Promise<string> {
  const year = brisbaneYear();
  const settings = await tx.businessSettings.findUnique({ where: { id: 1 } });
  if (!settings) throw new Error("Business settings missing");

  let seq = settings.ticketSeqNext;
  let seqYear = settings.ticketSeqYear;
  if (seqYear !== year) {
    seqYear = year;
    seq = 1;
  }

  await tx.businessSettings.update({
    where: { id: 1 },
    data: { ticketSeqYear: seqYear, ticketSeqNext: seq + 1 },
  });

  return `TB-${seqYear}-${String(seq).padStart(6, "0")}`;
}

export async function createStatusHistory(
  tx: Prisma.TransactionClient,
  args: {
    ticketId: string;
    actorId: string;
    fromStatus: TicketStatus | null;
    toStatus: TicketStatus;
    comment?: string;
  },
) {
  return tx.ticketStatusHistory.create({
    data: {
      ticketId: args.ticketId,
      actorId: args.actorId,
      fromStatus: args.fromStatus,
      toStatus: args.toStatus,
      comment: args.comment,
    },
  });
}
