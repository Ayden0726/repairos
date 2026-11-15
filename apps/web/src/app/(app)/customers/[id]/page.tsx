"use client";

import Link from "next/link";
import { useParams } from "next/navigation";
import { useCallback, useEffect, useState } from "react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  Card,
  CardContent,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { statusLabel, statusTone } from "@/lib/status";
import type { TicketStatus } from "@prisma/client";

type DeviceRow = {
  id: string;
  brand: string;
  model: string;
  category: string;
  imei: string | null;
  serial: string | null;
};

type TicketRow = {
  id: string;
  ticketNumber: string;
  status: TicketStatus;
};

type CustomerDetail = {
  id: string;
  firstName: string;
  lastName: string;
  phone: string;
  email: string | null;
  devices: DeviceRow[];
  tickets: TicketRow[];
};

export default function CustomerDetailPage() {
  const params = useParams<{ id: string }>();
  const [customer, setCustomer] = useState<CustomerDetail | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [category, setCategory] = useState("phone");
  const [brand, setBrand] = useState("");
  const [model, setModel] = useState("");
  const [imei, setImei] = useState("");
  const [serial, setSerial] = useState("");

  const load = useCallback(async () => {
    const res = await fetch(`/api/customers/${params.id}`);
    const data = await res.json();
    if (!res.ok) {
      setError(data.error || "Not found");
      return;
    }
    setCustomer(data.customer);
  }, [params.id]);

  useEffect(() => {
    void load();
  }, [load]);

  async function addDevice(e: React.FormEvent) {
    e.preventDefault();
    const res = await fetch("/api/devices", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        customerId: params.id,
        category,
        brand,
        model,
        imei: imei || undefined,
        serial: serial || undefined,
      }),
    });
    const data = await res.json();
    if (!res.ok) {
      setError(data.error || "Could not add device");
      return;
    }
    setBrand("");
    setModel("");
    setImei("");
    setSerial("");
    await load();
  }

  if (!customer && !error) {
    return <p className="text-sm text-muted-foreground">Loading…</p>;
  }
  if (!customer) {
    return <p className="text-sm text-destructive">{error}</p>;
  }

  return (
    <div className="space-y-6">
      <header className="space-y-1">
        <p className="text-sm text-muted-foreground">
          <Link href="/customers" className="hover:underline">
            Customers
          </Link>
        </p>
        <h1 className="font-display text-3xl font-semibold tracking-tight">
          {customer.firstName} {customer.lastName}
        </h1>
        <p className="text-sm text-muted-foreground">
          {customer.phone}
          {customer.email ? ` · ${customer.email}` : ""}
        </p>
      </header>

      {error && <p className="text-sm text-destructive">{error}</p>}

      <div className="grid gap-4 lg:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle>Devices</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <ul className="space-y-2 text-sm">
              {customer.devices.map((d) => (
                <li key={d.id} className="border-t border-border pt-2">
                  <p className="font-medium">
                    {d.brand} {d.model}{" "}
                    <span className="text-xs capitalize text-muted-foreground">
                      ({d.category})
                    </span>
                  </p>
                  <p className="text-muted-foreground">
                    {d.imei ? `IMEI ${d.imei}` : "No IMEI"}
                    {d.serial ? ` · S/N ${d.serial}` : ""}
                  </p>
                </li>
              ))}
              {customer.devices.length === 0 && (
                <li className="text-muted-foreground">No devices yet.</li>
              )}
            </ul>
            <form className="space-y-2 border-t border-border pt-3" onSubmit={addDevice}>
              <p className="text-sm font-medium">Add device</p>
              <select
                className="h-9 w-full rounded-md border border-input bg-background px-3 text-sm"
                value={category}
                onChange={(e) => setCategory(e.target.value)}
              >
                <option value="phone">Phone</option>
                <option value="tablet">Tablet</option>
                <option value="computer">Computer</option>
                <option value="other">Other</option>
              </select>
              <div className="grid gap-2 sm:grid-cols-2">
                <div className="space-y-1">
                  <Label>Brand</Label>
                  <Input value={brand} onChange={(e) => setBrand(e.target.value)} required />
                </div>
                <div className="space-y-1">
                  <Label>Model</Label>
                  <Input value={model} onChange={(e) => setModel(e.target.value)} required />
                </div>
                <div className="space-y-1">
                  <Label>IMEI (optional)</Label>
                  <Input value={imei} onChange={(e) => setImei(e.target.value)} />
                </div>
                <div className="space-y-1">
                  <Label>Serial (optional)</Label>
                  <Input value={serial} onChange={(e) => setSerial(e.target.value)} />
                </div>
              </div>
              <Button type="submit" size="sm">
                Save device
              </Button>
            </form>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Tickets</CardTitle>
          </CardHeader>
          <CardContent>
            <ul className="space-y-2 text-sm">
              {customer.tickets.map((t) => (
                <li
                  key={t.id}
                  className="flex items-center justify-between border-t border-border pt-2"
                >
                  <Link
                    href={`/tickets/${t.id}`}
                    className="font-medium text-primary hover:underline"
                  >
                    {t.ticketNumber}
                  </Link>
                  <Badge variant={statusTone(t.status)}>
                    {statusLabel(t.status)}
                  </Badge>
                </li>
              ))}
              {customer.tickets.length === 0 && (
                <li className="text-muted-foreground">No tickets yet.</li>
              )}
            </ul>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
