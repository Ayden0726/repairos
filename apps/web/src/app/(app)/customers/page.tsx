"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Card,
  CardContent,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";

type Customer = {
  id: string;
  firstName: string;
  lastName: string;
  phone: string;
  email: string | null;
  _count: { devices: number; tickets: number };
};

export default function CustomersPage() {
  const [q, setQ] = useState("");
  const [customers, setCustomers] = useState<Customer[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [firstName, setFirstName] = useState("");
  const [lastName, setLastName] = useState("");
  const [phone, setPhone] = useState("");
  const [email, setEmail] = useState("");

  async function load(query = q) {
    const res = await fetch(`/api/customers?q=${encodeURIComponent(query)}`);
    const data = await res.json();
    if (!res.ok) setError(data.error || "Load failed");
    else {
      setCustomers(data.customers);
      setError(null);
    }
  }

  useEffect(() => {
    void load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  async function createCustomer(e: React.FormEvent) {
    e.preventDefault();
    const res = await fetch("/api/customers", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ firstName, lastName, phone, email }),
    });
    const data = await res.json();
    if (!res.ok) {
      setError(data.error || "Could not create customer");
      return;
    }
    setFirstName("");
    setLastName("");
    setPhone("");
    setEmail("");
    await load("");
  }

  return (
    <div className="space-y-6">
      <header className="space-y-1">
        <h1 className="font-display text-3xl font-semibold tracking-tight">
          Customers
        </h1>
        <p className="text-sm text-muted-foreground">
          Create and search customers. Devices live on the customer record.
        </p>
      </header>

      <div className="grid gap-4 lg:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle>New customer</CardTitle>
          </CardHeader>
          <CardContent>
            <form className="space-y-3" onSubmit={createCustomer}>
              <div className="grid gap-3 sm:grid-cols-2">
                <div className="space-y-2">
                  <Label>First name</Label>
                  <Input value={firstName} onChange={(e) => setFirstName(e.target.value)} required />
                </div>
                <div className="space-y-2">
                  <Label>Last name</Label>
                  <Input value={lastName} onChange={(e) => setLastName(e.target.value)} />
                </div>
              </div>
              <div className="space-y-2">
                <Label>Phone</Label>
                <Input value={phone} onChange={(e) => setPhone(e.target.value)} required />
              </div>
              <div className="space-y-2">
                <Label>Email (optional)</Label>
                <Input type="email" value={email} onChange={(e) => setEmail(e.target.value)} />
              </div>
              <Button type="submit">Save customer</Button>
            </form>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Search</CardTitle>
          </CardHeader>
          <CardContent className="space-y-3">
            <form
              className="flex gap-2"
              onSubmit={(e) => {
                e.preventDefault();
                void load();
              }}
            >
              <Input
                placeholder="Name, phone or email"
                value={q}
                onChange={(e) => setQ(e.target.value)}
              />
              <Button type="submit" variant="secondary">
                Search
              </Button>
            </form>
            {error && <p className="text-sm text-destructive">{error}</p>}
            <ul className="divide-y divide-border">
              {customers.map((c) => (
                <li key={c.id} className="flex items-center justify-between py-3 text-sm">
                  <div>
                    <Link
                      href={`/customers/${c.id}`}
                      className="font-medium text-primary hover:underline"
                    >
                      {c.firstName} {c.lastName}
                    </Link>
                    <p className="text-muted-foreground">{c.phone}</p>
                  </div>
                  <p className="text-xs text-muted-foreground">
                    {c._count.devices} devices · {c._count.tickets} tickets
                  </p>
                </li>
              ))}
              {customers.length === 0 && (
                <li className="py-4 text-muted-foreground">No customers found.</li>
              )}
            </ul>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
