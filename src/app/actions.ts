"use server";

import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";
import { cookies } from "next/headers";
import { requireActor, requirePermission } from "@/server/actor";
import * as auth from "@/server/services/auth.service";
import * as tickets from "@/server/services/ticket.service";
import * as customers from "@/server/services/customer.service";
import * as inventory from "@/server/services/inventory.service";
import * as finance from "@/server/services/finance.service";
import * as workshop from "@/server/services/workshop.service";
import * as builds from "@/server/services/pcbuild.service";
import * as used from "@/server/services/used.service";
import * as calendar from "@/server/services/calendar.service";
import * as notes from "@/server/services/notification.service";
import { completeSetup } from "@/server/services/setup.service";
import { SETUP_COOKIE } from "@/server/auth/constants";
import { toPublicError } from "@/server/errors";

function formString(form: FormData, key: string) {
  return String(form.get(key) ?? "").trim();
}

export async function loginAction(formData: FormData) {
  try {
    const result = await auth.login({
      email: formString(formData, "email"),
      password: formString(formData, "password"),
    });
    if (result.requiresTwoFactor) redirect("/login/two-factor");
    redirect("/");
  } catch (error) {
    if ((error as { digest?: string }).digest?.startsWith("NEXT_REDIRECT")) throw error;
    const pub = toPublicError(error);
    redirect(`/login?error=${encodeURIComponent(pub.error)}`);
  }
}

export async function twoFactorAction(formData: FormData) {
  try {
    await auth.verifyTwoFactor(formString(formData, "token"), formString(formData, "recovery") || undefined);
    redirect("/");
  } catch (error) {
    if ((error as { digest?: string }).digest?.startsWith("NEXT_REDIRECT")) throw error;
    redirect(`/login/two-factor?error=${encodeURIComponent(toPublicError(error).error)}`);
  }
}

export async function logoutAction() {
  await auth.logout();
  redirect("/login");
}

export async function setupAction(formData: FormData) {
  try {
    await completeSetup({
      business: {
        name: formString(formData, "businessName"),
        phone: formString(formData, "phone"),
        email: formString(formData, "email"),
        addressLine1: formString(formData, "address"),
        suburb: formString(formData, "suburb"),
        state: formString(formData, "state") || "VIC",
        postcode: formString(formData, "postcode"),
        abn: formString(formData, "abn"),
      },
      gstRegistered: formData.get("gst") === "on",
      labourDefault: formString(formData, "labour") || "110.00",
      diagnosticFee: formString(formData, "diagnostic") || "89.00",
      owner: {
        name: formString(formData, "ownerName"),
        email: formString(formData, "ownerEmail"),
        password: formString(formData, "ownerPassword"),
      },
      skipIntegrations: true,
    });
    const jar = await cookies();
    jar.set(SETUP_COOKIE, "1", { path: "/", httpOnly: true, sameSite: "lax" });
    redirect("/login");
  } catch (error) {
    if ((error as { digest?: string }).digest?.startsWith("NEXT_REDIRECT")) throw error;
    redirect(`/setup?error=${encodeURIComponent(toPublicError(error).error)}`);
  }
}

export async function quickTicketAction(formData: FormData) {
  const actor = await requirePermission("tickets.create");
  const ticket = await tickets.quickCreateTicket(actor, {
    customerId: formString(formData, "customerId"),
    typeId: formString(formData, "typeId"),
    reportedIssue: formString(formData, "reportedIssue"),
    priorityId: formString(formData, "priorityId") || undefined,
    assignedToId: formString(formData, "assignedToId") || undefined,
    estimatedPrice: formString(formData, "estimatedPrice") ? Number(formString(formData, "estimatedPrice")) : undefined,
    dueAt: formString(formData, "dueAt") || undefined,
  });
  revalidatePath("/tickets");
  redirect(`/tickets/${ticket.id}`);
}

export async function customerAction(formData: FormData) {
  const actor = await requirePermission("customers.manage");
  const customer = await customers.upsertCustomer(actor, {
    id: formString(formData, "id") || undefined,
    type: formString(formData, "type") === "BUSINESS" ? "BUSINESS" : "INDIVIDUAL",
    firstName: formString(formData, "firstName"),
    lastName: formString(formData, "lastName"),
    companyName: formString(formData, "companyName"),
    phone: formString(formData, "phone"),
    email: formString(formData, "email"),
    suburb: formString(formData, "suburb"),
    state: formString(formData, "state"),
    postcode: formString(formData, "postcode"),
    notes: formString(formData, "notes"),
    preferredContact: (formString(formData, "preferredContact") as "SMS" | "EMAIL" | "PHONE") || "SMS",
  });
  revalidatePath("/customers");
  redirect(`/customers/${customer.id}`);
}

export async function statusAction(formData: FormData) {
  const actor = await requireActor();
  await tickets.changeStatus(actor, formString(formData, "ticketId"), formString(formData, "statusId"));
  revalidatePath(`/tickets/${formString(formData, "ticketId")}`);
}

export async function assignAction(formData: FormData) {
  const actor = await requireActor();
  await tickets.assignTicket(actor, formString(formData, "ticketId"), formString(formData, "assignedToId") || null);
  revalidatePath(`/tickets/${formString(formData, "ticketId")}`);
}

export async function noteAction(formData: FormData) {
  const actor = await requireActor();
  await tickets.addNote(actor, formString(formData, "ticketId"), formString(formData, "body"), formData.get("internal") === "on");
  revalidatePath(`/tickets/${formString(formData, "ticketId")}`);
}

export async function timerAction(formData: FormData) {
  const actor = await requireActor();
  const op = formString(formData, "op");
  if (op === "start") await workshop.startTimer(actor, formString(formData, "ticketId"));
  if (op === "pause") await workshop.pauseTimer(actor, formString(formData, "entryId"));
  if (op === "resume") await workshop.resumeTimer(actor, formString(formData, "entryId"));
  if (op === "stop") await workshop.stopTimer(actor, formString(formData, "entryId"));
  revalidatePath(`/tickets/${formString(formData, "ticketId")}`);
}

export async function partAction(formData: FormData) {
  const actor = await requireActor();
  const ticketId = formString(formData, "ticketId");
  if (formString(formData, "op") === "receive") {
    await inventory.markPartReceived(actor, formString(formData, "partId"));
  } else if (formString(formData, "op") === "reserve") {
    await inventory.reservePart(actor, {
      itemId: formString(formData, "itemId"),
      quantity: Number(formString(formData, "quantity") || 1),
      ticketId,
    });
  } else {
    await inventory.addNeededPart(actor, ticketId, {
      itemId: formString(formData, "itemId") || undefined,
      name: formString(formData, "name"),
      quantity: Number(formString(formData, "quantity") || 1),
    });
  }
  revalidatePath(`/tickets/${ticketId}`);
}

export async function invoiceFromTicketAction(formData: FormData) {
  const actor = await requireActor();
  const invoice = await finance.createInvoiceFromTicket(actor, formString(formData, "ticketId"));
  redirect(`/invoices/${invoice.id}`);
}

export async function paymentAction(formData: FormData) {
  const actor = await requireActor();
  await finance.recordPayment(actor, {
    invoiceId: formString(formData, "invoiceId"),
    methodKey: formString(formData, "methodKey"),
    amount: Number(formString(formData, "amount")),
    isDeposit: formData.get("deposit") === "on",
    notes: formString(formData, "notes"),
  });
  revalidatePath(`/invoices/${formString(formData, "invoiceId")}`);
}

export async function refundAction(formData: FormData) {
  const actor = await requireActor();
  await finance.issueRefund(actor, {
    invoiceId: formString(formData, "invoiceId"),
    amount: Number(formString(formData, "amount")),
    reason: formString(formData, "reason"),
  });
  revalidatePath(`/invoices/${formString(formData, "invoiceId")}`);
}

export async function markNotificationsRead() {
  const actor = await requireActor();
  await notes.markRead(actor.id);
  revalidatePath("/");
}

export async function createBookingAction(formData: FormData) {
  const actor = await requireActor();
  await calendar.createBooking(actor, {
    customerId: formString(formData, "customerId"),
    typeId: formString(formData, "typeId"),
    staffId: formString(formData, "staffId") || undefined,
    startsAt: formString(formData, "startsAt"),
    endsAt: formString(formData, "endsAt"),
    notes: formString(formData, "notes"),
  });
  revalidatePath("/calendar");
  redirect("/calendar");
}

export async function usedPurchaseAction(formData: FormData) {
  const actor = await requireActor();
  const result = await used.recordPurchase(actor, {
    sellerCustomerId: formString(formData, "sellerCustomerId"),
    deviceSummary: formString(formData, "deviceSummary"),
    serial: formString(formData, "serial"),
    imei: formString(formData, "imei"),
    conditionGrade: formString(formData, "conditionGrade") as "A" | "B" | "C" | "D" | "FAULTY",
    faults: formString(formData, "faults"),
    expectedResale: Number(formString(formData, "expectedResale")),
    expectedRepairCost: Number(formString(formData, "expectedRepairCost")),
    feesAllowance: Number(formString(formData, "feesAllowance")),
    requiredProfit: Number(formString(formData, "requiredProfit")),
    purchasePrice: Number(formString(formData, "purchasePrice")),
    overrideReason: formString(formData, "overrideReason"),
  });
  redirect(`/used-tech/${result.purchase.id}`);
}

export async function createBuildAction(formData: FormData) {
  const actor = await requireActor();
  const build = await builds.createBuild(actor, {
    customerId: formString(formData, "customerId"),
    budget: formString(formData, "budget") ? Number(formString(formData, "budget")) : undefined,
    useCase: formString(formData, "useCase"),
    targetWorkloads: formString(formData, "targetWorkloads"),
  });
  redirect(`/builds/${build.id}`);
}

export async function quoteAction(formData: FormData) {
  const actor = await requireActor();
  const quote = await finance.createQuote(actor, {
    customerId: formString(formData, "customerId"),
    issue: formString(formData, "issue"),
    lines: [
      {
        type: "SERVICE",
        description: formString(formData, "description") || "Quoted work",
        quantity: 1,
        unitPrice: Number(formString(formData, "amount") || 0),
      },
    ],
  });
  redirect(`/quotes/${quote.id}`);
}

export async function stockAdjustAction(formData: FormData) {
  const actor = await requireActor();
  await inventory.adjustStock(
    actor,
    formString(formData, "itemId"),
    Number(formString(formData, "quantity")),
    formString(formData, "reason"),
  );
  revalidatePath("/inventory");
}

export async function credentialAction(formData: FormData) {
  const actor = await requireActor();
  await tickets.storeCredential(actor, formString(formData, "ticketId"), formString(formData, "secret"), formString(formData, "label"));
  revalidatePath(`/tickets/${formString(formData, "ticketId")}`);
}

export async function diagnosticStartAction(formData: FormData) {
  const actor = await requireActor();
  await workshop.startDiagnostic(actor, formString(formData, "ticketId"), formString(formData, "templateId"), formString(formData, "phase") || "pre");
  revalidatePath(`/tickets/${formString(formData, "ticketId")}`);
}

export async function diagnosticResultAction(formData: FormData) {
  const actor = await requireActor();
  await workshop.saveDiagnosticResult(
    actor,
    formString(formData, "resultId"),
    formString(formData, "value") as "PASS" | "FAIL" | "NOT_TESTED" | "NOT_APPLICABLE",
  );
  revalidatePath(`/tickets/${formString(formData, "ticketId")}`);
}

export async function issueInvoiceAction(formData: FormData) {
  const actor = await requireActor();
  await finance.issueInvoice(actor, formString(formData, "invoiceId"));
  revalidatePath(`/invoices/${formString(formData, "invoiceId")}`);
}

export async function convertQuoteAction(formData: FormData) {
  const actor = await requireActor();
  const ticket = await finance.convertQuoteToTicket(actor, formString(formData, "quoteId"), formString(formData, "typeId"));
  redirect(`/tickets/${ticket.id}`);
}

export async function quoteApprovalAction(formData: FormData) {
  const actor = await requireActor();
  await finance.recordQuoteApproval(actor, formString(formData, "quoteId"), formString(formData, "status") as "approved" | "declined" | "pending", formString(formData, "notes"));
  revalidatePath(`/quotes/${formString(formData, "quoteId")}`);
}

export async function saveThemeAction(formData: FormData) {
  const actor = await requireActor();
  await auth.savePreferences(actor, {
    theme: formString(formData, "theme"),
    defaultTicketView: formString(formData, "defaultTicketView"),
  });
  revalidatePath("/");
}

export async function createStaffAction(formData: FormData) {
  try {
    const actor = await requirePermission("staff.manage");
    await auth.createStaffUser(actor, {
      name: formString(formData, "name"),
      email: formString(formData, "email"),
      password: formString(formData, "password"),
      roleId: formString(formData, "roleId"),
      phone: formString(formData, "phone") || undefined,
    });
    revalidatePath("/settings");
    redirect("/settings?staff=created");
  } catch (error) {
    if ((error as { digest?: string }).digest?.startsWith("NEXT_REDIRECT")) throw error;
    redirect(`/settings?error=${encodeURIComponent(toPublicError(error).error)}`);
  }
}

export async function updateStaffStatusAction(formData: FormData) {
  try {
    const actor = await requirePermission("staff.manage");
    const status = formString(formData, "status") === "SUSPENDED" ? "SUSPENDED" : "ACTIVE";
    await auth.updateStaffStatus(actor, formString(formData, "userId"), status);
    revalidatePath("/settings");
    redirect(status === "SUSPENDED" ? "/settings?staff=suspended" : "/settings?staff=restored");
  } catch (error) {
    if ((error as { digest?: string }).digest?.startsWith("NEXT_REDIRECT")) throw error;
    redirect(`/settings?error=${encodeURIComponent(toPublicError(error).error)}`);
  }
}
