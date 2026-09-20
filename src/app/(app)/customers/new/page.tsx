import { customerAction } from "@/app/actions";
import { requirePermission } from "@/server/actor";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";

export default async function NewCustomerPage() {
  await requirePermission("customers.manage");
  return (
    <div className="mx-auto max-w-xl space-y-4">
      <h1 className="text-2xl font-semibold">New customer</h1>
      <form action={customerAction} className="space-y-3 rounded-xl border border-border p-5">
        <Label>Type</Label>
        <select name="type" className="h-9 w-full rounded-lg border border-input bg-transparent px-2 text-sm">
          <option value="INDIVIDUAL">Individual</option>
          <option value="BUSINESS">Business</option>
        </select>
        <div className="grid gap-3 md:grid-cols-2">
          <div>
            <Label htmlFor="firstName">First name</Label>
            <Input id="firstName" name="firstName" />
          </div>
          <div>
            <Label htmlFor="lastName">Last name</Label>
            <Input id="lastName" name="lastName" />
          </div>
        </div>
        <div>
          <Label htmlFor="companyName">Company</Label>
          <Input id="companyName" name="companyName" />
        </div>
        <div>
          <Label htmlFor="phone">Phone</Label>
          <Input id="phone" name="phone" />
        </div>
        <div>
          <Label htmlFor="email">Email</Label>
          <Input id="email" name="email" type="email" />
        </div>
        <div className="grid gap-3 md:grid-cols-3">
          <div>
            <Label htmlFor="suburb">Suburb</Label>
            <Input id="suburb" name="suburb" />
          </div>
          <div>
            <Label htmlFor="state">State</Label>
            <Input id="state" name="state" defaultValue="VIC" />
          </div>
          <div>
            <Label htmlFor="postcode">Postcode</Label>
            <Input id="postcode" name="postcode" />
          </div>
        </div>
        <div>
          <Label htmlFor="notes">Notes</Label>
          <Textarea id="notes" name="notes" />
        </div>
        <Button type="submit">Save customer</Button>
      </form>
    </div>
  );
}
