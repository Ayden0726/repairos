import { cookies } from "next/headers";
import { prisma } from "../db";
import { randomToken, sha256 } from "../crypto";
import { SESSION_COOKIE, SESSION_DAYS, PENDING_2FA_MINUTES } from "./constants";
import { isProd } from "../env";

export async function createSession(userId: string, pendingTwoFactor = false, meta?: { ip?: string; userAgent?: string }) {
  const token = randomToken(32);
  const days = pendingTwoFactor ? PENDING_2FA_MINUTES / (60 * 24) : SESSION_DAYS;
  const expiresAt = new Date(Date.now() + days * 24 * 60 * 60 * 1000);
  await prisma.session.create({
    data: {
      userId,
      tokenHash: sha256(token),
      expiresAt,
      pendingTwoFactor,
      ip: meta?.ip,
      userAgent: meta?.userAgent,
    },
  });
  const jar = await cookies();
  jar.set(SESSION_COOKIE, token, {
    httpOnly: true,
    sameSite: "lax",
    secure: isProd(),
    path: "/",
    expires: expiresAt,
  });
  return { token, expiresAt, pendingTwoFactor };
}

export async function destroySession() {
  const jar = await cookies();
  const token = jar.get(SESSION_COOKIE)?.value;
  if (token) {
    await prisma.session.deleteMany({ where: { tokenHash: sha256(token) } });
  }
  jar.delete(SESSION_COOKIE);
}

export async function promoteSessionAfter2fa() {
  const jar = await cookies();
  const token = jar.get(SESSION_COOKIE)?.value;
  if (!token) return;
  const expiresAt = new Date(Date.now() + SESSION_DAYS * 24 * 60 * 60 * 1000);
  await prisma.session.updateMany({
    where: { tokenHash: sha256(token) },
    data: { pendingTwoFactor: false, expiresAt },
  });
  jar.set(SESSION_COOKIE, token, {
    httpOnly: true,
    sameSite: "lax",
    secure: isProd(),
    path: "/",
    expires: expiresAt,
  });
}

export async function getRawSession() {
  const jar = await cookies();
  const token = jar.get(SESSION_COOKIE)?.value;
  if (!token) return null;
  return prisma.session.findUnique({
    where: { tokenHash: sha256(token) },
    include: { user: { include: { role: true } } },
  });
}
