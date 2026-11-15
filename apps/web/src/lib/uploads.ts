import { mkdir, writeFile } from "fs/promises";
import path from "path";
import { randomUUID } from "crypto";

const ALLOWED = new Map([
  ["image/jpeg", ".jpg"],
  ["image/png", ".png"],
  ["image/webp", ".webp"],
]);

export const MAX_PHOTO_BYTES = 5 * 1024 * 1024;
export const MAX_PHOTOS_PER_TICKET = 5;

export function uploadRoot() {
  return process.env.UPLOAD_ROOT || path.join(process.cwd(), ".uploads");
}

export function sanitizeOriginalName(name: string): string {
  return name.replace(/[^\w.\-()+ ]+/g, "_").slice(0, 120) || "photo";
}

export async function saveTicketPhoto(file: File): Promise<{
  storedName: string;
  originalName: string;
  contentType: string;
  sizeBytes: number;
}> {
  const contentType = file.type;
  const ext = ALLOWED.get(contentType);
  if (!ext) throw new Error("Only JPEG, PNG or WebP images are allowed");
  if (file.size <= 0 || file.size > MAX_PHOTO_BYTES) {
    throw new Error("Each photo must be between 1 byte and 5 MB");
  }

  // Basic magic-byte check
  const buf = Buffer.from(await file.arrayBuffer());
  if (!looksLikeImage(buf, contentType)) {
    throw new Error("File content does not match an allowed image type");
  }

  const storedName = `${randomUUID()}${ext}`;
  const dir = path.join(uploadRoot(), "tickets");
  await mkdir(dir, { recursive: true });
  await writeFile(path.join(dir, storedName), buf);

  return {
    storedName,
    originalName: sanitizeOriginalName(file.name),
    contentType,
    sizeBytes: buf.length,
  };
}

function looksLikeImage(buf: Buffer, contentType: string): boolean {
  if (buf.length < 12) return false;
  if (contentType === "image/jpeg") return buf[0] === 0xff && buf[1] === 0xd8;
  if (contentType === "image/png")
    return buf[0] === 0x89 && buf[1] === 0x50 && buf[2] === 0x4e && buf[3] === 0x47;
  if (contentType === "image/webp")
    return buf.toString("ascii", 0, 4) === "RIFF" && buf.toString("ascii", 8, 12) === "WEBP";
  return false;
}
