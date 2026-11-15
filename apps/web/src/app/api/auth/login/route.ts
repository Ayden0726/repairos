import { NextResponse } from "next/server";
import bcrypt from "bcryptjs";
import { prisma } from "@/lib/db";
import { getSession } from "@/lib/session";
import { loginSchema } from "@/lib/validations";
import { ensureBusinessSettings } from "@/lib/tickets";

const INVALID = "Invalid sign-in details.";

export async function POST(req: Request) {
  await ensureBusinessSettings();
  const body = await req.json().catch(() => null);
  const parsed = loginSchema.safeParse(
    body?.mode ? body : { ...body, mode: "password" },
  );
  if (!parsed.success) {
    return NextResponse.json({ error: INVALID }, { status: 400 });
  }

  const user =
    parsed.data.mode === "password"
      ? await prisma.user.findUnique({
          where: { email: parsed.data.email.toLowerCase() },
        })
      : await prisma.user.findFirst({
          where: { role: "owner", pinHash: { not: null } },
          orderBy: { createdAt: "asc" },
        });

  if (!user) {
    return NextResponse.json({ error: INVALID }, { status: 401 });
  }

  let ok = false;
  if (parsed.data.mode === "password") {
    ok = await bcrypt.compare(parsed.data.password, user.passwordHash);
  } else if (user.pinHash) {
    ok = await bcrypt.compare(parsed.data.pin, user.pinHash);
  }

  if (!ok) {
    return NextResponse.json({ error: INVALID }, { status: 401 });
  }

  const session = await getSession();
  session.userId = user.id;
  session.email = user.email;
  session.displayName = user.displayName;
  session.role = user.role;
  await session.save();

  return NextResponse.json({
    user: {
      id: user.id,
      email: user.email,
      displayName: user.displayName,
      role: user.role,
    },
  });
}
