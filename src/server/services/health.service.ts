import { statfs } from "node:fs/promises";
import { env } from "../env";
import { prisma } from "../db";
import { redis } from "../redis";
import { getSetting } from "../config/settings";
import type { HealthState } from "@prisma/client";

export type Check = { key: string; state: HealthState; detail: string };

async function pingDb(): Promise<Check> {
  try {
    await prisma.$queryRaw`SELECT 1`;
    return { key: "database", state: "HEALTHY", detail: "PostgreSQL accepting connections" };
  } catch (e) {
    return { key: "database", state: "CRITICAL", detail: e instanceof Error ? e.message : "Database unreachable" };
  }
}

async function pingRedis(): Promise<Check> {
  try {
    const pong = await redis().ping();
    return { key: "redis", state: pong === "PONG" ? "HEALTHY" : "WARNING", detail: String(pong) };
  } catch (e) {
    return { key: "redis", state: "CRITICAL", detail: e instanceof Error ? e.message : "Redis unreachable" };
  }
}

async function disk(): Promise<Check> {
  try {
    const info = await statfs(env().UPLOAD_DIR);
    const free = info.bavail * info.bsize;
    const total = info.blocks * info.bsize;
    const pct = free / total;
    if (pct < 0.08) return { key: "disk", state: "CRITICAL", detail: `${Math.round(pct * 100)}% free` };
    if (pct < 0.15) return { key: "disk", state: "WARNING", detail: `${Math.round(pct * 100)}% free` };
    return { key: "disk", state: "HEALTHY", detail: `${Math.round(pct * 100)}% free` };
  } catch (e) {
    return { key: "disk", state: "WARNING", detail: e instanceof Error ? e.message : "Disk check failed" };
  }
}

async function lastBackup(): Promise<Check> {
  const row = await prisma.backupRecord.findFirst({ orderBy: { startedAt: "desc" } });
  if (!row) return { key: "backup", state: "WARNING", detail: "No backups recorded yet" };
  if (row.status === "FAILED") return { key: "backup", state: "CRITICAL", detail: row.error ?? "Last backup failed" };
  const ageH = (Date.now() - row.startedAt.getTime()) / 36e5;
  if (ageH > 48) return { key: "backup", state: "WARNING", detail: `Last success ${Math.round(ageH)}h ago` };
  return { key: "backup", state: "HEALTHY", detail: `Last backup ${row.status.toLowerCase()}` };
}

async function integration(key: string, settingKey: string, ping?: () => Promise<boolean>): Promise<Check> {
  const cfg = await getSetting(settingKey, { enabled: false });
  if (!cfg.enabled) return { key, state: "UNKNOWN", detail: "Not configured" };
  if (!ping) return { key, state: "HEALTHY", detail: "Enabled" };
  try {
    const ok = await ping();
    return { key, state: ok ? "HEALTHY" : "WARNING", detail: ok ? "Reachable" : "Unreachable" };
  } catch (e) {
    return { key, state: "WARNING", detail: e instanceof Error ? e.message : "Check failed" };
  }
}

export async function runHealthChecks() {
  const checks = await Promise.all([
    pingDb(),
    pingRedis(),
    disk(),
    lastBackup(),
    integration("sms", "integrations.sms"),
    integration("email", "integrations.email"),
    integration("square", "integrations.square"),
    integration("ollama", "integrations.ollama", async () => {
      const cfg = await getSetting("integrations.ollama", { enabled: false, baseUrl: "http://127.0.0.1:11434" });
      const res = await fetch(`${cfg.baseUrl}/api/tags`);
      return res.ok;
    }),
  ]);
  await prisma.healthCheck.createMany({
    data: checks.map((c) => ({ key: c.key, state: c.state, detail: c.detail })),
  });
  return {
    version: env().APP_VERSION,
    checks,
    overall: checks.some((c) => c.state === "CRITICAL")
      ? "CRITICAL"
      : checks.some((c) => c.state === "WARNING")
        ? "WARNING"
        : "HEALTHY",
  };
}
