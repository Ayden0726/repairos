import { PrismaClient } from "@prisma/client";
import { hashPassword } from "../src/server/auth/password";
import { ensureFoundation } from "../src/server/services/setup.service";
import { setSetting } from "../src/server/config/settings";
import { encryptString } from "../src/server/crypto";

const prisma = new PrismaClient();

async function main() {
  await ensureFoundation();

  const retail =
    (await prisma.pricingGroup.findFirst({ where: { isDefault: true } })) ??
    (await prisma.pricingGroup.create({ data: { name: "Retail", isDefault: true } }));
  const business = await prisma.pricingGroup.upsert({
    where: { id: "pg-business" },
    update: {},
    create: { id: "pg-business", name: "Business", labourMultiplier: "1.1", markupPercent: "8" },
  });
  await prisma.pricingGroup.upsert({
    where: { id: "pg-repeat" },
    update: {},
    create: { id: "pg-repeat", name: "Repeat Customer", discountPercent: "5" },
  });
  await prisma.pricingGroup.upsert({
    where: { id: "pg-vip" },
    update: {},
    create: { id: "pg-vip", name: "VIP", discountPercent: "10" },
  });
  await prisma.pricingGroup.upsert({
    where: { id: "pg-friends" },
    update: {},
    create: { id: "pg-friends", name: "Friends / Family", discountPercent: "20", labourMultiplier: "0.85" },
  });

  const ownerRole = await prisma.role.findUniqueOrThrow({ where: { key: "owner" } });
  const managerRole = await prisma.role.findUniqueOrThrow({ where: { key: "manager" } });
  const techRole = await prisma.role.findUniqueOrThrow({ where: { key: "technician" } });
  const deskRole = await prisma.role.findUniqueOrThrow({ where: { key: "front_desk" } });
  const password = await hashPassword("Riverside!2026");

  const maya = await prisma.user.upsert({
    where: { email: "maya@riversidetech.com.au" },
    update: {},
    create: {
      email: "maya@riversidetech.com.au",
      name: "Maya Krishnan",
      phone: "0412 880 114",
      passwordHash: password,
      roleId: ownerRole.id,
      isOwner: true,
      preferences: { create: { theme: "system", defaultTicketView: "board" } },
    },
  });
  const tom = await prisma.user.upsert({
    where: { email: "tom@riversidetech.com.au" },
    update: {},
    create: {
      email: "tom@riversidetech.com.au",
      name: "Tom Walsh",
      phone: "0403 219 776",
      passwordHash: password,
      roleId: managerRole.id,
      preferences: { create: { defaultTicketView: "table" } },
    },
  });
  const priya = await prisma.user.upsert({
    where: { email: "priya@riversidetech.com.au" },
    update: {},
    create: {
      email: "priya@riversidetech.com.au",
      name: "Priya Nair",
      phone: "0433 102 908",
      passwordHash: password,
      roleId: techRole.id,
      preferences: { create: { defaultTicketView: "board" } },
    },
  });
  const jack = await prisma.user.upsert({
    where: { email: "jack@riversidetech.com.au" },
    update: {},
    create: {
      email: "jack@riversidetech.com.au",
      name: "Jack Nguyen",
      phone: "0499 221 430",
      passwordHash: password,
      roleId: techRole.id,
      preferences: { create: {} },
    },
  });
  const sophie = await prisma.user.upsert({
    where: { email: "sophie@riversidetech.com.au" },
    update: {},
    create: {
      email: "sophie@riversidetech.com.au",
      name: "Sophie Bennett",
      phone: "0422 671 553",
      passwordHash: password,
      roleId: deskRole.id,
      preferences: { create: { defaultTicketView: "table" } },
    },
  });

  await setSetting("business.profile", {
    name: "Riverside Tech Repair",
    addressLine1: "118 Flinders Lane",
    suburb: "Melbourne",
    state: "VIC",
    postcode: "3000",
    phone: "(03) 9018 4420",
    email: "hello@riversidetech.com.au",
    abn: "84 612 449 307",
  }, maya.id);
  await setSetting("gst", { registered: true, rate: 0.1, inclusiveDefault: true }, maya.id);
  await setSetting("finance.diagnosticFee", { amount: "89.00", waiveIfProceeds: true, creditToInvoice: true }, maya.id);
  await setSetting("setup.completed", true, maya.id);
  await setSetting("security.twoFactor", {
    requireOwners: false,
    requireAdmins: false,
    requireManagers: false,
    requireRemote: false,
    requireAll: false,
  }, maya.id);
  await setSetting("integrations.sms", { enabled: true, provider: "console" }, maya.id);
  await setSetting("integrations.email", { enabled: false }, maya.id);
  await setSetting("integrations.square", { enabled: false }, maya.id);
  await setSetting("integrations.ollama", { enabled: false, baseUrl: "http://127.0.0.1:11434", model: "llama3.1" }, maya.id);
  await setSetting("backup", { directory: process.env.BACKUP_DIR ?? "./data/backups", schedule: "0 2 * * *", retainDays: 30 }, maya.id);
  await setSetting("numbering", { ticketFormat: "{PREFIX}-{YEAR}-{SEQ:5}", invoicePrefix: "INV", quotePrefix: "QTE", poPrefix: "PO", pcPrefix: "PCB" }, maya.id);

  const apple = await prisma.manufacturer.upsert({ where: { name: "Apple" }, update: {}, create: { name: "Apple" } });
  const samsung = await prisma.manufacturer.upsert({ where: { name: "Samsung" }, update: {}, create: { name: "Samsung" } });
  const google = await prisma.manufacturer.upsert({ where: { name: "Google" }, update: {}, create: { name: "Google" } });
  const dell = await prisma.manufacturer.upsert({ where: { name: "Dell" }, update: {}, create: { name: "Dell" } });
  const lenovo = await prisma.manufacturer.upsert({ where: { name: "Lenovo" }, update: {}, create: { name: "Lenovo" } });
  const sony = await prisma.manufacturer.upsert({ where: { name: "Sony" }, update: {}, create: { name: "Sony" } });
  const amd = await prisma.manufacturer.upsert({ where: { name: "AMD" }, update: {}, create: { name: "AMD" } });
  const nvidia = await prisma.manufacturer.upsert({ where: { name: "NVIDIA" }, update: {}, create: { name: "NVIDIA" } });
  const asustek = await prisma.manufacturer.upsert({ where: { name: "ASUS" }, update: {}, create: { name: "ASUS" } });

  const iphoneFamily = await prisma.deviceFamily.create({ data: { manufacturerId: apple.id, name: "iPhone", deviceClass: "phone" } }).catch(async () =>
    prisma.deviceFamily.findFirstOrThrow({ where: { manufacturerId: apple.id, name: "iPhone" } }),
  );
  const macFamily = await prisma.deviceFamily.create({ data: { manufacturerId: apple.id, name: "MacBook", deviceClass: "laptop" } }).catch(async () =>
    prisma.deviceFamily.findFirstOrThrow({ where: { manufacturerId: apple.id, name: "MacBook" } }),
  );
  const galaxy = await prisma.deviceFamily.create({ data: { manufacturerId: samsung.id, name: "Galaxy S", deviceClass: "phone" } }).catch(async () =>
    prisma.deviceFamily.findFirstOrThrow({ where: { manufacturerId: samsung.id, name: "Galaxy S" } }),
  );
  const pixel = await prisma.deviceFamily.create({ data: { manufacturerId: google.id, name: "Pixel", deviceClass: "phone" } }).catch(async () =>
    prisma.deviceFamily.findFirstOrThrow({ where: { manufacturerId: google.id, name: "Pixel" } }),
  );
  const xps = await prisma.deviceFamily.create({ data: { manufacturerId: dell.id, name: "XPS", deviceClass: "laptop" } }).catch(async () =>
    prisma.deviceFamily.findFirstOrThrow({ where: { manufacturerId: dell.id, name: "XPS" } }),
  );
  const thinkpad = await prisma.deviceFamily.create({ data: { manufacturerId: lenovo.id, name: "ThinkPad", deviceClass: "laptop" } }).catch(async () =>
    prisma.deviceFamily.findFirstOrThrow({ where: { manufacturerId: lenovo.id, name: "ThinkPad" } }),
  );
  const ps5 = await prisma.deviceFamily.create({ data: { manufacturerId: sony.id, name: "PlayStation", deviceClass: "console" } }).catch(async () =>
    prisma.deviceFamily.findFirstOrThrow({ where: { manufacturerId: sony.id, name: "PlayStation" } }),
  );

  const iphone15 = await prisma.deviceModel.upsert({
    where: { id: "model-iphone15" },
    update: {},
    create: { id: "model-iphone15", familyId: iphoneFamily.id, name: "iPhone 15", year: 2023, metadata: { storage: ["128GB", "256GB"], colours: ["Black", "Blue", "Pink"] } },
  });
  const iphone15pro = await prisma.deviceModel.upsert({
    where: { id: "model-iphone15pro" },
    update: {},
    create: { id: "model-iphone15pro", familyId: iphoneFamily.id, name: "iPhone 15 Pro", year: 2023 },
  });
  const s24 = await prisma.deviceModel.upsert({
    where: { id: "model-s24" },
    update: {},
    create: { id: "model-s24", familyId: galaxy.id, name: "Galaxy S24", year: 2024 },
  });
  const pixel8 = await prisma.deviceModel.upsert({
    where: { id: "model-pixel8" },
    update: {},
    create: { id: "model-pixel8", familyId: pixel.id, name: "Pixel 8", year: 2023 },
  });
  const mbp14 = await prisma.deviceModel.upsert({
    where: { id: "model-mbp14" },
    update: {},
    create: { id: "model-mbp14", familyId: macFamily.id, name: "MacBook Pro 14 (M3)", year: 2023 },
  });
  const xps15 = await prisma.deviceModel.upsert({
    where: { id: "model-xps15" },
    update: {},
    create: { id: "model-xps15", familyId: xps.id, name: "XPS 15 9530", year: 2023, metadata: { cpu: "i7-13700H", ram: "32GB" } },
  });
  const t14 = await prisma.deviceModel.upsert({
    where: { id: "model-t14" },
    update: {},
    create: { id: "model-t14", familyId: thinkpad.id, name: "ThinkPad T14 Gen 4", year: 2023 },
  });
  const ps5slim = await prisma.deviceModel.upsert({
    where: { id: "model-ps5" },
    update: {},
    create: { id: "model-ps5", familyId: ps5.id, name: "PlayStation 5 Slim", year: 2023 },
  });

  const phoneTpl = await prisma.diagnosticTemplate.create({
    data: {
      name: "Phone pre-repair",
      deviceClass: "phone",
      phase: "pre",
      items: {
        create: ["Screen", "Touch", "Battery", "Charging", "Speakers", "Microphones", "Rear camera", "Front camera", "Wi-Fi", "Bluetooth", "Buttons", "Face ID / fingerprint"].map((label, i) => ({ label, sortOrder: i })),
      },
    },
  }).catch(async () => prisma.diagnosticTemplate.findFirstOrThrow({ where: { name: "Phone pre-repair" } }));
  const laptopTpl = await prisma.diagnosticTemplate.create({
    data: {
      name: "Laptop pre-repair",
      deviceClass: "laptop",
      phase: "pre",
      items: {
        create: ["Display", "Keyboard", "Trackpad", "Battery", "Charger", "SSD health", "RAM", "Wi-Fi", "Bluetooth", "USB ports", "Audio", "Temperatures", "Fans"].map((label, i) => ({ label, sortOrder: i })),
      },
    },
  }).catch(async () => prisma.diagnosticTemplate.findFirstOrThrow({ where: { name: "Laptop pre-repair" } }));

  const phoneType = await prisma.ticketType.findUniqueOrThrow({ where: { key: "phone" } });
  const laptopType = await prisma.ticketType.findUniqueOrThrow({ where: { key: "laptop" } });
  const pcType = await prisma.ticketType.findUniqueOrThrow({ where: { key: "custom_pc" } });
  const boardType = await prisma.ticketType.findUniqueOrThrow({ where: { key: "board" } });
  const consoleType = await prisma.ticketType.findUniqueOrThrow({ where: { key: "console" } });
  const refurbType = await prisma.ticketType.findUniqueOrThrow({ where: { key: "refurb" } });
  const statuses = Object.fromEntries((await prisma.ticketStatus.findMany()).map((s) => [s.key, s]));
  const priorities = Object.fromEntries((await prisma.ticketPriority.findMany()).map((s) => [s.key, s]));
  const labourPhone = await prisma.labourRate.findUniqueOrThrow({ where: { key: "phone" } });
  const labourPc = await prisma.labourRate.findUniqueOrThrow({ where: { key: "pc_assembly" } });

  await prisma.customFieldDefinition.createMany({
    data: [
      { entityType: "TICKET", ticketTypeId: phoneType.id, key: "imei", label: "IMEI", fieldType: "SERIAL", required: false, sortOrder: 10 },
      { entityType: "TICKET", ticketTypeId: phoneType.id, key: "colour", label: "Colour", fieldType: "TEXT", sortOrder: 20 },
      { entityType: "TICKET", ticketTypeId: phoneType.id, key: "storage", label: "Storage", fieldType: "DROPDOWN", options: ["64GB", "128GB", "256GB", "512GB", "1TB"], sortOrder: 30 },
      { entityType: "TICKET", ticketTypeId: phoneType.id, key: "battery_health", label: "Battery health %", fieldType: "NUMBER", sortOrder: 40 },
      { entityType: "TICKET", ticketTypeId: laptopType.id, key: "serial", label: "Serial", fieldType: "SERIAL", sortOrder: 10 },
      { entityType: "TICKET", ticketTypeId: laptopType.id, key: "cpu", label: "CPU", fieldType: "TEXT", sortOrder: 20 },
      { entityType: "TICKET", ticketTypeId: laptopType.id, key: "ram", label: "RAM", fieldType: "TEXT", sortOrder: 30 },
      { entityType: "TICKET", ticketTypeId: laptopType.id, key: "storage_spec", label: "Storage", fieldType: "TEXT", sortOrder: 40 },
      { entityType: "TICKET", ticketTypeId: laptopType.id, key: "os", label: "Operating system", fieldType: "TEXT", sortOrder: 50 },
      { entityType: "TICKET", ticketTypeId: laptopType.id, key: "charger", label: "Charger provided", fieldType: "CHECKBOX", sortOrder: 60 },
      { entityType: "TICKET", ticketTypeId: pcType.id, key: "budget", label: "Customer budget", fieldType: "CURRENCY", sortOrder: 10 },
      { entityType: "TICKET", ticketTypeId: pcType.id, key: "purpose", label: "Purpose", fieldType: "LONG_TEXT", sortOrder: 20 },
    ],
    skipDuplicates: true,
  });

  const mobilehub = await prisma.supplier.upsert({
    where: { id: "sup-mobilehub" },
    update: {},
    create: { id: "sup-mobilehub", name: "MobileHub Wholesale", contactName: "Dana Liu", phone: "03 9112 4400", email: "orders@mobilehub.example", website: "https://mobilehub.example", accountNumber: "RTR-4412" },
  });
  const pccity = await prisma.supplier.upsert({
    where: { id: "sup-pccity" },
    update: {},
    create: { id: "sup-pccity", name: "PC City Components", contactName: "Marcus Hale", phone: "02 8901 2208", email: "sales@pccity.example", accountNumber: "NSW-8821" },
  });
  const boardparts = await prisma.supplier.upsert({
    where: { id: "sup-board" },
    update: {},
    create: { id: "sup-board", name: "Microsol Supplies", contactName: "Elena Park", email: "parts@microsol.example" },
  });

  const screen = await prisma.inventoryItem.upsert({
    where: { sku: "SCR-IP15-OLED" },
    update: {},
    create: { sku: "SCR-IP15-OLED", barcode: "9310000000151", name: "iPhone 15 OLED screen (aftermarket)", category: "Phone screens", manufacturer: "GX", supplierId: mobilehub.id, onHand: 6, reserved: 1, cost: "145.00", salePrice: "329.00", minStock: 3, reorderQty: 5, warrantyDays: 180 },
  });
  const batt = await prisma.inventoryItem.upsert({
    where: { sku: "BAT-IP15" },
    update: {},
    create: { sku: "BAT-IP15", barcode: "9310000000152", name: "iPhone 15 battery", category: "Batteries", manufacturer: "Apple-compatible", supplierId: mobilehub.id, onHand: 11, cost: "28.00", salePrice: "149.00", minStock: 4, reorderQty: 8, warrantyDays: 180 },
  });
  const port = await prisma.inventoryItem.upsert({
    where: { sku: "CHG-S24" },
    update: {},
    create: { sku: "CHG-S24", name: "Galaxy S24 charging port board", category: "Charging ports", supplierId: mobilehub.id, onHand: 2, cost: "18.50", salePrice: "89.00", minStock: 2, reorderQty: 4 },
  });
  const keyboard = await prisma.inventoryItem.upsert({
    where: { sku: "KB-XPS15" },
    update: {},
    create: { sku: "KB-XPS15", name: "Dell XPS 15 keyboard (AU)", category: "Laptop keyboards", supplierId: pccity.id, onHand: 1, cost: "62.00", salePrice: "189.00", minStock: 1, reorderQty: 2 },
  });
  const paste = await prisma.inventoryItem.upsert({
    where: { sku: "THM-NT-H1" },
    update: {},
    create: { sku: "THM-NT-H1", name: "Noctua NT-H1 thermal paste", category: "Consumables", supplierId: pccity.id, onHand: 14, cost: "6.50", salePrice: "19.00", minStock: 4, reorderQty: 10 },
  });
  const ssd = await prisma.inventoryItem.upsert({
    where: { sku: "SSD-WD-1TB" },
    update: {},
    create: { kind: "PC_COMPONENT", sku: "SSD-WD-1TB", name: "WD Black SN850X 1TB", category: "Storage", pcCategory: "SSD", manufacturer: "Western Digital", supplierId: pccity.id, onHand: 4, cost: "128.00", salePrice: "179.00", specifications: { interface: "NVMe", form: "M.2 2280" }, minStock: 2 },
  });
  const ram = await prisma.inventoryItem.upsert({
    where: { sku: "RAM-GSKILL-32" },
    update: {},
    create: { kind: "PC_COMPONENT", sku: "RAM-GSKILL-32", name: "G.Skill Trident Z5 32GB DDR5-6000", category: "Memory", pcCategory: "RAM", manufacturer: "G.Skill", supplierId: pccity.id, onHand: 5, cost: "118.00", salePrice: "169.00", specifications: { type: "DDR5", speed: 6000 } },
  });
  const cpu = await prisma.inventoryItem.upsert({
    where: { sku: "CPU-R7-7800X3D" },
    update: {},
    create: { kind: "PC_COMPONENT", sku: "CPU-R7-7800X3D", name: "AMD Ryzen 7 7800X3D", category: "Processors", pcCategory: "CPU", manufacturer: "AMD", supplierId: pccity.id, onHand: 2, cost: "489.00", salePrice: "579.00", specifications: { socket: "AM5", tdp: 120 } },
  });
  const mb = await prisma.inventoryItem.upsert({
    where: { sku: "MB-B650-TOM" },
    update: {},
    create: { kind: "PC_COMPONENT", sku: "MB-B650-TOM", name: "ASUS TUF B650-PLUS WIFI", category: "Motherboards", pcCategory: "MOTHERBOARD", manufacturer: "ASUS", supplierId: pccity.id, onHand: 2, cost: "249.00", salePrice: "319.00", specifications: { socket: "AM5", ramType: "DDR5", formFactor: "ATX" } },
  });
  const gpu = await prisma.inventoryItem.upsert({
    where: { sku: "GPU-4070S" },
    update: {},
    create: { kind: "PC_COMPONENT", sku: "GPU-4070S", name: "GeForce RTX 4070 SUPER 12GB", category: "Graphics", pcCategory: "GPU", manufacturer: "NVIDIA", supplierId: pccity.id, onHand: 1, cost: "890.00", salePrice: "1099.00", specifications: { lengthMm: 304, tdp: 220 } },
  });
  const psu = await prisma.inventoryItem.upsert({
    where: { sku: "PSU-RM750" },
    update: {},
    create: { kind: "PC_COMPONENT", sku: "PSU-RM750", name: "Corsair RM750e 750W Gold", category: "Power", pcCategory: "PSU", manufacturer: "Corsair", onHand: 3, cost: "139.00", salePrice: "189.00", specifications: { wattage: 750 } },
  });
  const pcCase = await prisma.inventoryItem.upsert({
    where: { sku: "CASE-LAN400" },
    update: {},
    create: { kind: "PC_COMPONENT", sku: "CASE-LAN400", name: "Lian Li LANCOOL 216", category: "Cases", pcCategory: "CASE", manufacturer: "Lian Li", onHand: 2, cost: "119.00", salePrice: "159.00", specifications: { motherboard: "ATX/mATX", maxGpuMm: 392, maxCoolerMm: 180 } },
  });
  await prisma.serializedItem.createMany({
    data: [
      { inventoryItemId: gpu.id, serialNumber: "GPU-4070S-18821", status: "IN_STOCK", cost: "890.00" },
      { inventoryItemId: ssd.id, serialNumber: "WD-SN850X-44190", status: "IN_STOCK", cost: "128.00" },
    ],
    skipDuplicates: true,
  });
  await prisma.partCostHistory.createMany({
    data: [
      { itemId: screen.id, supplierId: mobilehub.id, unitCost: "152.00", recordedAt: new Date("2026-04-02") },
      { itemId: screen.id, supplierId: mobilehub.id, unitCost: "145.00", recordedAt: new Date("2026-08-18") },
      { itemId: gpu.id, supplierId: pccity.id, unitCost: "910.00", recordedAt: new Date("2026-03-11") },
      { itemId: gpu.id, supplierId: pccity.id, unitCost: "890.00", recordedAt: new Date("2026-07-22") },
    ],
  });

  await prisma.serviceCatalogue.create({
    data: {
      name: "iPhone screen replacement",
      jobTypeId: phoneType.id,
      defaultPrice: "329.00",
      expectedLabourMinutes: 45,
      labourRateId: labourPhone.id,
      diagnosticTemplateId: phoneTpl.id,
      warrantyDays: 180,
      typicalParts: { create: [{ itemId: screen.id, quantity: 1 }] },
    },
  }).catch(() => undefined);
  await prisma.serviceCatalogue.create({
    data: {
      name: "iPhone battery replacement",
      jobTypeId: phoneType.id,
      defaultPrice: "149.00",
      expectedLabourMinutes: 30,
      labourRateId: labourPhone.id,
      warrantyDays: 180,
      typicalParts: { create: [{ itemId: batt.id, quantity: 1 }] },
    },
  }).catch(() => undefined);
  await prisma.serviceCatalogue.create({
    data: {
      name: "Custom PC assembly",
      jobTypeId: pcType.id,
      defaultPrice: "220.00",
      expectedLabourMinutes: 180,
      labourRateId: labourPc.id,
      warrantyDays: 365,
    },
  }).catch(() => undefined);

  const luca = await prisma.customer.upsert({
    where: { id: "cust-luca" },
    update: {},
    create: { id: "cust-luca", type: "INDIVIDUAL", firstName: "Luca", lastName: "Moretti", displayName: "Luca Moretti", phone: "0418 332 901", email: "luca.moretti@example.com", suburb: "Fitzroy", state: "VIC", postcode: "3065", preferredContact: "SMS", pricingGroupId: retail.id },
  });
  const amelia = await prisma.customer.upsert({
    where: { id: "cust-amelia" },
    update: {},
    create: { id: "cust-amelia", type: "INDIVIDUAL", firstName: "Amelia", lastName: "Chen", displayName: "Amelia Chen", phone: "0401 776 220", email: "amelia.chen@example.com", suburb: "South Yarra", state: "VIC", postcode: "3141", pricingGroupId: retail.id },
  });
  const harriet = await prisma.customer.upsert({
    where: { id: "cust-harriet" },
    update: {},
    create: { id: "cust-harriet", type: "INDIVIDUAL", firstName: "Harriet", lastName: "Okafor", displayName: "Harriet Okafor", phone: "0432 118 664", email: "harriet.o@example.com", suburb: "Brunswick", state: "VIC", postcode: "3056", pricingGroupId: retail.id },
  });
  const nora = await prisma.customer.upsert({
    where: { id: "cust-nora" },
    update: {},
    create: { id: "cust-nora", type: "INDIVIDUAL", firstName: "Nora", lastName: "Patterson", displayName: "Nora Patterson", phone: "0477 901 332", suburb: "Richmond", state: "VIC", postcode: "3121", pricingGroupId: retail.id },
  });
  const studio = await prisma.customer.upsert({
    where: { id: "cust-studio" },
    update: {},
    create: {
      id: "cust-studio",
      type: "BUSINESS",
      companyName: "Lantern Studio",
      displayName: "Lantern Studio",
      phone: "03 9419 2201",
      email: "it@lanternstudio.com.au",
      billingEmail: "accounts@lanternstudio.com.au",
      addressLine1: "9 Peel Street",
      suburb: "Collingwood",
      state: "VIC",
      postcode: "3066",
      abn: "51 228 901 447",
      purchaseOrderRequired: true,
      pricingGroupId: business.id,
      contacts: {
        create: [
          { name: "Owen Fraser", role: "TECHNICAL", phone: "0412 009 441", email: "owen@lanternstudio.com.au" },
          { name: "Sasha Reed", role: "BILLING", email: "accounts@lanternstudio.com.au", isPrimary: true },
        ],
      },
    },
  });
  const bakery = await prisma.customer.upsert({
    where: { id: "cust-bakery" },
    update: {},
    create: { id: "cust-bakery", type: "BUSINESS", companyName: "Northside Bakehouse", displayName: "Northside Bakehouse", phone: "03 9380 1144", email: "hello@northsidebakehouse.example", suburb: "Northcote", state: "VIC", postcode: "3070", pricingGroupId: business.id },
  });

  const lucaPhone = await prisma.customerDevice.create({
    data: { id: "dev-luca-15", customerId: luca.id, deviceModelId: iphone15.id, manufacturer: "Apple", modelName: "iPhone 15", deviceType: "Phone", serial: "F2LXT4QH1A", imei: "356938111298441", colour: "Blue", storage: "256GB" },
  }).catch(async () => prisma.customerDevice.findUniqueOrThrow({ where: { id: "dev-luca-15" } }));
  const ameliaLaptop = await prisma.customerDevice.create({
    data: { id: "dev-amelia-xps", customerId: amelia.id, deviceModelId: xps15.id, manufacturer: "Dell", modelName: "XPS 15 9530", deviceType: "Laptop", serial: "6Y2K4D3", colour: "Platinum" },
  }).catch(async () => prisma.customerDevice.findUniqueOrThrow({ where: { id: "dev-amelia-xps" } }));

  const t1 = await prisma.ticket.upsert({
    where: { ticketNumber: "REP-2026-00112" },
    update: {},
    create: {
      ticketNumber: "REP-2026-00112",
      customerId: luca.id,
      customerDeviceId: lucaPhone.id,
      deviceModelId: iphone15.id,
      typeId: phoneType.id,
      statusId: statuses.repair_progress.id,
      priorityId: priorities.high.id,
      reportedIssue: "Cracked front glass after a drop on bluestone. Touch still works, no Face ID issues reported.",
      createdById: sophie.id,
      assignedToId: priya.id,
      estimatedPrice: "329.00",
      dueAt: new Date(Date.now() + 86400000),
      labourRateId: labourPhone.id,
      estimatedLabourMinutes: 45,
      waitingForParts: false,
    },
  });
  await prisma.ticketPart.create({
    data: { ticketId: t1.id, itemId: screen.id, name: screen.name, sku: screen.sku, quantity: 1, status: "RESERVED", unitCost: screen.cost, unitPrice: screen.salePrice },
  }).catch(() => undefined);
  await prisma.stockReservation.create({
    data: { itemId: screen.id, quantity: 1, target: "TICKET", ticketId: t1.id },
  }).catch(() => undefined);
  await prisma.deviceCredential.create({
    data: { ticketId: t1.id, ciphertext: encryptString("2580"), label: "Passcode" },
  }).catch(() => undefined);
  await prisma.deviceCondition.create({
    data: { ticketId: t1.id, phase: "checkin", crackedGlass: true, scratches: true, notes: "Spider crack from top-left. Rear unmarked." , recordedById: sophie.id },
  }).catch(() => undefined);
  await prisma.ticketNote.create({
    data: { ticketId: t1.id, authorId: priya.id, isInternal: true, body: "GX aftermarket OLED in stock. Customer declined genuine due to price. Advised slightly warmer whites." },
  }).catch(() => undefined);
  await prisma.ticketEvent.createMany({
    data: [
      { ticketId: t1.id, actorId: sophie.id, type: "created", summary: "Ticket REP-2026-00112 created", visibility: "CUSTOMER" },
      { ticketId: t1.id, actorId: priya.id, type: "status", summary: "Status changed to Repair in Progress", visibility: "CUSTOMER" },
    ],
    skipDuplicates: true,
  });

  const t2 = await prisma.ticket.upsert({
    where: { ticketNumber: "LPT-2026-00048" },
    update: {},
    create: {
      ticketNumber: "LPT-2026-00048",
      customerId: amelia.id,
      customerDeviceId: ameliaLaptop.id,
      deviceModelId: xps15.id,
      typeId: laptopType.id,
      statusId: statuses.waiting_parts.id,
      priorityId: priorities.normal.id,
      reportedIssue: "Several keys on the left side not registering. Spilled oat latte two days ago, powered down immediately.",
      createdById: sophie.id,
      assignedToId: jack.id,
      estimatedPrice: "249.00",
      dueAt: new Date(Date.now() + 3 * 86400000),
      waitingForParts: true,
    },
  });
  await prisma.ticketPart.create({
    data: { ticketId: t2.id, itemId: keyboard.id, name: keyboard.name, sku: keyboard.sku, quantity: 1, status: "ORDERED", unitCost: keyboard.cost, unitPrice: keyboard.salePrice },
  }).catch(() => undefined);

  await prisma.ticket.upsert({
    where: { ticketNumber: "CON-2026-00019" },
    update: {},
    create: {
      ticketNumber: "CON-2026-00019",
      customerId: nora.id,
      deviceModelId: ps5slim.id,
      typeId: consoleType.id,
      statusId: statuses.diagnosing.id,
      priorityId: priorities.urgent.id,
      reportedIssue: "HDMI no signal after cable yank. Fan spins, blue light stays on.",
      createdById: tom.id,
      assignedToId: jack.id,
      estimatedPrice: "220.00",
      dueAt: new Date(Date.now() + 12 * 3600000),
      slaState: "APPROACHING",
      slaCompletionDue: new Date(Date.now() + 6 * 3600000),
    },
  });

  await prisma.ticket.upsert({
    where: { ticketNumber: "PCB-2026-00007" },
    update: {},
    create: {
      ticketNumber: "PCB-2026-00007",
      customerId: harriet.id,
      deviceModelId: iphone15pro.id,
      typeId: boardType.id,
      statusId: statuses.awaiting_diagnosis.id,
      priorityId: priorities.high.id,
      reportedIssue: "No power after water exposure. Orange liquid indicator. Customer wants board-level attempt before write-off.",
      createdById: sophie.id,
      assignedToId: priya.id,
    },
  });

  const tDone = await prisma.ticket.upsert({
    where: { ticketNumber: "REP-2026-00088" },
    update: {},
    create: {
      ticketNumber: "REP-2026-00088",
      customerId: bakery.id,
      deviceModelId: s24.id,
      typeId: phoneType.id,
      statusId: statuses.completed.id,
      priorityId: priorities.normal.id,
      reportedIssue: "Battery draining by lunch service. Health reported 76%.",
      createdById: sophie.id,
      assignedToId: priya.id,
      estimatedPrice: "149.00",
      closedAt: new Date(Date.now() - 4 * 86400000),
      closedById: priya.id,
    },
  });

  const ready = await prisma.ticket.upsert({
    where: { ticketNumber: "REP-2026-00108" },
    update: {},
    create: {
      ticketNumber: "REP-2026-00108",
      customerId: luca.id,
      deviceModelId: iphone15.id,
      typeId: phoneType.id,
      statusId: statuses.ready_pickup.id,
      priorityId: priorities.normal.id,
      reportedIssue: "Charging port intermittent. Works only at an angle.",
      createdById: sophie.id,
      assignedToId: priya.id,
      estimatedPrice: "129.00",
    },
  });

  const studioTicket = await prisma.ticket.upsert({
    where: { ticketNumber: "ITS-2026-00031" },
    update: {},
    create: {
      ticketNumber: "ITS-2026-00031",
      customerId: studio.id,
      deviceModelId: t14.id,
      typeId: (await prisma.ticketType.findUniqueOrThrow({ where: { key: "it_support" } })).id,
      statusId: statuses.waiting_approval.id,
      priorityId: priorities.high.id,
      reportedIssue: "Staff ThinkPad failing to join new VLAN after office move. Need on-site this week if possible.",
      createdById: tom.id,
      assignedToId: jack.id,
      estimatedPrice: "320.00",
    },
  });

  const cash = await prisma.paymentMethod.findUniqueOrThrow({ where: { key: "cash" } });
  const bank = await prisma.paymentMethod.findUniqueOrThrow({ where: { key: "bank" } });
  const invoice = await prisma.invoice.create({
    data: {
      number: "INV-2026-00041",
      customerId: bakery.id,
      ticketId: tDone.id,
      status: "PAID",
      issuedAt: new Date(Date.now() - 4 * 86400000),
      subtotal: "135.45",
      gst: "13.55",
      total: "149.00",
      amountPaid: "149.00",
      createdById: sophie.id,
      lines: {
        create: [
          { type: "PARTS", description: "Galaxy S24 battery", quantity: 1, unitPrice: "89.00" },
          { type: "LABOUR", description: "Phone repair labour", quantity: 0.5, unitPrice: "99.00" },
        ],
      },
      payments: {
        create: { methodId: cash.id, amount: "149.00", status: "SUCCEEDED", recordedById: sophie.id, provider: "manual" },
      },
    },
  }).catch(async () => prisma.invoice.findUniqueOrThrow({ where: { number: "INV-2026-00041" } }));

  const partPaid = await prisma.invoice.create({
    data: {
      number: "INV-2026-00052",
      customerId: luca.id,
      ticketId: t1.id,
      status: "PART_PAID",
      issuedAt: new Date(),
      subtotal: "299.09",
      gst: "29.91",
      total: "329.00",
      amountPaid: "100.00",
      createdById: sophie.id,
      lines: { create: [{ type: "PARTS", description: "iPhone 15 OLED screen", quantity: 1, unitPrice: "329.00" }] },
      payments: { create: { methodId: bank.id, amount: "100.00", status: "SUCCEEDED", isDeposit: true, recordedById: sophie.id } },
    },
  }).catch(async () => prisma.invoice.findUniqueOrThrow({ where: { number: "INV-2026-00052" } }));

  const quote = await prisma.quote.create({
    data: {
      number: "QTE-2026-00018",
      customerId: studio.id,
      status: "PENDING",
      issue: "Four staff laptops: SSD upgrades and Windows 11 clean builds.",
      deviceSummary: "ThinkPad T14 fleet",
      expiryDate: new Date(Date.now() + 14 * 86400000),
      subtotal: "1680.00",
      gst: "168.00",
      total: "1848.00",
      createdById: tom.id,
      lines: {
        create: [
          { type: "PARTS", description: "WD Black SN850X 1TB × 4", quantity: 4, unitPrice: "179.00" },
          { type: "LABOUR", description: "Imaging and setup", quantity: 6, unitPrice: "160.00" },
        ],
      },
    },
  }).catch(() => undefined);

  const build = await prisma.pcBuild.create({
    data: {
      number: "PCB-2026-00011",
      customerId: harriet.id,
      status: "PARTS_ORDERED",
      assignedToId: jack.id,
      budget: "2800.00",
      useCase: "1440p gaming and occasional Blender",
      targetWorkloads: "Cyberpunk, MSFS, Blender GPU renders",
      preferredBrands: "AMD CPU, NVIDIA GPU, quiet case",
      appearance: "Black, minimal RGB",
      partsCost: "2124.00",
      labourAmount: "220.00",
      markupAmount: "402.00",
      gstAmount: "274.60",
      estimatedPrice: "3020.60",
      depositAmount: "800.00",
      dueAt: new Date(Date.now() + 10 * 86400000),
      components: {
        create: [
          { itemId: cpu.id, category: "CPU", name: cpu.name, quantity: 1, unitCost: cpu.cost, unitPrice: cpu.salePrice, specs: cpu.specifications as object },
          { itemId: mb.id, category: "MOTHERBOARD", name: mb.name, quantity: 1, unitCost: mb.cost, unitPrice: mb.salePrice, specs: mb.specifications as object },
          { itemId: ram.id, category: "RAM", name: ram.name, quantity: 1, unitCost: ram.cost, unitPrice: ram.salePrice, specs: ram.specifications as object },
          { itemId: gpu.id, category: "GPU", name: gpu.name, quantity: 1, unitCost: gpu.cost, unitPrice: gpu.salePrice, specs: gpu.specifications as object },
          { itemId: ssd.id, category: "SSD", name: ssd.name, quantity: 1, unitCost: ssd.cost, unitPrice: ssd.salePrice, specs: ssd.specifications as object },
          { itemId: psu.id, category: "PSU", name: psu.name, quantity: 1, unitCost: psu.cost, unitPrice: psu.salePrice, specs: psu.specifications as object },
          { itemId: pcCase.id, category: "CASE", name: pcCase.name, quantity: 1, unitCost: pcCase.cost, unitPrice: pcCase.salePrice, specs: pcCase.specifications as object },
        ],
      },
    },
  }).catch(async () => prisma.pcBuild.findUniqueOrThrow({ where: { number: "PCB-2026-00011" } }));

  const used = await prisma.usedDevicePurchase.create({
    data: {
      number: "BUY-2026-00009",
      sellerCustomerId: nora.id,
      deviceSummary: "iPhone 13 128GB (Starlight) — cracked rear, battery 84%",
      serial: "F17XQ2N0Q1",
      imei: "353325118209441",
      conditionGrade: "C",
      faults: "Rear glass cracked, Face ID OK",
      expectedResale: "550.00",
      expectedRepairCost: "90.00",
      feesAllowance: "40.00",
      requiredProfit: "140.00",
      recommendedMaxOffer: "280.00",
      purchasePrice: "260.00",
      status: "REFURBISHING",
      refurbishment: { create: { partsCost: "45.00", labourCost: "40.00", totalInvested: "345.00" } },
    },
  }).catch(() => undefined);

  await prisma.purchaseOrder.create({
    data: {
      number: "PO-2026-00014",
      supplierId: mobilehub.id,
      status: "ORDERED",
      expectedAt: new Date(Date.now() + 2 * 86400000),
      createdById: tom.id,
      notes: "Keyboard for Amelia Chen XPS plus extra iPhone 15 batteries.",
      items: {
        create: [
          { itemId: keyboard.id, quantity: 2, unitCost: "62.00" },
          { itemId: batt.id, quantity: 8, unitCost: "28.00" },
        ],
      },
    },
  }).catch(() => undefined);

  const drop = await prisma.bookingType.findUniqueOrThrow({ where: { key: "dropoff" } });
  const consult = await prisma.bookingType.findUniqueOrThrow({ where: { key: "pc_consult" } });
  await prisma.booking.createMany({
    data: [
      { customerId: luca.id, typeId: drop.id, staffId: sophie.id, startsAt: new Date(new Date().setHours(10, 0, 0, 0)), endsAt: new Date(new Date().setHours(10, 15, 0, 0)), status: "CONFIRMED" },
      { customerId: harriet.id, typeId: consult.id, staffId: jack.id, startsAt: new Date(new Date().setHours(14, 30, 0, 0)), endsAt: new Date(new Date().setHours(15, 15, 0, 0)), notes: "Talk through 1440p GPU options." },
    ],
  }).catch(() => undefined);

  const start = new Date();
  start.setHours(9, 0, 0, 0);
  const end = new Date();
  end.setHours(17, 30, 0, 0);
  await prisma.rosterShift.createMany({
    data: [
      { userId: priya.id, startsAt: start, endsAt: end },
      { userId: jack.id, startsAt: start, endsAt: end },
      { userId: sophie.id, startsAt: start, endsAt: end },
    ],
  }).catch(() => undefined);

  const guides = await prisma.knowledgeCategory.findUniqueOrThrow({ where: { slug: "repair-guides" } });
  await prisma.knowledgeArticle.createMany({
    data: [
      {
        title: "iPhone 15 display transfer notes",
        slug: "iphone-15-display-transfer",
        body: "Workshop notes (not a copied OEM manual). Confirm True Tone after aftermarket OLED. Photograph flex seating. Do not reuse damaged earpiece meshes. Record battery health before opening.",
        tags: ["iphone", "display", "oled"],
        categoryId: guides.id,
        manufacturerId: apple.id,
        modelId: iphone15.id,
        repairType: "screen",
        authorId: priya.id,
      },
      {
        title: "XPS 15 liquid damage first pass",
        slug: "xps-15-liquid-first-pass",
        body: "Disconnect battery first. Inspect keyboard web and I/O board. If corrosion is localised to the keyboard, replacement often restores the machine. Log serial and spill details for warranty conversations with the customer — accidental damage is not covered by our repair warranty.",
        tags: ["dell", "liquid", "keyboard"],
        categoryId: guides.id,
        manufacturerId: dell.id,
        modelId: xps15.id,
        repairType: "keyboard",
        authorId: jack.id,
      },
    ],
    skipDuplicates: true,
  });

  await prisma.notification.createMany({
    data: [
      { userId: priya.id, title: "Job assigned", body: "REP-2026-00112 was assigned to you.", href: `/tickets/${t1.id}`, ticketId: t1.id },
      { userId: jack.id, title: "Waiting for parts", body: "Keyboard for LPT-2026-00048 is on PO-2026-00014.", href: `/tickets/${t2.id}`, ticketId: t2.id },
      { userId: maya.id, title: "Deposit received", body: "INV-2026-00052 part paid $100.00", href: `/invoices/${partPaid.id}` },
    ],
  }).catch(() => undefined);

  await prisma.printerProfile.createMany({
    data: [
      { name: "Front counter A4", kind: "document", paperWidthMm: 210, paperHeightMm: 297, isDefault: true },
      { name: "Brother QL-820NWB", kind: "label", paperWidthMm: 62, paperHeightMm: 29 },
      { name: "Receipt printer", kind: "receipt", paperWidthMm: 80 },
    ],
    skipDuplicates: true,
  });

  console.info("Seed complete. Sign in as maya@riversidetech.com.au / Riverside!2026");
}

main()
  .then(() => prisma.$disconnect())
  .catch(async (e) => {
    console.error(e);
    await prisma.$disconnect();
    process.exit(1);
  });
