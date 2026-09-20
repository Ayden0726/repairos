"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { CalendarDays, LayoutDashboard, Plus, Ticket, Users } from "lucide-react";
import { cn } from "@/lib/utils";
import type { Actor } from "@/server/actor";

export function MobileNav({ actor }: { actor: Actor }) {
  const pathname = usePathname();
  const tabs = [
    { href: "/", label: "Home", icon: LayoutDashboard },
    { href: "/tickets", label: "Jobs", icon: Ticket },
    { href: "/tickets/new", label: "New", icon: Plus, prominent: true },
    { href: "/customers", label: "People", icon: Users },
    { href: "/calendar", label: "Diary", icon: CalendarDays },
  ];
  if (!actor.permissions.has("tickets.create") && !actor.isOwner) {
    tabs.splice(2, 1);
  }
  return (
    <nav className="fixed inset-x-0 bottom-0 z-40 grid grid-cols-5 border-t border-border bg-background/95 px-1 py-1 backdrop-blur lg:hidden">
      {tabs.map((tab) => {
        const Icon = tab.icon;
        const active = tab.href === "/" ? pathname === "/" : pathname.startsWith(tab.href);
        return (
          <Link
            key={tab.href}
            href={tab.href}
            className={cn(
              "flex min-h-12 flex-col items-center justify-center gap-0.5 rounded-lg text-[11px]",
              "prominent" in tab && tab.prominent
                ? "text-primary"
                : active
                  ? "text-foreground"
                  : "text-muted-foreground",
            )}
          >
            <Icon className={cn("size-5", "prominent" in tab && tab.prominent && "size-6")} />
            {tab.label}
          </Link>
        );
      })}
    </nav>
  );
}
