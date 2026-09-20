import { Prisma } from "@prisma/client";

export type Money = Prisma.Decimal;

export function dec(value: number | string | Prisma.Decimal | null | undefined): Prisma.Decimal {
  if (value === null || value === undefined) return new Prisma.Decimal(0);
  return new Prisma.Decimal(value);
}

export function toNumber(value: Prisma.Decimal | number | string | null | undefined): number {
  return Number(dec(value));
}

export function roundMoney(value: Prisma.Decimal | number): Prisma.Decimal {
  return dec(value).toDecimalPlaces(2);
}

export function add(...values: Array<Prisma.Decimal | number | string>): Prisma.Decimal {
  return roundMoney(values.reduce((sum: Prisma.Decimal, value) => sum.add(dec(value)), new Prisma.Decimal(0)));
}

export function subtract(a: Prisma.Decimal | number, b: Prisma.Decimal | number): Prisma.Decimal {
  return roundMoney(dec(a).sub(dec(b)));
}

export function multiply(a: Prisma.Decimal | number, b: Prisma.Decimal | number): Prisma.Decimal {
  return roundMoney(dec(a).mul(dec(b)));
}

export function gstSplit(amountInclusive: Prisma.Decimal | number, rate = 0.1) {
  const inclusive = dec(amountInclusive);
  const gst = roundMoney(inclusive.mul(rate).div(1 + rate));
  const exclusive = roundMoney(inclusive.sub(gst));
  return { inclusive, exclusive, gst };
}

export function applyGst(exclusive: Prisma.Decimal | number, rate = 0.1) {
  const ex = dec(exclusive);
  const gst = roundMoney(ex.mul(rate));
  return { exclusive: roundMoney(ex), gst, inclusive: roundMoney(ex.add(gst)) };
}

export function formatAud(value: Prisma.Decimal | number | string | null | undefined): string {
  return new Intl.NumberFormat("en-AU", { style: "currency", currency: "AUD" }).format(toNumber(value));
}

export function margin(revenue: Prisma.Decimal | number, cost: Prisma.Decimal | number) {
  const rev = dec(revenue);
  const c = dec(cost);
  const profit = roundMoney(rev.sub(c));
  const marginPct = rev.eq(0) ? new Prisma.Decimal(0) : profit.div(rev);
  return { profit, margin: marginPct };
}
