import { notFound } from "next/navigation";
import { requirePermission } from "@/server/actor";
import { prisma } from "@/server/db";
import { startRefurbishment, markReadyForSale } from "@/server/services/used.service";
import { formatAud } from "@/server/money";
import { revalidatePath } from "next/cache";
import { Button } from "@/components/ui/button";

export default async function UsedDetail({ params }: { params: Promise<{ id: string }> }) {
  const actor = await requirePermission("used.view");
  const { id } = await params;
  const row = await prisma.usedDevicePurchase.findUnique({
    where: { id },
    include: { seller: true, refurbishment: { include: { ticket: true } } },
  });
  if (!row) notFound();
  const refurbType = await prisma.ticketType.findUnique({ where: { key: "refurb" } });

  async function refurb() {
    "use server";
    const { requirePermission: rp } = await import("@/server/actor");
    const a = await rp("used.manage");
    if (!refurbType) return;
    await startRefurbishment(a, id, refurbType.id);
    revalidatePath(`/used-tech/${id}`);
  }
  async function ready(formData: FormData) {
    "use server";
    const { requirePermission: rp } = await import("@/server/actor");
    const a = await rp("used.manage");
    await markReadyForSale(a, id, Number(formData.get("salePrice")));
    revalidatePath(`/used-tech/${id}`);
  }

  return (
    <div className="space-y-4">
      <h1 className="text-2xl font-semibold">{row.number}</h1>
      <p className="text-sm text-muted-foreground">
        {row.deviceSummary} · seller {row.seller.displayName}
      </p>
      <div className="grid gap-3 md:grid-cols-4 text-sm">
        <Tile label="Paid" value={formatAud(row.purchasePrice)} />
        <Tile label="Recommended max" value={formatAud(row.recommendedMaxOffer)} />
        <Tile label="Expected resale" value={formatAud(row.expectedResale)} />
        <Tile label="Status" value={row.status} />
      </div>
      {!row.refurbishment ? (
        <form action={refurb}>
          <Button type="submit">Create refurbishment ticket</Button>
        </form>
      ) : (
        <p className="text-sm">
          Refurb invested {formatAud(row.refurbishment.totalInvested)}
          {row.refurbishment.ticket ? (
            <>
              {" "}
              · <a className="underline" href={`/tickets/${row.refurbishment.ticket.id}`}>Open ticket</a>
            </>
          ) : null}
        </p>
      )}
      <form action={ready} className="flex gap-2">
        <input name="salePrice" placeholder="Sale price" className="h-8 rounded-lg border px-2 text-sm" />
        <Button type="submit" variant="outline">
          Ready for sale
        </Button>
      </form>
    </div>
  );
}

function Tile({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-xl border p-3">
      <div className="text-xs text-muted-foreground">{label}</div>
      <div className="font-medium">{value}</div>
    </div>
  );
}
