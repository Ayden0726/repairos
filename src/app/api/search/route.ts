import { NextRequest, NextResponse } from "next/server";
import { requireActor } from "@/server/actor";
import { globalSearch } from "@/server/services/search.service";
import { toPublicError } from "@/server/errors";

export async function GET(req: NextRequest) {
  try {
    const actor = await requireActor();
    const q = req.nextUrl.searchParams.get("q") ?? "";
    const data = await globalSearch(actor, q);
    return NextResponse.json(data);
  } catch (error) {
    const pub = toPublicError(error);
    return NextResponse.json(pub, { status: pub.status });
  }
}
