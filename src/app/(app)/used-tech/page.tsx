import Link from "next/link";
import { requirePermission } from "@/server/actor";
import { prisma } from "@/server/db";
import { usedPurchaseAction } from "@/app/actions";
import { recommendOffer } from "@/server/services/used.service";
import { formatAud } from "@/server/money";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { ValuationForm } from "@/components/used/valuation-form";

export default async function UsedTechPage() {
  await requirePermission("used.view");
  const [rows, customers] = await Promise.all([
    prisma.usedDevicePurchase.findMany({ include: { seller: true, refurbishment: true }, orderBy: { createdAt: "desc" } }),
    prisma.customer.findMany({ where: { archivedAt: null } }),
  ]);
  const sample = recommendOffer({ expectedResale: 550, expectedRepairCost: 90, feesAllowance: 40, requiredProfit: 140 });
  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-semibold">Used technology</h1>
      <ValuationForm example={sample.recommendedMaxOffer} />
      <form action={usedPurchaseAction} className="grid gap-2 rounded-xl border p-4 md:grid-cols-3">
        <select name="sellerCustomerId" required className="h-8 rounded-lg border px-2 text-sm">
          <option value="">Seller</option>
          {customers.map((c) => (
            <option key={c.id} value={c.id}>
              {c.displayName}
            </option>
          ))}
        </select>
        <Input name="deviceSummary" placeholder="Device" required />
        <Input name="serial" placeholder="Serial" />
        <Input name="imei" placeholder="IMEI" />
        <select name="conditionGrade" className="h-8 rounded-lg border px-2 text-sm">
          {["A", "B", "C", "D", "FAULTY"].map((g) => (
            <option key={g}>{g}</option>
          ))}
        </select>
        <Input name="expectedResale" placeholder="Likely resale" />
        <Input name="expectedRepairCost" placeholder="Repairs" />
        <Input name="feesAllowance" placeholder="Fees/warranty" />
        <Input name="requiredProfit" placeholder="Required profit" />
        <Input name="purchasePrice" placeholder="Offer paid" />
        <Input name="faults" placeholder="Faults" className="md:col-span-2" />
        <Button type="submit">Record buy-in</Button>
      </form>
      <ul className="divide-y rounded-xl border">
        {rows.map((r) => (
          <li key={r.id} className="flex justify-between px-4 py-3 text-sm">
            <Link href={`/used-tech/${r.id}`} className="hover:underline">
              {r.number} · {r.deviceSummary}
            </Link>
            <span>
              {r.status} · paid {formatAud(r.purchasePrice)}
            </span>
          </li>
        ))}
      </ul>
    </div>
  );
}
