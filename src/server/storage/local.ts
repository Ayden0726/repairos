import { mkdir, writeFile, readFile, unlink, copyFile } from "node:fs/promises";
import path from "node:path";
import { env } from "../env";
import { randomToken } from "../crypto";

export type StoredFile = {
  key: string;
  fileName: string;
  mimeType: string;
  sizeBytes: number;
};

const ALLOWED = new Set([
  "image/jpeg",
  "image/png",
  "image/webp",
  "image/gif",
  "application/pdf",
  "text/plain",
  "text/csv",
  "application/vnd.ms-excel",
  "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
  "application/zip",
]);

const MAX_BYTES = 25 * 1024 * 1024;

export function assertAllowedUpload(mimeType: string, sizeBytes: number) {
  if (!ALLOWED.has(mimeType)) {
    throw new Error("This file type is not allowed.");
  }
  if (sizeBytes > MAX_BYTES) {
    throw new Error("File is larger than the 25 MB limit.");
  }
}

export function localRoot() {
  return path.resolve(env().UPLOAD_DIR);
}

export async function saveLocalBuffer(buffer: Buffer, fileName: string, mimeType: string): Promise<StoredFile> {
  assertAllowedUpload(mimeType, buffer.length);
  const key = `${new Date().toISOString().slice(0, 10)}/${randomToken(12)}-${fileName.replace(/[^a-zA-Z0-9._-]/g, "_")}`;
  const full = path.join(localRoot(), key);
  await mkdir(path.dirname(full), { recursive: true });
  await writeFile(full, buffer);
  return { key, fileName, mimeType, sizeBytes: buffer.length };
}

export async function readLocal(key: string): Promise<Buffer> {
  return readFile(path.join(localRoot(), key));
}

export async function deleteLocal(key: string): Promise<void> {
  try {
    await unlink(path.join(localRoot(), key));
  } catch {
    // already gone
  }
}

export async function copyLocal(fromKey: string, toKey: string) {
  const from = path.join(localRoot(), fromKey);
  const to = path.join(localRoot(), toKey);
  await mkdir(path.dirname(to), { recursive: true });
  await copyFile(from, to);
}

export function publicUploadPath(key: string) {
  return `/api/files/${encodeURIComponent(key)}`;
}
