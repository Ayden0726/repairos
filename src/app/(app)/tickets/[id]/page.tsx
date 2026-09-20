import { notFound } from "next/navigation";
import { requirePermission } from "@/server/actor";
import { getTicket } from "@/server/services/ticket.service";
import { prisma } from "@/server/db";
import { formatAud } from "@/server/money";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import {
  assignAction,
  credentialAction,
  diagnosticResultAction,
  diagnosticStartAction,
  invoiceFromTicketAction,
  noteAction,
  partAction,
  statusAction,
  timerAction,
} from "@/app/actions";
import { format } from "date-fns";
import { SignaturePad } from "@/components/tickets/signature-pad";
import { RevealPin } from "@/components/tickets/reveal-pin";
import { LabelPrinter } from "@/components/tickets/label-printer";
import { AiPanel } from "@/components/ai/ai-panel";

export default async function TicketDetail({ params }: { params: Promise<{ id: string }> }) {
  const actor = await requirePermission("tickets.view");
  const { id } = await params;
  let ticket;
  try {
    ticket = await getTicket(actor, id);
  } catch {
    notFound();
  }
  const [statuses, techs, templates, inventory] = await Promise.all([
    prisma.ticketStatus.findMany({ where: { archivedAt: null }, orderBy: { sortOrder: "asc" } }),
    prisma.user.findMany({ where: { archivedAt: null, status: "ACTIVE" } }),
    prisma.diagnosticTemplate.findMany({ where: { archivedAt: null } }),
    prisma.inventoryItem.findMany({ where: { archivedAt: null, kind: "REPAIR_PART" }, take: 80 }),
  ]);
  const running = ticket.timeEntries.find((e) => e.state === "RUNNING" || e.state === "PAUSED");
  const canFinance = actor.isOwner || actor.permissions.has("pricing.view");

  return (
    <div className="space-y-6 pb-20">
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div>
          <div className="flex items-center gap-2">
            <h1 className="text-2xl font-semibold">{ticket.ticketNumber}</h1>
            <Badge variant="outline" style={{ borderColor: ticket.status.colour, color: ticket.status.colour }}>
              {ticket.status.name}
            </Badge>
            <Badge variant="outline" style={{ borderColor: ticket.priority.colour, color: ticket.priority.colour }}>
              {ticket.priority.name}
            </Badge>
            {ticket.slaState !== "NONE" ? (
              <Badge variant={ticket.slaState === "BREACHED" ? "destructive" : "secondary"}>{ticket.slaState.replace("_", " ")}</Badge>
            ) : null}
          </div>
          <p className="mt-1 text-sm text-muted-foreground">
            {ticket.customer.displayName} · {ticket.type.name}
            {ticket.customerDevice ? ` · ${ticket.customerDevice.modelName ?? ticket.customerDevice.deviceType}` : ""}
          </p>
        </div>
        <div className="flex flex-wrap gap-2">
          <LabelPrinter ticketId={ticket.id} ticketNumber={ticket.ticketNumber} />
          {canFinance ? (
            <form action={invoiceFromTicketAction}>
              <input type="hidden" name="ticketId" value={ticket.id} />
              <Button type="submit" variant="outline">
                Generate invoice
              </Button>
            </form>
          ) : null}
        </div>
      </div>

      <div className="grid gap-4 lg:grid-cols-3">
        <section className="space-y-4 lg:col-span-2">
          <Box title="Reported issue">
            <p className="text-sm whitespace-pre-wrap">{ticket.reportedIssue}</p>
          </Box>
          <Box title="Status & assignment">
            <div className="flex flex-wrap gap-3">
              <form action={statusAction} className="flex gap-2">
                <input type="hidden" name="ticketId" value={ticket.id} />
                <select name="statusId" defaultValue={ticket.statusId} className="h-8 rounded-lg border border-input bg-transparent px-2 text-sm">
                  {statuses.map((s) => (
                    <option key={s.id} value={s.id}>
                      {s.name}
                    </option>
                  ))}
                </select>
                <Button type="submit" size="sm">
                  Update status
                </Button>
              </form>
              <form action={assignAction} className="flex gap-2">
                <input type="hidden" name="ticketId" value={ticket.id} />
                <select name="assignedToId" defaultValue={ticket.assignedToId ?? ""} className="h-8 rounded-lg border border-input bg-transparent px-2 text-sm">
                  <option value="">Unassigned</option>
                  {techs.map((t) => (
                    <option key={t.id} value={t.id}>
                      {t.name}
                    </option>
                  ))}
                </select>
                <Button type="submit" size="sm" variant="outline">
                  Assign
                </Button>
              </form>
            </div>
          </Box>
          <Box title="Diagnostics">
            <form action={diagnosticStartAction} className="mb-3 flex gap-2">
              <input type="hidden" name="ticketId" value={ticket.id} />
              <select name="templateId" className="h-8 rounded-lg border border-input bg-transparent px-2 text-sm">
                {templates.map((t) => (
                  <option key={t.id} value={t.id}>
                    {t.name}
                  </option>
                ))}
              </select>
              <select name="phase" className="h-8 rounded-lg border border-input bg-transparent px-2 text-sm">
                <option value="pre">Pre-repair</option>
                <option value="post">Post-repair</option>
              </select>
              <Button size="sm" type="submit">
                Start checklist
              </Button>
            </form>
            {ticket.diagnosticRuns.map((run) => (
              <div key={run.id} className="mb-3 rounded-lg border border-border p-3">
                <div className="mb-2 text-xs font-medium uppercase tracking-wide text-muted-foreground">
                  {run.template.name} · {run.phase}
                </div>
                <div className="grid gap-1">
                  {run.results.map((result) => (
                    <form key={result.id} action={diagnosticResultAction} className="flex items-center justify-between gap-2 text-sm">
                      <input type="hidden" name="ticketId" value={ticket.id} />
                      <input type="hidden" name="resultId" value={result.id} />
                      <span>{result.item.label}</span>
                      <div className="flex gap-1">
                        <select name="value" defaultValue={result.value} className="h-7 rounded-md border border-input bg-transparent px-2 text-xs">
                          <option>PASS</option>
                          <option>FAIL</option>
                          <option>NOT_TESTED</option>
                          <option>NOT_APPLICABLE</option>
                        </select>
                        <Button size="xs" type="submit" variant="ghost">
                          Save
                        </Button>
                      </div>
                    </form>
                  ))}
                </div>
              </div>
            ))}
          </Box>
          <Box title="Parts needed">
            <form action={partAction} className="mb-3 grid gap-2 md:grid-cols-4">
              <input type="hidden" name="ticketId" value={ticket.id} />
              <select name="itemId" className="h-8 rounded-lg border border-input bg-transparent px-2 text-sm md:col-span-2">
                <option value="">Custom / not in stock</option>
                {inventory.map((i) => (
                  <option key={i.id} value={i.id}>
                    {i.sku} · {i.name} ({i.onHand - i.reserved} avail)
                  </option>
                ))}
              </select>
              <Input name="name" placeholder="Part name" />
              <Input name="quantity" defaultValue="1" />
              <Button type="submit" size="sm">
                Add
              </Button>
              <Button type="submit" size="sm" variant="outline" name="op" value="reserve">
                Reserve selected
              </Button>
            </form>
            <ul className="space-y-2 text-sm">
              {ticket.parts.map((p) => (
                <li key={p.id} className="flex items-center justify-between rounded-md border border-border px-3 py-2">
                  <span>
                    {p.quantity} × {p.name} · {p.status.replaceAll("_", " ")}
                  </span>
                  {p.status === "ORDERED" || p.status === "NEED_TO_ORDER" ? (
                    <form action={partAction}>
                      <input type="hidden" name="ticketId" value={ticket.id} />
                      <input type="hidden" name="partId" value={p.id} />
                      <input type="hidden" name="op" value="receive" />
                      <Button size="xs">Mark received</Button>
                    </form>
                  ) : null}
                </li>
              ))}
            </ul>
          </Box>
          <Box title="Time">
            <form action={timerAction} className="flex flex-wrap gap-2">
              <input type="hidden" name="ticketId" value={ticket.id} />
              {running ? (
                <>
                  <input type="hidden" name="entryId" value={running.id} />
                  {running.state === "RUNNING" ? (
                    <Button name="op" value="pause" variant="outline">
                      Pause
                    </Button>
                  ) : (
                    <Button name="op" value="resume" variant="outline">
                      Resume
                    </Button>
                  )}
                  <Button name="op" value="stop">
                    Stop work
                  </Button>
                </>
              ) : (
                <Button name="op" value="start">
                  Start work
                </Button>
              )}
            </form>
            <ul className="mt-3 space-y-1 text-sm">
              {ticket.timeEntries.map((e) => (
                <li key={e.id}>
                  {e.technician.name} · {e.state.toLowerCase()} · {format(e.startedAt, "d MMM HH:mm")}
                </li>
              ))}
            </ul>
          </Box>
          <Box title="Notes">
            <form action={noteAction} className="space-y-2">
              <input type="hidden" name="ticketId" value={ticket.id} />
              <Textarea name="body" required rows={3} />
              <label className="flex items-center gap-2 text-sm">
                <input type="checkbox" name="internal" defaultChecked className="size-4" />
                Internal (never shown to the customer)
              </label>
              <Button type="submit" size="sm">
                Add note
              </Button>
            </form>
            <ul className="mt-4 space-y-3">
              {ticket.notes.map((n) => (
                <li key={n.id} className="rounded-md bg-muted/40 p-3 text-sm">
                  <div className="text-xs text-muted-foreground">
                    {n.author.name} · {format(n.createdAt, "d MMM HH:mm")} · {n.isInternal ? "Internal" : "Customer-visible"}
                  </div>
                  <p className="mt-1 whitespace-pre-wrap">{n.body}</p>
                </li>
              ))}
            </ul>
          </Box>
          <Box title="Pickup / check-in signatures">
            <SignaturePad ticketId={ticket.id} />
          </Box>
        </section>
        <aside className="space-y-4">
          <Box title="Customer">
            <a href={`/customers/${ticket.customer.id}`} className="text-sm font-medium hover:underline">
              {ticket.customer.displayName}
            </a>
            <p className="text-sm text-muted-foreground">{ticket.customer.phone}</p>
            <p className="text-sm text-muted-foreground">{ticket.customer.email}</p>
          </Box>
          <Box title="Device password">
            {ticket.credentials && !ticket.credentials.deletedAt ? (
              <RevealPin ticketId={ticket.id} />
            ) : ticket.credentials?.deletedAt ? (
              <p className="text-sm text-muted-foreground">Permanently deleted when the job completed.</p>
            ) : (
              <form action={credentialAction} className="space-y-2">
                <input type="hidden" name="ticketId" value={ticket.id} />
                <Input name="label" placeholder="Label" defaultValue="Passcode" />
                <Input name="secret" placeholder="PIN / password" />
                <Button type="submit" size="sm" variant="outline">
                  Store encrypted
                </Button>
              </form>
            )}
          </Box>
          {canFinance ? (
            <Box title="Money">
              <p className="text-sm">Estimate {ticket.estimatedPrice ? formatAud(ticket.estimatedPrice) : "—"}</p>
              <p className="text-sm">Diagnostic fee {formatAud(ticket.diagnosticFeeCents / 100)}</p>
              {ticket.invoices.map((inv) => (
                <a key={inv.id} href={`/invoices/${inv.id}`} className="mt-2 block text-sm underline">
                  {inv.number} · {inv.status} · {formatAud(inv.total)}
                </a>
              ))}
            </Box>
          ) : null}
          <Box title="Timeline">
            <ol className="space-y-2">
              {ticket.events.map((ev) => (
                <li key={ev.id} className="text-xs">
                  <span className="text-muted-foreground">{format(ev.createdAt, "d MMM HH:mm")}</span>
                  <div className={ev.visibility === "INTERNAL" ? "text-muted-foreground" : ""}>{ev.summary}</div>
                </li>
              ))}
            </ol>
          </Box>
          <AiPanel ticketId={ticket.id} />
        </aside>
      </div>
    </div>
  );
}

function Box({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <section className="rounded-xl border border-border p-4">
      <h2 className="mb-3 text-sm font-semibold">{title}</h2>
      {children}
    </section>
  );
}
