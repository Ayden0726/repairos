import { Prisma } from "@prisma/client";
import { assertCan, type Actor } from "../actor";
import { audit } from "../audit";
import { prisma } from "../db";
import { ConflictError, NotFoundError, ValidationError } from "../errors";
import { notifyUsers } from "./notification.service";

export function availableQty(onHand: number, reserved: number) {
  return Math.max(0, onHand - reserved);
}

export async function listInventory(actor: Actor, kind?: string, q?: string) {
  assertCan(actor, "inventory.view");
  const where: Prisma.InventoryItemWhereInput = { archivedAt: null };
  if (kind) where.kind = kind as never;
  if (q) {
    where.OR = [
      { name: { contains: q, mode: "insensitive" } },
      { sku: { contains: q, mode: "insensitive" } },
      { barcode: { contains: q, mode: "insensitive" } },
      { manufacturer: { contains: q, mode: "insensitive" } },
    ];
  }
  const rows = await prisma.inventoryItem.findMany({
    where,
    include: { supplier: true, serialized: { where: { status: "IN_STOCK" } } },
    orderBy: { name: "asc" },
    take: 200,
  });
  return rows.map((row) => ({ ...row, available: availableQty(row.onHand, row.reserved) }));
}

export async function upsertItem(
  actor: Actor,
  input: {
    id?: string;
    kind?: "REPAIR_PART" | "PC_COMPONENT" | "ACCESSORY" | "REFURBISHED_DEVICE";
    sku: string;
    barcode?: string;
    name: string;
    category: string;
    pcCategory?: string;
    manufacturer?: string;
    supplierId?: string;
    cost: number;
    salePrice: number;
    minStock?: number;
    reorderQty?: number;
    onHand?: number;
    specifications?: Record<string, unknown>;
  },
) {
  assertCan(actor, "inventory.manage");
  const data = {
    kind: input.kind ?? "REPAIR_PART",
    sku: input.sku,
    barcode: input.barcode,
    name: input.name,
    category: input.category,
    pcCategory: input.pcCategory as never,
    manufacturer: input.manufacturer,
    supplierId: input.supplierId,
    cost: input.cost,
    salePrice: input.salePrice,
    minStock: input.minStock ?? 0,
    reorderQty: input.reorderQty ?? 1,
    specifications: input.specifications as never,
  };
  const item = input.id
    ? await prisma.inventoryItem.update({ where: { id: input.id }, data })
    : await prisma.inventoryItem.create({ data: { ...data, onHand: input.onHand ?? 0 } });
  await audit({ actor, action: input.id ? "inventory.update" : "inventory.create", entityType: "InventoryItem", entityId: item.id });
  return item;
}

export async function adjustStock(actor: Actor, itemId: string, quantity: number, reason: string) {
  assertCan(actor, "inventory.adjust");
  if (!reason.trim()) throw new ValidationError("A reason is required for inventory adjustments.");
  const item = await prisma.inventoryItem.findUnique({ where: { id: itemId } });
  if (!item) throw new NotFoundError("Part");
  const onHand = item.onHand + quantity;
  if (onHand < item.reserved) throw new ConflictError("Adjustment would drop on-hand below reserved quantity.");
  const updated = await prisma.inventoryItem.update({ where: { id: itemId }, data: { onHand } });
  await prisma.inventoryTransaction.create({
    data: { itemId, type: quantity >= 0 ? "adjust_in" : "adjust_out", quantity, reason, actorId: actor.id },
  });
  await audit({
    actor,
    action: "inventory.adjust",
    entityType: "InventoryItem",
    entityId: itemId,
    oldValue: { onHand: item.onHand },
    newValue: { onHand, reason },
  });
  return updated;
}

export async function reservePart(actor: Actor, input: { itemId: string; quantity: number; ticketId?: string; pcBuildId?: string }) {
  assertCan(actor, "inventory.manage");
  const item = await prisma.inventoryItem.findUnique({ where: { id: input.itemId } });
  if (!item) throw new NotFoundError("Part");
  const available = availableQty(item.onHand, item.reserved);
  if (input.quantity > available) throw new ConflictError("Not enough available stock to reserve.");
  const target = input.pcBuildId ? "PC_BUILD" : "TICKET";
  const [reservation] = await prisma.$transaction([
    prisma.stockReservation.create({
      data: {
        itemId: item.id,
        quantity: input.quantity,
        target,
        ticketId: input.ticketId,
        pcBuildId: input.pcBuildId,
      },
    }),
    prisma.inventoryItem.update({
      where: { id: item.id },
      data: { reserved: { increment: input.quantity } },
    }),
  ]);
  if (input.ticketId) {
    await prisma.ticketPart.create({
      data: {
        ticketId: input.ticketId,
        itemId: item.id,
        name: item.name,
        sku: item.sku,
        quantity: input.quantity,
        status: "RESERVED",
        unitCost: item.cost,
        unitPrice: item.salePrice,
      },
    });
    await prisma.ticketEvent.create({
      data: {
        ticketId: input.ticketId,
        actorId: actor.id,
        type: "parts",
        summary: `Reserved ${input.quantity} × ${item.name}`,
        visibility: "INTERNAL",
      },
    });
  }
  return reservation;
}

export async function addNeededPart(
  actor: Actor,
  ticketId: string,
  input: { itemId?: string; name: string; quantity: number; unitCost?: number; unitPrice?: number },
) {
  assertCan(actor, "tickets.edit");
  const item = input.itemId ? await prisma.inventoryItem.findUnique({ where: { id: input.itemId } }) : null;
  let status: "IN_STOCK" | "NEED_TO_ORDER" = "NEED_TO_ORDER";
  if (item && availableQty(item.onHand, item.reserved) >= input.quantity) status = "IN_STOCK";
  const part = await prisma.ticketPart.create({
    data: {
      ticketId,
      itemId: item?.id,
      name: input.name,
      sku: item?.sku,
      quantity: input.quantity,
      status,
      unitCost: input.unitCost ?? item?.cost ?? 0,
      unitPrice: input.unitPrice ?? item?.salePrice ?? 0,
    },
  });
  if (status === "NEED_TO_ORDER") {
    const waiting = await prisma.ticketStatus.findUnique({ where: { key: "waiting_parts" } });
    if (waiting) {
      await prisma.ticket.update({
        where: { id: ticketId },
        data: { statusId: waiting.id, waitingForParts: true },
      });
    }
  }
  return part;
}

export async function markPartReceived(actor: Actor, ticketPartId: string) {
  assertCan(actor, "inventory.manage");
  const part = await prisma.ticketPart.update({
    where: { id: ticketPartId },
    data: { status: "RECEIVED" },
    include: { ticket: true, item: true },
  });
  if (part.itemId) {
    await prisma.inventoryItem.update({ where: { id: part.itemId }, data: { onHand: { increment: part.quantity } } });
    await prisma.inventoryTransaction.create({
      data: { itemId: part.itemId, type: "receive", quantity: part.quantity, actorId: actor.id, reference: part.ticketId },
    });
  }
  const remaining = await prisma.ticketPart.count({
    where: { ticketId: part.ticketId, status: { in: ["NEED_TO_ORDER", "ORDERED"] } },
  });
  if (remaining === 0 && part.ticket.assignedToId) {
    await notifyUsers([part.ticket.assignedToId], {
      title: "Parts received",
      body: `Parts for ${part.ticket.ticketNumber} have arrived.`,
      href: `/tickets/${part.ticketId}`,
      ticketId: part.ticketId,
    });
    const progress = await prisma.ticketStatus.findUnique({ where: { key: "repair_progress" } });
    if (progress) {
      await prisma.ticket.update({
        where: { id: part.ticketId },
        data: { waitingForParts: false, statusId: progress.id },
      });
    }
  }
  return part;
}

export async function receivePurchaseOrder(actor: Actor, poId: string, received: Array<{ id: string; quantity: number }>) {
  assertCan(actor, "inventory.purchase_orders");
  const po = await prisma.purchaseOrder.findUnique({
    where: { id: poId },
    include: { items: true },
  });
  if (!po) throw new NotFoundError("Purchase order");
  for (const line of received) {
    const item = po.items.find((i) => i.id === line.id);
    if (!item) continue;
    const qty = Math.min(line.quantity, item.quantity - item.receivedQty);
    if (qty <= 0) continue;
    await prisma.purchaseOrderItem.update({
      where: { id: item.id },
      data: { receivedQty: { increment: qty } },
    });
    await prisma.inventoryItem.update({
      where: { id: item.itemId },
      data: { onHand: { increment: qty }, cost: item.unitCost },
    });
    await prisma.partCostHistory.create({
      data: { itemId: item.itemId, supplierId: po.supplierId, unitCost: item.unitCost, source: po.number },
    });
    await prisma.inventoryTransaction.create({
      data: { itemId: item.itemId, type: "po_receive", quantity: qty, actorId: actor.id, reference: po.number },
    });
  }
  const fresh = await prisma.purchaseOrder.findUnique({ where: { id: poId }, include: { items: true } });
  const all = fresh!.items.every((i) => i.receivedQty >= i.quantity);
  const some = fresh!.items.some((i) => i.receivedQty > 0);
  await prisma.purchaseOrder.update({
    where: { id: poId },
    data: { status: all ? "RECEIVED" : some ? "PARTIALLY_RECEIVED" : po.status, receivedAt: all ? new Date() : undefined },
  });
  await audit({ actor, action: "po.receive", entityType: "PurchaseOrder", entityId: poId });
  return fresh;
}

export async function completeStocktake(actor: Actor, stocktakeId: string, lines: Array<{ id: string; actualQty: number; reason?: string }>) {
  assertCan(actor, "inventory.adjust");
  for (const line of lines) {
    const existing = await prisma.stocktakeLine.findUnique({ where: { id: line.id }, include: { item: true } });
    if (!existing) continue;
    const variance = line.actualQty - existing.expectedQty;
    if (variance !== 0 && !line.reason) {
      throw new ValidationError(`Reason required for variance on ${existing.item.name}.`);
    }
    await prisma.stocktakeLine.update({
      where: { id: line.id },
      data: { actualQty: line.actualQty, variance, reason: line.reason },
    });
    if (variance !== 0) {
      await adjustStock(actor, existing.itemId, variance, line.reason ?? "Stocktake");
    }
  }
  return prisma.stocktake.update({
    where: { id: stocktakeId },
    data: { status: "completed", completedAt: new Date() },
  });
}
