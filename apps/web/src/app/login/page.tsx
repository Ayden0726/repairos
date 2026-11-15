"use client";

import { useRouter } from "next/navigation";
import { useMemo, useState } from "react";
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
import { cn } from "@/lib/utils";

type Mode = "pin" | "password";

export default function LoginPage() {
  const router = useRouter();
  const [mode, setMode] = useState<Mode>("pin");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [pin, setPin] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const pinDots = useMemo(
    () => Array.from({ length: Math.max(4, pin.length || 4) }, (_, i) => i < pin.length),
    [pin],
  );

  function appendPin(digit: string) {
    setError(null);
    setPin((p) => (p.length >= 8 ? p : p + digit));
  }

  function clearPin() {
    setPin("");
    setError(null);
  }

  async function submitPassword(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    setBusy(true);
    try {
      const res = await fetch("/api/auth/login", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ mode: "password", email, password }),
      });
      const data = await res.json().catch(() => ({}));
      if (!res.ok) {
        setError(data.error || "Sign-in failed");
        return;
      }
      router.replace("/today");
      router.refresh();
    } finally {
      setBusy(false);
    }
  }

  async function submitPin() {
    if (pin.length < 4) {
      setError("Enter your 4–8 digit PIN");
      return;
    }
    setError(null);
    setBusy(true);
    try {
      const res = await fetch("/api/auth/login", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ mode: "pin", pin }),
      });
      const data = await res.json().catch(() => ({}));
      if (!res.ok) {
        setError(data.error || "Sign-in failed");
        setPin("");
        return;
      }
      router.replace("/today");
      router.refresh();
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="relative flex min-h-screen items-center justify-center overflow-hidden px-4">
      <div className="pointer-events-none absolute inset-0 bench-grid opacity-30" />
      <div
        className="pointer-events-none absolute -left-24 top-0 h-[28rem] w-[28rem] rounded-full blur-3xl"
        style={{ background: "hsl(168 60% 35% / 0.28)" }}
      />
      <div
        className="pointer-events-none absolute -right-16 bottom-0 h-[22rem] w-[22rem] rounded-full blur-3xl"
        style={{ background: "hsl(198 50% 40% / 0.18)" }}
      />

      <Card className="relative w-full max-w-md glass-panel animate-enter shadow-[0_24px_80px_hsl(0_0%_0%/0.35)]">
        <CardHeader className="space-y-3">
          <p className="font-display text-3xl font-bold tracking-tight text-primary">
            TechBench NQ
          </p>
          <CardTitle className="text-xl">Workshop sign in</CardTitle>
          <CardDescription>
            Use a PIN on the bench tablet, or email and password from a desktop.
          </CardDescription>
          <div className="flex gap-1 rounded-xl bg-secondary/80 p-1">
            {(
              [
                ["pin", "PIN"],
                ["password", "Password"],
              ] as const
            ).map(([value, label]) => (
              <button
                key={value}
                type="button"
                className={cn(
                  "pressable flex-1 rounded-lg px-3 py-2 text-sm font-medium transition-colors",
                  mode === value
                    ? "bg-primary text-primary-foreground"
                    : "text-muted-foreground hover:text-foreground",
                )}
                onClick={() => {
                  setMode(value);
                  setError(null);
                }}
              >
                {label}
              </button>
            ))}
          </div>
        </CardHeader>
        <CardContent>
          {mode === "pin" ? (
            <div className="space-y-5 animate-fade">
              <div className="flex justify-center gap-2.5 py-2">
                {pinDots.map((on, i) => (
                  <span
                    key={i}
                    className={cn(
                      "h-3 w-3 rounded-full transition-all duration-200",
                      on
                        ? "scale-110 bg-primary shadow-[0_0_12px_hsl(var(--primary)/0.55)]"
                        : "bg-muted-foreground/35",
                    )}
                  />
                ))}
              </div>
              <div className="grid grid-cols-3 gap-2">
                {["1", "2", "3", "4", "5", "6", "7", "8", "9", "clear", "0", "go"].map(
                  (key) => {
                    if (key === "clear") {
                      return (
                        <Button
                          key={key}
                          type="button"
                          variant="secondary"
                          className="pressable h-14 text-base"
                          onClick={clearPin}
                          disabled={busy}
                        >
                          Clear
                        </Button>
                      );
                    }
                    if (key === "go") {
                      return (
                        <Button
                          key={key}
                          type="button"
                          className="pressable h-14 text-base"
                          onClick={() => void submitPin()}
                          disabled={busy || pin.length < 4}
                        >
                          {busy ? "…" : "Go"}
                        </Button>
                      );
                    }
                    return (
                      <Button
                        key={key}
                        type="button"
                        variant="secondary"
                        className="pressable h-14 font-display text-xl"
                        onClick={() => appendPin(key)}
                        disabled={busy}
                      >
                        {key}
                      </Button>
                    );
                  },
                )}
              </div>
              {error && (
                <p className="text-center text-sm text-destructive animate-fade">
                  {error}
                </p>
              )}
            </div>
          ) : (
            <form className="space-y-4 animate-fade" onSubmit={submitPassword}>
              <div className="space-y-2">
                <Label htmlFor="email">Email</Label>
                <Input
                  id="email"
                  type="email"
                  autoComplete="username"
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  required
                  className="h-11"
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="password">Password</Label>
                <Input
                  id="password"
                  type="password"
                  autoComplete="current-password"
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  required
                  className="h-11"
                />
              </div>
              {error && <p className="text-sm text-destructive">{error}</p>}
              <Button type="submit" className="pressable h-11 w-full" disabled={busy}>
                {busy ? "Signing in…" : "Sign in"}
              </Button>
            </form>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
