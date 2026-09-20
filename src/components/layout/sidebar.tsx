"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import {
  LayoutDashboard,
  Ticket,
  Users,
  Package,
  Cpu,
  FileText,
  Receipt,
  Recycle,
  CalendarDays,
  BookOpen,
  BarChart3,
  Settings,
  Plus,
  Wrench,
  Truck,
} from "lucide-react";
import { cn } from "@/lib/utils";
import type { Actor } from "@/server/actor";
import { buttonVariants } from "@/components/ui/button";

const items = [
  { href: "/", label: "Dashboard", icon: LayoutDashboard, perm: null },
  { href: "/tickets", label: "Tickets", icon: Ticket, perm: "tickets.view" },
  { href: "/customers", label: "Customers", icon: Users, perm: "customers.view" },
  { href: "/inventory", label: "Inventory", icon: Package, perm: "inventory.view" },
  { href: "/builds", label: "PC Builds", icon: Cpu, perm: "builds.view" },
  { href: "/quotes", label: "Quotes", icon: FileText, perm: "quotes.view" },
  { href: "/invoices", label: "Invoices", icon: Receipt, perm: "invoices.view" },
  { href: "/used-tech", label: "Used Tech", icon: Recycle, perm: "used.view" },
  { href: "/calendar", label: "Calendar", icon: CalendarDays, perm: "bookings.view" },
  { href: "/purchase-orders", label: "Purchasing", icon: Truck, perm: "inventory.purchase_orders" },
  { href: "/knowledge", label: "Knowledge", icon: BookOpen, perm: "knowledge.view" },
  { href: "/reports", label: "Reports", icon: BarChart3, perm: "reports.view" },
  { href: "/settings", label: "Settings", icon: Settings, perm: "settings.view" },
];

export function Sidebar({ actor }: { actor: Actor }) {
  const pathname = usePathname();
  const visible = items.filter((item) => !item.perm || actor.isOwner || actor.permissions.has(item.perm));
  return (
    <aside className="hidden w-60 shrink-0 flex-col border-r border-border bg-sidebar text-sidebar-foreground lg:flex">
      <div className="flex items-center gap-2 px-4 py-4">
        <span className="flex size-8 items-center justify-center rounded-lg bg-foreground text-background">
          <Wrench className="size-4" />
        </span>
        <div>
          <div className="text-sm font-semibold tracking-tight">WorkshopOS</div>
          <div className="text-xs text-muted-foreground">Riverside Tech</div>
        </div>
      </div>
      {actor.permissions.has("tickets.create") || actor.isOwner ? (
        <div className="px-3 pb-3">
          <Link
            href="/tickets/new"
            className={cn(buttonVariants({ size: "lg" }), "h-10 w-full justify-center gap-2 text-sm")}
          >
            <Plus className="size-4" />
            New Job
          </Link>
        </div>
      ) : null}
      <nav className="flex-1 space-y-0.5 overflow-y-auto px-2 pb-4">
        {visible.map((item) => {
          const active = item.href === "/" ? pathname === "/" : pathname.startsWith(item.href);
          const Icon = item.icon;
          return (
            <Link
              key={item.href}
              href={item.href}
              className={cn(
                "flex items-center gap-2 rounded-lg px-2.5 py-2 text-sm transition-colors",
                active ? "bg-sidebar-accent font-medium text-foreground" : "text-muted-foreground hover:bg-sidebar-accent/70 hover:text-foreground",
              )}
            >
              <Icon className="size-4" />
              {item.label}
            </Link>
          );
        })}
      </nav>
      <div className="border-t border-border px-4 py-3 text-xs text-muted-foreground">
        <div className="font-medium text-foreground">{actor.name}</div>
        <div>{actor.roleName}</div>
      </div>
    </aside>
  );
}
