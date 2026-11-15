import { redirect } from "next/navigation";

/** Calendar is a later milestone — not available in M1. */
export default function CalendarPage() {
  redirect("/today");
}
