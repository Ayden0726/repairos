import { redirect } from "next/navigation";

/** POS is a later milestone — not available in M1. */
export default function PosPage() {
  redirect("/today");
}
