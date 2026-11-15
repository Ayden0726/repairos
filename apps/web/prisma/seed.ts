import { PrismaClient, TicketStatus } from "@prisma/client";
import bcrypt from "bcryptjs";

const prisma = new PrismaClient();

async function main() {
  const year = Number(
    new Intl.DateTimeFormat("en-AU", {
      timeZone: "Australia/Brisbane",
      year: "numeric",
    }).formatToParts(new Date()).find((p) => p.type === "year")?.value,
  );
  if (!Number.isFinite(year)) {
    throw new Error("Could not resolve Brisbane calendar year for seed");
  }

  await prisma.businessSettings.upsert({
    where: { id: 1 },
    update: {},
    create: {
      id: 1,
      businessName: "TechBench NQ",
      timezone: "Australia/Brisbane",
      currency: "AUD",
      gstRegistered: false,
      gstRateBp: 1000,
      defaultLabourFeeCents: 5000n,
      ticketSeqYear: year,
      ticketSeqNext: 1,
    },
  });

  const ownerCount = await prisma.user.count({ where: { role: "owner" } });
  if (ownerCount === 0) {
    const email = (process.env.SEED_OWNER_EMAIL || "").trim().toLowerCase();
    const password = process.env.SEED_OWNER_PASSWORD || "";
    const pin = process.env.SEED_OWNER_PIN || "";
    if (!email || password.length < 8) {
      throw new Error(
        "No owner exists. Set SEED_OWNER_EMAIL and SEED_OWNER_PASSWORD (8+) to create one.",
      );
    }
    if (pin && !/^\d{4,8}$/.test(pin)) {
      throw new Error("SEED_OWNER_PIN must be 4–8 digits when set");
    }
    await prisma.user.create({
      data: {
        email,
        passwordHash: await bcrypt.hash(password, 12),
        pinHash: pin ? await bcrypt.hash(pin, 12) : null,
        displayName: process.env.SEED_OWNER_NAME || "Owner",
        role: "owner",
      },
    });
    console.log(`Created owner ${email}`);
  } else {
    console.log("Owner already present — leaving credentials unchanged");
    const pin = process.env.SEED_OWNER_PIN || "";
    if (pin && /^\d{4,8}$/.test(pin)) {
      const owner = await prisma.user.findFirst({
        where: { role: "owner", pinHash: null },
        orderBy: { createdAt: "asc" },
      });
      if (owner) {
        await prisma.user.update({
          where: { id: owner.id },
          data: { pinHash: await bcrypt.hash(pin, 12) },
        });
        console.log("Set PIN on existing owner (was unset)");
      }
    }
  }

  if (process.env.SEED_DEMO !== "true") {
    console.log("Seed complete (SEED_DEMO != true)");
    return;
  }

  const existing = await prisma.customer.count();
  if (existing > 0) {
    console.log("Demo customers already present — skipping demo tickets");
    return;
  }

  const owner = await prisma.user.findFirstOrThrow({
    where: { role: "owner" },
    orderBy: { createdAt: "asc" },
  });

  const sam = await prisma.customer.create({
    data: {
      firstName: "Sam",
      lastName: "Nguyen",
      phone: "0400 111 222",
      email: "sam.nguyen@example.com",
      notes: "Prefers SMS updates",
    },
  });
  const jordan = await prisma.customer.create({
    data: {
      firstName: "Jordan",
      lastName: "Lee",
      phone: "0412 555 019",
      email: "jordan.lee@example.com",
    },
  });

  const iphone = await prisma.device.create({
    data: {
      customerId: sam.id,
      category: "phone",
      brand: "Apple",
      model: "iPhone 13",
      colour: "Midnight",
      storage: "128GB",
      imei: "356938035643809",
    },
  });
  const laptop = await prisma.device.create({
    data: {
      customerId: jordan.id,
      category: "computer",
      brand: "Dell",
      model: "XPS 13",
      serial: "DXPS-DEMO-8841",
    },
  });

  const settings = await prisma.businessSettings.findUniqueOrThrow({
    where: { id: 1 },
  });
  let seq = settings.ticketSeqNext;

  async function ticket(
    status: TicketStatus,
    customerId: string,
    deviceId: string,
    fault: string,
    labour: bigint,
    estimate: bigint | null,
  ) {
    const number = `TB-${year}-${String(seq).padStart(6, "0")}`;
    seq += 1;
    const t = await prisma.ticket.create({
      data: {
        ticketNumber: number,
        customerId,
        deviceId,
        status,
        reportedFault: fault,
        conditionPowersOn: true,
        conditionCracks: status !== "received",
        accessoryCharger: true,
        labourFeeCents: labour,
        estimateCents: estimate,
        createdById: owner.id,
      },
    });
    await prisma.ticketStatusHistory.create({
      data: {
        ticketId: t.id,
        actorId: owner.id,
        fromStatus: null,
        toStatus: "received",
        comment: "Intake",
      },
    });
    if (status !== "received") {
      await prisma.ticketStatusHistory.create({
        data: {
          ticketId: t.id,
          actorId: owner.id,
          fromStatus: "received",
          toStatus: status,
        },
      });
    }
    await prisma.ticketNote.create({
      data: {
        ticketId: t.id,
        authorId: owner.id,
        body: "Fictional demo note for local development.",
      },
    });
    return t;
  }

  await ticket(
    "awaiting_approval",
    sam.id,
    iphone.id,
    "Cracked display after drop. Touch still works.",
    5000n,
    28100n,
  );
  await ticket(
    "diagnosing",
    jordan.id,
    laptop.id,
    "Intermittent no-post; fan spins, no display.",
    5000n,
    8900n,
  );

  await prisma.businessSettings.update({
    where: { id: 1 },
    data: { ticketSeqNext: seq },
  });

  console.log("Seeded fictional demo customers and tickets");
}

main()
  .catch((e) => {
    console.error(e);
    process.exit(1);
  })
  .finally(async () => {
    await prisma.$disconnect();
  });
