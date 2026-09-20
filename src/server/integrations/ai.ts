import { getSetting } from "../config/settings";

export type AiMessage = { role: "system" | "user" | "assistant"; content: string };

export interface AiProvider {
  name: string;
  complete(messages: AiMessage[], options?: { model?: string }): Promise<string>;
}

class OllamaProvider implements AiProvider {
  name = "ollama";
  constructor(
    private baseUrl: string,
    private model: string,
  ) {}
  async complete(messages: AiMessage[], options?: { model?: string }) {
    const res = await fetch(`${this.baseUrl.replace(/\/$/, "")}/api/chat`, {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify({
        model: options?.model ?? this.model,
        stream: false,
        messages,
      }),
    });
    if (!res.ok) throw new Error(`Ollama error ${res.status}`);
    const json = (await res.json()) as { message?: { content?: string } };
    return json.message?.content ?? "";
  }
}

export async function getAiProvider(): Promise<AiProvider | null> {
  const cfg = await getSetting("integrations.ollama", {
    enabled: false,
    baseUrl: "http://127.0.0.1:11434",
    model: "llama3.1",
  });
  if (!cfg.enabled) return null;
  return new OllamaProvider(cfg.baseUrl, cfg.model);
}

const BLOCKED = [
  "customer",
  "name",
  "phone",
  "email",
  "address",
  "password",
  "pin",
  "payment",
  "card",
  "invoice",
  "abn",
];

export function sanitiseForAi(input: Record<string, unknown>) {
  const allowed: Record<string, unknown> = {};
  const excluded: string[] = [];
  for (const [key, value] of Object.entries(input)) {
    const lower = key.toLowerCase();
    if (BLOCKED.some((b) => lower.includes(b))) {
      excluded.push(key);
      continue;
    }
    allowed[key] = value;
  }
  return { allowed, excluded };
}
