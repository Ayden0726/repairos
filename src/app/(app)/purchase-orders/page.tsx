import { requirePermission } from "@/server/actor";
import { prisma } from "@/server/db";
import { receivePurchaseOrder } from "@/server/services/inventory.service";
import { formatAud } from "@/server/money";
import { revalidatePath } from "next/cache";
import { Button } from "@/components/ui/button";

export default async function PurchaseOrdersPage() {
  const actor = await requirePermission("inventory.purchase_orders");
  const pos = await prisma.purchaseOrder.findMany({
    include: { supplier: true, items: { include: { item: true } } },
    orderBy: { createdAt: "desc" },
  });

  async function receive(formData: FormData) {
    "use server";
    const { requirePermission: rp } = await import("@/server/actor");
    const a = await rp("inventory.purchase_orders");
    const id = String(formData.get("id"));
    const po = await prisma.purchaseOrder.findUnique({ where: { id }, include: { items: true } });
    if (!po) return;
    await receivePurchaseOrder(
      a,
      id,
      po.items.map((i) => ({ id: i.id, quantity: i.quantity - i.receivedQty })),
    );
    revalidatePath("/purchase-orders");
  }

  return (
    <div className="space-y-4">
      <h1 className="text-2xl font-semibold">Purchase orders</h1>
      {pos.map((po) => (
        <section key={po.id} className="rounded-xl border p-4">
          <div className="flex justify-between">
            <h2 className="font-medium">
              {po.number} · {po.supplier.name}
            </h2>
            <span className="text-sm">{po.status.replaceAll("_", " ")}</span>
          </div>
          <ul className="mt-2 text-sm">
            {po.items.map((i) => (
              <li key={i.id}>
                {i.quantity} × {i.item.name} @ {formatAud(i.unitCost)} · received {i.receivedQty}
              </li>
            ))}
          </ul>
          {po.status !== "RECEIVED" && po.status !== "CANCELLED" ? (
            <form action={receive} className="mt-2">
              <input type="hidden" name="id" value={po.id} />
              <Button size="sm" type="submit">
                Receive remaining
              </Button>
            </form>
          ) : null}
        </section>
      ))}
    </div>
  );
}
