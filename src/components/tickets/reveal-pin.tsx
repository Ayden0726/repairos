"use client";

import { useState } from "react";
import { Button } from "@/components/ui/button";

export function RevealPin({ ticketId }: { ticketId: string }) {
  const [value, setValue] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  async function reveal() {
    setError(null);
    const res = await fetch(`/api/tickets/${ticketId}/credential`, { method: "POST" });
    const json = await res.json();
    if (!res.ok) {
      setError(json.error ?? "Unable to reveal");
      return;
    }
    setValue(json.secret);
  }
  return (
    <div className="space-y-2">
      {value ? <p className="rounded bg-muted px-2 py-1 font-mono text-sm">{value}</p> : <p className="text-sm text-muted-foreground">Hidden. Reveal is audited.</p>}
      {error ? <p className="text-sm text-destructive">{error}</p> : null}
      <Button type="button" size="sm" variant="outline" onClick={() => void reveal()}>
        Reveal
      </Button>
    </div>
  );
}
