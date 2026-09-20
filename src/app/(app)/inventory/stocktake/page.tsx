import { requirePermission } from "@/server/actor";
import { prisma } from "@/server/db";
import { completeStocktake } from "@/server/services/inventory.service";
import { revalidatePath } from "next/cache";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";

export default async function StocktakePage() {
  const actor = await requirePermission("inventory.adjust");
  let open = await prisma.stocktake.findFirst({
    where: { status: "open" },
    include: { lines: { include: { item: true } } },
    orderBy: { createdAt: "desc" },
  });
  if (!open) {
    const items = await prisma.inventoryItem.findMany({ where: { archivedAt: null }, take: 200 });
    open = await prisma.stocktake.create({
      data: {
        name: `Stocktake ${new Date().toISOString().slice(0, 10)}`,
        startedById: actor.id,
        lines: { create: items.map((i) => ({ itemId: i.id, expectedQty: i.onHand })) },
      },
      include: { lines: { include: { item: true } } },
    });
  }

  async function save(formData: FormData) {
    "use server";
    const { requirePermission: rp } = await import("@/server/actor");
    const a = await rp("inventory.adjust");
    const lines = open!.lines.map((line) => ({
      id: line.id,
      actualQty: Number(formData.get(`qty-${line.id}`) ?? line.expectedQty),
      reason: String(formData.get(`reason-${line.id}`) ?? "") || undefined,
    }));
    await completeStocktake(a, open!.id, lines);
    revalidatePath("/inventory/stocktake");
  }

  return (
    <div className="space-y-4">
      <h1 className="text-2xl font-semibold">{open.name}</h1>
      <p className="text-sm text-muted-foreground">USB barcode scanners type into the focused quantity field. Variances need a reason.</p>
      <form action={save} className="space-y-2">
        {open.lines.map((line) => (
          <div key={line.id} className="grid grid-cols-4 items-center gap-2 rounded-lg border px-3 py-2 text-sm">
            <div>
              {line.item.name}
              <div className="font-mono text-xs text-muted-foreground">{line.item.sku}</div>
            </div>
            <div>Expected {line.expectedQty}</div>
            <Input name={`qty-${line.id}`} defaultValue={String(line.actualQty ?? line.expectedQty)} />
            <Input name={`reason-${line.id}`} placeholder="Reason if variance" />
          </div>
        ))}
        <Button type="submit">Post stocktake</Button>
      </form>
    </div>
  );
}
