import { prisma } from "../db";

export async function notifyUsers(
  userIds: string[],
  input: { title: string; body: string; href?: string; ticketId?: string; severity?: string },
) {
  if (!userIds.length) return;
  await prisma.notification.createMany({
    data: userIds.map((userId) => ({
      userId,
      title: input.title,
      body: input.body,
      href: input.href,
      ticketId: input.ticketId,
      severity: input.severity ?? "info",
    })),
  });
}

export async function listNotifications(userId: string) {
  return prisma.notification.findMany({
    where: { userId },
    orderBy: { createdAt: "desc" },
    take: 50,
  });
}

export async function unreadCount(userId: string) {
  return prisma.notification.count({ where: { userId, readAt: null } });
}

export async function markRead(userId: string, id?: string) {
  if (id) {
    await prisma.notification.updateMany({ where: { id, userId }, data: { readAt: new Date() } });
    return;
  }
  await prisma.notification.updateMany({ where: { userId, readAt: null }, data: { readAt: new Date() } });
}
