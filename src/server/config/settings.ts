import { Prisma } from "@prisma/client";
import { prisma } from "../db";

export async function getSetting<T>(key: string, fallback: T): Promise<T> {
  const row = await prisma.setting.findUnique({ where: { key } });
  if (!row) return fallback;
  return row.value as T;
}

export async function setSetting(key: string, value: unknown, updatedById?: string) {
  await prisma.setting.upsert({
    where: { key },
    create: { key, value: value as Prisma.InputJsonValue, updatedById },
    update: { value: value as Prisma.InputJsonValue, updatedById },
  });
}

export async function isSetupComplete(): Promise<boolean> {
  return getSetting("setup.completed", false);
}

export type BusinessProfile = {
  name: string;
  logoKey?: string | null;
  addressLine1?: string;
  addressLine2?: string;
  suburb?: string;
  state?: string;
  postcode?: string;
  phone?: string;
  email?: string;
  abn?: string;
  website?: string;
};

export type GstSettings = {
  registered: boolean;
  rate: number;
  inclusiveDefault: boolean;
};

export type NumberingSettings = {
  ticketFormat: string;
  invoicePrefix: string;
  quotePrefix: string;
  poPrefix: string;
  pcPrefix: string;
};

export const defaultBusiness: BusinessProfile = {
  name: "",
  state: "VIC",
};

export const defaultGst: GstSettings = {
  registered: true,
  rate: 0.1,
  inclusiveDefault: true,
};

export const defaultNumbering: NumberingSettings = {
  ticketFormat: "{PREFIX}-{YEAR}-{SEQ:5}",
  invoicePrefix: "INV",
  quotePrefix: "QTE",
  poPrefix: "PO",
  pcPrefix: "PCB",
};
