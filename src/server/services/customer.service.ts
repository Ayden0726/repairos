import { Prisma } from "@prisma/client";
import { assertCan, type Actor } from "../actor";
import { audit } from "../audit";
import { prisma } from "../db";
import { NotFoundError, ValidationError } from "../errors";

export async function listCustomers(actor: Actor, q?: string, type?: string) {
  assertCan(actor, "customers.view");
  const where: Prisma.CustomerWhereInput = { archivedAt: null };
  if (type === "BUSINESS" || type === "INDIVIDUAL") where.type = type;
  if (q) {
    where.OR = [
      { displayName: { contains: q, mode: "insensitive" } },
      { companyName: { contains: q, mode: "insensitive" } },
      { phone: { contains: q, mode: "insensitive" } },
      { email: { contains: q, mode: "insensitive" } },
    ];
  }
  return prisma.customer.findMany({
    where,
    include: { pricingGroup: true, _count: { select: { tickets: true, devices: true } } },
    orderBy: { displayName: "asc" },
    take: 100,
  });
}

export async function getCustomer(actor: Actor, id: string) {
  assertCan(actor, "customers.view");
  const customer = await prisma.customer.findFirst({
    where: { id, archivedAt: null },
    include: {
      pricingGroup: true,
      contacts: true,
      devices: { include: { deviceModel: true } },
      tickets: { include: { status: true, type: true, priority: true }, orderBy: { createdAt: "desc" }, take: 50 },
      quotes: { orderBy: { createdAt: "desc" }, take: 20 },
      invoices: { orderBy: { createdAt: "desc" }, take: 20 },
      pcBuilds: { orderBy: { createdAt: "desc" } },
      communications: { orderBy: { createdAt: "desc" }, take: 30 },
      warranties: true,
      usedPurchases: { take: 10, orderBy: { createdAt: "desc" } },
      privacyConsents: { include: { template: true } },
    },
  });
  if (!customer) throw new NotFoundError("Customer");
  return customer;
}

export async function upsertCustomer(
  actor: Actor,
  input: {
    id?: string;
    type: "INDIVIDUAL" | "BUSINESS";
    firstName?: string;
    lastName?: string;
    companyName?: string;
    phone?: string;
    email?: string;
    billingEmail?: string;
    addressLine1?: string;
    suburb?: string;
    state?: string;
    postcode?: string;
    preferredContact?: "SMS" | "EMAIL" | "PHONE";
    notes?: string;
    pricingGroupId?: string;
    abn?: string;
    contacts?: Array<{ name: string; role: "PRIMARY" | "BILLING" | "TECHNICAL" | "OTHER"; phone?: string; email?: string }>;
  },
) {
  assertCan(actor, "customers.manage");
  const displayName =
    input.type === "BUSINESS"
      ? input.companyName?.trim() || "Business customer"
      : [input.firstName, input.lastName].filter(Boolean).join(" ") || "Customer";
  if (input.type === "INDIVIDUAL" && !input.firstName && !input.lastName && !input.phone) {
    throw new ValidationError("Enter a name or phone number.");
  }
  const group =
    input.pricingGroupId ||
    (await prisma.pricingGroup.findFirst({ where: { isDefault: true } }))?.id;
  if (!group) throw new ValidationError("Create a pricing group first.");

  const data = {
    type: input.type,
    firstName: input.firstName,
    lastName: input.lastName,
    displayName,
    companyName: input.companyName,
    phone: input.phone,
    email: input.email,
    billingEmail: input.billingEmail,
    addressLine1: input.addressLine1,
    suburb: input.suburb,
    state: input.state,
    postcode: input.postcode,
    preferredContact: input.preferredContact ?? "SMS",
    notes: input.notes,
    pricingGroupId: group,
    abn: input.abn,
  };

  const customer = input.id
    ? await prisma.customer.update({ where: { id: input.id }, data })
    : await prisma.customer.create({ data });

  if (input.contacts) {
    await prisma.customerContact.deleteMany({ where: { customerId: customer.id } });
    if (input.contacts.length) {
      await prisma.customerContact.createMany({
        data: input.contacts.map((c) => ({ ...c, customerId: customer.id })),
      });
    }
  }
  await audit({
    actor,
    action: input.id ? "customer.update" : "customer.create",
    entityType: "Customer",
    entityId: customer.id,
    newValue: { displayName },
  });
  return customer;
}

export async function archiveCustomer(actor: Actor, id: string) {
  assertCan(actor, "customers.manage");
  await prisma.customer.update({ where: { id }, data: { archivedAt: new Date() } });
  await audit({ actor, action: "customer.archive", entityType: "Customer", entityId: id });
}

export async function addCustomerDevice(
  actor: Actor,
  customerId: string,
  input: {
    deviceModelId?: string;
    deviceType?: string;
    manufacturer?: string;
    modelName?: string;
    serial?: string;
    imei?: string;
    colour?: string;
    storage?: string;
    notes?: string;
  },
) {
  assertCan(actor, "customers.manage");
  return prisma.customerDevice.create({
    data: { customerId, ...input },
  });
}
