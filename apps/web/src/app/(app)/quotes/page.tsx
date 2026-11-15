import { redirect } from "next/navigation";

/** Quotes/invoices are a later milestone — not available in M1. */
export default function QuotesPage() {
  redirect("/today");
}
