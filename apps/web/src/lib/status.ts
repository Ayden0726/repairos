import { TicketStatus } from "@prisma/client";

export const TICKET_STATUSES: {
  value: TicketStatus;
  label: string;
}[] = [
  { value: "received", label: "Received" },
  { value: "diagnosing", label: "Diagnosing" },
  { value: "awaiting_approval", label: "Awaiting Approval" },
  { value: "awaiting_parts", label: "Awaiting Parts" },
  { value: "in_progress", label: "In Progress" },
  { value: "ready_for_collection", label: "Ready for Collection" },
  { value: "collected", label: "Collected" },
  { value: "cancelled", label: "Cancelled" },
];

export function statusLabel(status: TicketStatus): string {
  return TICKET_STATUSES.find((s) => s.value === status)?.label ?? status;
}

export function statusTone(
  status: TicketStatus,
): "default" | "secondary" | "success" | "warning" | "outline" {
  switch (status) {
    case "ready_for_collection":
    case "collected":
      return "success";
    case "awaiting_parts":
    case "awaiting_approval":
      return "warning";
    case "cancelled":
      return "outline";
    default:
      return "secondary";
  }
}
