import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { getActor } from "@/server/actor";
import { isSetupComplete } from "@/server/config/settings";
import { AppShell } from "@/components/layout/app-shell";

export default async function AppLayout({ children }: { children: React.ReactNode }) {
  const setup = await isSetupComplete();
  if (!setup) redirect("/setup");
  const actor = await getActor();
  if (!actor) {
    const jar = await cookies();
    const token = jar.get("workshopos_session")?.value;
    if (token) redirect("/login/two-factor");
    redirect("/login");
  }
  return <AppShell actor={actor}>{children}</AppShell>;
}
