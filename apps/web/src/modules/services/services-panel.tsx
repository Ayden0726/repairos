"use client";

import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Switch } from "@/components/ui/switch";
import { services as seed, type Service } from "@/lib/mock-data";
import { money } from "@/lib/utils";

/** Services module — editable catalogue; website flag drives sync. */
export function ServicesPanel() {
  const [items, setItems] = useState<Service[]>(seed);

  function toggleWebsite(id: string) {
    setItems((prev) =>
      prev.map((s) =>
        s.id === id ? { ...s, onWebsite: !s.onWebsite } : s,
      ),
    );
  }

  function toggleActive(id: string) {
    setItems((prev) =>
      prev.map((s) => (s.id === id ? { ...s, active: !s.active } : s)),
    );
  }

  return (
    <div className="space-y-6">
      <header className="flex flex-wrap items-end justify-between gap-3">
        <div className="space-y-1">
          <h1 className="font-display text-3xl font-semibold tracking-tight">
            Services
          </h1>
          <p className="text-sm text-muted-foreground">
            Editable catalogue. Toggle “On website” to sync via the website
            module.
          </p>
        </div>
        <Button
          onClick={() =>
            alert("Add service — persists through the services module API.")
          }
        >
          Add service
        </Button>
      </header>

      <div className="overflow-hidden rounded-lg border border-border bg-card">
        <table className="w-full text-sm">
          <thead className="border-b border-border bg-muted/50 text-left text-muted-foreground">
            <tr>
              <th className="px-4 py-3 font-medium">Service</th>
              <th className="px-4 py-3 font-medium">Device</th>
              <th className="px-4 py-3 font-medium">Price</th>
              <th className="px-4 py-3 font-medium">Website</th>
              <th className="px-4 py-3 font-medium">Active</th>
            </tr>
          </thead>
          <tbody>
            {items.map((s) => (
              <tr key={s.id} className="border-b border-border last:border-0">
                <td className="px-4 py-3 font-medium">{s.name}</td>
                <td className="px-4 py-3">{s.device}</td>
                <td className="px-4 py-3 tabular-nums">{money(s.price)}</td>
                <td className="px-4 py-3">
                  <Switch
                    checked={s.onWebsite}
                    onCheckedChange={() => toggleWebsite(s.id)}
                    aria-label={`Website sync for ${s.name}`}
                  />
                </td>
                <td className="px-4 py-3">
                  <Switch
                    checked={s.active}
                    onCheckedChange={() => toggleActive(s.id)}
                    aria-label={`Active ${s.name}`}
                  />
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <div className="rounded-lg border border-border bg-card p-4">
        <p className="text-sm font-semibold">Website sync</p>
        <p className="mt-1 text-sm text-muted-foreground">
          Services marked on website publish through Settings → Website. Bookings
          and quote requests come back the same connector — one module, not a
          second product.
        </p>
      </div>
    </div>
  );
}
