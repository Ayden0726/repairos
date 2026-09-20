"use client";

import { useMemo, useState } from "react";
import { Badge } from "@/components/ui/badge";

type Props = {
  actorName: string;
  canManage: boolean;
  permissions: Array<{ key: string; group: string; label: string }>;
  data: Record<string, unknown>;
};

const SECTIONS = [
  "Business",
  "Users",
  "Roles",
  "Security",
  "Tickets",
  "Labour & GST",
  "Integrations",
  "Printers",
  "Backups",
  "Health",
  "Incidents",
];

export function SettingsCentre({ data, permissions }: Props) {
  const [q, setQ] = useState("");
  const shown = useMemo(
    () => SECTIONS.filter((s) => s.toLowerCase().includes(q.toLowerCase()) || q.length === 0),
    [q],
  );
  const typed = data as {
    business: { name?: string; phone?: string; email?: string };
    gst: { registered: boolean; rate: number };
    statuses: Array<{ name: string; colour: string }>;
    types: Array<{ name: string; prefix: string }>;
    priorities: Array<{ name: string }>;
    staff: Array<{ name: string; email: string; role: { name: string } }>;
    roles: Array<{ name: string; permissions: Array<{ permission: string }> }>;
    labour: Array<{ name: string; hourlyRate: unknown }>;
    groups: Array<{ name: string }>;
    methods: Array<{ name: string; isActive: boolean }>;
    sms: Array<{ name: string; enabled: boolean }>;
    printers: Array<{ name: string; kind: string }>;
    backups: Array<{ type: string; status: string; startedAt: string }>;
    incidents: Array<{ number: string; status: string; description: string }>;
    tfa: { requireOwners?: boolean };
    smsCfg: { enabled: boolean; provider: string };
    emailCfg: { enabled: boolean };
    squareCfg: { enabled: boolean };
    ollamaCfg: { enabled: boolean; model?: string };
    health: { overall: string; checks: Array<{ key: string; state: string; detail: string }>; version: string };
  };

  return (
    <div className="space-y-6 pb-16">
      <div>
        <h1 className="text-2xl font-semibold">Settings</h1>
        <p className="text-sm text-muted-foreground">Search the control centre. Permissions are always enforced on the server, not just by hiding these panels.</p>
      </div>
      <input
        value={q}
        onChange={(e) => setQ(e.target.value)}
        placeholder="Search settings"
        className="h-9 w-full max-w-md rounded-lg border px-3 text-sm"
      />
      {shown.includes("Business") ? (
        <Panel title="Business">
          <p>{typed.business.name}</p>
          <p className="text-sm text-muted-foreground">{typed.business.phone} · {typed.business.email}</p>
          <p className="text-sm">GST {typed.gst.registered ? "registered" : "not registered"} at {(typed.gst.rate * 100).toFixed(0)}%</p>
        </Panel>
      ) : null}
      {shown.includes("Users") ? (
        <Panel title="Users">
          <ul className="text-sm">
            {typed.staff.map((s) => (
              <li key={s.email}>
                {s.name} · {s.email} · {s.role.name}
              </li>
            ))}
          </ul>
        </Panel>
      ) : null}
      {shown.includes("Roles") ? (
        <Panel title="Roles & permissions">
          {typed.roles.map((r) => (
            <div key={r.name} className="mb-3">
              <div className="font-medium">{r.name}</div>
              <div className="flex flex-wrap gap-1">
                {r.permissions.slice(0, 8).map((p) => (
                  <Badge key={p.permission} variant="outline">
                    {p.permission}
                  </Badge>
                ))}
                {r.permissions.length > 8 ? <span className="text-xs text-muted-foreground">+{r.permissions.length - 8}</span> : null}
              </div>
            </div>
          ))}
          <p className="text-xs text-muted-foreground">{permissions.length} permission keys available for custom roles.</p>
        </Panel>
      ) : null}
      {shown.includes("Security") ? (
        <Panel title="Security / 2FA">
          <p className="text-sm">Owner 2FA required: {typed.tfa.requireOwners ? "yes" : "no (demo seed disables this so you can sign in)"}</p>
          <p className="text-xs text-muted-foreground">TOTP secrets are stored encrypted. Recovery codes are hashed. Device passwords never go to AI.</p>
        </Panel>
      ) : null}
      {shown.includes("Tickets") ? (
        <Panel title="Ticket types, statuses, numbering">
          <div className="grid gap-3 md:grid-cols-3 text-sm">
            <div>
              <div className="font-medium">Types</div>
              {typed.types.map((t) => (
                <div key={t.name}>
                  {t.prefix} · {t.name}
                </div>
              ))}
            </div>
            <div>
              <div className="font-medium">Statuses</div>
              {typed.statuses.map((s) => (
                <div key={s.name} className="flex items-center gap-2">
                  <span className="size-2 rounded-full" style={{ background: s.colour }} />
                  {s.name}
                </div>
              ))}
            </div>
            <div>
              <div className="font-medium">Priorities</div>
              {typed.priorities.map((p) => (
                <div key={p.name}>{p.name}</div>
              ))}
            </div>
          </div>
        </Panel>
      ) : null}
      {shown.includes("Labour & GST") ? (
        <Panel title="Labour rates, pricing groups, payments">
          <ul className="text-sm">
            {typed.labour.map((l) => (
              <li key={l.name}>
                {l.name} · ${String(l.hourlyRate)}/hr
              </li>
            ))}
          </ul>
          <p className="mt-2 text-sm">Groups: {typed.groups.map((g) => g.name).join(", ")}</p>
          <p className="text-sm">Methods: {typed.methods.map((m) => m.name).join(", ")}</p>
        </Panel>
      ) : null}
      {shown.includes("Integrations") ? (
        <Panel title="SMS, email, Square, Ollama">
          <p className="text-sm">SMS: {typed.smsCfg.enabled ? typed.smsCfg.provider : "disabled (console in demo)"}</p>
          <p className="text-sm">Email: {typed.emailCfg.enabled ? "enabled" : "console / not configured"}</p>
          <p className="text-sm">Square: {typed.squareCfg.enabled ? "enabled" : "optional — not connected"}</p>
          <p className="text-sm">Ollama: {typed.ollamaCfg.enabled ? typed.ollamaCfg.model : "optional — not connected"}</p>
          <ul className="mt-2 text-sm">
            {typed.sms.map((s) => (
              <li key={s.name}>
                {s.name} {s.enabled ? "on" : "off"}
              </li>
            ))}
          </ul>
        </Panel>
      ) : null}
      {shown.includes("Printers") ? (
        <Panel title="Printers">
          {typed.printers.map((p) => (
            <p key={p.name} className="text-sm">
              {p.name} · {p.kind}
            </p>
          ))}
        </Panel>
      ) : null}
      {shown.includes("Backups") ? (
        <Panel title="Backups">
          <form action="/api/backups" method="post">
            <button className="rounded-lg border px-3 py-1 text-sm" type="submit">
              Backup now
            </button>
          </form>
          <ul className="mt-2 text-sm">
            {typed.backups.map((b, i) => (
              <li key={i}>
                {b.type} · {b.status}
              </li>
            ))}
          </ul>
        </Panel>
      ) : null}
      {shown.includes("Health") ? (
        <Panel title="System health">
          <p className="mb-2 text-sm">
            Version {typed.health.version} · {typed.health.overall}
          </p>
          <ul className="text-sm">
            {typed.health.checks.map((c) => (
              <li key={c.key}>
                {c.key}: {c.state} — {c.detail}
              </li>
            ))}
          </ul>
        </Panel>
      ) : null}
      {shown.includes("Incidents") ? (
        <Panel title="Security incident register">
          {typed.incidents.length === 0 ? (
            <p className="text-sm text-muted-foreground">No incidents recorded. This register never auto-decides whether OAIC notification is required.</p>
          ) : (
            typed.incidents.map((i) => (
              <p key={i.number} className="text-sm">
                {i.number} · {i.status} · {i.description}
              </p>
            ))
          )}
        </Panel>
      ) : null}
    </div>
  );
}

function Panel({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <section className="rounded-xl border p-4">
      <h2 className="mb-3 font-medium">{title}</h2>
      {children}
    </section>
  );
}
