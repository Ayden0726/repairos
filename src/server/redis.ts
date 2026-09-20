import Redis from "ioredis";
import { env } from "./env";

const globalForRedis = globalThis as unknown as { redis?: Redis };

export function redis(): Redis {
  if (!globalForRedis.redis) {
    globalForRedis.redis = new Redis(env().REDIS_URL, {
      maxRetriesPerRequest: null,
      enableReadyCheck: true,
    });
  }
  return globalForRedis.redis;
}

export async function rateLimit(key: string, limit: number, windowSec: number): Promise<boolean> {
  const redisKey = `rl:${key}`;
  const count = await redis().incr(redisKey);
  if (count === 1) await redis().expire(redisKey, windowSec);
  return count <= limit;
}
