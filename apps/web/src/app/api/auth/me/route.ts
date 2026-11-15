import { NextResponse } from "next/server";
import { requireUser } from "@/lib/session";

export async function GET() {
  const session = await requireUser();
  if (!session?.userId) {
    return NextResponse.json({ user: null }, { status: 401 });
  }
  return NextResponse.json({
    user: {
      id: session.userId,
      email: session.email,
      displayName: session.displayName,
      role: session.role,
    },
  });
}
