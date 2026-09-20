import { requirePermission } from "@/server/actor";
import { boardTickets } from "@/server/services/ticket.service";
import { TicketKanban } from "@/components/tickets/kanban";
import Link from "next/link";
import { buttonVariants } from "@/components/ui/button";
import { cn } from "@/lib/utils";

export default async function BoardPage() {
  const actor = await requirePermission("tickets.view");
  const data = await boardTickets(actor);
  return (
    <div className="space-y-4 pb-16">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold">Job board</h1>
          <p className="text-sm text-muted-foreground">Drag a card to change status if you have permission.</p>
        </div>
        <div className="flex gap-2">
          <Link href="/tickets" className={cn(buttonVariants({ variant: "outline" }))}>
            Table
          </Link>
          <Link href="/tickets/new" className={cn(buttonVariants())}>
            New Job
          </Link>
        </div>
      </div>
      <TicketKanban
        statuses={data.statuses}
        tickets={data.tickets.map((t) => ({
          id: t.id,
          ticketNumber: t.ticketNumber,
          issue: t.reportedIssue,
          customer: t.customer.displayName,
          statusId: t.statusId,
          priority: t.priority.name,
          priorityKey: t.priority.key,
          colour: t.priority.colour,
          assignee: t.assignedTo?.name ?? null,
          dueAt: t.dueAt?.toISOString() ?? null,
          slaState: t.slaState,
        }))}
        canMove={actor.isOwner || actor.permissions.has("tickets.status")}
      />
    </div>
  );
}
