import { requirePermission } from "@/server/actor";
import { listInventory } from "@/server/services/inventory.service";
import { formatAud } from "@/server/money";

export default async function PcComponentsPage() {
  const actor = await requirePermission("inventory.view");
  const rows = await listInventory(actor, "PC_COMPONENT");
  return (
    <div className="space-y-4">
      <h1 className="text-2xl font-semibold">PC hardware inventory</h1>
      <div className="overflow-x-auto rounded-xl border">
        <table className="w-full text-sm">
          <thead className="bg-muted/50 text-left text-xs text-muted-foreground">
            <tr>
              <th className="px-3 py-2">Category</th>
              <th className="px-3 py-2">Name</th>
              <th className="px-3 py-2">SKU</th>
              <th className="px-3 py-2">Stock</th>
              <th className="px-3 py-2">Cost</th>
              <th className="px-3 py-2">Sell</th>
            </tr>
          </thead>
          <tbody>
            {rows.map((r) => (
              <tr key={r.id} className="border-t">
                <td className="px-3 py-2">{r.pcCategory}</td>
                <td className="px-3 py-2">{r.name}</td>
                <td className="px-3 py-2 font-mono text-xs">{r.sku}</td>
                <td className="px-3 py-2">{r.available}</td>
                <td className="px-3 py-2">{formatAud(r.cost)}</td>
                <td className="px-3 py-2">{formatAud(r.salePrice)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
