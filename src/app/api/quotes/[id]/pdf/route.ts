import { quotePdf } from "@/server/services/document.service";
import { requireActor } from "@/server/actor";

export async function GET(_req: Request, ctx: { params: Promise<{ id: string }> }) {
  await requireActor();
  const { id } = await ctx.params;
  const pdf = await quotePdf(id);
  return new Response(new Uint8Array(pdf), {
    headers: { "content-type": "application/pdf", "content-disposition": `inline; filename="quote.pdf"` },
  });
}
