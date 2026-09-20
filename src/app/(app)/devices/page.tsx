import { requirePermission } from "@/server/actor";
import { prisma } from "@/server/db";

export default async function DevicesPage() {
  await requirePermission("devices.view");
  const manufacturers = await prisma.manufacturer.findMany({
    include: { families: { include: { models: true } } },
    orderBy: { name: "asc" },
  });
  return (
    <div className="space-y-4">
      <h1 className="text-2xl font-semibold">Device catalogue</h1>
      {manufacturers.map((m) => (
        <section key={m.id} className="rounded-xl border p-4">
          <h2 className="font-medium">{m.name}</h2>
          {m.families.map((f) => (
            <div key={f.id} className="mt-2 text-sm">
              <div className="text-muted-foreground">
                {f.name} · {f.deviceClass}
              </div>
              <p>{f.models.map((model) => model.name).join(", ")}</p>
            </div>
          ))}
        </section>
      ))}
    </div>
  );
}
