import { Queue, type JobsOptions } from "bullmq";
import { env } from "./env";

let queues: Record<string, Queue> | null = null;

function connection() {
  const url = new URL(env().REDIS_URL);
  return {
    host: url.hostname,
    port: Number(url.port || 6379),
    password: url.password || undefined,
  };
}

export function jobQueue(name: "mail" | "sms" | "pdf" | "backup" | "maintenance") {
  if (!queues) queues = {};
  if (!queues[name]) {
    queues[name] = new Queue(name, { connection: connection() });
  }
  return queues[name];
}

export async function enqueue(
  name: "mail" | "sms" | "pdf" | "backup" | "maintenance",
  jobName: string,
  data: Record<string, unknown>,
  opts?: JobsOptions,
) {
  return jobQueue(name).add(jobName, data, {
    attempts: 3,
    backoff: { type: "exponential", delay: 2000 },
    removeOnComplete: 200,
    removeOnFail: 200,
    ...opts,
  });
}
