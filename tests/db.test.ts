import { beforeAll, describe, expect, it } from "vitest";
import { PrismaClient } from "@prisma/client";
import { hashPassword } from "../src/server/auth/password";
import { decryptString, encryptString } from "../src/server/crypto";
import { availableQty } from "../src/server/services/inventory.service";
import { toActor } from "../src/server/actor";
import { createStaffUser, updateStaffStatus } from "../src/server/services/auth.service";

const prisma = new PrismaClient();

beforeAll(() => {
  process.env.ENCRYPTION_KEY ||= "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
  process.env.SESSION_SECRET ||= "test-session-secret-32chars-minimum";
  process.env.DATABASE_URL ||= "postgresql://workshopos:workshopos_dev@127.0.0.1:5432/workshopos?schema=public";
});

describe("crypto", () => {
  it("round-trips secrets", () => {
    const cipher = encryptString("2580");
    expect(cipher).not.toContain("2580");
    expect(decryptString(cipher)).toBe("2580");
  });
});

describe("inventory math", () => {
  it("prevents negative available", () => {
    expect(availableQty(3, 5)).toBe(0);
    expect(availableQty(10, 2)).toBe(8);
  });
});

describe("seeded workshop", () => {
  it("has owner and open tickets", async () => {
    const owner = await prisma.user.findUnique({ where: { email: "maya@riversidetech.com.au" } });
    expect(owner?.isOwner).toBe(true);
    const tickets = await prisma.ticket.count({ where: { archivedAt: null } });
    expect(tickets).toBeGreaterThan(3);
  });

  it("stores device credentials encrypted", async () => {
    const cred = await prisma.deviceCredential.findFirst({ where: { deletedAt: null } });
    expect(cred?.ciphertext.startsWith("v1:")).toBe(true);
    expect(decryptString(cred!.ciphertext)).toBe("2580");
  });

  it("hashes passwords with argon2", async () => {
    const user = await prisma.user.findUnique({ where: { email: "maya@riversidetech.com.au" } });
    expect(user?.passwordHash.startsWith("$argon2")).toBe(true);
    const again = await hashPassword("Riverside!2026");
    expect(again.startsWith("$argon2")).toBe(true);
  });

  it("lets the owner add a technician login", async () => {
    const owner = await prisma.user.findUnique({
      where: { email: "maya@riversidetech.com.au" },
      include: { role: { include: { permissions: true } }, preferences: true },
    });
    const techRole = await prisma.role.findUnique({ where: { key: "technician" } });
    expect(owner && techRole).toBeTruthy();
    const email = `test-tech-${Date.now()}@riversidetech.com.au`;
    const created = await createStaffUser(toActor(owner!), {
      name: "Casual Bench",
      email,
      password: "Workshop!2026",
      roleId: techRole!.id,
    });
    expect(created.email).toBe(email);
    expect(created.isOwner).toBe(false);
    await prisma.user.delete({ where: { id: created.id } });
  });

  it("does not let the owner suspend themselves", async () => {
    const owner = await prisma.user.findUnique({
      where: { email: "maya@riversidetech.com.au" },
      include: { role: { include: { permissions: true } }, preferences: true },
    });
    await expect(updateStaffStatus(toActor(owner!), owner!.id, "SUSPENDED")).rejects.toThrow("your own account");
  });
});
