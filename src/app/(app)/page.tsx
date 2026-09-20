import Link from "next/link";
import { getActor } from "@/server/actor";
import { dashboard } from "@/server/services/reporting.service";
import { activeTimer } from "@/server/services/workshop.service";
import { formatAud } from "@/server/money";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { buttonVariants } from "@/components/ui/button";
import { cn } from "@/lib/utils";
import { subDays } from "date-fns";
import { prisma } from "@/server/db";

export default async function HomePage() {
  const actor = (await getActor())!;
  const from = subDays(new Date(), 30);
  const data = await dashboard(actor, from, new Date());
  const timer = await activeTimer(actor.id);
  const techNames = Object.fromEntries(
    (await prisma.user.findMany({ select: { id: true, name: true } })).map((u) => [u.id, u.name]),
  );
  const isTech = actor.roleKey === "technician";

  return (
    <div className="space-y-6 pb-16 lg:pb-0">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">{isTech ? "Technician board" : "Workshop overview"}</h1>
          <p className="text-sm text-muted-foreground">Good to have you in, {actor.name.split(" ")[0]}.</p>
        </div>
        <Link href="/tickets/new" className={cn(buttonVariants({ size: "lg" }), "h-10")}>
          New Job
        </Link>
      </div>

      {timer ? (
        <div className="rounded-lg border border-amber-500/40 bg-amber-500/10 px-4 py-3 text-sm">
          Timer running on{" "}
          <Link className="font-medium underline" href={`/tickets/${timer.ticket.id}`}>
            {timer.ticket.ticketNumber}
          </Link>{" "}
          · {timer.state.toLowerCase()}
        </div>
      ) : null}

      <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
        <Stat label="Open jobs" value={String(data.open)} href="/tickets" />
        <Stat label="Overdue" value={String(data.overdue)} warn={data.overdue > 0} href="/tickets?overdue=1" />
        <Stat label="Waiting for parts" value={String(data.waitingParts)} href="/tickets?waitingForParts=1" />
        <Stat label="Ready for pickup" value={String(data.ready)} href="/tickets?status=ready_pickup" />
        {!isTech && data.revenue !== null ? (
          <Stat label="Payments (30d)" value={formatAud(data.revenue)} href="/reports" />
        ) : null}
        <Stat label="Today's bookings" value={String(data.bookings)} href="/calendar" />
        <Stat label="Low stock" value={String(data.lowStock)} warn={data.lowStock > 0} href="/inventory?low=1" />
        <Stat label="Avg repair (hrs)" value={String(data.avgRepairHours)} href="/reports" />
      </div>

      <div className="grid gap-4 lg:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle>{isTech ? "My assigned jobs" : "Urgent & SLA"}</CardTitle>
          </CardHeader>
          <CardContent className="space-y-2">
            {(isTech ? data.myJobs : [...data.urgent, ...data.sla]).slice(0, 8).map((t) => (
              <Link key={t.id} href={`/tickets/${t.id}`} className="flex items-center justify-between rounded-md px-2 py-2 hover:bg-muted">
                <div>
                  <div className="text-sm font-medium">{t.ticketNumber}</div>
                  <div className="text-xs text-muted-foreground">{t.customer.displayName}</div>
                </div>
                <Badge variant="outline" style={{ borderColor: t.priority.colour, color: t.priority.colour }}>
                  {t.priority.name}
                </Badge>
              </Link>
            ))}
            {(isTech ? data.myJobs : [...data.urgent, ...data.sla]).length === 0 ? (
              <p className="text-sm text-muted-foreground">Nothing needs attention right now.</p>
            ) : null}
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <CardTitle>Technician workload</CardTitle>
          </CardHeader>
          <CardContent>
            {data.workload.length === 0 ? (
              <p className="text-sm text-muted-foreground">No open assigned jobs.</p>
            ) : (
              <ul className="space-y-2 text-sm">
                {data.workload.map((w) => (
                  <li key={w.assignedToId} className="flex justify-between">
                    <span className="text-muted-foreground">{w.assignedToId ? techNames[w.assignedToId] ?? "Technician" : "Unassigned"}</span>
                    <span className="font-medium">{w._count} open</span>
                  </li>
                ))}
              </ul>
            )}
          </CardContent>
        </Card>
      </div>
    </div>
  );
}

function Stat({ label, value, href, warn }: { label: string; value: string; href: string; warn?: boolean }) {
  return (
    <Link href={href} className="rounded-xl border border-border bg-card p-4 hover:bg-muted/40">
      <div className="text-xs text-muted-foreground">{label}</div>
      <div className={cn("mt-1 text-2xl font-semibold", warn && "text-destructive")}>{value}</div>
    </Link>
  );
}
