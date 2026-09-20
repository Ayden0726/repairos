import { notFound } from "next/navigation";
import { requirePermission } from "@/server/actor";
import { prisma } from "@/server/db";
import { convertQuoteAction, quoteApprovalAction } from "@/app/actions";
import { formatAud } from "@/server/money";
import { Button } from "@/components/ui/button";

export default async function QuoteDetail({ params }: { params: Promise<{ id: string }> }) {
  await requirePermission("quotes.view");
  const { id } = await params;
  const quote = await prisma.quote.findUnique({ where: { id }, include: { customer: true, lines: true } });
  if (!quote) notFound();
  const types = await prisma.ticketType.findMany({ where: { archivedAt: null } });
  return (
    <div className="space-y-4">
      <h1 className="text-2xl font-semibold">{quote.number}</h1>
      <p className="text-sm text-muted-foreground">
        {quote.customer.displayName} · {quote.status} · approval {quote.approvalStatus}
      </p>
      <ul className="rounded-xl border p-4 text-sm">
        {quote.lines.map((l) => (
          <li key={l.id} className="flex justify-between py-1">
            <span>{l.description}</span>
            <span>{formatAud(l.unitPrice)}</span>
          </li>
        ))}
        <li className="mt-2 flex justify-between font-medium">
          <span>Total</span>
          <span>{formatAud(quote.total)}</span>
        </li>
      </ul>
      <form action={quoteApprovalAction} className="flex gap-2">
        <input type="hidden" name="quoteId" value={quote.id} />
        <select name="status" className="h-8 rounded-lg border px-2 text-sm">
          <option value="pending">Pending</option>
          <option value="approved">Approved</option>
          <option value="declined">Declined</option>
        </select>
        <input name="notes" placeholder="Approval notes" className="h-8 rounded-lg border px-2 text-sm" />
        <Button size="sm" type="submit">
          Record decision
        </Button>
      </form>
      <form action={convertQuoteAction} className="flex gap-2">
        <input type="hidden" name="quoteId" value={quote.id} />
        <select name="typeId" className="h-8 rounded-lg border px-2 text-sm">
          {types.map((t) => (
            <option key={t.id} value={t.id}>
              {t.name}
            </option>
          ))}
        </select>
        <Button size="sm" variant="outline" type="submit">
          Convert to ticket
        </Button>
      </form>
      <a className="text-sm underline" href={`/api/quotes/${quote.id}/pdf`}>
        Download PDF
      </a>
    </div>
  );
}
