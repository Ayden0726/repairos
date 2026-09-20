import { requirePermission } from "@/server/actor";
import { prisma } from "@/server/db";
import { upsertShift } from "@/server/services/calendar.service";
import { technicianWorkload } from "@/server/services/calendar.service";
import { revalidatePath } from "next/cache";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { format } from "date-fns";

export default async function RosterPage() {
  const actor = await requirePermission("roster.view");
  const [shifts, staff, load] = await Promise.all([
    prisma.rosterShift.findMany({ include: { user: true }, orderBy: { startsAt: "asc" }, take: 40 }),
    prisma.user.findMany({ where: { archivedAt: null, status: "ACTIVE" } }),
    technicianWorkload(),
  ]);
  const names = Object.fromEntries(staff.map((s) => [s.id, s.name]));

  async function add(formData: FormData) {
    "use server";
    const { requirePermission: rp } = await import("@/server/actor");
    const a = await rp("roster.manage");
    await upsertShift(a, {
      userId: String(formData.get("userId")),
      startsAt: String(formData.get("startsAt")),
      endsAt: String(formData.get("endsAt")),
    });
    revalidatePath("/roster");
  }

  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-semibold">Staff roster</h1>
      <form action={add} className="flex flex-wrap gap-2">
        <select name="userId" className="h-8 rounded-lg border px-2 text-sm">
          {staff.map((s) => (
            <option key={s.id} value={s.id}>
              {s.name}
            </option>
          ))}
        </select>
        <Input name="startsAt" type="datetime-local" />
        <Input name="endsAt" type="datetime-local" />
        <Button type="submit">Add shift</Button>
      </form>
      <section className="rounded-xl border p-4 text-sm">
        <h2 className="mb-2 font-medium">Open ticket load</h2>
        {load.map((l) => (
          <div key={l.assignedToId} className="flex justify-between">
            <span>{l.assignedToId ? names[l.assignedToId] ?? l.assignedToId : "Unassigned"}</span>
            <span>{l._count}</span>
          </div>
        ))}
      </section>
      <ul className="divide-y rounded-xl border text-sm">
        {shifts.map((s) => (
          <li key={s.id} className="flex justify-between px-4 py-2">
            <span>{s.user.name}</span>
            <span>
              {format(s.startsAt, "EEE d MMM HH:mm")} – {format(s.endsAt, "HH:mm")}
            </span>
          </li>
        ))}
      </ul>
    </div>
  );
}
