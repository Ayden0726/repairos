import { describe, expect, it } from "vitest";
import { applyGst, formatAud, gstSplit, margin } from "../src/server/money";
import { assessCompatibility } from "../src/server/services/pcbuild.service";
import { recommendOffer } from "../src/server/services/used.service";
import { ROLE_PERMISSIONS, ALL_PERMISSION_KEYS } from "../src/server/permissions";
import { validatePasswordStrength } from "../src/server/auth/password";
import { sanitiseForAi } from "../src/server/integrations/ai";

describe("GST", () => {
  it("splits inclusive 10% GST", () => {
    const { gst, exclusive } = gstSplit(110);
    expect(Number(gst)).toBeCloseTo(10, 2);
    expect(Number(exclusive)).toBeCloseTo(100, 2);
  });
  it("applies exclusive GST", () => {
    const { inclusive } = applyGst(100);
    expect(Number(inclusive)).toBe(110);
  });
  it("formats AUD", () => {
    expect(formatAud(329)).toContain("329");
  });
  it("computes margin", () => {
    const { profit, margin: m } = margin(200, 80);
    expect(Number(profit)).toBe(120);
    expect(Number(m)).toBeCloseTo(0.6, 2);
  });
});

describe("PC compatibility", () => {
  it("warns on socket mismatch", () => {
    const warnings = assessCompatibility([
      { category: "CPU", name: "CPU", specs: { socket: "AM5" } },
      { category: "MOTHERBOARD", name: "MB", specs: { socket: "LGA1700" } },
    ]);
    expect(warnings.some((w) => w.level === "warning")).toBe(true);
  });
  it("does not claim compatibility when data is missing", () => {
    const warnings = assessCompatibility([
      { category: "CPU", name: "CPU", specs: {} },
      { category: "MOTHERBOARD", name: "MB", specs: {} },
    ]);
    expect(warnings.some((w) => w.level === "unknown")).toBe(true);
  });
});

describe("Used valuation", () => {
  it("matches the specification example", () => {
    const rec = recommendOffer({
      expectedResale: 550,
      expectedRepairCost: 90,
      feesAllowance: 40,
      requiredProfit: 140,
    });
    expect(rec.recommendedMaxOffer).toBe(280);
  });
});

describe("RBAC catalogue", () => {
  it("does not grant technicians financial admin permissions", () => {
    expect(ROLE_PERMISSIONS.technician).not.toContain("pricing.view");
    expect(ROLE_PERMISSIONS.technician).not.toContain("reports.financial");
    expect(ROLE_PERMISSIONS.technician).not.toContain("payments.refund");
    expect(ROLE_PERMISSIONS.technician).not.toContain("staff.manage");
    expect(ROLE_PERMISSIONS.owner).toContain("staff.manage");
    expect(ROLE_PERMISSIONS.owner.length).toBe(ALL_PERMISSION_KEYS.length);
  });
});

describe("Passwords", () => {
  it("rejects short passwords", () => {
    expect(validatePasswordStrength("short")).toBeTruthy();
    expect(validatePasswordStrength("Riverside!2026")).toBeNull();
  });
});

describe("AI sanitisation", () => {
  it("strips customer and payment fields", () => {
    const { allowed, excluded } = sanitiseForAi({
      customerName: "Luca",
      phone: "0418",
      device: "iPhone 15",
      password: "1234",
    });
    expect(allowed.device).toBe("iPhone 15");
    expect(excluded).toContain("customerName");
    expect(excluded).toContain("password");
  });
});
