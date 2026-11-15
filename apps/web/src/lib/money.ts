/** Money helpers — integer cents only. Never use floating point for storage. */

export function dollarsToCents(input: string | number): bigint {
  if (typeof input === "number") {
    if (!Number.isFinite(input)) throw new Error("Invalid money amount");
    // Reject non-integer cent results from float noise by requiring exact 2dp via string path
    return dollarsToCents(input.toFixed(2));
  }
  const cleaned = input.trim().replace(/,/g, "").replace(/\$/g, "");
  if (!cleaned || cleaned === "-" || cleaned === ".") {
    throw new Error("Invalid money amount");
  }
  if (!/^-?\d+(\.\d{1,2})?$/.test(cleaned)) {
    throw new Error("Use at most two decimal places");
  }
  const negative = cleaned.startsWith("-");
  const raw = negative ? cleaned.slice(1) : cleaned;
  const [wholePart, fracPart = ""] = raw.split(".");
  const whole = BigInt(wholePart || "0");
  const frac = BigInt((fracPart + "00").slice(0, 2));
  const cents = whole * 100n + frac;
  return negative ? -cents : cents;
}

export function centsToAud(cents: bigint | number | null | undefined): string {
  if (cents === null || cents === undefined) return "—";
  const n = typeof cents === "bigint" ? cents : BigInt(cents);
  const neg = n < 0n;
  const abs = neg ? -n : n;
  const whole = abs / 100n;
  const frac = (abs % 100n).toString().padStart(2, "0");
  return `${neg ? "-" : ""}$${whole.toLocaleString("en-AU")}.${frac}`;
}

export const DEFAULT_LABOUR_FEE_CENTS = 5000n;
