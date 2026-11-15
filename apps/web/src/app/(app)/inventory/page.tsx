import { redirect } from "next/navigation";

/** Inventory is a later milestone — not available in M1. */
export default function InventoryPage() {
  redirect("/today");
}
