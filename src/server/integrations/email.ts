import nodemailer, { type Transporter } from "nodemailer";
import { getSetting } from "../config/settings";

export type EmailMessage = {
  to: string;
  subject: string;
  text: string;
  html?: string;
  attachments?: Array<{ filename: string; content: Buffer }>;
};

export interface EmailProvider {
  send(message: EmailMessage): Promise<{ id: string; ok: boolean; error?: string }>;
}

class ConsoleEmail implements EmailProvider {
  async send(message: EmailMessage) {
    console.info("[email:console]", message.to, message.subject);
    return { id: `console-${Date.now()}`, ok: true };
  }
}

class SmtpEmail implements EmailProvider {
  constructor(private transport: Transporter, private from: string) {}
  async send(message: EmailMessage) {
    const info = await this.transport.sendMail({
      from: this.from,
      to: message.to,
      subject: message.subject,
      text: message.text,
      html: message.html,
      attachments: message.attachments,
    });
    return { id: String(info.messageId), ok: true };
  }
}

export async function getEmailProvider(): Promise<EmailProvider> {
  const cfg = await getSetting("integrations.email", {
    enabled: false,
    host: "",
    port: 587,
    user: "",
    pass: "",
    from: "",
    secure: false,
  });
  if (!cfg.enabled || !cfg.host) return new ConsoleEmail();
  const transport = nodemailer.createTransport({
    host: cfg.host,
    port: cfg.port,
    secure: cfg.secure,
    auth: cfg.user ? { user: cfg.user, pass: cfg.pass } : undefined,
  });
  return new SmtpEmail(transport, cfg.from || cfg.user);
}
