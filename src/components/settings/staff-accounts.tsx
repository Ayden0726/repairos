"use client";

import { createStaffAction, updateStaffStatusAction } from "@/app/actions";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";

export type StaffRow = {
  id: string;
  name: string;
  email: string;
  phone: string | null;
  status: "ACTIVE" | "SUSPENDED";
  isOwner: boolean;
  role: { name: string; key: string };
};

export type RoleOption = {
  id: string;
  name: string;
  key: string;
  description: string | null;
};

const ROLE_HINTS: Record<string, string> = {
  admin: "Same shop-wide access as you, without replacing the founding owner.",
  manager: "Day-to-day operations. Cannot change roles, updates or privacy settings.",
  technician: "Workshop jobs, diagnostics, timers and assigned work. No finance or settings.",
  front_desk: "Intake, customers, bookings and taking payment at the counter.",
};

export function StaffAccounts({
  staff,
  roles,
  canManage,
  actorId,
  notice,
  error,
}: {
  staff: StaffRow[];
  roles: RoleOption[];
  canManage: boolean;
  actorId: string;
  notice?: string;
  error?: string;
}) {
  const creatableRoles = roles.filter((role) => role.key !== "owner");
  const defaultRole = creatableRoles.find((role) => role.key === "technician") ?? creatableRoles[0];

  return (
    <section className="rounded-xl border p-4">
      <h2 className="mb-1 font-medium">Users</h2>
      <p className="mb-4 text-sm text-muted-foreground">
        One owner account is enough if you are also the technician. Sign in as yourself, create jobs and assign them to
        you. Add another login only when someone else needs the bench, the counter or a backup admin.
      </p>
      {notice ? <p className="mb-3 text-sm text-foreground">{notice}</p> : null}
      {error ? <p className="mb-3 text-sm text-destructive">{error}</p> : null}
      <ul className="divide-y text-sm">
        {staff.map((person) => (
          <li key={person.id} className="flex flex-col gap-2 py-3 sm:flex-row sm:items-center sm:justify-between">
            <div>
              <div className="font-medium">
                {person.name}
                {person.id === actorId ? <span className="ml-2 text-xs font-normal text-muted-foreground">you</span> : null}
              </div>
              <div className="text-muted-foreground">
                {person.email}
                {person.phone ? ` · ${person.phone}` : ""}
              </div>
            </div>
            <div className="flex flex-wrap items-center gap-2">
              <Badge variant="outline">{person.role.name}</Badge>
              <Badge variant={person.status === "ACTIVE" ? "secondary" : "destructive"}>
                {person.status === "ACTIVE" ? "Active" : "Suspended"}
              </Badge>
              {person.isOwner ? <Badge>Owner</Badge> : null}
              {canManage && person.id !== actorId && !person.isOwner ? (
                <form action={updateStaffStatusAction}>
                  <input type="hidden" name="userId" value={person.id} />
                  <input type="hidden" name="status" value={person.status === "ACTIVE" ? "SUSPENDED" : "ACTIVE"} />
                  <Button type="submit" variant="outline" size="sm">
                    {person.status === "ACTIVE" ? "Suspend" : "Restore"}
                  </Button>
                </form>
              ) : null}
            </div>
          </li>
        ))}
      </ul>
      {canManage ? (
        <form action={createStaffAction} className="mt-4 space-y-3 rounded-lg border bg-muted/20 p-4">
          <div>
            <h3 className="font-medium">Add staff account</h3>
            <p className="text-xs text-muted-foreground">
              Give them the password in person. They sign in with that email. They do not get an invite email.
            </p>
          </div>
          <div className="grid gap-3 md:grid-cols-2">
            <div>
              <Label htmlFor="staff-name">Full name</Label>
              <Input id="staff-name" name="name" required className="mt-1" autoComplete="off" />
            </div>
            <div>
              <Label htmlFor="staff-email">Email</Label>
              <Input id="staff-email" name="email" type="email" required className="mt-1" autoComplete="off" />
            </div>
            <div>
              <Label htmlFor="staff-phone">Phone</Label>
              <Input id="staff-phone" name="phone" className="mt-1" autoComplete="off" />
            </div>
            <div>
              <Label htmlFor="staff-role">Role</Label>
              <select
                id="staff-role"
                name="roleId"
                required
                defaultValue={defaultRole?.id}
                className="mt-1 h-8 w-full rounded-lg border border-input bg-transparent px-2.5 text-sm"
              >
                {creatableRoles.map((role) => (
                  <option key={role.id} value={role.id}>
                    {role.name}
                  </option>
                ))}
              </select>
              <p className="mt-1 text-xs text-muted-foreground">
                {ROLE_HINTS.technician} Admin is the right choice for a second person who also needs invoices and
                settings.
              </p>
            </div>
            <div className="md:col-span-2">
              <Label htmlFor="staff-password">Temporary password</Label>
              <Input
                id="staff-password"
                name="password"
                type="password"
                required
                minLength={10}
                className="mt-1"
                autoComplete="new-password"
              />
              <p className="mt-1 text-xs text-muted-foreground">At least 10 characters, with upper, lower and a number.</p>
            </div>
          </div>
          <Button type="submit">Create account</Button>
        </form>
      ) : (
        <p className="mt-3 text-sm text-muted-foreground">Only the owner or someone with staff management can add logins.</p>
      )}
    </section>
  );
}
