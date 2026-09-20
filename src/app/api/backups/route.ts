import { NextResponse } from "next/server";
import { requirePermission } from "@/server/actor";
import { runBackup } from "@/server/services/backup.service";
import { toPublicError } from "@/server/errors";

export async function POST() {
  try {
    const actor = await requirePermission("backups.manage");
    const result = await runBackup(actor, "manual");
    return NextResponse.json(result);
  } catch (error) {
    const pub = toPublicError(error);
    return NextResponse.json(pub, { status: pub.status });
  }
}
