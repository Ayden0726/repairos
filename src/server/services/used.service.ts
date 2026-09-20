import { assertCan, type Actor } from "../actor";
import { audit } from "../audit";
import { prisma } from "../db";
import { roundMoney } from "../money";
import { nextNumber } from "../numbering";
import { NotFoundError } from "../errors";

export function recommendOffer(input: {
  expectedResale: number;
  expectedRepairCost: number;
  feesAllowance: number;
  requiredProfit: number;
}) {
  const recommended = roundMoney(
    input.expectedResale - input.expectedRepairCost - input.feesAllowance - input.requiredProfit,
  );
  return {
    recommendedMaxOffer: Number(recommended),
    formula: {
      likelyResale: input.expectedResale,
      repairs: input.expectedRepairCost,
      feesWarrantyAllowance: input.feesAllowance,
      requiredProfit: input.requiredProfit,
    },
  };
}

export async function recordPurchase(
  actor: Actor,
  input: {
    sellerCustomerId: string;
    deviceSummary: string;
    serial?: string;
    imei?: string;
    conditionGrade: "A" | "B" | "C" | "D" | "FAULTY";
    faults?: string;
    expectedResale: number;
    expectedRepairCost: number;
    feesAllowance: number;
    requiredProfit: number;
    purchasePrice: number;
    overrideReason?: string;
    accessories?: string;
  },
) {
  assertCan(actor, "used.manage");
  const rec = recommendOffer(input);
  const number = await nextNumber("used", "BUY");
  const purchase = await prisma.usedDevicePurchase.create({
    data: {
      number,
      sellerCustomerId: input.sellerCustomerId,
      deviceSummary: input.deviceSummary,
      serial: input.serial,
      imei: input.imei,
      conditionGrade: input.conditionGrade,
      faults: input.faults,
      accessories: input.accessories,
      expectedResale: input.expectedResale,
      expectedRepairCost: input.expectedRepairCost,
      feesAllowance: input.feesAllowance,
      requiredProfit: input.requiredProfit,
      recommendedMaxOffer: rec.recommendedMaxOffer,
      purchasePrice: input.purchasePrice,
      overrideReason: input.overrideReason,
      status: "PURCHASED",
    },
  });
  await audit({ actor, action: "used.purchase", entityType: "UsedDevicePurchase", entityId: purchase.id });
  return { purchase, recommendation: rec };
}

export async function startRefurbishment(actor: Actor, purchaseId: string, typeId: string) {
  assertCan(actor, "used.manage");
  const purchase = await prisma.usedDevicePurchase.findUnique({ where: { id: purchaseId } });
  if (!purchase) throw new NotFoundError("Buy-in");
  const refurb = await prisma.refurbishment.create({
    data: { purchaseId, totalInvested: purchase.purchasePrice },
  });
  const { quickCreateTicket } = await import("./ticket.service");
  const ticket = await quickCreateTicket(actor, {
    customerId: purchase.sellerCustomerId,
    typeId,
    reportedIssue: `Refurbishment of ${purchase.deviceSummary}`,
  });
  await prisma.ticket.update({ where: { id: ticket.id }, data: { refurbishmentId: refurb.id } });
  await prisma.usedDevicePurchase.update({ where: { id: purchaseId }, data: { status: "REFURBISHING" } });
  return { refurb, ticket };
}

export async function markReadyForSale(actor: Actor, purchaseId: string, salePrice: number) {
  assertCan(actor, "used.manage");
  const purchase = await prisma.usedDevicePurchase.findUnique({
    where: { id: purchaseId },
    include: { refurbishment: true },
  });
  if (!purchase) throw new NotFoundError("Buy-in");
  const sku = `REF-${purchase.number}`;
  const item = await prisma.inventoryItem.create({
    data: {
      kind: "REFURBISHED_DEVICE",
      sku,
      name: purchase.deviceSummary,
      category: "Refurbished",
      onHand: 1,
      cost: purchase.refurbishment?.totalInvested ?? purchase.purchasePrice,
      salePrice,
    },
  });
  const serial = await prisma.serializedItem.create({
    data: {
      inventoryItemId: item.id,
      serialNumber: purchase.serial ?? sku,
      imei: purchase.imei,
      status: "IN_STOCK",
      cost: item.cost,
    },
  });
  await prisma.usedDevicePurchase.update({
    where: { id: purchaseId },
    data: { status: "READY_FOR_SALE", serializedItemId: serial.id },
  });
  if (purchase.refurbishment) {
    await prisma.refurbishment.update({
      where: { id: purchase.refurbishment.id },
      data: { salePrice, readyForSaleAt: new Date() },
    });
  }
  return { item, serial };
}
