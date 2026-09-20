import { requirePermission } from "@/server/actor";
import { prisma } from "@/server/db";

export default async function SuppliersPage() {
  await requirePermission("inventory.view");
  const rows = await prisma.supplier.findMany({ where: { archivedAt: null }, include: { _count: { select: { items: true, purchaseOrders: true } } } });
  return (
    <div className="space-y-4">
      <h1 className="text-2xl font-semibold">Suppliers</h1>
      <ul className="divide-y rounded-xl border">
        {rows.map((s) => (
          <li key={s.id} className="px-4 py-3 text-sm">
            <div className="font-medium">{s.name}</div>
            <div className="text-muted-foreground">
              {s.contactName} · {s.email} · {s._count.items} parts · {s._count.purchaseOrders} POs
            </div>
          </li>
        ))}
      </ul>
    </div>
  );
}
