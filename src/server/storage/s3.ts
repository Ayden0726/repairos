import { DeleteObjectCommand, GetObjectCommand, PutObjectCommand, S3Client } from "@aws-sdk/client-s3";
import { env } from "../env";
import { randomToken } from "../crypto";
import { assertAllowedUpload, type StoredFile } from "./local";

function client() {
  const e = env();
  return new S3Client({
    region: e.S3_REGION ?? "us-east-1",
    endpoint: e.S3_ENDPOINT,
    forcePathStyle: true,
    credentials: e.S3_ACCESS_KEY
      ? { accessKeyId: e.S3_ACCESS_KEY, secretAccessKey: e.S3_SECRET_KEY ?? "" }
      : undefined,
  });
}

export async function saveS3(buffer: Buffer, fileName: string, mimeType: string): Promise<StoredFile> {
  assertAllowedUpload(mimeType, buffer.length);
  const e = env();
  const key = `${new Date().toISOString().slice(0, 10)}/${randomToken(12)}-${fileName.replace(/[^a-zA-Z0-9._-]/g, "_")}`;
  await client().send(
    new PutObjectCommand({
      Bucket: e.S3_BUCKET,
      Key: key,
      Body: buffer,
      ContentType: mimeType,
    }),
  );
  return { key, fileName, mimeType, sizeBytes: buffer.length };
}

export async function readS3(key: string): Promise<Buffer> {
  const e = env();
  const res = await client().send(new GetObjectCommand({ Bucket: e.S3_BUCKET, Key: key }));
  const bytes = await res.Body?.transformToByteArray();
  return Buffer.from(bytes ?? []);
}

export async function deleteS3(key: string) {
  const e = env();
  await client().send(new DeleteObjectCommand({ Bucket: e.S3_BUCKET, Key: key }));
}
