import { redirect } from "next/navigation";

/** Settings UI beyond seed defaults is a later milestone — not available in M1. */
export default function SettingsPage() {
  redirect("/today");
}
