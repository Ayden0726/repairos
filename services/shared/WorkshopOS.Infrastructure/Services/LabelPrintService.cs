using System.Net;
using Microsoft.EntityFrameworkCore;
using WorkshopOS.Application.Abstractions;
using WorkshopOS.Application.Common;
using WorkshopOS.Contracts.Auth;
using WorkshopOS.Infrastructure.Persistence;

namespace WorkshopOS.Infrastructure.Services;

/// <summary>
/// Monochrome HTML label templates (PDF/printer fallback). Barcodes use internal IDs, not raw IMEI.
/// Battery health is only shown when recorded — never invented.
/// </summary>
public sealed class LabelPrintService : ILabelPrintService
{
    private readonly WorkshopDbContext _db;

    public LabelPrintService(WorkshopDbContext db) => _db = db;

    public async Task<string> RenderRepairLabelHtmlAsync(Guid repairId, CancellationToken ct = default)
    {
        var t = await _db.RepairTickets.AsNoTracking()
            .Include(x => x.Customer)
            .Include(x => x.Device)
            .Include(x => x.Status)
            .FirstOrDefaultAsync(x => x.Id == repairId && x.ArchivedAt == null, ct)
            ?? throw new AppException("not_found", "Repair ticket was not found.", 404);

        var profile = await LoadBusinessNameAsync(ct);
        var device = t.Device;
        var brandModel = string.Join(" ", new[] { device?.Brand, device?.Model }.Where(s => !string.IsNullOrWhiteSpace(s)));
        var idCode = t.TicketNumber;
        var serialOrImei = First(device?.Serial, MaskImei(device?.Imei));

        var body =
            $"<div class=\"brand\">{Esc(profile)}</div>" +
            $"<div class=\"title\">{Esc(t.TicketNumber)}</div>" +
            $"<div class=\"row\"><span>Device</span><b>{Esc(NullDash(brandModel))}</b></div>" +
            $"<div class=\"row\"><span>Storage</span><b>{Esc(NullDash(device?.StorageCapacity))}</b></div>" +
            $"<div class=\"row\"><span>Colour</span><b>{Esc(NullDash(device?.Colour))}</b></div>" +
            $"<div class=\"row\"><span>Serial / IMEI</span><b>{Esc(NullDash(serialOrImei))}</b></div>" +
            $"<div class=\"row\"><span>Fault</span><b>{Esc(NullDash(t.ReportedIssue))}</b></div>" +
            $"<div class=\"row\"><span>Status</span><b>{Esc(t.Status.Name)}</b></div>" +
            $"<div class=\"row\"><span>Received</span><b>{Esc(t.CreatedAt.ToLocalTime().ToString("d MMM yyyy"))}</b></div>" +
            $"<div class=\"row\"><span>Customer</span><b>{Esc(t.Customer.DisplayName)}</b></div>" +
            $"<div class=\"code\">{Esc(idCode)}</div>" +
            "<div class=\"hint\">Lookup by job number (internal id). Full IMEI is not printed by default.</div>";

        return Wrap("Repair job label", body);
    }

    public async Task<string> RenderUsedDeviceLabelHtmlAsync(Guid usedDeviceId, bool includePrice, CancellationToken ct = default)
    {
        var d = await _db.UsedDevices.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == usedDeviceId && x.ArchivedAt == null, ct)
            ?? throw new AppException("not_found", "Refurbished device was not found.", 404);

        var profile = await LoadBusinessNameAsync(ct);
        var brandModel = string.Join(" ", new[] { d.Brand, d.Model }.Where(s => !string.IsNullOrWhiteSpace(s)));
        var stockId = First(d.StockId, d.Sku, d.Barcode, d.Id.ToString("N")[..8].ToUpperInvariant())!;
        var battery = d.BatteryHealthPercent is int pct ? $"{pct}%" : "Untested";
        var priceRow = includePrice
            ? $"<div class=\"row\"><span>Price</span><b>{Esc(d.ExpectedResale.ToString("C"))}</b></div>"
            : "";

        var body =
            $"<div class=\"brand\">{Esc(profile)}</div>" +
            $"<div class=\"title\">{Esc(stockId)}</div>" +
            $"<div class=\"row\"><span>Device</span><b>{Esc(NullDash(brandModel))}</b></div>" +
            $"<div class=\"row\"><span>Storage</span><b>{Esc(NullDash(d.StorageCapacity))}</b></div>" +
            $"<div class=\"row\"><span>Colour</span><b>{Esc(NullDash(d.Colour))}</b></div>" +
            $"<div class=\"row\"><span>Battery</span><b>{Esc(battery)}</b></div>" +
            $"<div class=\"row\"><span>Grade</span><b>{Esc(d.ConditionGrade)}</b></div>" +
            $"<div class=\"row\"><span>Functional</span><b>{Esc(NullDash(d.FunctionalGrade) ?? "Untested")}</b></div>" +
            $"<div class=\"row\"><span>Status</span><b>{Esc(d.Status)}</b></div>" +
            priceRow +
            $"<div class=\"code\">{Esc(stockId)}</div>" +
            "<div class=\"hint\">Barcode value is the internal stock id.</div>";

        return Wrap("Refurbished stock label", body);
    }

    public async Task<string> RenderPartLabelHtmlAsync(Guid inventoryItemId, CancellationToken ct = default)
    {
        var i = await _db.InventoryItems.AsNoTracking()
            .Include(x => x.Supplier)
            .FirstOrDefaultAsync(x => x.Id == inventoryItemId && x.ArchivedAt == null, ct)
            ?? throw new AppException("not_found", "Inventory item was not found.", 404);

        var profile = await LoadBusinessNameAsync(ct);
        var code = First(i.Barcode, i.Sku)!;

        var body =
            $"<div class=\"brand\">{Esc(profile)}</div>" +
            $"<div class=\"title\">{Esc(i.Sku)}</div>" +
            $"<div class=\"row\"><span>Name</span><b>{Esc(i.Name)}</b></div>" +
            $"<div class=\"row\"><span>Category</span><b>{Esc(i.Category)}</b></div>" +
            $"<div class=\"row\"><span>Type</span><b>{Esc(i.ComponentType)}</b></div>" +
            $"<div class=\"row\"><span>Location</span><b>{Esc(NullDash(i.LocationBin))}</b></div>" +
            $"<div class=\"row\"><span>Supplier</span><b>{Esc(NullDash(i.Supplier?.Name))}</b></div>" +
            $"<div class=\"row\"><span>On hand</span><b>{i.QuantityOnHand}</b></div>" +
            $"<div class=\"code\">{Esc(code)}</div>";

        return Wrap("Parts label", body);
    }

    private async Task<string> LoadBusinessNameAsync(CancellationToken ct)
    {
        try
        {
            var profile = await DbSeed.GetSettingAsync(
                _db,
                SettingKeys.BusinessProfile,
                new BusinessProfileDto("WorkshopOS", null, null, null, null, null, null, null, null, "#0F766E", true, 0.10m, "AUD", 110m),
                ct);
            return string.IsNullOrWhiteSpace(profile.Name) ? "WorkshopOS" : profile.Name.Trim();
        }
        catch
        {
            return "WorkshopOS";
        }
    }

    private static string Wrap(string title, string body) =>
        "<!DOCTYPE html><html><head><meta charset=\"utf-8\"/><title>" + Esc(title) + "</title>" +
        "<style>" +
        "body{font-family:Segoe UI,Arial,sans-serif;color:#000;background:#fff;margin:0;padding:8px}" +
        ".label{width:90mm;border:1px solid #000;padding:8px;box-sizing:border-box}" +
        ".brand{font-size:11pt;font-weight:700;letter-spacing:.04em;text-transform:uppercase}" +
        ".title{font-size:16pt;font-weight:700;margin:6px 0 8px;border-bottom:2px solid #000;padding-bottom:4px}" +
        ".row{display:flex;justify-content:space-between;gap:8px;font-size:9pt;margin:3px 0}" +
        ".row span{opacity:.75}" +
        ".code{margin-top:10px;font-family:ui-monospace,Consolas,monospace;font-size:12pt;font-weight:700;" +
        "border:1px dashed #000;padding:6px;text-align:center;letter-spacing:.08em}" +
        ".hint{font-size:7.5pt;margin-top:6px;opacity:.7}" +
        "@media print{body{padding:0}.label{border:none}}" +
        "</style></head><body>" +
        "<button onclick=\"window.print()\" style=\"margin-bottom:8px\">Print</button>" +
        "<div class=\"label\">" + body + "</div></body></html>";

    private static string Esc(string? s) => WebUtility.HtmlEncode(s ?? "");
    private static string NullDash(string? s) => string.IsNullOrWhiteSpace(s) ? "—" : s.Trim();
    private static string? First(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim();

    private static string? MaskImei(string? imei)
    {
        if (string.IsNullOrWhiteSpace(imei)) return null;
        var digits = new string(imei.Where(char.IsDigit).ToArray());
        if (digits.Length < 8) return "••••";
        return digits[..4] + new string('•', Math.Max(0, digits.Length - 8)) + digits[^4..];
    }
}
