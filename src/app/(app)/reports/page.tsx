import { requirePermission } from "@/server/actor";
import { reportSummary } from "@/server/services/reporting.service";
import { formatAud } from "@/server/money";
import { subDays } from "date-fns";

export default async function ReportsPage() {
  const actor = await requirePermission("reports.view");
  const data = await reportSummary(actor, subDays(new Date(), 30), new Date());
  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-semibold">Reports · last 30 days</h1>
        <a className="text-sm underline" href="/api/reports/export">
          Export CSV
        </a>
      </div>
      <div className="grid gap-3 md:grid-cols-4">
        <Tile label="Jobs" value={String(data.repairCount)} />
        <Tile label="Avg hours" value={data.averageRepairHours.toFixed(1)} />
        <Tile label="Warranty returns" value={String(data.warrantyReturns)} />
        <Tile label="PC builds" value={String(data.pcBuildCount)} />
        {data.revenue !== null ? <Tile label="Revenue" value={formatAud(data.revenue)} /> : null}
        {data.profit !== null ? <Tile label="Gross profit (approx)" value={formatAud(data.profit)} /> : null}
        {data.gst !== null ? <Tile label="GST collected" value={formatAud(data.gst)} /> : null}
        {data.refunds !== null ? <Tile label="Refunds" value={formatAud(data.refunds)} /> : null}
      </div>
      <section className="rounded-xl border p-4">
        <h2 className="mb-2 font-medium">By category</h2>
        <ul className="text-sm">
          {data.categories.map((c) => (
            <li key={c.name} className="flex justify-between">
              <span>{c.name}</span>
              <span>{c.count}</span>
            </li>
          ))}
        </ul>
      </section>
      <section className="rounded-xl border p-4">
        <h2 className="mb-2 font-medium">Low stock</h2>
        <ul className="text-sm">
          {data.lowStock.map((i) => (
            <li key={i.id}>
              {i.sku} {i.name} · {i.onHand} / min {i.minStock}
            </li>
          ))}
        </ul>
      </section>
    </div>
  );
}

function Tile({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-xl border p-4">
      <div className="text-xs text-muted-foreground">{label}</div>
      <div className="text-xl font-semibold">{value}</div>
    </div>
  );
}
