import type { Actor } from "@/server/actor";
import { can } from "@/server/actor";
import { Sidebar } from "./sidebar";
import { Topbar } from "./topbar";
import { MobileNav } from "./mobile-nav";
import { Shortcuts } from "./shortcuts";

export function AppShell({ actor, children }: { actor: Actor; children: React.ReactNode }) {
  return (
    <div className="flex min-h-dvh bg-background">
      <Sidebar actor={actor} />
      <div className="flex min-w-0 flex-1 flex-col">
        <Topbar actor={actor} />
        <main className="flex-1 overflow-x-hidden px-4 py-5 md:px-6 lg:px-8">
          {children}
        </main>
      </div>
      <MobileNav actor={actor} />
      <Shortcuts defaultTicketView={actor.defaultTicketView} />
    </div>
  );
}

export { can };
