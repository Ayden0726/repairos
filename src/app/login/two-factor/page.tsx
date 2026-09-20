import { twoFactorAction } from "@/app/actions";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";

export default async function TwoFactorPage({ searchParams }: { searchParams: Promise<{ error?: string }> }) {
  const params = await searchParams;
  return (
    <div className="flex min-h-dvh items-center justify-center bg-muted/40 px-4">
      <div className="w-full max-w-md rounded-xl border border-border bg-card p-8">
        <h1 className="text-lg font-semibold">Two-factor authentication</h1>
        <p className="mt-1 mb-6 text-sm text-muted-foreground">
          Enter the 6-digit code from your authenticator app, or a backup recovery code.
        </p>
        {params.error ? <p className="mb-4 text-sm text-destructive">{params.error}</p> : null}
        <form action={twoFactorAction} className="space-y-4">
          <div className="space-y-1.5">
            <Label htmlFor="token">Authenticator code</Label>
            <Input id="token" name="token" inputMode="numeric" autoComplete="one-time-code" />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="recovery">Recovery code</Label>
            <Input id="recovery" name="recovery" />
          </div>
          <Button type="submit" className="w-full">
            Continue
          </Button>
        </form>
      </div>
    </div>
  );
}
