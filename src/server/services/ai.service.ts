import { getAiProvider, sanitiseForAi, type AiMessage } from "../integrations/ai";
import { assertCan, type Actor } from "../actor";
import { prisma } from "../db";
import { ValidationError } from "../errors";

export async function aiAssist(
  actor: Actor,
  input: { intent: "diagnose" | "price" | "summary" | "sms" | "kb"; ticketId?: string; prompt?: string },
) {
  assertCan(actor, "ai.use");
  const provider = await getAiProvider();
  if (!provider) throw new ValidationError("Local AI is not enabled. Configure Ollama in Settings.");

  let technical: Record<string, unknown> = { intent: input.intent, prompt: input.prompt };
  if (input.ticketId) {
    const ticket = await prisma.ticket.findUnique({
      where: { id: input.ticketId },
      include: {
        type: true,
        parts: true,
        diagnosticRuns: { include: { results: { include: { item: true } } } },
        timeEntries: true,
        deviceModel: true,
      },
    });
    if (ticket) {
      technical = {
        intent: input.intent,
        jobType: ticket.type.name,
        issue: ticket.reportedIssue,
        device: ticket.deviceModel?.name,
        parts: ticket.parts.map((p) => ({ name: p.name, status: p.status })),
        diagnostics: ticket.diagnosticRuns.flatMap((r) =>
          r.results.map((res) => ({ item: res.item.label, value: res.value })),
        ),
        labourMinutes: ticket.estimatedLabourMinutes,
      };
    }
  }

  const { allowed, excluded } = sanitiseForAi(technical);
  const system: AiMessage = {
    role: "system",
    content:
      "You are a workshop assistant for a technology repair shop. You never invent customer personal data. You never change prices or send messages. Give practical technical suggestions. If data is missing, say so.",
  };
  const user: AiMessage = {
    role: "user",
    content: JSON.stringify({ data: allowed, staffPrompt: input.prompt ?? null }),
  };
  const output = await provider.complete([system, user]);
  return { output, sent: allowed, excluded, provider: provider.name };
}
