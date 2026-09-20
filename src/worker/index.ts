import { Worker } from "bullmq";
import { env } from "../server/env";
import { sendTicketSms } from "../server/integrations/sms";
import { getEmailProvider } from "../server/integrations/email";
import { runBackup } from "../server/services/backup.service";
import { runHealthChecks } from "../server/services/health.service";
import { refreshSlaStates } from "../server/services/ticket.service";
import { prisma } from "../server/db";

function connection() {
  const url = new URL(env().REDIS_URL);
  return { host: url.hostname, port: Number(url.port || 6379), password: url.password || undefined };
}

function start() {
  new Worker(
    "sms",
    async (job) => {
      if (job.name === "ticket") {
        await sendTicketSms(String(job.data.ticketId), String(job.data.templateKey));
      }
    },
    { connection: connection() },
  );

  new Worker(
    "mail",
    async (job) => {
      const provider = await getEmailProvider();
      await provider.send({
        to: String(job.data.to),
        subject: String(job.data.subject),
        text: String(job.data.text),
      });
    },
    { connection: connection() },
  );

  new Worker(
    "backup",
    async () => {
      await runBackup(null, "scheduled");
    },
    { connection: connection() },
  );

  new Worker(
    "maintenance",
    async (job) => {
      if (job.name === "health") await runHealthChecks();
      if (job.name === "sla") await refreshSlaStates();
      if (job.name === "retention") await applyRetention();
    },
    { connection: connection() },
  );

  console.info("WorkshopOS worker listening");
}

async function applyRetention() {
  const policies = await prisma.retentionPolicy.findMany({ where: { enabled: true, warningAck: true } });
  for (const policy of policies) {
    if (!policy.days) continue;
    const cutoff = new Date(Date.now() - policy.days * 86400000);
    if (policy.recordType === "sms_logs") {
      await prisma.communication.deleteMany({ where: { channel: "SMS", createdAt: { lt: cutoff } } });
    }
  }
}

start();
