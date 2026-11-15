"use client";

import Link from "next/link";
import { useParams } from "next/navigation";
import { useCallback, useEffect, useState } from "react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  Card,
  CardContent,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { centsToAud } from "@/lib/money";
import { TICKET_STATUSES, statusLabel, statusTone } from "@/lib/status";
import { formatBrisbane } from "@/lib/time";

type TicketDetail = {
  id: string;
  ticketNumber: string;
  status: Parameters<typeof statusLabel>[0];
  reportedFault: string;
  conditionPowersOn: boolean | null;
  conditionCracks: boolean | null;
  conditionScratches: boolean | null;
  conditionLiquid: boolean | null;
  conditionNotes: string | null;
  accessoryCharger: boolean;
  accessorySim: boolean;
  accessoryCase: boolean;
  accessoryOther: string | null;
  labourFeeCents: string;
  estimateCents: string | null;
  createdAt: string;
  customer: { id: string; firstName: string; lastName: string; phone: string };
  device: {
    brand: string;
    model: string;
    imei: string | null;
    serial: string | null;
    category: string;
  } | null;
  notes: { id: string; body: string; createdAt: string; author: { displayName: string } }[];
  statusHistory: {
    id: string;
    fromStatus: Parameters<typeof statusLabel>[0] | null;
    toStatus: Parameters<typeof statusLabel>[0];
    comment: string | null;
    createdAt: string;
    actor: { displayName: string } | null;
  }[];
  photos: { id: string; originalName: string }[];
};

export default function TicketDetailPage() {
  const params = useParams<{ id: string }>();
  const [ticket, setTicket] = useState<TicketDetail | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [note, setNote] = useState("");
  const [status, setStatus] = useState("");
  const [busy, setBusy] = useState(false);

  const load = useCallback(async () => {
    const res = await fetch(`/api/tickets/${params.id}`);
    const data = await res.json().catch(() => ({}));
    if (!res.ok) {
      setError(data.error || "Not found");
      setTicket(null);
      return;
    }
    setTicket(data.ticket);
    setStatus(data.ticket.status);
    setError(null);
  }, [params.id]);

  useEffect(() => {
    void load();
  }, [load]);

  async function addNote() {
    if (!note.trim()) return;
    setBusy(true);
    const res = await fetch(`/api/tickets/${params.id}/notes`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ body: note }),
    });
    setBusy(false);
    if (res.ok) {
      setNote("");
      await load();
    } else {
      const data = await res.json().catch(() => ({}));
      setError(data.error || "Could not add note");
    }
  }

  async function changeStatus() {
    setBusy(true);
    const res = await fetch(`/api/tickets/${params.id}/status`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ status }),
    });
    setBusy(false);
    if (res.ok) await load();
    else {
      const data = await res.json().catch(() => ({}));
      setError(data.error || "Could not change status");
    }
  }

  if (!ticket && !error) {
    return <p className="text-sm text-muted-foreground">Loading…</p>;
  }
  if (!ticket) {
    return <p className="text-sm text-destructive">{error}</p>;
  }

  const accessories = [
    ticket.accessoryCharger && "Charger",
    ticket.accessorySim && "SIM",
    ticket.accessoryCase && "Case",
    ticket.accessoryOther,
  ].filter(Boolean);

  return (
    <div className="space-y-6">
      <header className="flex flex-wrap items-start justify-between gap-3">
        <div className="space-y-1">
          <p className="text-sm text-muted-foreground">
            <Link href="/tickets" className="hover:underline">
              Repairs
            </Link>
          </p>
          <h1 className="font-display text-3xl font-semibold tracking-tight">
            {ticket.ticketNumber}
          </h1>
          <div className="flex flex-wrap items-center gap-2">
            <Badge variant={statusTone(ticket.status)}>
              {statusLabel(ticket.status)}
            </Badge>
            <span className="text-sm text-muted-foreground">
              Opened {formatBrisbane(ticket.createdAt)}
            </span>
          </div>
        </div>
      </header>

      {error && <p className="text-sm text-destructive">{error}</p>}

      <div className="grid gap-4 lg:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle>Job</CardTitle>
          </CardHeader>
          <CardContent className="space-y-2 text-sm">
            <p>
              <span className="text-muted-foreground">Customer · </span>
              <Link
                className="text-primary hover:underline"
                href={`/customers/${ticket.customer.id}`}
              >
                {ticket.customer.firstName} {ticket.customer.lastName}
              </Link>{" "}
              · {ticket.customer.phone}
            </p>
            <p>
              <span className="text-muted-foreground">Device · </span>
              {ticket.device
                ? `${ticket.device.brand} ${ticket.device.model}`
                : "—"}
              {ticket.device?.imei ? ` · IMEI ${ticket.device.imei}` : ""}
              {ticket.device?.serial ? ` · S/N ${ticket.device.serial}` : ""}
            </p>
            <p className="whitespace-pre-wrap">
              <span className="text-muted-foreground">Fault · </span>
              {ticket.reportedFault}
            </p>
            <p>
              <span className="text-muted-foreground">Condition · </span>
              Powers on:{" "}
              {ticket.conditionPowersOn === null
                ? "unknown"
                : ticket.conditionPowersOn
                  ? "yes"
                  : "no"}
              {ticket.conditionCracks ? " · cracks" : ""}
              {ticket.conditionScratches ? " · scratches" : ""}
              {ticket.conditionLiquid ? " · liquid" : ""}
              {ticket.conditionNotes ? ` · ${ticket.conditionNotes}` : ""}
            </p>
            <p>
              <span className="text-muted-foreground">Accessories · </span>
              {accessories.length ? accessories.join(", ") : "None noted"}
            </p>
            <p>
              <span className="text-muted-foreground">Labour fee · </span>
              {centsToAud(BigInt(ticket.labourFeeCents))}
            </p>
            <p>
              <span className="text-muted-foreground">Estimate · </span>
              {centsToAud(
                ticket.estimateCents ? BigInt(ticket.estimateCents) : null,
              )}{" "}
              <span className="text-xs text-muted-foreground">
                (not a quote or payment)
              </span>
            </p>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Update status</CardTitle>
          </CardHeader>
          <CardContent className="flex flex-wrap gap-2">
            <select
              className="h-9 rounded-md border border-input bg-background px-3 text-sm"
              value={status}
              onChange={(e) => setStatus(e.target.value)}
            >
              {TICKET_STATUSES.map((s) => (
                <option key={s.value} value={s.value}>
                  {s.label}
                </option>
              ))}
            </select>
            <Button disabled={busy} onClick={() => void changeStatus()}>
              Save status
            </Button>
          </CardContent>
        </Card>
      </div>

      {ticket.photos.length > 0 && (
        <Card>
          <CardHeader>
            <CardTitle>Photos</CardTitle>
          </CardHeader>
          <CardContent className="flex flex-wrap gap-3">
            {ticket.photos.map((p) => (
              // eslint-disable-next-line @next/next/no-img-element
              <img
                key={p.id}
                src={`/api/tickets/${ticket.id}/photos/${p.id}`}
                alt={p.originalName}
                className="h-28 w-28 rounded-md border border-border object-cover"
              />
            ))}
          </CardContent>
        </Card>
      )}

      <div className="grid gap-4 lg:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle>Notes</CardTitle>
          </CardHeader>
          <CardContent className="space-y-3">
            <div className="flex gap-2">
              <Input
                value={note}
                onChange={(e) => setNote(e.target.value)}
                placeholder="Add a note…"
              />
              <Button disabled={busy} onClick={() => void addNote()}>
                Add
              </Button>
            </div>
            <ul className="space-y-3">
              {ticket.notes.map((n) => (
                <li key={n.id} className="border-t border-border pt-3 text-sm">
                  <p className="text-xs text-muted-foreground">
                    {n.author.displayName} · {formatBrisbane(n.createdAt)}
                  </p>
                  <p className="whitespace-pre-wrap">{n.body}</p>
                </li>
              ))}
              {ticket.notes.length === 0 && (
                <li className="text-sm text-muted-foreground">No notes yet.</li>
              )}
            </ul>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Status history</CardTitle>
          </CardHeader>
          <CardContent>
            <ul className="space-y-3 text-sm">
              {ticket.statusHistory.map((h) => (
                <li key={h.id} className="border-t border-border pt-3">
                  <p>
                    {h.fromStatus ? statusLabel(h.fromStatus) : "—"} →{" "}
                    <strong>{statusLabel(h.toStatus)}</strong>
                  </p>
                  <p className="text-xs text-muted-foreground">
                    {h.actor?.displayName ?? "System"} ·{" "}
                    {formatBrisbane(h.createdAt)}
                    {h.comment ? ` · ${h.comment}` : ""}
                  </p>
                </li>
              ))}
            </ul>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
