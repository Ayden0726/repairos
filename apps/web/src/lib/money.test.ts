import { describe, expect, it } from "vitest";
import { centsToAud, dollarsToCents } from "./money";

describe("dollarsToCents", () => {
  it("parses whole dollars", () => {
    expect(dollarsToCents("50")).toBe(5000n);
    expect(dollarsToCents("0")).toBe(0n);
  });

  it("parses two decimal places", () => {
    expect(dollarsToCents("50.00")).toBe(5000n);
    expect(dollarsToCents("12.34")).toBe(1234n);
    expect(dollarsToCents("0.05")).toBe(5n);
  });

  it("rejects more than two decimals", () => {
    expect(() => dollarsToCents("1.234")).toThrow();
  });

  it("rejects empty and non-numeric", () => {
    expect(() => dollarsToCents("")).toThrow();
    expect(() => dollarsToCents("abc")).toThrow();
  });

  it("supports lower-priced tasks", () => {
    expect(dollarsToCents("15.00")).toBe(1500n);
    expect(dollarsToCents("9.50")).toBe(950n);
  });
});

describe("centsToAud", () => {
  it("formats AUD without floating point", () => {
    expect(centsToAud(5000n)).toBe("$50.00");
    expect(centsToAud(5n)).toBe("$0.05");
    expect(centsToAud(null)).toBe("—");
  });
});
