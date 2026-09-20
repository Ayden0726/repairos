"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { Input } from "@/components/ui/input";

type Result = {
  tickets: Array<{ id: string; ticketNumber: string; reportedIssue: string }>;
  customers: Array<{ id: string; displayName: string }>;
  invoices: Array<{ id: string; number: string }>;
  parts: Array<{ id: string; name: string; sku: string }>;
  builds: Array<{ id: string; number: string }>;
};

export function GlobalSearch() {
  const [q, setQ] = useState("");
  const [open, setOpen] = useState(false);
  const [results, setResults] = useState<Result | null>(null);
  const router = useRouter();

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if ((e.metaKey || e.ctrlKey) && e.key.toLowerCase() === "k") {
        e.preventDefault();
        document.getElementById("global-search")?.focus();
      }
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, []);

  useEffect(() => {
    if (q.length < 2) return;
    const t = setTimeout(async () => {
      const res = await fetch(`/api/search?q=${encodeURIComponent(q)}`);
      if (res.ok) setResults(await res.json());
    }, 180);
    return () => clearTimeout(t);
  }, [q]);

  return (
    <div className="relative max-w-xl">
      <Input
        id="global-search"
        value={q}
        onChange={(e) => {
          setQ(e.target.value);
          setOpen(true);
        }}
        onFocus={() => setOpen(true)}
        placeholder="Search tickets, customers, serials, SKUs…  ⌘K"
        className="h-9"
        aria-label="Global search"
      />
      {open && q.length >= 2 && results ? (
        <div className="absolute z-40 mt-1 w-full rounded-lg border border-border bg-popover p-2 shadow-lg">
          {!results.tickets.length && !results.customers.length && !results.parts.length ? (
            <p className="p-2 text-sm text-muted-foreground">No matches</p>
          ) : null}
          {results.tickets.map((t) => (
            <button
              key={t.id}
              className="block w-full rounded-md px-2 py-1.5 text-left text-sm hover:bg-muted"
              onClick={() => {
                router.push(`/tickets/${t.id}`);
                setOpen(false);
              }}
            >
              <span className="font-medium">{t.ticketNumber}</span>
              <span className="ml-2 text-muted-foreground">{t.reportedIssue.slice(0, 60)}</span>
            </button>
          ))}
          {results.customers.map((c) => (
            <button
              key={c.id}
              className="block w-full rounded-md px-2 py-1.5 text-left text-sm hover:bg-muted"
              onClick={() => {
                router.push(`/customers/${c.id}`);
                setOpen(false);
              }}
            >
              Customer · {c.displayName}
            </button>
          ))}
          {results.parts.map((p) => (
            <button
              key={p.id}
              className="block w-full rounded-md px-2 py-1.5 text-left text-sm hover:bg-muted"
              onClick={() => {
                router.push(`/inventory?q=${p.sku}`);
                setOpen(false);
              }}
            >
              Part · {p.sku} {p.name}
            </button>
          ))}
        </div>
      ) : null}
    </div>
  );
}
