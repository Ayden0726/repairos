import { redirect } from "next/navigation";

/** Editable services catalogue is a later milestone — not available in M1. */
export default function ServicesPage() {
  redirect("/today");
}
