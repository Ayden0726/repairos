import Link from "next/link";
import { requirePermission } from "@/server/actor";
import { listCustomers } from "@/server/services/customer.service";
import { buttonVariants } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { cn } from "@/lib/utils";

export default async function CustomersPage({ searchParams }: { searchParams: Promise<{ q?: string; type?: string }> }) {
  const actor = await requirePermission("customers.view");
  const sp = await searchParams;
  const rows = await listCustomers(actor, sp.q, sp.type);
  return (
    <div className="space-y-4 pb-16">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-semibold">Customers</h1>
        <Link href="/customers/new" className={cn(buttonVariants())}>
          Add customer
        </Link>
      </div>
      <form className="flex gap-2">
        <Input name="q" defaultValue={sp.q} placeholder="Name, phone, email" />
        <select name="type" defaultValue={sp.type ?? ""} className="h-8 rounded-lg border border-input bg-transparent px-2 text-sm">
          <option value="">All</option>
          <option value="INDIVIDUAL">Individuals</option>
          <option value="BUSINESS">Business</option>
        </select>
        <button className={cn(buttonVariants({ variant: "secondary" }))}>Search</button>
      </form>
      <div className="overflow-x-auto rounded-xl border border-border">
        <table className="w-full text-sm">
          <thead className="bg-muted/50 text-left text-xs text-muted-foreground">
            <tr>
              <th className="px-3 py-2">Name</th>
              <th className="px-3 py-2">Phone</th>
              <th className="px-3 py-2">Type</th>
              <th className="px-3 py-2">Jobs</th>
              <th className="px-3 py-2">Pricing</th>
            </tr>
          </thead>
          <tbody>
            {rows.map((c) => (
              <tr key={c.id} className="border-t border-border">
                <td className="px-3 py-2">
                  <Link href={`/customers/${c.id}`} className="font-medium hover:underline">
                    {c.displayName}
                  </Link>
                </td>
                <td className="px-3 py-2">{c.phone ?? "—"}</td>
                <td className="px-3 py-2">{c.type === "BUSINESS" ? "Business" : "Individual"}</td>
                <td className="px-3 py-2">{c._count.tickets}</td>
                <td className="px-3 py-2">{c.pricingGroup.name}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
