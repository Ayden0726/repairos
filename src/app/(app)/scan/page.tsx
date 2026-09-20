import { ScanBox } from "@/components/tickets/scan-box";

export default function ScanPage() {
  return (
    <div className="mx-auto max-w-lg space-y-4">
      <h1 className="text-2xl font-semibold">Scan label</h1>
      <p className="text-sm text-muted-foreground">
        USB barcode scanners type the ticket number then Enter. On a phone you can also use the camera.
      </p>
      <ScanBox />
    </div>
  );
}
