import { env } from "../env";
import * as local from "./local";

export async function saveUpload(buffer: Buffer, fileName: string, mimeType: string) {
  if (env().FILE_STORAGE_DRIVER === "s3") {
    const { saveS3 } = await import("./s3");
    return saveS3(buffer, fileName, mimeType);
  }
  return local.saveLocalBuffer(buffer, fileName, mimeType);
}

export async function readUpload(key: string) {
  if (env().FILE_STORAGE_DRIVER === "s3") {
    const { readS3 } = await import("./s3");
    return readS3(key);
  }
  return local.readLocal(key);
}

export async function deleteUpload(key: string) {
  if (env().FILE_STORAGE_DRIVER === "s3") {
    const { deleteS3 } = await import("./s3");
    return deleteS3(key);
  }
  return local.deleteLocal(key);
}

export { assertAllowedUpload } from "./local";
