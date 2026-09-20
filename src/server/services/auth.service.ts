import { z } from "zod";
import { prisma } from "../db";
import { audit } from "../audit";
import { hashPassword, validatePasswordStrength, verifyPassword } from "../auth/password";
import { createSession, destroySession, getRawSession, promoteSessionAfter2fa } from "../auth/session";
import { consumeRecoveryCode, generateRecoveryCodes, generateTotpSecret, storeRecoveryCodes, verifyTotp, revealTotpSecret } from "../auth/totp";
import { rateLimit } from "../redis";
import { AppError, RateLimitError, UnauthorizedError, ValidationError } from "../errors";
import { assertCan, requestMeta, type Actor } from "../actor";
import { getSetting } from "../config/settings";

const loginSchema = z.object({
  email: z.string().email(),
  password: z.string().min(1),
});

export async function login(input: unknown) {
  const data = loginSchema.parse(input);
  const email = data.email.toLowerCase().trim();
  const meta = await requestMeta();
  const allowed = await rateLimit(`login:${email}:${meta.ip}`, 8, 15 * 60);
  if (!allowed) throw new RateLimitError();

  const user = await prisma.user.findUnique({
    where: { email },
    include: { role: true },
  });
  const ok = user ? await verifyPassword(user.passwordHash, data.password) : false;
  await prisma.loginAttempt.create({
    data: { email, ip: meta.ip, success: ok, reason: ok ? null : "invalid" },
  });
  if (!user || !ok || user.status !== "ACTIVE" || user.archivedAt) {
    throw new UnauthorizedError("Email or password is incorrect.");
  }

  const policy = await getSetting("security.twoFactor", {
    requireOwners: true,
    requireAdmins: true,
    requireManagers: false,
    requireRemote: false,
    requireAll: false,
  });
  const roleRequires =
    policy.requireAll ||
    (policy.requireOwners && user.isOwner) ||
    (policy.requireAdmins && (user.role.key === "admin" || user.role.key === "owner")) ||
    (policy.requireManagers && user.role.key === "manager");

  await prisma.user.update({
    where: { id: user.id },
    data: { lastLoginAt: new Date(), lastLoginIp: meta.ip },
  });

  if (user.totpEnabled || roleRequires) {
    await createSession(user.id, true, meta);
    return { requiresTwoFactor: true, totpEnabled: user.totpEnabled, mustEnrol: roleRequires && !user.totpEnabled };
  }

  await createSession(user.id, false, meta);
  await audit({ actor: { id: user.id } as Actor, action: "auth.login", entityType: "User", entityId: user.id, ip: meta.ip });
  return { requiresTwoFactor: false };
}

export async function verifyTwoFactor(token: string, recoveryCode?: string) {
  const session = await getRawSession();
  if (!session?.pendingTwoFactor) throw new UnauthorizedError();
  const user = await prisma.user.findUnique({ where: { id: session.userId } });
  if (!user) throw new UnauthorizedError();

  let ok = false;
  if (recoveryCode) {
    ok = await consumeRecoveryCode(user.id, recoveryCode);
  } else if (user.totpSecretEnc) {
    ok = verifyTotp(revealTotpSecret(user.totpSecretEnc), token);
  }
  if (!ok) throw new UnauthorizedError("That verification code is not valid.");
  await promoteSessionAfter2fa();
  const meta = await requestMeta();
  await audit({ actor: { id: user.id } as Actor, action: "auth.2fa", entityType: "User", entityId: user.id, ip: meta.ip });
  return { ok: true };
}

export async function logout() {
  await destroySession();
}

export async function beginTotpEnrolment(actor: Actor) {
  const secret = generateTotpSecret(actor.email);
  await prisma.user.update({
    where: { id: actor.id },
    data: { totpSecretEnc: secret.encrypted, totpEnabled: false },
  });
  return { uri: secret.uri, secret: secret.secret };
}

export async function confirmTotpEnrolment(actor: Actor, token: string) {
  const user = await prisma.user.findUnique({ where: { id: actor.id } });
  if (!user?.totpSecretEnc) throw new ValidationError("Start two-factor setup first.");
  if (!verifyTotp(revealTotpSecret(user.totpSecretEnc), token)) {
    throw new ValidationError("That authenticator code is not valid.");
  }
  const codes = generateRecoveryCodes();
  await storeRecoveryCodes(user.id, codes);
  await prisma.user.update({
    where: { id: user.id },
    data: { totpEnabled: true, totpConfirmedAt: new Date() },
  });
  await audit({ actor, action: "auth.2fa.enable", entityType: "User", entityId: user.id });
  return { recoveryCodes: codes };
}

export async function createStaffUser(
  actor: Actor,
  input: { name: string; email: string; password: string; roleId: string; phone?: string },
) {
  assertCan(actor, "staff.manage");
  const data = z
    .object({
      name: z.string().trim().min(1, "Name is required."),
      email: z.string().email("Enter a valid email."),
      password: z.string().min(1),
      roleId: z.string().min(1, "Choose a role."),
      phone: z.string().trim().optional(),
    })
    .parse(input);
  const problem = validatePasswordStrength(data.password);
  if (problem) throw new ValidationError(problem);
  const role = await prisma.role.findUnique({ where: { id: data.roleId } });
  if (!role) throw new ValidationError("Choose a valid role.");
  if (role.key === "owner") {
    throw new ValidationError("The owner login is created during setup. Add an Admin if you need a second full-access account.");
  }
  const email = data.email.toLowerCase();
  const existing = await prisma.user.findUnique({ where: { email } });
  if (existing) throw new AppError("EXISTS", "A staff member with that email already exists.");
  const user = await prisma.user.create({
    data: {
      name: data.name,
      email,
      phone: data.phone || null,
      passwordHash: await hashPassword(data.password),
      roleId: data.roleId,
      preferences: { create: {} },
    },
  });
  await audit({ actor, action: "staff.create", entityType: "User", entityId: user.id, newValue: { email: user.email, role: role.key } });
  return user;
}

export async function updateStaffStatus(actor: Actor, userId: string, status: "ACTIVE" | "SUSPENDED") {
  assertCan(actor, "staff.manage");
  if (userId === actor.id) throw new ValidationError("You cannot suspend your own account.");
  const target = await prisma.user.findUnique({ where: { id: userId } });
  if (!target || target.archivedAt) throw new ValidationError("That staff member was not found.");
  if (status === "SUSPENDED" && target.isOwner) {
    const otherOwners = await prisma.user.count({
      where: { isOwner: true, status: "ACTIVE", archivedAt: null, id: { not: userId } },
    });
    if (otherOwners === 0) throw new ValidationError("You cannot suspend the only owner.");
  }
  const user = await prisma.user.update({ where: { id: userId }, data: { status } });
  await audit({ actor, action: "staff.status", entityType: "User", entityId: userId, newValue: { status } });
  return user;
}

export async function savePreferences(actor: Actor, data: { theme?: string; defaultTicketView?: string; dashboardConfig?: unknown }) {
  return prisma.userPreference.upsert({
    where: { userId: actor.id },
    create: {
      userId: actor.id,
      theme: data.theme ?? "system",
      defaultTicketView: data.defaultTicketView ?? "board",
      dashboardConfig: data.dashboardConfig as never,
    },
    update: {
      theme: data.theme,
      defaultTicketView: data.defaultTicketView,
      dashboardConfig: data.dashboardConfig as never,
    },
  });
}
