"use client";

import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import { APP_MODULES } from "@/lib/modules";
import { cn } from "@/lib/utils";
import { Button } from "@/components/ui/button";

type Me = { displayName: string; email: string; role: string };

export function AppShell({ children }: { children: React.ReactNode }) {
  const pathname = usePathname();
  const router = useRouter();
  const [user, setUser] = useState<Me | null>(null);
  const [ready, setReady] = useState(false);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      const res = await fetch("/api/auth/me");
      if (!res.ok) {
        router.replace("/login");
        return;
      }
      const data = await res.json();
      if (!cancelled) {
        setUser(data.user);
        setReady(true);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [router]);

  if (!ready) {
    return (
      <div className="flex min-h-screen items-center justify-center text-sm text-muted-foreground animate-fade">
        Opening workshop…
      </div>
    );
  }

  return (
    <div className="flex min-h-screen text-foreground">
      <aside className="relative z-10 flex w-[15.5rem] shrink-0 flex-col border-r border-sidebar-border bg-sidebar/90 px-3 py-5 backdrop-blur-md">
        <div className="mb-8 px-2 animate-enter">
          <p className="font-display text-xl font-bold tracking-tight text-primary">
            TechBench NQ
          </p>
          <p className="mt-0.5 text-xs text-muted-foreground">
            Workshop · 1.2.42
          </p>
        </div>
        <nav className="flex flex-1 flex-col gap-1">
          {APP_MODULES.map((mod, i) => {
            const active =
              pathname === mod.href ||
              (mod.href !== "/today" && pathname.startsWith(`${mod.href}`));
            return (
              <Link
                key={mod.id}
                href={mod.href}
                className={cn(
                  "pressable relative rounded-xl px-3 py-2.5 text-sm transition-colors animate-enter",
                  i === 0 && "animate-enter-delay-1",
                  i === 1 && "animate-enter-delay-2",
                  i === 2 && "animate-enter-delay-3",
                  i === 3 && "animate-enter-delay-4",
                  active
                    ? "bg-primary text-primary-foreground shadow-[0_0_0_1px_hsl(var(--primary)/0.35)]"
                    : "text-sidebar-foreground hover:bg-sidebar-accent",
                )}
              >
                {mod.label}
              </Link>
            );
          })}
        </nav>
        <div className="mt-4 border-t border-sidebar-border px-1 pt-4 animate-enter animate-enter-delay-4">
          <p className="truncate text-sm font-medium">{user?.displayName}</p>
          <p className="truncate text-xs capitalize text-muted-foreground">
            {user?.role}
          </p>
          <Button
            variant="ghost"
            size="sm"
            className="mt-2 w-full justify-start px-2"
            onClick={async () => {
              await fetch("/api/auth/logout", { method: "POST" });
              router.replace("/login");
            }}
          >
            Sign out
          </Button>
        </div>
      </aside>
      <main className="relative flex-1 overflow-auto">
        <div className="pointer-events-none absolute inset-0 bench-grid opacity-[0.18]" />
        <div className="relative mx-auto max-w-5xl px-6 py-7 animate-enter">
          {children}
        </div>
      </main>
    </div>
  );
}
