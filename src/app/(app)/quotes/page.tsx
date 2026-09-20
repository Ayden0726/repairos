import Link from "next/link";
import { requirePermission } from "@/server/actor";
import { prisma } from "@/server/db";
import { quoteAction } from "@/app/actions";
import { formatAud } from "@/server/money";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";

export default async function QuotesPage() {
  await requirePermission("quotes.view");
  const [quotes, customers] = await Promise.all([
    prisma.quote.findMany({ where: { archivedAt: null }, include: { customer: true }, orderBy: { createdAt: "desc" } }),
    prisma.customer.findMany({ where: { archivedAt: null }, orderBy: { displayName: "asc" } }),
  ]);
  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-semibold">Quotes</h1>
      <form action={quoteAction} className="grid gap-2 rounded-xl border p-4 md:grid-cols-4">
        <select name="customerId" required className="h-8 rounded-lg border px-2 text-sm">
          <option value="">Customer</option>
          {customers.map((c) => (
            <option key={c.id} value={c.id}>
              {c.displayName}
            </option>
          ))}
        </select>
        <Input name="issue" placeholder="Issue / scope" />
        <Input name="amount" placeholder="Amount AUD" />
        <Button type="submit">Create quote</Button>
      </form>
      <ul className="divide-y rounded-xl border">
        {quotes.map((q) => (
          <li key={q.id} className="flex justify-between px-4 py-3 text-sm">
            <Link href={`/quotes/${q.id}`} className="font-medium hover:underline">
              {q.number} · {q.customer.displayName}
            </Link>
            <span>
              {q.status} · {formatAud(q.total)}
            </span>
          </li>
        ))}
      </ul>
    </div>
  );
}
