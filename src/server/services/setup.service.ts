import { ALL_PERMISSION_KEYS, ROLE_PERMISSIONS } from "../permissions";
import { prisma } from "../db";
import { hashPassword, validatePasswordStrength } from "../auth/password";
import { setSetting } from "../config/settings";
import { ValidationError } from "../errors";

export const DEFAULT_STATUSES = [
  { key: "checked_in", name: "Checked In", colour: "#64748b", isActive: true, sortOrder: 10 },
  { key: "awaiting_diagnosis", name: "Awaiting Diagnosis", colour: "#0ea5e9", isActive: true, sortOrder: 20 },
  { key: "diagnosing", name: "Diagnosing", colour: "#2563eb", isActive: true, sortOrder: 30 },
  { key: "waiting_approval", name: "Waiting for Customer Approval", colour: "#d97706", isActive: true, sortOrder: 40 },
  { key: "waiting_parts", name: "Waiting for Parts", colour: "#c2410c", isActive: true, countsAsWaitingForParts: true, sortOrder: 50 },
  { key: "repair_progress", name: "Repair in Progress", colour: "#7c3aed", isActive: true, sortOrder: 60 },
  { key: "testing", name: "Testing / Quality Check", colour: "#0f766e", isActive: true, sortOrder: 70 },
  { key: "ready_pickup", name: "Ready for Pickup", colour: "#15803d", isActive: true, sortOrder: 80 },
  { key: "completed", name: "Completed", colour: "#166534", isActive: false, isCompleted: true, sortOrder: 90 },
  { key: "cancelled", name: "Cancelled", colour: "#9f1239", isActive: false, isCancelled: true, sortOrder: 100 },
];

export const DEFAULT_TYPES = [
  { key: "phone", name: "Phone Repair", prefix: "REP", colour: "#2563eb", sortOrder: 10 },
  { key: "tablet", name: "Tablet Repair", prefix: "TAB", colour: "#4f46e5", sortOrder: 20 },
  { key: "laptop", name: "Laptop Repair", prefix: "LPT", colour: "#0f766e", sortOrder: 30 },
  { key: "desktop", name: "Desktop Repair", prefix: "DSK", colour: "#0369a1", sortOrder: 40 },
  { key: "custom_pc", name: "Custom PC Build", prefix: "PCB", colour: "#7c3aed", sortOrder: 50 },
  { key: "console", name: "Console Repair", prefix: "CON", colour: "#db2777", sortOrder: 60 },
  { key: "it_support", name: "IT Support", prefix: "ITS", colour: "#0e7490", sortOrder: 70 },
  { key: "board", name: "Board-Level Repair", prefix: "PCB", colour: "#b45309", sortOrder: 80 },
  { key: "microsoldering", name: "Microsoldering", prefix: "MSD", colour: "#c2410c", sortOrder: 90 },
  { key: "diagnostic", name: "Diagnostic", prefix: "DIA", colour: "#475569", sortOrder: 100 },
  { key: "warranty", name: "Warranty Return", prefix: "WAR", colour: "#be123c", sortOrder: 110 },
  { key: "refurb", name: "Refurbishment", prefix: "REF", colour: "#3f6212", sortOrder: 120 },
];

export const DEFAULT_PRIORITIES = [
  { key: "low", name: "Low", colour: "#64748b", sortOrder: 10 },
  { key: "normal", name: "Normal", colour: "#2563eb", isDefault: true, slaResponseMinutes: 240, slaCompletionMinutes: 2880, sortOrder: 20 },
  { key: "high", name: "High", colour: "#d97706", slaResponseMinutes: 60, slaCompletionMinutes: 1440, sortOrder: 30 },
  { key: "urgent", name: "Urgent", colour: "#e11d48", slaResponseMinutes: 15, slaCompletionMinutes: 480, sortOrder: 40 },
];

export const DEFAULT_LABOUR = [
  { key: "standard", name: "Standard repair", hourlyRate: "110.00", costRate: "45.00", isDefault: true },
  { key: "phone", name: "Phone repair", hourlyRate: "99.00", costRate: "40.00" },
  { key: "computer", name: "Computer repair", hourlyRate: "120.00", costRate: "50.00" },
  { key: "advanced", name: "Advanced diagnostics", hourlyRate: "150.00", costRate: "60.00" },
  { key: "board", name: "Board repair", hourlyRate: "180.00", costRate: "70.00" },
  { key: "micro", name: "Microsoldering", hourlyRate: "220.00", costRate: "80.00" },
  { key: "pc_assembly", name: "Custom PC assembly", hourlyRate: "95.00", costRate: "40.00" },
  { key: "business", name: "Business support", hourlyRate: "160.00", costRate: "70.00" },
  { key: "onsite", name: "On-site support", hourlyRate: "180.00", costRate: "75.00" },
];

export async function ensureFoundation() {
  for (const [key, name, description] of [
    ["owner", "Owner / Admin", "Full access to the shop"],
    ["admin", "Admin", "Full administrative access"],
    ["manager", "Manager", "Operations and limited administration"],
    ["technician", "Technician", "Workshop and assigned jobs"],
    ["front_desk", "Front Desk", "Intake, customers and payments"],
  ] as const) {
    const role = await prisma.role.upsert({
      where: { key },
      update: { name, description, isSystem: true },
      create: { key, name, description, isSystem: true },
    });
    const perms = key === "owner" || key === "admin" ? ALL_PERMISSION_KEYS : ROLE_PERMISSIONS[key];
    await prisma.rolePermission.deleteMany({ where: { roleId: role.id } });
    await prisma.rolePermission.createMany({
      data: perms.map((permission) => ({ roleId: role.id, permission })),
    });
  }

  for (const status of DEFAULT_STATUSES) {
    await prisma.ticketStatus.upsert({
      where: { key: status.key },
      update: { name: status.name, colour: status.colour, sortOrder: status.sortOrder },
      create: { ...status, isSystem: true },
    });
  }
  for (const type of DEFAULT_TYPES) {
    await prisma.ticketType.upsert({
      where: { key: type.key },
      update: { name: type.name, prefix: type.prefix, colour: type.colour },
      create: { ...type, isSystem: true },
    });
  }
  for (const priority of DEFAULT_PRIORITIES) {
    await prisma.ticketPriority.upsert({
      where: { key: priority.key },
      update: { name: priority.name, colour: priority.colour },
      create: priority,
    });
  }
  for (const rate of DEFAULT_LABOUR) {
    await prisma.labourRate.upsert({
      where: { key: rate.key },
      update: { name: rate.name, hourlyRate: rate.hourlyRate, costRate: rate.costRate },
      create: rate,
    });
  }
  await prisma.pricingGroup.upsert({
    where: { id: "seed-retail" },
    update: {},
    create: { id: "seed-retail", name: "Retail", isDefault: true },
  }).catch(async () => {
    const existing = await prisma.pricingGroup.findFirst({ where: { isDefault: true } });
    if (!existing) {
      await prisma.pricingGroup.create({ data: { name: "Retail", isDefault: true } });
    }
  });

  const methods = [
    { key: "cash", name: "Cash", provider: "manual" },
    { key: "bank", name: "Bank Transfer", provider: "manual" },
    { key: "square", name: "Square", provider: "square" },
    { key: "manual_card", name: "Manual Card Entry Record", provider: "manual" },
    { key: "other", name: "Other", provider: "manual" },
  ];
  for (const method of methods) {
    await prisma.paymentMethod.upsert({
      where: { key: method.key },
      update: { name: method.name },
      create: method,
    });
  }

  const sms = [
    { key: "checked_in", name: "Device checked in", triggerStatusKey: "checked_in", body: "Hi {{customer}}, {{business}} has checked in {{ticket}}. We'll keep you updated." },
    { key: "awaiting_approval", name: "Awaiting approval", triggerStatusKey: "waiting_approval", body: "Hi {{customer}}, we have an update on {{ticket}}. Please call us to approve the repair." },
    { key: "waiting_parts", name: "Waiting for parts", triggerStatusKey: "waiting_parts", body: "Hi {{customer}}, {{ticket}} is waiting on parts. We'll SMS you when work continues." },
    { key: "ready_pickup", name: "Ready for pickup", triggerStatusKey: "ready_pickup", body: "Hi {{customer}}, {{ticket}} is ready for pickup at {{business}}." },
    { key: "completed", name: "Completed", triggerStatusKey: "completed", body: "Hi {{customer}}, thanks for choosing {{business}}. {{ticket}} is complete." },
  ];
  for (const tpl of sms) {
    await prisma.smsTemplate.upsert({ where: { key: tpl.key }, update: {}, create: tpl });
  }

  const bookings = [
    { key: "dropoff", name: "Repair drop-off", colour: "#2563eb", durationMin: 15 },
    { key: "diagnostic", name: "Diagnostic appointment", colour: "#0f766e", durationMin: 45 },
    { key: "pc_consult", name: "Custom PC consultation", colour: "#7c3aed", durationMin: 45 },
    { key: "onsite", name: "On-site IT visit", colour: "#c2410c", durationMin: 120 },
  ];
  for (const type of bookings) {
    await prisma.bookingType.upsert({ where: { key: type.key }, update: {}, create: type });
  }

  const kb = [
    { name: "Repair guides", slug: "repair-guides" },
    { name: "Workshop notes", slug: "workshop-notes" },
    { name: "Troubleshooting", slug: "troubleshooting" },
    { name: "Common faults", slug: "common-faults" },
    { name: "Service manuals", slug: "service-manuals" },
    { name: "Board repair notes", slug: "board-repair" },
  ];
  for (const cat of kb) {
    await prisma.knowledgeCategory.upsert({ where: { slug: cat.slug }, update: {}, create: cat });
  }

  await prisma.intakeAgreementTemplate.createMany({
    data: [
      {
        name: "Standard repair authorisation",
        version: 1,
        isActive: true,
        body: "I authorise Riverside Tech Repair to inspect and, if approved, repair the device described on this job. I understand data loss is possible, existing damage is recorded at check-in, abandoned devices may be recycled after 90 days, and warranty covers the repaired fault only.",
        clauses: [
          "Existing damage acknowledgement",
          "Data loss risk",
          "Diagnostic authorisation",
          "Repair authorisation",
          "Abandoned device policy",
          "Warranty limitations",
          "Privacy notice acknowledgement",
        ],
      },
    ],
    skipDuplicates: true,
  });

  await prisma.privacyNoticeTemplate.createMany({
    data: [
      {
        name: "Customer privacy notice",
        version: 1,
        isActive: true,
        body: "We collect contact and device details to perform repairs, process payments and contact you about your job. We do not sell personal information. You may ask us to correct or export your details. The business owner is responsible for configuring retention in line with Australian privacy and tax record requirements.",
      },
    ],
    skipDuplicates: true,
  });
}

export async function completeSetup(input: {
  business: {
    name: string;
    phone?: string;
    email?: string;
    addressLine1?: string;
    suburb?: string;
    state?: string;
    postcode?: string;
    abn?: string;
  };
  gstRegistered: boolean;
  labourDefault: string;
  diagnosticFee: string;
  owner: { name: string; email: string; password: string };
  skipIntegrations?: boolean;
}) {
  const problem = validatePasswordStrength(input.owner.password);
  if (problem) throw new ValidationError(problem);
  await ensureFoundation();

  const ownerRole = await prisma.role.findUniqueOrThrow({ where: { key: "owner" } });
  const owner = await prisma.user.create({
    data: {
      name: input.owner.name,
      email: input.owner.email.toLowerCase(),
      passwordHash: await hashPassword(input.owner.password),
      roleId: ownerRole.id,
      isOwner: true,
      preferences: { create: { defaultTicketView: "board" } },
    },
  });

  await setSetting("business.profile", input.business, owner.id);
  await setSetting("gst", { registered: input.gstRegistered, rate: 0.1, inclusiveDefault: true }, owner.id);
  await setSetting("finance.diagnosticFee", { amount: input.diagnosticFee, waiveIfProceeds: true, creditToInvoice: true }, owner.id);
  await setSetting("finance.defaultLabourRate", input.labourDefault, owner.id);
  await setSetting("warranty.defaultDays", 90, owner.id);
  await setSetting("security.twoFactor", {
    requireOwners: true,
    requireAdmins: true,
    requireManagers: false,
    requireRemote: true,
    requireAll: false,
  }, owner.id);
  await setSetting("backup", { directory: process.env.BACKUP_DIR ?? "./data/backups", schedule: "0 2 * * *", retainDays: 30 }, owner.id);
  await setSetting("integrations.sms", { enabled: false, provider: "console" }, owner.id);
  await setSetting("integrations.email", { enabled: false }, owner.id);
  await setSetting("integrations.square", { enabled: false }, owner.id);
  await setSetting("integrations.ollama", { enabled: false, baseUrl: "http://127.0.0.1:11434", model: "llama3.1" }, owner.id);
  await setSetting("setup.completed", true, owner.id);
  return owner;
}
