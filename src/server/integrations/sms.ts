import { prisma } from "../db";
import { getSetting } from "../config/settings";

export type SmsMessage = { to: string; body: string };

export interface SmsProvider {
  send(message: SmsMessage): Promise<{ id: string; ok: boolean; error?: string }>;
}

class ConsoleSms implements SmsProvider {
  async send(message: SmsMessage) {
    console.info("[sms:console]", message.to, message.body);
    return { id: `console-${Date.now()}`, ok: true };
  }
}

class HttpSms implements SmsProvider {
  constructor(
    private endpoint: string,
    private token: string,
    private from: string,
  ) {}
  async send(message: SmsMessage) {
    const res = await fetch(this.endpoint, {
      method: "POST",
      headers: { "content-type": "application/json", authorization: `Bearer ${this.token}` },
      body: JSON.stringify({ to: message.to, from: this.from, body: message.body }),
    });
    if (!res.ok) return { id: "", ok: false, error: await res.text() };
    const json = (await res.json().catch(() => ({}))) as { id?: string };
    return { id: json.id ?? `sms-${Date.now()}`, ok: true };
  }
}

export async function getSmsProvider(): Promise<SmsProvider> {
  const cfg = await getSetting("integrations.sms", {
    enabled: false,
    provider: "console",
    endpoint: "",
    token: "",
    from: "",
  });
  if (!cfg.enabled || cfg.provider === "console") return new ConsoleSms();
  return new HttpSms(cfg.endpoint, cfg.token, cfg.from);
}

export function renderTemplate(template: string, vars: Record<string, string>) {
  return template.replace(/\{\{(\w+)\}\}/g, (_, key: string) => vars[key] ?? "");
}

export async function sendTicketSms(ticketId: string, templateKey: string) {
  const ticket = await prisma.ticket.findUnique({
    where: { id: ticketId },
    include: { customer: true, status: true, type: true },
  });
  if (!ticket?.customer.phone) return;
  const tpl = await prisma.smsTemplate.findUnique({ where: { key: templateKey } });
  if (!tpl?.enabled) return;
  const body = renderTemplate(tpl.body, {
    customer: ticket.customer.displayName,
    ticket: ticket.ticketNumber,
    status: ticket.status.name,
    business: (await getSetting("business.profile", { name: "the shop" })).name,
  });
  const provider = await getSmsProvider();
  const result = await provider.send({ to: ticket.customer.phone, body });
  await prisma.communication.create({
    data: {
      ticketId,
      customerId: ticket.customerId,
      channel: "SMS",
      visibility: "CUSTOMER",
      direction: "outbound",
      toAddress: ticket.customer.phone,
      body,
      providerRef: result.id,
      status: result.ok ? "sent" : "failed",
    },
  });
  await prisma.ticketEvent.create({
    data: {
      ticketId,
      type: "sms",
      summary: result.ok ? `SMS sent (${tpl.name})` : `SMS failed (${tpl.name})`,
      visibility: "CUSTOMER",
    },
  });
}
