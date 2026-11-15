"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { centsToAud } from "@/lib/money";
import { TICKET_STATUSES, statusLabel, statusTone } from "@/lib/status";

type TicketRow = {
  id: string;
  ticketNumber: string;
  status: Parameters<typeof statusLabel>[0];
  reportedFault: string;
  estimateCents: string | null;
  customer: { firstName: string; lastName: string; phone: string };
  device: { brand: string; model: string } | null;
};

export default function TicketsPage() {
  const [q, setQ] = useState("");
  const [status, setStatus] = useState("");
  const [tickets, setTickets] = useState<TicketRow[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  async function load(nextQ = q, nextStatus = status) {
    setLoading(true);
    setError(null);
    const params = new URLSearchParams();
    if (nextQ.trim()) params.set("q", nextQ.trim());
    if (nextStatus) params.set("status", nextStatus);
    const res = await fetch(`/api/tickets?${params}`);
    const data = await res.json().catch(() => ({}));
    if (!res.ok) {
      setError(data.error || "Could not load tickets");
      setTickets([]);
    } else {
      setTickets(data.tickets);
    }
    setLoading(false);
  }

  useEffect(() => {
    void load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  return (
    <div className="space-y-7">
      <header className="flex flex-wrap items-end justify-between gap-3">
        <div className="space-y-2">
          <p className="text-xs font-medium uppercase tracking-[0.18em] text-primary/80">
            Pipeline
          </p>
          <h1 className="font-display text-4xl font-bold tracking-tight">
            Repairs
          </h1>
          <p className="text-sm text-muted-foreground">
            Search by ticket, customer, phone, IMEI, serial or fault.
          </p>
        </div>
        <Button asChild className="pressable">
          <Link href="/tickets/new">New repair</Link>
        </Button>
      </header>

      <form
        className="glass-panel flex flex-wrap gap-2 rounded-2xl p-3"
        onSubmit={(e) => {
          e.preventDefault();
          void load();
        }}
      >
        <Input
          className="max-w-sm border-0 bg-secondary/60"
          placeholder="Search tickets…"
          value={q}
          onChange={(e) => setQ(e.target.value)}
        />
        <select
          className="h-10 rounded-xl border border-input bg-secondary/60 px-3 text-sm"
          value={status}
          onChange={(e) => setStatus(e.target.value)}
        >
          <option value="">All statuses</option>
          {TICKET_STATUSES.map((s) => (
            <option key={s.value} value={s.value}>
              {s.label}
            </option>
          ))}
        </select>
        <Button type="submit" variant="secondary" className="pressable">
          Search
        </Button>
      </form>

      {error && <p className="text-sm text-destructive">{error}</p>}
      {loading ? (
        <p className="text-sm text-muted-foreground animate-fade">Loading…</p>
      ) : (
        <div className="overflow-hidden rounded-2xl glass-panel">
          <table className="w-full text-sm">
            <thead className="border-b border-border text-left text-muted-foreground">
              <tr>
                <th className="px-4 py-3 font-medium">Ticket</th>
                <th className="px-4 py-3 font-medium">Customer</th>
                <th className="hidden px-4 py-3 font-medium md:table-cell">
                  Device
                </th>
                <th className="hidden px-4 py-3 font-medium lg:table-cell">
                  Fault
                </th>
                <th className="px-4 py-3 font-medium">Status</th>
                <th className="px-4 py-3 font-medium">Estimate</th>
              </tr>
            </thead>
            <tbody>
              {tickets.map((t, i) => (
                <tr
                  key={t.id}
                  className="border-b border-border/70 last:border-0 transition-colors hover:bg-accent/25 animate-enter"
                  style={{ animationDelay: `${Math.min(i, 8) * 0.04}s` }}
                >
                  <td className="px-4 py-3.5">
                    <Link
                      href={`/tickets/${t.id}`}
                      className="font-medium text-primary hover:underline"
                    >
                      {t.ticketNumber}
                    </Link>
                  </td>
                  <td className="px-4 py-3.5">
                    {t.customer.firstName} {t.customer.lastName}
                    <div className="text-xs text-muted-foreground">
                      {t.customer.phone}
                    </div>
                  </td>
                  <td className="hidden px-4 py-3.5 text-muted-foreground md:table-cell">
                    {t.device ? `${t.device.brand} ${t.device.model}` : "—"}
                  </td>
                  <td className="hidden max-w-[220px] truncate px-4 py-3.5 lg:table-cell">
                    {t.reportedFault}
                  </td>
                  <td className="px-4 py-3.5">
                    <Badge variant={statusTone(t.status)}>
                      {statusLabel(t.status)}
                    </Badge>
                  </td>
                  <td className="px-4 py-3.5 tabular-nums">
                    {centsToAud(
                      t.estimateCents ? BigInt(t.estimateCents) : null,
                    )}
                  </td>
                </tr>
              ))}
              {tickets.length === 0 && (
                <tr>
                  <td
                    colSpan={6}
                    className="px-4 py-10 text-center text-muted-foreground"
                  >
                    No tickets match.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
