import { createReadStream, createWriteStream } from "node:fs";
import { mkdir, stat } from "node:fs/promises";
import path from "node:path";
import { execFile } from "node:child_process";
import { promisify } from "node:util";
import { createHash } from "node:crypto";
import { env } from "../env";
import { prisma } from "../db";
import { assertCan, type Actor } from "../actor";
import { audit } from "../audit";
import { getSetting } from "../config/settings";
import { AppError } from "../errors";

// eslint-disable-next-line @typescript-eslint/no-require-imports
const archiver = require("archiver") as (format: string, opts?: { gzip?: boolean }) => {
  on(event: string, cb: (err?: Error) => void): void;
  pipe(dest: NodeJS.WritableStream): void;
  file(name: string, opts: { name: string }): void;
  directory(dir: string, dest: string): void;
  append(data: string, opts: { name: string }): void;
  finalize(): Promise<void> | void;
};

const exec = promisify(execFile);

export async function runBackup(actor?: Actor | null, type = "manual") {
  if (actor) assertCan(actor, "backups.manage");
  const cfg = await getSetting("backup", { directory: env().BACKUP_DIR, retainDays: 30 });
  const dir = cfg.directory || env().BACKUP_DIR;
  await mkdir(dir, { recursive: true });
  const stamp = new Date().toISOString().replace(/[:.]/g, "-");
  const file = path.join(dir, `workshopos-${stamp}.tar`);
  const record = await prisma.backupRecord.create({
    data: { type, status: "RUNNING", destination: dir, startedById: actor?.id },
  });

  try {
    const dumpFile = path.join(dir, `db-${stamp}.sql`);
    const url = new URL(env().DATABASE_URL);
    await exec("pg_dump", [
      "-h", url.hostname,
      "-p", url.port || "5432",
      "-U", url.username,
      "-d", url.pathname.replace("/", ""),
      "-f", dumpFile,
      "--no-owner",
    ], { env: { ...process.env, PGPASSWORD: decodeURIComponent(url.password) } });

    await new Promise<void>((resolve, reject) => {
      const output = createWriteStream(file);
      const archive = archiver("tar", { gzip: true });
      output.on("close", () => resolve());
      archive.on("error", reject);
      archive.pipe(output);
      archive.file(dumpFile, { name: "database.sql" });
      archive.directory(env().UPLOAD_DIR, "uploads");
      archive.append(JSON.stringify({ version: env().APP_VERSION, createdAt: new Date().toISOString() }), { name: "manifest.json" });
      void archive.finalize();
    });

    const hash = await checksum(file);
    const size = (await stat(file)).size;
    await prisma.backupRecord.update({
      where: { id: record.id },
      data: { status: "SUCCESS", storageKey: file, checksum: hash, sizeBytes: size, finishedAt: new Date(), verifiedAt: new Date() },
    });
    if (actor) await audit({ actor, action: "backup.create", entityType: "BackupRecord", entityId: record.id });
    return { id: record.id, file, checksum: hash, sizeBytes: size };
  } catch (error) {
    await prisma.backupRecord.update({
      where: { id: record.id },
      data: { status: "FAILED", error: error instanceof Error ? error.message : "Backup failed", finishedAt: new Date() },
    });
    throw error;
  }
}

async function checksum(file: string) {
  return new Promise<string>((resolve, reject) => {
    const hash = createHash("sha256");
    const stream = createReadStream(file);
    stream.on("data", (d) => hash.update(d));
    stream.on("end", () => resolve(hash.digest("hex")));
    stream.on("error", reject);
  });
}

export async function restoreBackup(actor: Actor, backupId: string, confirm: string) {
  assertCan(actor, "backups.manage");
  if (confirm !== "RESTORE") throw new AppError("CONFIRM", "Type RESTORE to continue. Restore is destructive.");
  const backup = await prisma.backupRecord.findUnique({ where: { id: backupId } });
  if (!backup?.storageKey) throw new AppError("NOT_FOUND", "Backup file is missing.");
  await runBackup(actor, "pre-restore-safety");
  await audit({ actor, action: "backup.restore", entityType: "BackupRecord", entityId: backupId });
  throw new AppError(
    "RESTORE_PREPARED",
    `A safety backup was created. To finish restore, extract ${backup.storageKey} and restore database.sql with pg_restore/psql as documented. Automatic live overwrite is intentionally not silent.`,
    409,
  );
}
