import PDFDocument from "pdfkit";
import QRCode from "qrcode";
import { prisma } from "../db";
import { getSetting, type BusinessProfile } from "../config/settings";
import { formatAud, toNumber } from "../money";
import { readUpload } from "../storage";

async function business(): Promise<BusinessProfile> {
  return getSetting("business.profile", { name: "Workshop" });
}

function header(doc: PDFKit.PDFDocument, biz: BusinessProfile, title: string, number: string) {
  doc.fontSize(18).text(biz.name || "WorkshopOS", { continued: false });
  doc.fontSize(9).fillColor("#475569").text([biz.addressLine1, biz.suburb, biz.state, biz.postcode].filter(Boolean).join(", "));
  doc.text([biz.phone, biz.email].filter(Boolean).join(" · "));
  doc.moveDown();
  doc.fillColor("#0f172a").fontSize(16).text(title);
  doc.fontSize(10).text(number);
  doc.moveDown();
}

export async function invoicePdf(invoiceId: string): Promise<Buffer> {
  const invoice = await prisma.invoice.findUnique({
    where: { id: invoiceId },
    include: { customer: true, lines: true, ticket: true },
  });
  if (!invoice) throw new Error("Invoice not found");
  const biz = await business();
  const doc = new PDFDocument({ size: "A4", margin: 50 });
  const chunks: Buffer[] = [];
  doc.on("data", (c) => chunks.push(c as Buffer));
  header(doc, biz, "Tax Invoice", invoice.number);
  doc.fontSize(11).text(invoice.customer.displayName);
  if (invoice.customer.abn) doc.fontSize(9).text(`ABN ${invoice.customer.abn}`);
  doc.moveDown();
  invoice.lines.forEach((line) => {
    doc.fontSize(10).text(`${line.description}  × ${line.quantity}    ${formatAud(line.unitPrice)}`);
  });
  doc.moveDown();
  doc.text(`Subtotal ${formatAud(invoice.subtotal)}`);
  doc.text(`GST ${formatAud(invoice.gst)}`);
  doc.fontSize(12).text(`Total ${formatAud(invoice.total)}`);
  doc.fontSize(9).fillColor("#64748b").text("Prices in AUD. GST treatment follows the shop GST registration setting.");
  doc.end();
  return await new Promise((resolve) => doc.on("end", () => resolve(Buffer.concat(chunks))));
}

export async function quotePdf(quoteId: string): Promise<Buffer> {
  const quote = await prisma.quote.findUnique({ where: { id: quoteId }, include: { customer: true, lines: true } });
  if (!quote) throw new Error("Quote not found");
  const biz = await business();
  const doc = new PDFDocument({ size: "A4", margin: 50 });
  const chunks: Buffer[] = [];
  doc.on("data", (c) => chunks.push(c as Buffer));
  header(doc, biz, "Quote", quote.number);
  doc.fontSize(11).text(quote.customer.displayName);
  doc.moveDown();
  quote.lines.forEach((line) => doc.fontSize(10).text(`${line.description}  ${formatAud(line.unitPrice)}`));
  doc.moveDown();
  doc.fontSize(12).text(`Total ${formatAud(quote.total)}`);
  if (quote.expiryDate) doc.fontSize(9).text(`Valid until ${quote.expiryDate.toLocaleDateString("en-AU")}`);
  doc.end();
  return await new Promise((resolve) => doc.on("end", () => resolve(Buffer.concat(chunks))));
}

export async function labelPng(ticketId: string): Promise<Buffer> {
  const ticket = await prisma.ticket.findUnique({
    where: { id: ticketId },
    include: { customer: true, customerDevice: true, priority: true },
  });
  if (!ticket) throw new Error("Ticket not found");
  const url = `${process.env.APP_URL ?? ""}/tickets/${ticket.id}`;
  return QRCode.toBuffer(url || ticket.ticketNumber, { width: 256, margin: 1 });
}

export { toNumber, readUpload };
