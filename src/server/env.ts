import { z } from "zod";

const schema = z.object({
  NODE_ENV: z.string().default("development"),
  APP_NAME: z.string().default("WorkshopOS"),
  APP_VERSION: z.string().default("1.0.0"),
  APP_URL: z.string().default("http://127.0.0.1:47821"),
  PORT: z.coerce.number().default(47821),
  DATABASE_URL: z.string().min(1),
  REDIS_URL: z.string().default("redis://127.0.0.1:6379"),
  SESSION_SECRET: z.string().min(16),
  ENCRYPTION_KEY: z.string().min(64),
  UPLOAD_DIR: z.string().default("./data/uploads"),
  BACKUP_DIR: z.string().default("./data/backups"),
  FILE_STORAGE_DRIVER: z.enum(["local", "s3"]).default("local"),
  S3_ENDPOINT: z.string().optional(),
  S3_REGION: z.string().optional(),
  S3_BUCKET: z.string().optional(),
  S3_ACCESS_KEY: z.string().optional(),
  S3_SECRET_KEY: z.string().optional(),
  SEED_DEMO: z.string().optional(),
});

export type Env = z.infer<typeof schema>;

let cached: Env | null = null;

export function env(): Env {
  if (cached) return cached;
  const parsed = schema.safeParse(process.env);
  if (!parsed.success) {
    throw new Error(`Invalid environment: ${parsed.error.message}`);
  }
  cached = parsed.data;
  return cached;
}

export const isProd = () => env().NODE_ENV === "production";
