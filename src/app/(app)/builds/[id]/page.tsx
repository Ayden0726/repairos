import { notFound } from "next/navigation";
import { requirePermission } from "@/server/actor";
import { prisma } from "@/server/db";
import { recalculateBuild, setBuildStatus } from "@/server/services/pcbuild.service";
import { formatAud, toNumber } from "@/server/money";
import { Badge } from "@/components/ui/badge";
import { revalidatePath } from "next/cache";

export default async function BuildDetail({ params }: { params: Promise<{ id: string }> }) {
  const actor = await requirePermission("builds.view");
  const { id } = await params;
  const data = await recalculateBuild(actor, id).catch(() => null);
  if (!data) notFound();
  const statuses = [
    "QUOTE",
    "AWAITING_APPROVAL",
    "DEPOSIT_REQUIRED",
    "PARTS_REQUIRED",
    "PARTS_ORDERED",
    "READY_TO_BUILD",
    "ASSEMBLY",
    "TESTING",
    "READY_FOR_PICKUP",
    "COMPLETED",
  ];

  async function updateStatus(formData: FormData) {
    "use server";
    const { requirePermission: rp } = await import("@/server/actor");
    const a = await rp("builds.manage");
    await setBuildStatus(a, id, formData.get("status") as never);
    revalidatePath(`/builds/${id}`);
  }

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold">{data.number}</h1>
          <p className="text-sm text-muted-foreground">
            {data.customer.displayName} · {data.useCase}
          </p>
        </div>
        <Badge variant="outline">{data.status.replaceAll("_", " ")}</Badge>
      </div>
      <form action={updateStatus} className="flex gap-2">
        <select name="status" defaultValue={data.status} className="h-8 rounded-lg border px-2 text-sm">
          {statuses.map((s) => (
            <option key={s} value={s}>
              {s.replaceAll("_", " ")}
            </option>
          ))}
        </select>
        <button className="rounded-lg border px-3 text-sm">Update</button>
      </form>
      <div className="grid gap-4 md:grid-cols-3">
        <Tile label="Parts cost" value={formatAud(data.partsCost)} />
        <Tile label="Estimated price" value={formatAud(data.estimatedPrice)} />
        <Tile label="Expected profit" value={formatAud(data.profit)} />
      </div>
      <section className="rounded-xl border p-4">
        <h2 className="mb-3 font-medium">Components</h2>
        <table className="w-full text-sm">
          <tbody>
            {data.components.map((c) => (
              <tr key={c.id} className="border-t">
                <td className="py-2">{c.category}</td>
                <td>{c.name}</td>
                <td>{formatAud(c.unitPrice)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </section>
      <section className="rounded-xl border p-4">
        <h2 className="mb-2 font-medium">Compatibility</h2>
        <ul className="space-y-1 text-sm">
          {data.warnings.map((w, i) => (
            <li key={i} className={w.level === "warning" ? "text-amber-700 dark:text-amber-400" : "text-muted-foreground"}>
              {w.level.toUpperCase()}: {w.message}
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
