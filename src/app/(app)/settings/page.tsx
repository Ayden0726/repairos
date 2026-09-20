import { requirePermission } from "@/server/actor";
import { prisma } from "@/server/db";
import { getSetting } from "@/server/config/settings";
import { runHealthChecks } from "@/server/services/health.service";
import { PERMISSIONS } from "@/server/permissions";
import { SettingsCentre } from "@/components/settings/settings-centre";

const STAFF_NOTICES: Record<string, string> = {
  created: "Staff account created. They can sign in with the email and password you set.",
  suspended: "That account can no longer sign in.",
  restored: "That account can sign in again.",
};

export default async function SettingsPage({
  searchParams,
}: {
  searchParams: Promise<{ error?: string; staff?: string }>;
}) {
  const actor = await requirePermission("settings.view");
  const params = await searchParams;
  const [roles, staff, statuses, types, priorities, labour, groups, methods, sms, printers, backups, incidents] =
    await Promise.all([
      prisma.role.findMany({
        include: { permissions: true },
        orderBy: { name: "asc" },
      }),
      prisma.user.findMany({
        where: { archivedAt: null },
        select: {
          id: true,
          name: true,
          email: true,
          phone: true,
          status: true,
          isOwner: true,
          role: { select: { name: true, key: true } },
        },
        orderBy: { name: "asc" },
      }),
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
      actorId={actor.id}
      canManage={actor.isOwner || actor.permissions.has("settings.manage")}
      canManageStaff={actor.isOwner || actor.permissions.has("staff.manage")}
      notice={params.staff ? STAFF_NOTICES[params.staff] : undefined}
      error={params.error}
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
