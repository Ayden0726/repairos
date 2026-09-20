import { Prisma } from "@prisma/client";
import type { Actor } from "./actor";
import { prisma } from "./db";

export async function audit(input: {
  actor?: Actor | null;
  action: string;
  entityType: string;
  entityId: string;
  oldValue?: unknown;
  newValue?: unknown;
  ip?: string;
  userAgent?: string;
}) {
  await prisma.auditEvent.create({
    data: {
      actorId: input.actor?.id,
      action: input.action,
      entityType: input.entityType,
      entityId: input.entityId,
      oldValue: (input.oldValue as Prisma.InputJsonValue) ?? undefined,
      newValue: (input.newValue as Prisma.InputJsonValue) ?? undefined,
      ip: input.ip,
      userAgent: input.userAgent,
    },
  });
}
