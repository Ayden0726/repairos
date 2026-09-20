"use client";

import { useMemo, useState } from "react";

export function ValuationForm({ example }: { example: number }) {
  const [resale, setResale] = useState(550);
  const [repairs, setRepairs] = useState(90);
  const [fees, setFees] = useState(40);
  const [profit, setProfit] = useState(140);
  const max = useMemo(() => resale - repairs - fees - profit, [resale, repairs, fees, profit]);
  return (
    <div className="rounded-xl border p-4">
      <h2 className="mb-2 font-medium">Buy-in valuation</h2>
      <p className="mb-3 text-xs text-muted-foreground">
        Recommended maximum offer = resale − repairs − fees/warranty allowance − required profit. Worked example: ${example.toFixed(2)}.
      </p>
      <div className="grid gap-2 text-sm md:grid-cols-4">
        <label>
          Likely resale
          <input type="number" className="mt-1 h-8 w-full rounded-lg border px-2" value={resale} onChange={(e) => setResale(Number(e.target.value))} />
        </label>
        <label>
          Repairs
          <input type="number" className="mt-1 h-8 w-full rounded-lg border px-2" value={repairs} onChange={(e) => setRepairs(Number(e.target.value))} />
        </label>
        <label>
          Fees / warranty
          <input type="number" className="mt-1 h-8 w-full rounded-lg border px-2" value={fees} onChange={(e) => setFees(Number(e.target.value))} />
        </label>
        <label>
          Required profit
          <input type="number" className="mt-1 h-8 w-full rounded-lg border px-2" value={profit} onChange={(e) => setProfit(Number(e.target.value))} />
        </label>
      </div>
      <p className="mt-3 text-lg font-semibold">Recommended maximum purchase price: ${max.toFixed(2)}</p>
    </div>
  );
}
