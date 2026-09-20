import Link from "next/link";
import { notFound } from "next/navigation";
import { requirePermission } from "@/server/actor";
import { getCustomer } from "@/server/services/customer.service";
import { formatAud } from "@/server/money";
import { Badge } from "@/components/ui/badge";

export default async function CustomerDetail({ params }: { params: Promise<{ id: string }> }) {
  const actor = await requirePermission("customers.view");
  const { id } = await params;
  let customer;
  try {
    customer = await getCustomer(actor, id);
  } catch {
    notFound();
  }
  return (
    <div className="space-y-6 pb-16">
      <div>
        <h1 className="text-2xl font-semibold">{customer.displayName}</h1>
        <p className="text-sm text-muted-foreground">
          {customer.type === "BUSINESS" ? "Business" : "Individual"} · {customer.pricingGroup.name} · {customer.preferredContact}
        </p>
      </div>
      <div className="grid gap-4 md:grid-cols-2">
        <section className="rounded-xl border border-border p-4 text-sm">
          <h2 className="mb-2 font-medium">Contact</h2>
          <p>{customer.phone}</p>
          <p>{customer.email}</p>
          <p>{[customer.addressLine1, customer.suburb, customer.state, customer.postcode].filter(Boolean).join(", ")}</p>
          {customer.abn ? <p>ABN {customer.abn}</p> : null}
          {customer.contacts.map((c) => (
            <p key={c.id} className="mt-1 text-muted-foreground">
              {c.name} · {c.role.toLowerCase()} · {c.email ?? c.phone}
            </p>
          ))}
        </section>
        <section className="rounded-xl border border-border p-4 text-sm">
          <h2 className="mb-2 font-medium">Devices</h2>
          {customer.devices.map((d) => (
            <p key={d.id}>
              {d.manufacturer} {d.modelName} {d.serial ? `· ${d.serial}` : ""} {d.imei ? `· IMEI ${d.imei}` : ""}
            </p>
          ))}
          {customer.devices.length === 0 ? <p className="text-muted-foreground">No devices on file.</p> : null}
        </section>
      </div>
      <section className="rounded-xl border border-border p-4">
        <h2 className="mb-3 font-medium">Repair history</h2>
        <ul className="space-y-2 text-sm">
          {customer.tickets.map((t) => (
            <li key={t.id} className="flex justify-between">
              <Link href={`/tickets/${t.id}`} className="hover:underline">
                {t.ticketNumber} · {t.type.name}
              </Link>
              <Badge variant="outline">{t.status.name}</Badge>
            </li>
          ))}
        </ul>
      </section>
      <div className="grid gap-4 md:grid-cols-2">
        <section className="rounded-xl border border-border p-4 text-sm">
          <h2 className="mb-2 font-medium">Quotes</h2>
          {customer.quotes.map((q) => (
            <Link key={q.id} href={`/quotes/${q.id}`} className="block hover:underline">
              {q.number} · {q.status} · {formatAud(q.total)}
            </Link>
          ))}
        </section>
        <section className="rounded-xl border border-border p-4 text-sm">
          <h2 className="mb-2 font-medium">Invoices & payments</h2>
          {customer.invoices.map((i) => (
            <Link key={i.id} href={`/invoices/${i.id}`} className="block hover:underline">
              {i.number} · {i.status} · {formatAud(i.total)} paid {formatAud(i.amountPaid)}
            </Link>
          ))}
        </section>
      </div>
    </div>
  );
}
