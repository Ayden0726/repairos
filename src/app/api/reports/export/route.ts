import { requirePermission } from "@/server/actor";
import { reportSummary } from "@/server/services/reporting.service";
import { subDays } from "date-fns";

export async function GET() {
  const actor = await requirePermission("reports.view");
  const data = await reportSummary(actor, subDays(new Date(), 30), new Date());
  const lines = [
    "metric,value",
    `repairCount,${data.repairCount}`,
    `revenue,${data.revenue ?? ""}`,
    `profit,${data.profit ?? ""}`,
    `gst,${data.gst ?? ""}`,
    `warrantyReturns,${data.warrantyReturns}`,
  ];
  return new Response(lines.join("\n"), {
    headers: { "content-type": "text/csv", "content-disposition": "attachment; filename=report.csv" },
  });
}
