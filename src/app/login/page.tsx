import Link from "next/link";
import { Wrench } from "lucide-react";
import { loginAction } from "@/app/actions";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { isSetupComplete } from "@/server/config/settings";
import { redirect } from "next/navigation";

export default async function LoginPage({ searchParams }: { searchParams: Promise<{ error?: string; next?: string }> }) {
  if (!(await isSetupComplete())) redirect("/setup");
  const params = await searchParams;
  return (
    <div className="flex min-h-dvh items-center justify-center bg-muted/40 px-4">
      <div className="w-full max-w-md rounded-xl border border-border bg-card p-8 shadow-sm">
        <div className="mb-6 flex items-center gap-3">
          <span className="flex size-10 items-center justify-center rounded-lg bg-foreground text-background">
            <Wrench className="size-5" />
          </span>
          <div>
            <h1 className="text-lg font-semibold">WorkshopOS</h1>
            <p className="text-sm text-muted-foreground">Sign in to the workshop</p>
          </div>
        </div>
        {params.error ? (
          <p className="mb-4 rounded-md border border-destructive/30 bg-destructive/10 px-3 py-2 text-sm text-destructive">
            {params.error}
          </p>
        ) : null}
        <form action={loginAction} className="space-y-4">
          <div className="space-y-1.5">
            <Label htmlFor="email">Email</Label>
            <Input id="email" name="email" type="email" autoComplete="username" required defaultValue="maya@riversidetech.com.au" />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="password">Password</Label>
            <Input id="password" name="password" type="password" autoComplete="current-password" required />
          </div>
          <Button type="submit" className="w-full" size="lg">
            Sign in
          </Button>
        </form>
        <p className="mt-6 text-xs text-muted-foreground">
          Demo owner: maya@riversidetech.com.au · Riverside!2026
        </p>
        <p className="mt-2 text-xs text-muted-foreground">
          First install? <Link className="underline" href="/setup">Run the setup wizard</Link>
        </p>
      </div>
    </div>
  );
}
