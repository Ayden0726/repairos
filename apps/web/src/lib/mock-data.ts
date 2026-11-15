/** In-memory sample data for web testing. Swap for API later. */

export type Job = {
  id: string;
  ticket: string;
  customer: string;
  device: string;
  status: "In progress" | "Waiting parts" | "Ready" | "Quoted" | "Done";
  due: string;
};

export type Quote = {
  id: string;
  number: string;
  customer: string;
  status: "Draft" | "Sent" | "Accepted";
  total: number;
  lines: { description: string; qty: number; price: number }[];
};

export type Part = {
  id: string;
  sku: string;
  name: string;
  onHand: number;
  cost: number;
  sell: number;
};

export type Service = {
  id: string;
  name: string;
  device: string;
  price: number;
  onWebsite: boolean;
  active: boolean;
};

export type Booking = {
  id: string;
  day: string;
  time: string;
  label: string;
};

export type StaffUser = {
  id: string;
  name: string;
  role: string;
  status: "Active" | "Disabled";
};

export const jobs: Job[] = [
  {
    id: "1",
    ticket: "R-2401",
    customer: "Sam Lee",
    device: "iPhone 13",
    status: "In progress",
    due: "Today",
  },
  {
    id: "2",
    ticket: "R-2400",
    customer: "J. Nguyen",
    device: "Dell XPS",
    status: "Waiting parts",
    due: "Tomorrow",
  },
  {
    id: "3",
    ticket: "R-2398",
    customer: "A. Chen",
    device: "Galaxy S23",
    status: "Ready",
    due: "Today",
  },
  {
    id: "4",
    ticket: "R-2395",
    customer: "Walk-in",
    device: "PS5",
    status: "Quoted",
    due: "Fri",
  },
];

export const quotes: Quote[] = [
  {
    id: "q1",
    number: "Q-1042",
    customer: "Sam Lee",
    status: "Sent",
    total: 281,
    lines: [
      { description: "Screen replacement (Phone)", qty: 1, price: 189 },
      { description: "OLED assembly (stock)", qty: 1, price: 92 },
    ],
  },
  {
    id: "q2",
    number: "Q-1041",
    customer: "Walk-in",
    status: "Draft",
    total: 95,
    lines: [{ description: "Diagnostic fee", qty: 1, price: 49 }],
  },
  {
    id: "q3",
    number: "Q-1040",
    customer: "J. Nguyen",
    status: "Accepted",
    total: 420,
    lines: [{ description: "SSD upgrade + clone", qty: 1, price: 149 }],
  },
];

export const parts: Part[] = [
  {
    id: "p1",
    sku: "SCR-IP13",
    name: "iPhone 13 OLED",
    onHand: 2,
    cost: 78,
    sell: 92,
  },
  {
    id: "p2",
    sku: "BAT-IP12",
    name: "iPhone 12 battery",
    onHand: 8,
    cost: 18,
    sell: 35,
  },
  {
    id: "p3",
    sku: "SSD-1TB",
    name: "1TB NVMe",
    onHand: 3,
    cost: 89,
    sell: 129,
  },
  {
    id: "p4",
    sku: "THM-PS5",
    name: "PS5 thermal paste kit",
    onHand: 1,
    cost: 12,
    sell: 45,
  },
];

export const services: Service[] = [
  {
    id: "s1",
    name: "Screen replacement",
    device: "Phone",
    price: 189,
    onWebsite: true,
    active: true,
  },
  {
    id: "s2",
    name: "Battery replacement",
    device: "Phone",
    price: 99,
    onWebsite: true,
    active: true,
  },
  {
    id: "s3",
    name: "SSD upgrade + clone",
    device: "Laptop",
    price: 149,
    onWebsite: true,
    active: true,
  },
  {
    id: "s4",
    name: "Diagnostic fee",
    device: "Any",
    price: 49,
    onWebsite: true,
    active: true,
  },
  {
    id: "s5",
    name: "Console clean & paste",
    device: "Other",
    price: 120,
    onWebsite: false,
    active: true,
  },
];

export const bookings: Booking[] = [
  { id: "b1", day: "Thu 13", time: "9:30", label: "Sam — screen" },
  { id: "b2", day: "Thu 13", time: "14:00", label: "Walk-in diag" },
  { id: "b3", day: "Fri 14", time: "10:00", label: "Nguyen — SSD" },
  { id: "b4", day: "Fri 14", time: "15:30", label: "Battery" },
  { id: "b5", day: "Sat 15", time: "11:00", label: "Console clean" },
];

export const staff: StaffUser[] = [
  { id: "u1", name: "Maya Owner", role: "Owner", status: "Active" },
  { id: "u2", name: "Alex Tech", role: "Technician", status: "Active" },
  { id: "u3", name: "Front Desk", role: "Front desk", status: "Active" },
];

export const discountPresets = [
  { id: "d1", label: "5%", kind: "percent" as const, value: 5 },
  { id: "d2", label: "10%", kind: "percent" as const, value: 10 },
  { id: "d3", label: "$10 off", kind: "fixed" as const, value: 10 },
];
