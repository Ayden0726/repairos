import { prisma } from "./db";

export async function nextNumber(kind: string, prefix: string, pad = 5): Promise<string> {
  const year = new Date().getFullYear();
  const rows = await prisma.$queryRaw<Array<{ lastNumber: number }>>`
    INSERT INTO "DocumentNumberSequence" (id, kind, year, "lastNumber")
    VALUES (${`${kind}-${year}`}, ${kind}, ${year}, 1)
    ON CONFLICT (kind, year)
    DO UPDATE SET "lastNumber" = "DocumentNumberSequence"."lastNumber" + 1
    RETURNING "lastNumber"
  `;
  const n = rows[0]?.lastNumber ?? 1;
  return `${prefix}-${year}-${String(n).padStart(pad, "0")}`;
}

export async function nextTicketNumber(prefix: string, pad = 5): Promise<string> {
  const year = new Date().getFullYear();
  const rows = await prisma.$queryRaw<Array<{ lastNumber: number }>>`
    INSERT INTO "TicketNumberSequence" (id, prefix, year, "lastNumber")
    VALUES (${`${prefix}-${year}`}, ${prefix}, ${year}, 1)
    ON CONFLICT (prefix, year)
    DO UPDATE SET "lastNumber" = "TicketNumberSequence"."lastNumber" + 1
    RETURNING "lastNumber"
  `;
  const n = rows[0]?.lastNumber ?? 1;
  return `${prefix}-${year}-${String(n).padStart(pad, "0")}`;
}
