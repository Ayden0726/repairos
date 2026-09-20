"use client";

import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Textarea } from "@/components/ui/textarea";

export function AiPanel({ ticketId }: { ticketId: string }) {
  const [out, setOut] = useState<string>("");
  const [excluded, setExcluded] = useState<string[]>([]);
  const [prompt, setPrompt] = useState("Suggest diagnostic steps for this fault. Do not invent customer details.");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function run(intent: string) {
    setBusy(true);
    setError(null);
    const res = await fetch("/api/ai", {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify({ intent, ticketId, prompt }),
    });
    const json = await res.json();
    setBusy(false);
    if (!res.ok) {
      setError(json.error ?? "AI is not available");
      return;
    }
    setOut(json.output);
    setExcluded(json.excluded ?? []);
  }

  return (
    <section className="rounded-xl border border-border p-4">
      <h2 className="mb-2 text-sm font-semibold">Local AI assistant</h2>
      <p className="mb-2 text-xs text-muted-foreground">
        Suggestions only. Customer-facing messages must be approved by staff. Personal data is stripped before the model sees the prompt.
      </p>
      <Textarea value={prompt} onChange={(e) => setPrompt(e.target.value)} rows={3} />
      <div className="mt-2 flex flex-wrap gap-2">
        <Button type="button" size="sm" variant="outline" disabled={busy} onClick={() => void run("diagnose")}>
          Diagnose
        </Button>
        <Button type="button" size="sm" variant="outline" disabled={busy} onClick={() => void run("sms")}>
          Draft SMS
        </Button>
        <Button type="button" size="sm" variant="outline" disabled={busy} onClick={() => void run("price")}>
          Price hint
        </Button>
      </div>
      {excluded.length ? <p className="mt-2 text-[11px] text-muted-foreground">Excluded: {excluded.join(", ")}</p> : null}
      {error ? <p className="mt-2 text-sm text-destructive">{error}</p> : null}
      {out ? <pre className="mt-2 whitespace-pre-wrap rounded-md bg-muted/50 p-2 text-xs">{out}</pre> : null}
    </section>
  );
}
