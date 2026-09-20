import Link from "next/link";
import { requirePermission } from "@/server/actor";
import { prisma } from "@/server/db";
import { formatAud } from "@/server/money";
import { Badge } from "@/components/ui/badge";

export default async function InvoicesPage() {
  await requirePermission("invoices.view");
  const invoices = await prisma.invoice.findMany({
    where: { archivedAt: null },
    include: { customer: true },
    orderBy: { createdAt: "desc" },
  });
  return (
    <div className="space-y-4">
      <h1 className="text-2xl font-semibold">Invoices</h1>
      <div className="overflow-x-auto rounded-xl border">
        <table className="w-full text-sm">
          <thead className="bg-muted/50 text-left text-xs text-muted-foreground">
            <tr>
              <th className="px-3 py-2">Number</th>
              <th className="px-3 py-2">Customer</th>
              <th className="px-3 py-2">Status</th>
              <th className="px-3 py-2">Total</th>
              <th className="px-3 py-2">Paid</th>
            </tr>
          </thead>
          <tbody>
            {invoices.map((i) => (
              <tr key={i.id} className="border-t">
                <td className="px-3 py-2">
                  <Link href={`/invoices/${i.id}`} className="font-medium hover:underline">
                    {i.number}
                  </Link>
                </td>
                <td className="px-3 py-2">{i.customer.displayName}</td>
                <td className="px-3 py-2">
                  <Badge variant="outline">{i.status.replaceAll("_", " ")}</Badge>
                </td>
                <td className="px-3 py-2">{formatAud(i.total)}</td>
                <td className="px-3 py-2">{formatAud(i.amountPaid)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
