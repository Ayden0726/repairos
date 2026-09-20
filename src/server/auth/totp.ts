import { Secret, TOTP } from "otpauth";
import { decryptString, encryptString, sha256 } from "../crypto";
import { prisma } from "../db";
import { randomBytes } from "node:crypto";

export function generateTotpSecret(accountName: string, issuer = "WorkshopOS") {
  const secret = new Secret({ size: 20 });
  const totp = new TOTP({ issuer, label: accountName, algorithm: "SHA1", digits: 6, period: 30, secret });
  return {
    secret: secret.base32,
    encrypted: encryptString(secret.base32),
    uri: totp.toString(),
  };
}

export function verifyTotp(secretBase32: string, token: string): boolean {
  const totp = new TOTP({
    issuer: "WorkshopOS",
    label: "user",
    algorithm: "SHA1",
    digits: 6,
    period: 30,
    secret: Secret.fromBase32(secretBase32),
  });
  return totp.validate({ token: token.replace(/\s/g, ""), window: 1 }) !== null;
}

export function generateRecoveryCodes(count = 10): string[] {
  return Array.from({ length: count }, () => randomBytes(5).toString("hex").toUpperCase());
}

export async function storeRecoveryCodes(userId: string, codes: string[]) {
  await prisma.recoveryCode.deleteMany({ where: { userId, usedAt: null } });
  await prisma.recoveryCode.createMany({
    data: codes.map((code) => ({ userId, codeHash: sha256(code.toUpperCase()) })),
  });
}

export async function consumeRecoveryCode(userId: string, code: string): Promise<boolean> {
  const row = await prisma.recoveryCode.findFirst({
    where: { userId, codeHash: sha256(code.toUpperCase()), usedAt: null },
  });
  if (!row) return false;
  await prisma.recoveryCode.update({ where: { id: row.id }, data: { usedAt: new Date() } });
  return true;
}

export function revealTotpSecret(encrypted: string): string {
  return decryptString(encrypted);
}
