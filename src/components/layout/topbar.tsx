import Link from "next/link";
import { Bell, Search } from "lucide-react";
import type { Actor } from "@/server/actor";
import { Button } from "@/components/ui/button";
import { unreadCount } from "@/server/services/notification.service";
import { logoutAction } from "@/app/actions";
import { GlobalSearch } from "./global-search";
import { ThemeToggle } from "./theme-toggle";

export async function Topbar({ actor }: { actor: Actor }) {
  const unread = await unreadCount(actor.id);
  return (
    <header className="sticky top-0 z-30 flex h-14 items-center gap-3 border-b border-border bg-background/90 px-4 backdrop-blur lg:px-6">
      <div className="hidden flex-1 md:block">
        <GlobalSearch />
      </div>
      <Link href="/search" className="md:hidden" aria-label="Search">
        <Search className="size-5" />
      </Link>
      <div className="ml-auto flex items-center gap-1">
        <ThemeToggle />
        <Link href="/notifications" className="relative rounded-lg p-2 hover:bg-muted">
          <Bell className="size-4" />
          {unread > 0 ? (
            <span className="absolute top-1 right-1 flex size-4 items-center justify-center rounded-full bg-destructive text-[10px] text-white">
              {unread > 9 ? "9+" : unread}
            </span>
          ) : null}
        </Link>
        <form action={logoutAction}>
          <Button variant="ghost" size="sm" type="submit">
            Sign out
          </Button>
        </form>
      </div>
    </header>
  );
}
