"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";

type Customer = {
  id: string;
  firstName: string;
  lastName: string;
  phone: string;
};

type Device = {
  id: string;
  brand: string;
  model: string;
  imei: string | null;
  serial: string | null;
  category: string;
};

export default function NewRepairPage() {
  const router = useRouter();
  const [q, setQ] = useState("");
  const [customers, setCustomers] = useState<Customer[]>([]);
  const [customerId, setCustomerId] = useState("");
  const [devices, setDevices] = useState<Device[]>([]);
  const [deviceId, setDeviceId] = useState("");
  const [useNewDevice, setUseNewDevice] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const [deviceCategory, setDeviceCategory] = useState("phone");
  const [deviceBrand, setDeviceBrand] = useState("");
  const [deviceModel, setDeviceModel] = useState("");
  const [deviceImei, setDeviceImei] = useState("");
  const [deviceSerial, setDeviceSerial] = useState("");
  const [fault, setFault] = useState("");
  const [powersOn, setPowersOn] = useState<string>("");
  const [cracks, setCracks] = useState(false);
  const [scratches, setScratches] = useState(false);
  const [liquid, setLiquid] = useState(false);
  const [conditionNotes, setConditionNotes] = useState("");
  const [charger, setCharger] = useState(false);
  const [sim, setSim] = useState(false);
  const [caseInc, setCaseInc] = useState(false);
  const [accessoryOther, setAccessoryOther] = useState("");
  const [labour, setLabour] = useState("50.00");
  const [estimate, setEstimate] = useState("");
  const [photo, setPhoto] = useState<File | null>(null);

  async function searchCustomers() {
    const res = await fetch(`/api/customers?q=${encodeURIComponent(q)}`);
    const data = await res.json();
    if (res.ok) setCustomers(data.customers);
  }

  useEffect(() => {
    void searchCustomers();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  useEffect(() => {
    if (!customerId) {
      setDevices([]);
      return;
    }
    (async () => {
      const res = await fetch(`/api/customers/${customerId}`);
      const data = await res.json();
      if (res.ok) {
        setDevices(data.customer.devices || []);
        setUseNewDevice((data.customer.devices || []).length === 0);
      }
    })();
  }, [customerId]);

  async function onSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    if (!customerId) {
      setError("Select a customer");
      return;
    }
    setBusy(true);
    try {
      const payload: Record<string, unknown> = {
        customerId,
        reportedFault: fault,
        conditionPowersOn:
          powersOn === "" ? null : powersOn === "yes",
        conditionCracks: cracks,
        conditionScratches: scratches,
        conditionLiquid: liquid,
        conditionNotes: conditionNotes || undefined,
        accessoryCharger: charger,
        accessorySim: sim,
        accessoryCase: caseInc,
        accessoryOther: accessoryOther || undefined,
        labourFeeAud: labour,
        estimateAud: estimate || undefined,
      };
      if (useNewDevice) {
        payload.deviceCategory = deviceCategory;
        payload.deviceBrand = deviceBrand;
        payload.deviceModel = deviceModel;
        payload.deviceImei = deviceImei || undefined;
        payload.deviceSerial = deviceSerial || undefined;
      } else {
        payload.deviceId = deviceId;
      }

      const res = await fetch("/api/tickets", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload),
      });
      const data = await res.json();
      if (!res.ok) {
        setError(data.error || "Could not create ticket");
        return;
      }

      if (photo) {
        const fd = new FormData();
        fd.set("file", photo);
        await fetch(`/api/tickets/${data.ticket.id}/photos`, {
          method: "POST",
          body: fd,
        });
      }

      router.push(`/tickets/${data.ticket.id}`);
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="mx-auto max-w-2xl space-y-6">
      <header className="space-y-1">
        <h1 className="font-display text-3xl font-semibold tracking-tight">
          New repair
        </h1>
        <p className="text-sm text-muted-foreground">
          Intake only — no passcode required. Estimate is not an invoice or
          quote document.
        </p>
      </header>

      <form className="space-y-4" onSubmit={onSubmit}>
        <Card>
          <CardHeader>
            <CardTitle>Customer</CardTitle>
            <CardDescription>Search existing or create under Customers first.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-3">
            <div className="flex gap-2">
              <Input
                placeholder="Name or phone"
                value={q}
                onChange={(e) => setQ(e.target.value)}
              />
              <Button type="button" variant="secondary" onClick={() => void searchCustomers()}>
                Search
              </Button>
            </div>
            <select
              className="h-9 w-full rounded-md border border-input bg-background px-3 text-sm"
              value={customerId}
              onChange={(e) => setCustomerId(e.target.value)}
              required
            >
              <option value="">Select customer…</option>
              {customers.map((c) => (
                <option key={c.id} value={c.id}>
                  {c.firstName} {c.lastName} · {c.phone}
                </option>
              ))}
            </select>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Device</CardTitle>
          </CardHeader>
          <CardContent className="space-y-3">
            <label className="flex items-center gap-2 text-sm">
              <input
                type="checkbox"
                checked={useNewDevice}
                onChange={(e) => setUseNewDevice(e.target.checked)}
              />
              Create new device on intake
            </label>
            {!useNewDevice ? (
              <select
                className="h-9 w-full rounded-md border border-input bg-background px-3 text-sm"
                value={deviceId}
                onChange={(e) => setDeviceId(e.target.value)}
                required={!useNewDevice}
              >
                <option value="">Select device…</option>
                {devices.map((d) => (
                  <option key={d.id} value={d.id}>
                    {d.brand} {d.model}
                    {d.imei ? ` · IMEI ${d.imei.slice(-4)}` : ""}
                  </option>
                ))}
              </select>
            ) : (
              <div className="grid gap-3 sm:grid-cols-2">
                <div className="space-y-2">
                  <Label>Category</Label>
                  <select
                    className="h-9 w-full rounded-md border border-input bg-background px-3 text-sm"
                    value={deviceCategory}
                    onChange={(e) => setDeviceCategory(e.target.value)}
                  >
                    <option value="phone">Phone</option>
                    <option value="tablet">Tablet</option>
                    <option value="computer">Computer</option>
                    <option value="other">Other</option>
                  </select>
                </div>
                <div className="space-y-2">
                  <Label>Brand</Label>
                  <Input value={deviceBrand} onChange={(e) => setDeviceBrand(e.target.value)} required={useNewDevice} />
                </div>
                <div className="space-y-2">
                  <Label>Model</Label>
                  <Input value={deviceModel} onChange={(e) => setDeviceModel(e.target.value)} required={useNewDevice} />
                </div>
                <div className="space-y-2">
                  <Label>IMEI (optional)</Label>
                  <Input value={deviceImei} onChange={(e) => setDeviceImei(e.target.value)} />
                </div>
                <div className="space-y-2 sm:col-span-2">
                  <Label>Serial (optional)</Label>
                  <Input value={deviceSerial} onChange={(e) => setDeviceSerial(e.target.value)} />
                </div>
              </div>
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Fault, condition & accessories</CardTitle>
          </CardHeader>
          <CardContent className="space-y-3">
            <div className="space-y-2">
              <Label>Reported fault</Label>
              <textarea
                className="min-h-24 w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
                value={fault}
                onChange={(e) => setFault(e.target.value)}
                required
                minLength={3}
              />
            </div>
            <div className="space-y-2">
              <Label>Powers on?</Label>
              <select
                className="h-9 w-full rounded-md border border-input bg-background px-3 text-sm"
                value={powersOn}
                onChange={(e) => setPowersOn(e.target.value)}
              >
                <option value="">Unknown</option>
                <option value="yes">Yes</option>
                <option value="no">No</option>
              </select>
            </div>
            <div className="flex flex-wrap gap-4 text-sm">
              <label className="flex items-center gap-2">
                <input type="checkbox" checked={cracks} onChange={(e) => setCracks(e.target.checked)} />
                Cracks / broken glass
              </label>
              <label className="flex items-center gap-2">
                <input type="checkbox" checked={scratches} onChange={(e) => setScratches(e.target.checked)} />
                Scratches
              </label>
              <label className="flex items-center gap-2">
                <input type="checkbox" checked={liquid} onChange={(e) => setLiquid(e.target.checked)} />
                Liquid indicators
              </label>
            </div>
            <div className="space-y-2">
              <Label>Condition notes</Label>
              <Input value={conditionNotes} onChange={(e) => setConditionNotes(e.target.value)} />
            </div>
            <div className="flex flex-wrap gap-4 text-sm">
              <label className="flex items-center gap-2">
                <input type="checkbox" checked={charger} onChange={(e) => setCharger(e.target.checked)} />
                Charger
              </label>
              <label className="flex items-center gap-2">
                <input type="checkbox" checked={sim} onChange={(e) => setSim(e.target.checked)} />
                SIM
              </label>
              <label className="flex items-center gap-2">
                <input type="checkbox" checked={caseInc} onChange={(e) => setCaseInc(e.target.checked)} />
                Case
              </label>
            </div>
            <div className="space-y-2">
              <Label>Other accessories</Label>
              <Input value={accessoryOther} onChange={(e) => setAccessoryOther(e.target.value)} />
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Estimate & photo</CardTitle>
            <CardDescription>
              Default labour/service fee AUD $50 — editable. Estimate is not a payment.
            </CardDescription>
          </CardHeader>
          <CardContent className="grid gap-3 sm:grid-cols-2">
            <div className="space-y-2">
              <Label>Labour / service fee (AUD)</Label>
              <Input value={labour} onChange={(e) => setLabour(e.target.value)} required />
            </div>
            <div className="space-y-2">
              <Label>Estimate total (AUD, optional)</Label>
              <Input value={estimate} onChange={(e) => setEstimate(e.target.value)} placeholder="e.g. 189.00" />
            </div>
            <div className="space-y-2 sm:col-span-2">
              <Label>Photo (optional, JPEG/PNG/WebP ≤ 5 MB)</Label>
              <Input
                type="file"
                accept="image/jpeg,image/png,image/webp"
                onChange={(e) => setPhoto(e.target.files?.[0] ?? null)}
              />
            </div>
          </CardContent>
        </Card>

        {error && <p className="text-sm text-destructive">{error}</p>}
        <Button type="submit" disabled={busy}>
          {busy ? "Saving…" : "Create ticket"}
        </Button>
      </form>
    </div>
  );
}
