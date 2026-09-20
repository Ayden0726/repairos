import { requirePermission } from "@/server/actor";
import { prisma } from "@/server/db";
import { getSetting } from "@/server/config/settings";
import { runHealthChecks } from "@/server/services/health.service";
import { PERMISSIONS } from "@/server/permissions";
import { SettingsCentre } from "@/components/settings/settings-centre";

export default async function SettingsPage() {
  const actor = await requirePermission("settings.view");
  const [roles, staff, statuses, types, priorities, labour, groups, methods, sms, printers, backups, incidents] =
    await Promise.all([
      prisma.role.findMany({ include: { permissions: true } }),
      prisma.user.findMany({ where: { archivedAt: null }, include: { role: true } }),
      prisma.ticketStatus.findMany({ orderBy: { sortOrder: "asc" } }),
      prisma.ticketType.findMany({ orderBy: { sortOrder: "asc" } }),
      prisma.ticketPriority.findMany({ orderBy: { sortOrder: "asc" } }),
      prisma.labourRate.findMany(),
      prisma.pricingGroup.findMany(),
      prisma.paymentMethod.findMany(),
      prisma.smsTemplate.findMany(),
      prisma.printerProfile.findMany(),
      prisma.backupRecord.findMany({ orderBy: { startedAt: "desc" }, take: 8 }),
      prisma.securityIncident.findMany({ orderBy: { createdAt: "desc" }, take: 8 }),
    ]);
  const [business, gst, tfa, smsCfg, emailCfg, squareCfg, ollamaCfg] = await Promise.all([
    getSetting("business.profile", { name: "" }),
    getSetting("gst", { registered: true, rate: 0.1 }),
    getSetting("security.twoFactor", { requireOwners: true }),
    getSetting("integrations.sms", { enabled: false, provider: "console" }),
    getSetting("integrations.email", { enabled: false }),
    getSetting("integrations.square", { enabled: false }),
    getSetting("integrations.ollama", { enabled: false, baseUrl: "http://127.0.0.1:11434", model: "llama3.1" }),
  ]);
  const health = await runHealthChecks();
  return (
    <SettingsCentre
      actorName={actor.name}
      canManage={actor.isOwner || actor.permissions.has("settings.manage")}
      permissions={[...PERMISSIONS]}
      data={{
        roles,
        staff,
        statuses,
        types,
        priorities,
        labour,
        groups,
        methods,
        sms,
        printers,
        backups,
        incidents,
        business,
        gst,
        tfa,
        smsCfg,
        emailCfg,
        squareCfg,
        ollamaCfg,
        health,
      }}
    />
  );
}
