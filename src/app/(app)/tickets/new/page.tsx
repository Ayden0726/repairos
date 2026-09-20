import { quickTicketAction } from "@/app/actions";
import { requirePermission } from "@/server/actor";
import { prisma } from "@/server/db";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import Link from "next/link";

export default async function NewTicketPage() {
  await requirePermission("tickets.create");
  const [customers, types, priorities, techs, services] = await Promise.all([
    prisma.customer.findMany({ where: { archivedAt: null }, orderBy: { displayName: "asc" }, take: 200 }),
    prisma.ticketType.findMany({ where: { archivedAt: null }, orderBy: { sortOrder: "asc" } }),
    prisma.ticketPriority.findMany({ where: { archivedAt: null }, orderBy: { sortOrder: "asc" } }),
    prisma.user.findMany({ where: { archivedAt: null, status: "ACTIVE" }, orderBy: { name: "asc" } }),
    prisma.serviceCatalogue.findMany({ where: { archivedAt: null }, orderBy: { name: "asc" } }),
  ]);
  return (
    <div className="mx-auto max-w-2xl space-y-4 pb-20">
      <div>
        <h1 className="text-2xl font-semibold">Quick intake</h1>
        <p className="text-sm text-muted-foreground">
          Create the job in under a minute. Device details, photos and diagnostics can wait.
        </p>
      </div>
      <form action={quickTicketAction} className="space-y-4 rounded-xl border border-border p-5">
        <div className="grid gap-4 md:grid-cols-2">
          <div className="md:col-span-2">
            <Label htmlFor="customerId">Customer</Label>
            <select id="customerId" name="customerId" required className="mt-1 h-9 w-full rounded-lg border border-input bg-transparent px-2 text-sm">
              <option value="">Select customer</option>
              {customers.map((c) => (
                <option key={c.id} value={c.id}>
                  {c.displayName} {c.phone ? `· ${c.phone}` : ""}
                </option>
              ))}
            </select>
            <Link href="/customers/new" className="mt-1 inline-block text-xs underline">
              Customer not on file? Add them first
            </Link>
          </div>
          <div>
            <Label htmlFor="typeId">Job type</Label>
            <select id="typeId" name="typeId" required className="mt-1 h-9 w-full rounded-lg border border-input bg-transparent px-2 text-sm">
              {types.map((t) => (
                <option key={t.id} value={t.id}>
                  {t.name}
                </option>
              ))}
            </select>
          </div>
          <div>
            <Label htmlFor="priorityId">Priority</Label>
            <select id="priorityId" name="priorityId" className="mt-1 h-9 w-full rounded-lg border border-input bg-transparent px-2 text-sm">
              {priorities.map((t) => (
                <option key={t.id} value={t.id}>
                  {t.name}
                </option>
              ))}
            </select>
          </div>
          <div className="md:col-span-2">
            <Label htmlFor="reportedIssue">Reported issue</Label>
            <Textarea id="reportedIssue" name="reportedIssue" required className="mt-1" rows={4} />
          </div>
          <div>
            <Label htmlFor="assignedToId">Technician</Label>
            <select id="assignedToId" name="assignedToId" className="mt-1 h-9 w-full rounded-lg border border-input bg-transparent px-2 text-sm">
              <option value="">Unassigned</option>
              {techs.map((t) => (
                <option key={t.id} value={t.id}>
                  {t.name}
                </option>
              ))}
            </select>
          </div>
          <div>
            <Label htmlFor="estimatedPrice">Estimated price (AUD)</Label>
            <Input id="estimatedPrice" name="estimatedPrice" inputMode="decimal" className="mt-1" />
          </div>
          <div>
            <Label htmlFor="dueAt">Due date</Label>
            <Input id="dueAt" name="dueAt" type="datetime-local" className="mt-1" />
          </div>
          <div>
            <Label htmlFor="serviceId">Service catalogue (optional)</Label>
            <select id="serviceId" name="serviceId" className="mt-1 h-9 w-full rounded-lg border border-input bg-transparent px-2 text-sm">
              <option value="">None</option>
              {services.map((s) => (
                <option key={s.id} value={s.id}>
                  {s.name}
                </option>
              ))}
            </select>
          </div>
        </div>
        <div className="flex gap-2">
          <Button type="submit" size="lg">
            Create ticket
          </Button>
          <Link href="/tickets/new/full" className="text-sm underline self-center">
            Full intake
          </Link>
        </div>
      </form>
    </div>
  );
}
