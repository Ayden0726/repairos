"use client";

import { Button } from "@/components/ui/button";

export function LabelPrinter({ ticketId, ticketNumber }: { ticketId: string; ticketNumber: string }) {
  return (
    <Button
      type="button"
      variant="outline"
      onClick={() => window.open(`/api/tickets/${ticketId}/label`, "_blank")}
    >
      Print label {ticketNumber}
    </Button>
  );
}
