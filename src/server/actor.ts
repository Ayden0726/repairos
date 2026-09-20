import { cookies, headers } from "next/headers";
import type { PermissionKey } from "./permissions";
import { prisma } from "./db";
import { sha256 } from "./crypto";
import { ForbiddenError, UnauthorizedError } from "./errors";
import { SESSION_COOKIE } from "./auth/constants";

export type Actor = {
  id: string;
  email: string;
  name: string;
  isOwner: boolean;
  roleId: string;
  roleKey: string;
  roleName: string;
  permissions: Set<string>;
  totpEnabled: boolean;
  theme: string;
  defaultTicketView: string;
  dashboardConfig: unknown;
};

export function can(actor: Actor, permission: PermissionKey): boolean {
  return actor.isOwner || actor.permissions.has(permission);
}

export function assertCan(actor: Actor, permission: PermissionKey) {
  if (!can(actor, permission)) {
    throw new ForbiddenError();
  }
}

export async function getActor(): Promise<Actor | null> {
  const jar = await cookies();
  const token = jar.get(SESSION_COOKIE)?.value;
  if (!token) return null;
  const session = await prisma.session.findUnique({
    where: { tokenHash: sha256(token) },
    include: {
      user: {
        include: { role: { include: { permissions: true } }, preferences: true },
      },
    },
  });
  if (!session || session.expiresAt < new Date() || session.pendingTwoFactor) return null;
  if (session.user.status !== "ACTIVE" || session.user.archivedAt) return null;

  await prisma.session.update({
    where: { id: session.id },
    data: { lastSeenAt: new Date() },
  });

  return toActor(session.user);
}

export async function requireActor(): Promise<Actor> {
  const actor = await getActor();
  if (!actor) throw new UnauthorizedError();
  return actor;
}

export async function requirePermission(permission: PermissionKey): Promise<Actor> {
  const actor = await requireActor();
  assertCan(actor, permission);
  return actor;
}

export function toActor(user: {
  id: string;
  email: string;
  name: string;
  isOwner: boolean;
  totpEnabled: boolean;
  roleId: string;
  role: { key: string; name: string; permissions: { permission: string }[] };
  preferences: { theme: string; defaultTicketView: string; dashboardConfig: unknown } | null;
}): Actor {
  return {
    id: user.id,
    email: user.email,
    name: user.name,
    isOwner: user.isOwner,
    roleId: user.roleId,
    roleKey: user.role.key,
    roleName: user.role.name,
    totpEnabled: user.totpEnabled,
    permissions: new Set(user.role.permissions.map((p) => p.permission)),
    theme: user.preferences?.theme ?? "system",
    defaultTicketView: user.preferences?.defaultTicketView ?? "board",
    dashboardConfig: user.preferences?.dashboardConfig,
  };
}

export async function requestMeta() {
  const h = await headers();
  return {
    ip: h.get("x-forwarded-for")?.split(",")[0]?.trim() ?? h.get("x-real-ip") ?? "unknown",
    userAgent: h.get("user-agent") ?? "unknown",
  };
}
