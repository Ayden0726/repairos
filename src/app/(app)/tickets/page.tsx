import Link from "next/link";
import { requirePermission } from "@/server/actor";
import { listTickets } from "@/server/services/ticket.service";
import { prisma } from "@/server/db";
import { Badge } from "@/components/ui/badge";
import { buttonVariants } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { cn } from "@/lib/utils";
import { format } from "date-fns";

export default async function TicketsPage({
  searchParams,
}: {
  searchParams: Promise<Record<string, string | undefined>>;
}) {
  const actor = await requirePermission("tickets.view");
  const sp = await searchParams;
  const data = await listTickets(actor, {
    q: sp.q,
    statusId: sp.statusId,
    technicianId: sp.technicianId,
    priorityId: sp.priorityId,
    typeId: sp.typeId,
    waitingForParts: sp.waitingForParts === "1",
    overdue: sp.overdue === "1",
    page: Number(sp.page ?? 1),
  });
  const [techs, types, priorities] = await Promise.all([
    prisma.user.findMany({ where: { archivedAt: null, status: "ACTIVE" }, select: { id: true, name: true } }),
    prisma.ticketType.findMany({ where: { archivedAt: null }, orderBy: { sortOrder: "asc" } }),
    prisma.ticketPriority.findMany({ where: { archivedAt: null }, orderBy: { sortOrder: "asc" } }),
  ]);

  return (
    <div className="space-y-4 pb-16">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-2xl font-semibold">Tickets</h1>
          <p className="text-sm text-muted-foreground">{data.total} matching jobs</p>
        </div>
        <div className="flex gap-2">
          <Link href="/tickets/board" className={cn(buttonVariants({ variant: "outline" }))}>
            Kanban
          </Link>
          <Link href="/tickets/new" className={cn(buttonVariants())}>
            New Job
          </Link>
        </div>
      </div>
      <form className="grid gap-2 rounded-xl border border-border p-3 md:grid-cols-6">
        <Input name="q" placeholder="Search" defaultValue={sp.q} className="md:col-span-2" />
        <select name="statusId" defaultValue={sp.statusId ?? ""} className="h-8 rounded-lg border border-input bg-transparent px-2 text-sm">
          <option value="">All statuses</option>
          {data.statuses.map((s) => (
            <option key={s.id} value={s.id}>
              {s.name}
            </option>
          ))}
        </select>
        <select name="technicianId" defaultValue={sp.technicianId ?? ""} className="h-8 rounded-lg border border-input bg-transparent px-2 text-sm">
          <option value="">All technicians</option>
          {techs.map((t) => (
            <option key={t.id} value={t.id}>
              {t.name}
            </option>
          ))}
        </select>
        <select name="typeId" defaultValue={sp.typeId ?? ""} className="h-8 rounded-lg border border-input bg-transparent px-2 text-sm">
          <option value="">All job types</option>
          {types.map((t) => (
            <option key={t.id} value={t.id}>
              {t.name}
            </option>
          ))}
        </select>
        <button className={cn(buttonVariants({ variant: "secondary" }))} type="submit">
          Filter
        </button>
      </form>
      <div className="overflow-x-auto rounded-xl border border-border">
        <table className="w-full min-w-[720px] text-sm">
          <thead className="bg-muted/50 text-left text-xs text-muted-foreground">
            <tr>
              <th className="px-3 py-2 font-medium">Ticket</th>
              <th className="px-3 py-2 font-medium">Customer</th>
              <th className="px-3 py-2 font-medium">Type</th>
              <th className="px-3 py-2 font-medium">Status</th>
              <th className="px-3 py-2 font-medium">Priority</th>
              <th className="px-3 py-2 font-medium">Tech</th>
              <th className="px-3 py-2 font-medium">Due</th>
            </tr>
          </thead>
          <tbody>
            {data.rows.map((row) => (
              <tr key={row.id} className="border-t border-border hover:bg-muted/40">
                <td className="px-3 py-2">
                  <Link href={`/tickets/${row.id}`} className="font-medium hover:underline">
                    {row.ticketNumber}
                  </Link>
                  <div className="max-w-xs truncate text-xs text-muted-foreground">{row.reportedIssue}</div>
                </td>
                <td className="px-3 py-2">{row.customer.displayName}</td>
                <td className="px-3 py-2">{row.type.name}</td>
                <td className="px-3 py-2">
                  <Badge variant="outline" style={{ borderColor: row.status.colour, color: row.status.colour }}>
                    {row.status.name}
                  </Badge>
                </td>
                <td className="px-3 py-2">
                  <span className={cn(row.priority.key === "urgent" && "font-semibold text-destructive")}>{row.priority.name}</span>
                </td>
                <td className="px-3 py-2">{row.assignedTo?.name ?? "—"}</td>
                <td className="px-3 py-2">
                  {row.dueAt ? format(row.dueAt, "d MMM") : "—"}
                  {row.dueAt && row.dueAt < new Date() && !row.status.isCompleted ? (
                    <span className="ml-1 text-xs text-destructive">overdue</span>
                  ) : null}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
        {data.rows.length === 0 ? <p className="p-6 text-sm text-muted-foreground">No tickets match these filters.</p> : null}
      </div>
    </div>
  );
}
