import { formatInTimeZone } from "date-fns-tz";

export const BUSINESS_TZ = "Australia/Brisbane";

export function formatBrisbane(
  date: Date | string,
  pattern = "dd MMM yyyy HH:mm",
): string {
  const d = typeof date === "string" ? new Date(date) : date;
  return formatInTimeZone(d, BUSINESS_TZ, pattern);
}

export function brisbaneYear(date = new Date()): number {
  return Number(formatInTimeZone(date, BUSINESS_TZ, "yyyy"));
}
