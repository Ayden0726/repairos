"use client";

import { useMemo, useState } from "react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  Card,
  CardContent,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import { discountPresets } from "@/lib/mock-data";
import { cn, money } from "@/lib/utils";

/** POS module — register, discounts, Square Readers charge path. */
export function PosPanel() {
  const subtotal = 281;
  const [discountId, setDiscountId] = useState<string | null>("d2");
  const [readerReady] = useState(true);
  const [status, setStatus] = useState<string | null>(null);

  const discount = discountPresets.find((d) => d.id === discountId);
  const discountAmt = useMemo(() => {
    if (!discount) return 0;
    return discount.kind === "percent"
      ? (subtotal * discount.value) / 100
      : discount.value;
  }, [discount, subtotal]);
  const due = Math.max(0, subtotal - discountAmt);

  return (
    <div className="space-y-6">
      <header className="space-y-1">
        <h1 className="font-display text-3xl font-semibold tracking-tight">
          POS
        </h1>
        <p className="text-sm text-muted-foreground">
          Same register + discount presets. Charge on Square Reader when
          attached.
        </p>
      </header>

      <div className="grid gap-4 lg:grid-cols-2">
        <Card>
          <CardHeader className="flex-row items-center justify-between space-y-0">
            <CardTitle>Sale</CardTitle>
            <Badge variant={readerReady ? "success" : "warning"}>
              {readerReady ? "Reader ready" : "Reader offline"}
            </Badge>
          </CardHeader>
          <CardContent className="space-y-4">
            <table className="w-full text-sm">
              <tbody>
                <tr className="border-b border-border">
                  <td className="py-2">Screen job — Sam Lee</td>
                  <td className="py-2 text-right tabular-nums">
                    {money(subtotal)}
                  </td>
                </tr>
                <tr className="border-b border-border text-muted-foreground">
                  <td className="py-2">
                    Discount{discount ? ` (${discount.label})` : ""}
                  </td>
                  <td className="py-2 text-right tabular-nums">
                    −{money(discountAmt)}
                  </td>
                </tr>
              </tbody>
            </table>
            <div className="flex justify-between font-semibold">
              <span>Due</span>
              <span className="tabular-nums">{money(due)}</span>
            </div>
            <div className="flex flex-wrap gap-2">
              {discountPresets.map((d) => (
                <button
                  key={d.id}
                  type="button"
                  onClick={() =>
                    setDiscountId((cur) => (cur === d.id ? null : d.id))
                  }
                  className={cn(
                    "rounded-md border px-3 py-1 text-xs font-medium",
                    discountId === d.id
                      ? "border-primary bg-primary text-primary-foreground"
                      : "border-border bg-card hover:bg-accent",
                  )}
                >
                  {d.label}
                </button>
              ))}
              <button
                type="button"
                className="rounded-md border border-border px-3 py-1 text-xs font-medium hover:bg-accent"
                onClick={() =>
                  alert("Custom discount — same dialog as desktop POS.")
                }
              >
                Custom…
              </button>
            </div>
            <div className="flex gap-2">
              <Button
                onClick={() =>
                  setStatus(
                    readerReady
                      ? `Charged ${money(due)} on Square Reader (mock).`
                      : "Reader offline — use cash or reconnect in Settings.",
                  )
                }
              >
                Charge on Square
              </Button>
              <Button
                variant="secondary"
                onClick={() => setStatus(`Cash sale ${money(due)} (mock).`)}
              >
                Cash
              </Button>
            </div>
            {status && (
              <p className="text-sm text-muted-foreground" role="status">
                {status}
              </p>
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Square</CardTitle>
          </CardHeader>
          <CardContent className="space-y-2 text-sm">
            <p>Terminal: Shop counter Reader</p>
            <p className="text-muted-foreground">
              API: Readers · Payments · refunds stay in this module
            </p>
            <p className="text-muted-foreground">
              Discounts: same presets as today (%, $)
            </p>
            <p className="pt-2 text-xs text-muted-foreground">
              Settings → Square holds location ID, reader pairing, and test mode.
            </p>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
