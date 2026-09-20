import { requireActor } from "@/server/actor";
import { listNotifications, markRead } from "@/server/services/notification.service";
import { markNotificationsRead } from "@/app/actions";
import { Button } from "@/components/ui/button";
import Link from "next/link";
import { format } from "date-fns";

export default async function NotificationsPage() {
  const actor = await requireActor();
  const rows = await listNotifications(actor.id);
  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-semibold">Notifications</h1>
        <form action={markNotificationsRead}>
          <Button variant="outline" type="submit">
            Mark all read
          </Button>
        </form>
      </div>
      <ul className="divide-y rounded-xl border">
        {rows.map((n) => (
          <li key={n.id} className="px-4 py-3 text-sm">
            <div className="flex justify-between">
              <span className={n.readAt ? "text-muted-foreground" : "font-medium"}>{n.title}</span>
              <span className="text-xs text-muted-foreground">{format(n.createdAt, "d MMM HH:mm")}</span>
            </div>
            <p>{n.body}</p>
            {n.href ? (
              <Link href={n.href} className="text-xs underline">
                Open
              </Link>
            ) : null}
          </li>
        ))}
      </ul>
    </div>
  );
}
