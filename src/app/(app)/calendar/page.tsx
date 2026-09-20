import { requirePermission } from "@/server/actor";
import { calendarEvents } from "@/server/services/calendar.service";
import { prisma } from "@/server/db";
import { createBookingAction } from "@/app/actions";
import { addDays, startOfWeek } from "date-fns";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";

export default async function CalendarPage() {
  const actor = await requirePermission("bookings.view");
  const from = startOfWeek(new Date(), { weekStartsOn: 1 });
  const to = addDays(from, 7);
  const events = await calendarEvents(actor, from, to);
  const [customers, types, staff] = await Promise.all([
    prisma.customer.findMany({ where: { archivedAt: null } }),
    prisma.bookingType.findMany({ where: { archivedAt: null } }),
    prisma.user.findMany({ where: { archivedAt: null, status: "ACTIVE" } }),
  ]);
  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-semibold">Calendar</h1>
      <form action={createBookingAction} className="grid gap-2 rounded-xl border p-4 md:grid-cols-3">
        <select name="customerId" required className="h-8 rounded-lg border px-2 text-sm">
          <option value="">Customer</option>
          {customers.map((c) => (
            <option key={c.id} value={c.id}>
              {c.displayName}
            </option>
          ))}
        </select>
        <select name="typeId" className="h-8 rounded-lg border px-2 text-sm">
          {types.map((t) => (
            <option key={t.id} value={t.id}>
              {t.name}
            </option>
          ))}
        </select>
        <select name="staffId" className="h-8 rounded-lg border px-2 text-sm">
          <option value="">Any staff</option>
          {staff.map((s) => (
            <option key={s.id} value={s.id}>
              {s.name}
            </option>
          ))}
        </select>
        <Input name="startsAt" type="datetime-local" required />
        <Input name="endsAt" type="datetime-local" required />
        <Button type="submit">Book</Button>
      </form>
      <ul className="divide-y rounded-xl border">
        {events.map((e) => (
          <li key={`${e.kind}-${e.id}`} className="flex justify-between px-4 py-3 text-sm">
            <a href={e.href} className="hover:underline">
              <span className="mr-2 inline-block size-2 rounded-full" style={{ background: e.colour }} />
              {e.title}
            </a>
            <span className="text-muted-foreground">
              {e.start.toLocaleString("en-AU", { weekday: "short", hour: "2-digit", minute: "2-digit" })}
            </span>
          </li>
        ))}
      </ul>
    </div>
  );
}
