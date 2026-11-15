import { z } from "zod";

export const passwordLoginSchema = z.object({
  mode: z.literal("password"),
  email: z.string().email().max(200),
  password: z.string().min(8).max(200),
});

export const pinLoginSchema = z.object({
  mode: z.literal("pin"),
  pin: z.string().regex(/^\d{4,8}$/, "PIN must be 4–8 digits"),
});

export const loginSchema = z.discriminatedUnion("mode", [
  passwordLoginSchema,
  pinLoginSchema,
]);

export const customerSchema = z.object({
  firstName: z.string().trim().min(1).max(100),
  lastName: z.string().trim().max(100).optional().default(""),
  phone: z.string().trim().min(6).max(40),
  email: z
    .string()
    .trim()
    .email()
    .max(200)
    .optional()
    .or(z.literal(""))
    .transform((v) => (v ? v : undefined)),
  notes: z.string().trim().max(2000).optional(),
});

export const deviceSchema = z.object({
  customerId: z.string().uuid(),
  category: z.enum(["phone", "tablet", "computer", "other"]),
  brand: z.string().trim().min(1).max(100),
  model: z.string().trim().min(1).max(100),
  colour: z.string().trim().max(80).optional(),
  storage: z.string().trim().max(40).optional(),
  imei: z.string().trim().max(32).optional(),
  serial: z.string().trim().max(80).optional(),
  notes: z.string().trim().max(2000).optional(),
});

export const ticketIntakeSchema = z.object({
  customerId: z.string().uuid(),
  deviceId: z.string().uuid().optional(),
  deviceCategory: z.enum(["phone", "tablet", "computer", "other"]).optional(),
  deviceBrand: z.string().trim().max(100).optional(),
  deviceModel: z.string().trim().max(100).optional(),
  deviceImei: z.string().trim().max(32).optional(),
  deviceSerial: z.string().trim().max(80).optional(),
  reportedFault: z.string().trim().min(3).max(4000),
  conditionPowersOn: z.boolean().nullable().optional(),
  conditionCracks: z.boolean().nullable().optional(),
  conditionScratches: z.boolean().nullable().optional(),
  conditionLiquid: z.boolean().nullable().optional(),
  conditionNotes: z.string().trim().max(2000).optional(),
  accessoryCharger: z.boolean().optional().default(false),
  accessorySim: z.boolean().optional().default(false),
  accessoryCase: z.boolean().optional().default(false),
  accessoryOther: z.string().trim().max(500).optional(),
  labourFeeAud: z.string().trim().min(1),
  estimateAud: z.string().trim().optional(),
});

export const noteSchema = z.object({
  body: z.string().trim().min(1).max(4000),
});

export const statusChangeSchema = z.object({
  status: z.enum([
    "received",
    "diagnosing",
    "awaiting_approval",
    "awaiting_parts",
    "in_progress",
    "ready_for_collection",
    "collected",
    "cancelled",
  ]),
  comment: z.string().trim().max(1000).optional(),
});
