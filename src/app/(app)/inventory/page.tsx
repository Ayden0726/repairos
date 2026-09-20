import { requirePermission } from "@/server/actor";
import { listInventory } from "@/server/services/inventory.service";
import { stockAdjustAction } from "@/app/actions";
import { formatAud } from "@/server/money";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import Link from "next/link";
import { buttonVariants } from "@/components/ui/button";
import { cn } from "@/lib/utils";

export default async function InventoryPage({ searchParams }: { searchParams: Promise<{ q?: string; kind?: string; low?: string }> }) {
  const actor = await requirePermission("inventory.view");
  const sp = await searchParams;
  const rows = await listInventory(actor, sp.kind, sp.q);
  const shown = sp.low === "1" ? rows.filter((r) => r.minStock > 0 && r.onHand <= r.minStock) : rows;
  return (
    <div className="space-y-4 pb-16">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-semibold">Inventory</h1>
        <div className="flex gap-2">
          <Link href="/inventory/pc-components" className={cn(buttonVariants({ variant: "outline" }))}>
            PC components
          </Link>
          <Link href="/inventory/stocktake" className={cn(buttonVariants({ variant: "outline" }))}>
            Stocktake
          </Link>
        </div>
      </div>
      <form className="flex gap-2">
        <Input name="q" defaultValue={sp.q} placeholder="SKU, barcode, name" />
        <select name="kind" defaultValue={sp.kind ?? ""} className="h-8 rounded-lg border px-2 text-sm">
          <option value="">All kinds</option>
          <option value="REPAIR_PART">Repair parts</option>
          <option value="PC_COMPONENT">PC components</option>
          <option value="REFURBISHED_DEVICE">Refurbished</option>
        </select>
        <button className={cn(buttonVariants({ variant: "secondary" }))}>Search</button>
      </form>
      <div className="overflow-x-auto rounded-xl border">
        <table className="w-full min-w-[800px] text-sm">
          <thead className="bg-muted/50 text-left text-xs text-muted-foreground">
            <tr>
              <th className="px-3 py-2">SKU</th>
              <th className="px-3 py-2">Name</th>
              <th className="px-3 py-2">On hand</th>
              <th className="px-3 py-2">Reserved</th>
              <th className="px-3 py-2">Available</th>
              <th className="px-3 py-2">Cost</th>
              <th className="px-3 py-2">Sell</th>
              <th className="px-3 py-2">Adjust</th>
            </tr>
          </thead>
          <tbody>
            {shown.map((row) => (
              <tr key={row.id} className="border-t">
                <td className="px-3 py-2 font-mono text-xs">{row.sku}</td>
                <td className="px-3 py-2">{row.name}</td>
                <td className="px-3 py-2">{row.onHand}</td>
                <td className="px-3 py-2">{row.reserved}</td>
                <td className={cn("px-3 py-2", row.available <= row.minStock && "text-destructive font-medium")}>{row.available}</td>
                <td className="px-3 py-2">{formatAud(row.cost)}</td>
                <td className="px-3 py-2">{formatAud(row.salePrice)}</td>
                <td className="px-3 py-2">
                  {actor.permissions.has("inventory.adjust") || actor.isOwner ? (
                    <form action={stockAdjustAction} className="flex gap-1">
                      <input type="hidden" name="itemId" value={row.id} />
                      <Input name="quantity" placeholder="+/-" className="h-7 w-16" />
                      <Input name="reason" placeholder="Reason" className="h-7 w-28" />
                      <Button size="xs" type="submit">
                        Save
                      </Button>
                    </form>
                  ) : (
                    "—"
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
