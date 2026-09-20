"use client";

import { useRouter } from "next/navigation";
import { useState } from "react";
import Link from "next/link";
import { cn } from "@/lib/utils";

type Card = {
  id: string;
  ticketNumber: string;
  issue: string;
  customer: string;
  statusId: string;
  priority: string;
  priorityKey: string;
  colour: string;
  assignee: string | null;
  dueAt: string | null;
  slaState: string;
};

export function TicketKanban({
  statuses,
  tickets,
  canMove,
}: {
  statuses: Array<{ id: string; name: string; colour: string }>;
  tickets: Card[];
  canMove: boolean;
}) {
  const [items, setItems] = useState(tickets);
  const router = useRouter();

  async function move(ticketId: string, statusId: string) {
    if (!canMove) return;
    setItems((prev) => prev.map((t) => (t.id === ticketId ? { ...t, statusId } : t)));
    await fetch(`/api/tickets/${ticketId}/status`, {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify({ statusId }),
    });
    router.refresh();
  }

  return (
    <div className="flex gap-3 overflow-x-auto pb-4">
      {statuses.map((status) => {
        const column = items.filter((t) => t.statusId === status.id);
        return (
          <section
            key={status.id}
            className="w-72 shrink-0 rounded-xl border border-border bg-muted/30"
            onDragOver={(e) => e.preventDefault()}
            onDrop={(e) => {
              const id = e.dataTransfer.getData("text/ticket");
              if (id) void move(id, status.id);
            }}
          >
            <header className="flex items-center justify-between px-3 py-2">
              <div className="flex items-center gap-2 text-sm font-medium">
                <span className="size-2 rounded-full" style={{ background: status.colour }} />
                {status.name}
              </div>
              <span className="text-xs text-muted-foreground">{column.length}</span>
            </header>
            <div className="space-y-2 px-2 pb-3">
              {column.map((ticket) => (
                <article
                  key={ticket.id}
                  draggable={canMove}
                  onDragStart={(e) => e.dataTransfer.setData("text/ticket", ticket.id)}
                  className={cn(
                    "rounded-lg border border-border bg-card p-3 shadow-sm",
                    ticket.priorityKey === "urgent" && "border-destructive/50",
                    ticket.slaState === "BREACHED" && "ring-1 ring-destructive/40",
                  )}
                >
                  <Link href={`/tickets/${ticket.id}`} className="text-sm font-semibold hover:underline">
                    {ticket.ticketNumber}
                  </Link>
                  <p className="mt-1 line-clamp-2 text-xs text-muted-foreground">{ticket.issue}</p>
                  <div className="mt-2 flex items-center justify-between text-[11px]">
                    <span>{ticket.customer}</span>
                    <span style={{ color: ticket.colour }}>{ticket.priority}</span>
                  </div>
                </article>
              ))}
            </div>
          </section>
        );
      })}
    </div>
  );
}
