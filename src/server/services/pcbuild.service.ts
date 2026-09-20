import { Prisma } from "@prisma/client";
import { assertCan, type Actor } from "../actor";
import { audit } from "../audit";
import { prisma } from "../db";
import { add, applyGst, margin, roundMoney } from "../money";
import { nextNumber } from "../numbering";
import { getSetting } from "../config/settings";
import { NotFoundError } from "../errors";

export type CompatWarning = { level: "info" | "warning" | "unknown"; message: string };

export function assessCompatibility(components: Array<{ category: string; specs?: Record<string, unknown> | null; name: string }>): CompatWarning[] {
  const warnings: CompatWarning[] = [];
  const cpu = components.find((c) => c.category === "CPU");
  const mb = components.find((c) => c.category === "MOTHERBOARD");
  const ram = components.find((c) => c.category === "RAM");
  const psu = components.find((c) => c.category === "PSU");
  const gpu = components.find((c) => c.category === "GPU");
  const pcCase = components.find((c) => c.category === "CASE");
  const cooler = components.find((c) => c.category === "CPU_COOLER");

  const cpuSocket = cpu?.specs?.socket as string | undefined;
  const mbSocket = mb?.specs?.socket as string | undefined;
  if (cpu && mb && (!cpuSocket || !mbSocket)) {
    warnings.push({ level: "unknown", message: "CPU/motherboard socket data is missing — compatibility is not confirmed." });
  } else if (cpuSocket && mbSocket && cpuSocket !== mbSocket) {
    warnings.push({ level: "warning", message: `CPU socket ${cpuSocket} does not match motherboard ${mbSocket}.` });
  } else if (cpuSocket && mbSocket) {
    warnings.push({ level: "info", message: `CPU and motherboard share socket ${cpuSocket}.` });
  }

  const ramType = ram?.specs?.type as string | undefined;
  const mbRam = mb?.specs?.ramType as string | undefined;
  if (ram && mb && (!ramType || !mbRam)) {
    warnings.push({ level: "unknown", message: "RAM type vs motherboard support cannot be confirmed." });
  } else if (ramType && mbRam && ramType !== mbRam) {
    warnings.push({ level: "warning", message: `RAM ${ramType} may not match motherboard ${mbRam}.` });
  }

  const form = mb?.specs?.formFactor as string | undefined;
  const caseForm = pcCase?.specs?.motherboard as string | undefined;
  if (mb && pcCase && (!form || !caseForm)) {
    warnings.push({ level: "unknown", message: "Case/motherboard form factor is incomplete." });
  } else if (form && caseForm && !String(caseForm).includes(form)) {
    warnings.push({ level: "warning", message: `Motherboard ${form} may not fit a ${caseForm} case.` });
  }

  const gpuLen = Number(gpu?.specs?.lengthMm ?? 0);
  const caseGpu = Number(pcCase?.specs?.maxGpuMm ?? 0);
  if (gpu && pcCase && (!gpuLen || !caseGpu)) {
    warnings.push({ level: "unknown", message: "GPU length vs case clearance is unknown." });
  } else if (gpuLen && caseGpu && gpuLen > caseGpu) {
    warnings.push({ level: "warning", message: `GPU length ${gpuLen}mm exceeds case clearance ${caseGpu}mm.` });
  }

  const coolerH = Number(cooler?.specs?.heightMm ?? 0);
  const caseCooler = Number(pcCase?.specs?.maxCoolerMm ?? 0);
  if (cooler && pcCase && (!coolerH || !caseCooler)) {
    warnings.push({ level: "unknown", message: "CPU cooler height clearance is unknown." });
  } else if (coolerH && caseCooler && coolerH > caseCooler) {
    warnings.push({ level: "warning", message: `Cooler height ${coolerH}mm exceeds case limit ${caseCooler}mm.` });
  }

  const psuW = Number(psu?.specs?.wattage ?? 0);
  if (psu && psuW && psuW < 550 && gpu) {
    warnings.push({ level: "warning", message: `PSU ${psuW}W may be tight for a discrete GPU. Treat as guidance only.` });
  } else if (psu && !psuW) {
    warnings.push({ level: "unknown", message: "PSU wattage is not on file." });
  }

  return warnings;
}

export async function createBuild(actor: Actor, input: {
  customerId: string;
  budget?: number;
  useCase?: string;
  targetWorkloads?: string;
  preferredBrands?: string;
  appearance?: string;
  quoteId?: string;
}) {
  assertCan(actor, "builds.manage");
  const number = await nextNumber("pc", (await getSetting("numbering", { pcPrefix: "PCB" })).pcPrefix);
  const build = await prisma.pcBuild.create({
    data: { number, ...input, assignedToId: actor.id },
  });
  await audit({ actor, action: "pcbuild.create", entityType: "PcBuild", entityId: build.id });
  return build;
}

export async function addBuildComponent(actor: Actor, buildId: string, input: {
  itemId?: string;
  category: string;
  name: string;
  quantity: number;
  unitCost: number;
  unitPrice: number;
  specs?: Record<string, unknown>;
}) {
  assertCan(actor, "builds.manage");
  await prisma.pcBuildComponent.create({
    data: {
      buildId,
      itemId: input.itemId,
      category: input.category as never,
      name: input.name,
      quantity: input.quantity,
      unitCost: input.unitCost,
      unitPrice: input.unitPrice,
      specs: input.specs as never,
    },
  });
  return recalculateBuild(actor, buildId);
}

export async function recalculateBuild(actor: Actor, buildId: string) {
  const build = await prisma.pcBuild.findUnique({
    where: { id: buildId },
    include: { components: true },
  });
  if (!build) throw new NotFoundError("PC build");
  const partsCost = build.components.reduce((s, c) => add(s, Number(c.unitCost) * c.quantity), new Prisma.Decimal(0));
  const partsPrice = build.components.reduce((s, c) => add(s, Number(c.unitPrice) * c.quantity), new Prisma.Decimal(0));
  const labour = build.labourAmount;
  const gstCfg = await getSetting("gst", { registered: true, rate: 0.1 });
  const pretax = add(partsPrice, labour);
  const tax = gstCfg.registered ? applyGst(pretax, gstCfg.rate) : { exclusive: pretax, gst: new Prisma.Decimal(0), inclusive: pretax };
  const estimated = tax.inclusive;
  const { profit, margin: m } = margin(estimated, add(partsCost, labour));
  const warnings = assessCompatibility(
    build.components.map((c) => ({ category: c.category, specs: c.specs as Record<string, unknown> | null, name: c.name })),
  );
  const updated = await prisma.pcBuild.update({
    where: { id: buildId },
    data: {
      partsCost,
      markupAmount: roundMoney(Number(partsPrice) - Number(partsCost)),
      gstAmount: tax.gst,
      estimatedPrice: estimated,
      compatibilityJson: warnings as never,
    },
    include: { components: true, customer: true, benchmarks: true, assignedTo: true },
  });
  return { ...updated, profit, margin: m, warnings };
}

export async function setBuildStatus(actor: Actor, buildId: string, status: Prisma.PcBuildUpdateInput["status"]) {
  assertCan(actor, "builds.manage");
  const build = await prisma.pcBuild.update({ where: { id: buildId }, data: { status } });
  await audit({ actor, action: "pcbuild.status", entityType: "PcBuild", entityId: buildId, newValue: { status } });
  return build;
}
