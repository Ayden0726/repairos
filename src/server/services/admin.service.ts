import { assertCan, type Actor } from "../actor";
import { prisma } from "../db";
import { audit } from "../audit";
import { ALL_PERMISSION_KEYS, type PermissionKey } from "../permissions";
import { ValidationError } from "../errors";

export async function listRoles(actor: Actor) {
  assertCan(actor, "roles.manage");
  return prisma.role.findMany({ include: { permissions: true, _count: { select: { users: true } } }, orderBy: { name: "asc" } });
}

export async function saveRole(actor: Actor, input: { id?: string; name: string; description?: string; permissions: string[] }) {
  assertCan(actor, "roles.manage");
  const perms = input.permissions.filter((p): p is PermissionKey => ALL_PERMISSION_KEYS.includes(p as PermissionKey));
  if (input.id) {
    const role = await prisma.role.findUnique({ where: { id: input.id } });
    if (role?.isSystem && role.key === "owner") throw new ValidationError("The owner role cannot be narrowed.");
    await prisma.rolePermission.deleteMany({ where: { roleId: input.id } });
    await prisma.rolePermission.createMany({ data: perms.map((permission) => ({ roleId: input.id!, permission })) });
    await prisma.role.update({ where: { id: input.id }, data: { name: input.name, description: input.description } });
    await audit({ actor, action: "role.update", entityType: "Role", entityId: input.id, newValue: { permissions: perms } });
    return;
  }
  const key = input.name.toLowerCase().replace(/[^a-z0-9]+/g, "_");
  const role = await prisma.role.create({ data: { key, name: input.name, description: input.description } });
  await prisma.rolePermission.createMany({ data: perms.map((permission) => ({ roleId: role.id, permission })) });
  await audit({ actor, action: "role.create", entityType: "Role", entityId: role.id });
}

export async function listStaff(actor: Actor) {
  assertCan(actor, "staff.view");
  return prisma.user.findMany({
    where: { archivedAt: null },
    include: { role: true },
    orderBy: { name: "asc" },
  });
}

export async function saveCustomField(actor: Actor, input: {
  id?: string;
  entityType: string;
  ticketTypeId?: string;
  key: string;
  label: string;
  fieldType: string;
  options?: unknown;
  required?: boolean;
  sortOrder?: number;
}) {
  assertCan(actor, "settings.manage");
  if (input.id) {
    return prisma.customFieldDefinition.update({
      where: { id: input.id },
      data: input as never,
    });
  }
  return prisma.customFieldDefinition.create({ data: input as never });
}
