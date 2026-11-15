"use client";

import { useState } from "react";
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
import { staff } from "@/lib/mock-data";
import { cn } from "@/lib/utils";

type SettingsTab = "users" | "square" | "website";

const TABS: { id: SettingsTab; label: string }[] = [
  { id: "users", label: "Users" },
  { id: "square", label: "Square" },
  { id: "website", label: "Website" },
];

/** Settings module — users, Square Readers API, website sync. */
export function SettingsPanel() {
  const [tab, setTab] = useState<SettingsTab>("users");
  const [syncMsg, setSyncMsg] = useState("Last sync OK");
  const [squareMsg, setSquareMsg] = useState<string | null>(null);

  return (
    <div className="space-y-6">
      <header className="space-y-1">
        <h1 className="font-display text-3xl font-semibold tracking-tight">
          Settings
        </h1>
        <p className="text-sm text-muted-foreground">
          Users, Square, and website sync — the three places that matter.
        </p>
      </header>

      <div
        role="tablist"
        aria-label="Settings sections"
        className="inline-flex h-9 items-center gap-1 rounded-lg bg-muted p-1"
      >
        {TABS.map((t) => (
          <button
            key={t.id}
            type="button"
            role="tab"
            aria-selected={tab === t.id}
            id={`settings-tab-${t.id}`}
            onClick={() => setTab(t.id)}
            className={cn(
              "rounded-md px-3 py-1 text-sm font-medium transition-colors",
              tab === t.id
                ? "bg-background text-foreground"
                : "text-muted-foreground hover:text-foreground",
            )}
          >
            {t.label}
          </button>
        ))}
      </div>

      {tab === "users" && (
        <div role="tabpanel" aria-labelledby="settings-tab-users">
          <div className="overflow-hidden rounded-lg border border-border bg-card">
            <table className="w-full text-sm">
              <thead className="border-b border-border bg-muted/50 text-left text-muted-foreground">
                <tr>
                  <th className="px-4 py-3 font-medium">Name</th>
                  <th className="px-4 py-3 font-medium">Role</th>
                  <th className="px-4 py-3 font-medium">Status</th>
                </tr>
              </thead>
              <tbody>
                {staff.map((u) => (
                  <tr key={u.id} className="border-b border-border last:border-0">
                    <td className="px-4 py-3 font-medium">{u.name}</td>
                    <td className="px-4 py-3">{u.role}</td>
                    <td className="px-4 py-3">
                      <Badge variant="success">{u.status}</Badge>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <p className="mt-3 text-xs text-muted-foreground">
            Same login and role model as the desktop app — Owner, Technician,
            Front desk. Wired to the real API next.
          </p>
        </div>
      )}

      {tab === "square" && (
        <div role="tabpanel" aria-labelledby="settings-tab-square">
          <Card>
            <CardHeader>
              <CardTitle>Square Readers API</CardTitle>
            </CardHeader>
            <CardContent className="max-w-md space-y-4">
              <div className="space-y-2">
                <Label htmlFor="appId">Application ID</Label>
                <Input id="appId" placeholder="sq0idp-…" defaultValue="" />
              </div>
              <div className="space-y-2">
                <Label htmlFor="location">Location ID</Label>
                <Input id="location" placeholder="L…" defaultValue="" />
              </div>
              <div className="space-y-2">
                <Label htmlFor="token">Access token</Label>
                <Input
                  id="token"
                  type="password"
                  placeholder="Stored as a secret — not in chat"
                />
              </div>
              <Button
                variant="secondary"
                onClick={() =>
                  setSquareMsg(
                    "Connection test queued (mock). Pair a Reader after credentials are bound.",
                  )
                }
              >
                Test connection
              </Button>
              {squareMsg && (
                <p className="text-sm text-muted-foreground">{squareMsg}</p>
              )}
            </CardContent>
          </Card>
        </div>
      )}

      {tab === "website" && (
        <div role="tabpanel" aria-labelledby="settings-tab-website">
          <Card>
            <CardHeader>
              <CardTitle>Website connector</CardTitle>
            </CardHeader>
            <CardContent className="space-y-3 text-sm">
              <p>Push: services catalogue, booking availability</p>
              <p>Pull: booking requests, quote enquiries</p>
              <p className="text-muted-foreground">
                Modular — enable without touching POS or inventory internals.
              </p>
              <div className="flex items-center gap-3 pt-2">
                <Button
                  onClick={() =>
                    setSyncMsg(
                      `Synced just now · ${new Date().toLocaleTimeString()}`,
                    )
                  }
                >
                  Sync now
                </Button>
                <Badge variant="success">{syncMsg}</Badge>
              </div>
            </CardContent>
          </Card>
        </div>
      )}
    </div>
  );
}
