import { setupAction } from "@/app/actions";
import { isSetupComplete } from "@/server/config/settings";
import { redirect } from "next/navigation";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";

export default async function SetupPage({ searchParams }: { searchParams: Promise<{ error?: string }> }) {
  if (await isSetupComplete()) redirect("/login");
  const params = await searchParams;
  return (
    <div className="mx-auto min-h-dvh max-w-3xl px-4 py-10">
      <h1 className="text-2xl font-semibold tracking-tight">First-run setup</h1>
      <p className="mt-1 mb-8 text-sm text-muted-foreground">
        Configure this shop before anyone else can sign in. Square, SMS and Ollama can be skipped and finished later in Settings.
      </p>
      {params.error ? <p className="mb-4 text-sm text-destructive">{params.error}</p> : null}
      <form action={setupAction} className="space-y-8">
        <section className="space-y-3 rounded-xl border border-border p-5">
          <h2 className="font-medium">Business</h2>
          <div className="grid gap-3 md:grid-cols-2">
            <Field name="businessName" label="Business name" required />
            <Field name="phone" label="Phone" />
            <Field name="email" label="Email" type="email" />
            <Field name="abn" label="ABN" />
            <Field name="address" label="Address" className="md:col-span-2" />
            <Field name="suburb" label="Suburb" />
            <Field name="state" label="State" defaultValue="VIC" />
            <Field name="postcode" label="Postcode" />
          </div>
          <label className="flex items-center gap-2 text-sm">
            <input type="checkbox" name="gst" defaultChecked className="size-4" />
            GST registered (Australia, 10%)
          </label>
        </section>
        <section className="space-y-3 rounded-xl border border-border p-5">
          <h2 className="font-medium">Defaults</h2>
          <div className="grid gap-3 md:grid-cols-2">
            <Field name="labour" label="Default labour rate (AUD / hour)" defaultValue="110.00" />
            <Field name="diagnostic" label="Diagnostic fee (AUD)" defaultValue="89.00" />
          </div>
          <p className="text-xs text-muted-foreground">
            Repair statuses, warranty periods and ticket prefixes are created automatically and can be edited after setup.
          </p>
        </section>
        <section className="space-y-3 rounded-xl border border-border p-5">
          <h2 className="font-medium">Owner / Admin account</h2>
          <div className="grid gap-3 md:grid-cols-2">
            <Field name="ownerName" label="Full name" required />
            <Field name="ownerEmail" label="Email" type="email" required />
            <Field name="ownerPassword" label="Password" type="password" required className="md:col-span-2" />
          </div>
          <p className="text-xs text-muted-foreground">
            2FA can be required for owners, managers or remote access from Settings → Security after you sign in.
          </p>
        </section>
        <Button type="submit" size="lg">
          Complete setup
        </Button>
      </form>
    </div>
  );
}

function Field({
  name,
  label,
  type = "text",
  required,
  defaultValue,
  className,
}: {
  name: string;
  label: string;
  type?: string;
  required?: boolean;
  defaultValue?: string;
  className?: string;
}) {
  return (
    <div className={className}>
      <Label htmlFor={name}>{label}</Label>
      <Input id={name} name={name} type={type} required={required} defaultValue={defaultValue} className="mt-1" />
    </div>
  );
}
