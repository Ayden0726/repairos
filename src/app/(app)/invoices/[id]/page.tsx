import { notFound } from "next/navigation";
import { requirePermission } from "@/server/actor";
import { prisma } from "@/server/db";
import { issueInvoiceAction, paymentAction, refundAction } from "@/app/actions";
import { formatAud } from "@/server/money";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";

export default async function InvoiceDetail({ params }: { params: Promise<{ id: string }> }) {
  await requirePermission("invoices.view");
  const { id } = await params;
  const invoice = await prisma.invoice.findUnique({
    where: { id },
    include: { customer: true, lines: true, payments: { include: { method: true } }, refunds: true },
  });
  if (!invoice) notFound();
  const methods = await prisma.paymentMethod.findMany({ where: { isActive: true } });
  return (
    <div className="space-y-5">
      <div>
        <h1 className="text-2xl font-semibold">{invoice.number}</h1>
        <p className="text-sm text-muted-foreground">
          {invoice.customer.displayName} · {invoice.status.replaceAll("_", " ")}
        </p>
      </div>
      <ul className="rounded-xl border p-4 text-sm">
        {invoice.lines.map((l) => (
          <li key={l.id} className="flex justify-between py-1">
            <span>
              {l.description} × {l.quantity.toString()}
            </span>
            <span>{formatAud(l.unitPrice)}</span>
          </li>
        ))}
        <li className="mt-2 flex justify-between">
          <span>GST</span>
          <span>{formatAud(invoice.gst)}</span>
        </li>
        <li className="flex justify-between font-medium">
          <span>Total</span>
          <span>{formatAud(invoice.total)}</span>
        </li>
        <li className="flex justify-between">
          <span>Paid</span>
          <span>{formatAud(invoice.amountPaid)}</span>
        </li>
      </ul>
      {invoice.status === "DRAFT" ? (
        <form action={issueInvoiceAction}>
          <input type="hidden" name="invoiceId" value={invoice.id} />
          <Button type="submit">Issue invoice</Button>
        </form>
      ) : null}
      <section className="rounded-xl border p-4">
        <h2 className="mb-2 font-medium">Record payment</h2>
        <form action={paymentAction} className="grid gap-2 md:grid-cols-5">
          <input type="hidden" name="invoiceId" value={invoice.id} />
          <select name="methodKey" className="h-8 rounded-lg border px-2 text-sm">
            {methods.map((m) => (
              <option key={m.key} value={m.key}>
                {m.name}
              </option>
            ))}
          </select>
          <Input name="amount" placeholder="Amount" />
          <label className="flex items-center gap-2 text-sm">
            <input type="checkbox" name="deposit" className="size-4" /> Deposit
          </label>
          <Input name="notes" placeholder="Notes" />
          <Button type="submit">Record</Button>
        </form>
        <p className="mt-2 text-xs text-muted-foreground">
          Square stays pending until the provider confirms. Cash and bank transfer are recorded immediately as staff-confirmed.
        </p>
      </section>
      <section className="rounded-xl border p-4">
        <h2 className="mb-2 font-medium">Refund</h2>
        <form action={refundAction} className="flex gap-2">
          <input type="hidden" name="invoiceId" value={invoice.id} />
          <Input name="amount" placeholder="Amount" />
          <Input name="reason" placeholder="Reason" required />
          <Button type="submit" variant="destructive">
            Refund
          </Button>
        </form>
      </section>
      <a className="text-sm underline" href={`/api/invoices/${invoice.id}/pdf`}>
        Download PDF / receipt
      </a>
    </div>
  );
}
