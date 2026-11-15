/**
 * Milestone 1 module registry — later milestones add invoices, inventory, Square, etc.
 */
export type AppModuleId =
  | "today"
  | "tickets"
  | "customers"
  | "intake";

export type AppModule = {
  id: AppModuleId;
  label: string;
  href: string;
  description: string;
};

export const APP_MODULES: AppModule[] = [
  {
    id: "today",
    label: "Today",
    href: "/today",
    description: "Open work snapshot",
  },
  {
    id: "tickets",
    label: "Repairs",
    href: "/tickets",
    description: "Searchable ticket list",
  },
  {
    id: "customers",
    label: "Customers",
    href: "/customers",
    description: "Customers and devices",
  },
  {
    id: "intake",
    label: "New repair",
    href: "/tickets/new",
    description: "Repair intake",
  },
];
