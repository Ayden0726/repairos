import { redirect } from "next/navigation";

/** Legacy mock route — repairs live under /tickets. */
export default function JobsPage() {
  redirect("/tickets");
}
