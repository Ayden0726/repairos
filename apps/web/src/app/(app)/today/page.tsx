import Link from "next/link";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { prisma } from "@/lib/db";
import { requireUser } from "@/lib/session";
import { redirect } from "next/navigation";
import { centsToAud } from "@/lib/money";
import { statusLabel, statusTone } from "@/lib/status";
import { formatBrisbane } from "@/lib/time";

export default async function TodayPage() {
  const session = await requireUser();
  if (!session?.userId) redirect("/login");

  const openWhere = {
    archivedAt: null as null,
    status: {
      notIn: ["collected" as const, "cancelled" as const],
    },
  };

  const [open, awaitingApproval, awaitingParts, ready, recent] =
    await Promise.all([
      prisma.ticket.count({ where: openWhere }),
      prisma.ticket.count({
        where: { archivedAt: null, status: "awaiting_approval" },
      }),
      prisma.ticket.count({
        where: { archivedAt: null, status: "awaiting_parts" },
      }),
      prisma.ticket.count({
        where: { archivedAt: null, status: "ready_for_collection" },
      }),
      prisma.ticket.findMany({
        where: openWhere,
        include: { customer: true, device: true },
        orderBy: { updatedAt: "desc" },
        take: 8,
      }),
    ]);

  const stats = [
    { label: "Open repairs", value: open, delay: "animate-enter-delay-1" },
    {
      label: "Awaiting approval",
      value: awaitingApproval,
      delay: "animate-enter-delay-2",
    },
    {
      label: "Awaiting parts",
      value: awaitingParts,
      delay: "animate-enter-delay-3",
    },
    {
      label: "Ready for collection",
      value: ready,
      delay: "animate-enter-delay-4",
    },
  ];

  return (
    <div className="space-y-8">
      <header className="flex flex-wrap items-end justify-between gap-4">
        <div className="space-y-2">
          <p className="text-xs font-medium uppercase tracking-[0.18em] text-primary/80">
            Bench board
          </p>
          <h1 className="font-display text-4xl font-bold tracking-tight">
            Today
          </h1>
          <p className="text-sm text-muted-foreground">
            Brisbane · {formatBrisbane(new Date(), "EEE d MMM · HH:mm")}
          </p>
        </div>
        <Button asChild className="pressable">
          <Link href="/tickets/new">New repair</Link>
        </Button>
      </header>

      <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
        {stats.map((s) => (
          <div
            key={s.label}
            className={`glass-panel rounded-2xl px-4 py-4 animate-enter ${s.delay}`}
          >
            <p className="font-display text-3xl font-semibold tabular-nums text-foreground">
              {s.value}
            </p>
            <p className="mt-1 text-sm text-muted-foreground">{s.label}</p>
          </div>
        ))}
      </div>

      <section className="glass-panel rounded-2xl p-5 animate-enter animate-enter-delay-2">
        <div className="mb-4 flex items-center justify-between gap-3">
          <h2 className="font-display text-lg font-semibold">Open tickets</h2>
          <Button asChild variant="secondary" size="sm" className="pressable">
            <Link href="/tickets">View all</Link>
          </Button>
        </div>
        {recent.length === 0 ? (
          <p className="text-sm text-muted-foreground">
            No open tickets. Start with a new repair intake.
          </p>
        ) : (
          <ul className="divide-y divide-border/80">
            {recent.map((t) => (
              <li
                key={t.id}
                className="flex flex-wrap items-center justify-between gap-3 py-3 transition-colors hover:bg-accent/30"
              >
                <div className="min-w-0 space-y-0.5">
                  <Link
                    className="font-medium text-primary transition-colors hover:text-primary/80"
                    href={`/tickets/${t.id}`}
                  >
                    {t.ticketNumber}
                  </Link>
                  <p className="truncate text-sm text-muted-foreground">
                    {t.customer.firstName} {t.customer.lastName}
                    {t.device
                      ? ` · ${t.device.brand} ${t.device.model}`
                      : ""}
                  </p>
                </div>
                <div className="flex items-center gap-3">
                  <span className="tabular-nums text-sm text-muted-foreground">
                    {centsToAud(t.estimateCents)}
                  </span>
                  <Badge variant={statusTone(t.status)}>
                    {statusLabel(t.status)}
                  </Badge>
                </div>
              </li>
            ))}
          </ul>
        )}
      </section>
    </div>
  );
}
