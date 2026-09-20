import Link from "next/link";
import { requirePermission } from "@/server/actor";
import { prisma } from "@/server/db";
import { createBuildAction } from "@/app/actions";
import { formatAud } from "@/server/money";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";

export default async function BuildsPage() {
  const actor = await requirePermission("builds.view");
  const [builds, customers] = await Promise.all([
    prisma.pcBuild.findMany({ where: { archivedAt: null }, include: { customer: true, assignedTo: true }, orderBy: { createdAt: "desc" } }),
    prisma.customer.findMany({ where: { archivedAt: null }, orderBy: { displayName: "asc" } }),
  ]);
  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-semibold">Custom PC builds</h1>
      {actor.permissions.has("builds.manage") || actor.isOwner ? (
        <form action={createBuildAction} className="grid gap-2 rounded-xl border p-4 md:grid-cols-4">
          <select name="customerId" required className="h-8 rounded-lg border px-2 text-sm">
            <option value="">Customer</option>
            {customers.map((c) => (
              <option key={c.id} value={c.id}>
                {c.displayName}
              </option>
            ))}
          </select>
          <Input name="budget" placeholder="Budget AUD" />
          <Input name="useCase" placeholder="Use case" />
          <Button type="submit">Start build</Button>
        </form>
      ) : null}
      <div className="overflow-x-auto rounded-xl border">
        <table className="w-full text-sm">
          <thead className="bg-muted/50 text-left text-xs text-muted-foreground">
            <tr>
              <th className="px-3 py-2">Build</th>
              <th className="px-3 py-2">Customer</th>
              <th className="px-3 py-2">Status</th>
              <th className="px-3 py-2">Estimate</th>
              <th className="px-3 py-2">Tech</th>
            </tr>
          </thead>
          <tbody>
            {builds.map((b) => (
              <tr key={b.id} className="border-t">
                <td className="px-3 py-2">
                  <Link href={`/builds/${b.id}`} className="font-medium hover:underline">
                    {b.number}
                  </Link>
                </td>
                <td className="px-3 py-2">{b.customer.displayName}</td>
                <td className="px-3 py-2">
                  <Badge variant="outline">{b.status.replaceAll("_", " ")}</Badge>
                </td>
                <td className="px-3 py-2">{formatAud(b.estimatedPrice)}</td>
                <td className="px-3 py-2">{b.assignedTo?.name ?? "—"}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
