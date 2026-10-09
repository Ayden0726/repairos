using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using WorkshopOS.Application.Abstractions;
using WorkshopOS.Application.Common;
using WorkshopOS.Contracts.Website;
using WorkshopOS.Domain.Entities;
using WorkshopOS.Infrastructure.Persistence;

namespace WorkshopOS.Infrastructure.Services;

public sealed class MessagingService : IMessagingService
{
    private readonly WorkshopDbContext _db;

    public MessagingService(WorkshopDbContext db) => _db = db;

    public async Task<IReadOnlyList<StaffThreadDto>> ListThreadsAsync(CancellationToken ct = default)
    {
        var rows = await _db.ConversationThreads.AsNoTracking()
            .Include(t => t.Customer).Include(t => t.RepairTicket)
            .OrderByDescending(t => t.UpdatedAt).Take(100).ToListAsync(ct);
        return rows.Select(t => new StaffThreadDto(
            t.Id, t.Subject, t.Status, t.Customer?.DisplayName, t.RepairTicket?.TicketNumber, t.UpdatedAt)).ToList();
    }

    public async Task<IReadOnlyList<StaffMessageDto>> ListMessagesAsync(Guid threadId, bool includeInternal, CancellationToken ct = default)
    {
        var q = _db.ConversationMessages.AsNoTracking().Where(m => m.ThreadId == threadId);
        if (!includeInternal) q = q.Where(m => m.Visibility != "Internal");
        var rows = await q.OrderBy(m => m.CreatedAt).ToListAsync(ct);
        return rows.Select(m => new StaffMessageDto(m.Id, m.Visibility, m.Channel, m.Body, m.AuthorName, m.CreatedAt)).ToList();
    }

    public async Task<StaffMessageDto> PostStaffMessageAsync(Guid threadId, StaffPostMessageRequest request, Guid actorId, CancellationToken ct = default)
    {
        var thread = await _db.ConversationThreads.FirstOrDefaultAsync(t => t.Id == threadId, ct)
            ?? throw new AppException("not_found", "Thread not found.", 404);
        var user = await _db.Users.AsNoTracking().FirstAsync(u => u.Id == actorId, ct);
        var msg = new ConversationMessage
        {
            ThreadId = thread.Id,
            Visibility = string.IsNullOrWhiteSpace(request.Visibility) ? "CustomerVisible" : request.Visibility,
            Channel = string.IsNullOrWhiteSpace(request.Channel) ? "Portal" : request.Channel,
            Body = request.Body.Trim(),
            AuthorUserId = actorId,
            AuthorName = user.DisplayName
        };
        thread.UpdatedAt = DateTimeOffset.UtcNow;
        _db.ConversationMessages.Add(msg);
        await _db.SaveChangesAsync(ct);
        return new StaffMessageDto(msg.Id, msg.Visibility, msg.Channel, msg.Body, msg.AuthorName, msg.CreatedAt);
    }

    public async Task<ConversationThread> EnsureRepairThreadAsync(Guid ticketId, CancellationToken ct = default)
    {
        var existing = await _db.ConversationThreads.FirstOrDefaultAsync(t => t.RepairTicketId == ticketId, ct);
        if (existing is not null) return existing;
        var ticket = await _db.RepairTickets.FirstOrDefaultAsync(t => t.Id == ticketId, ct)
            ?? throw new AppException("not_found", "Ticket not found.", 404);
        var thread = new ConversationThread
        {
            Subject = $"Repair {ticket.TicketNumber}",
            CustomerId = ticket.CustomerId,
            RepairTicketId = ticket.Id
        };
        _db.ConversationThreads.Add(thread);
        await _db.SaveChangesAsync(ct);
        return thread;
    }
}

public sealed class WarrantyService : IWarrantyService
{
    private readonly WorkshopDbContext _db;
    private readonly IAuditService _audit;

    public WarrantyService(WorkshopDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<IReadOnlyList<CustomerWarrantyListItemDto>> ListForCustomerAsync(Guid customerId, CancellationToken ct = default)
    {
        var rows = await _db.Warranties.AsNoTracking().Include(w => w.Device)
            .Where(w => w.CustomerId == customerId).OrderByDescending(w => w.ExpiresAt).ToListAsync(ct);
        return rows.Select(w => new CustomerWarrantyListItemDto(
            w.Id, w.CoverageSummary, w.Status, w.StartsAt, w.ExpiresAt,
            w.Device is null ? null : $"{w.Device.Brand} {w.Device.Model}")).ToList();
    }

    public async Task<WarrantyRecord> CreateForRepairAsync(Guid ticketId, int warrantyDays, string summary, Guid actorId, CancellationToken ct = default)
    {
        var ticket = await _db.RepairTickets.FirstOrDefaultAsync(t => t.Id == ticketId, ct)
            ?? throw new AppException("not_found", "Ticket not found.", 404);
        var starts = DateTimeOffset.UtcNow;
        var row = new WarrantyRecord
        {
            CustomerId = ticket.CustomerId,
            RepairTicketId = ticket.Id,
            DeviceId = ticket.DeviceId,
            CoverageSummary = summary,
            WarrantyDays = warrantyDays,
            StartsAt = starts,
            ExpiresAt = starts.AddDays(warrantyDays),
            Status = "Active"
        };
        _db.Warranties.Add(row);
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(actorId, "warranty.create", "WarrantyRecord", row.Id.ToString(), ct: ct);
        return row;
    }
}

public sealed class NotificationTemplateService : INotificationTemplateService
{
    private readonly WorkshopDbContext _db;

    public NotificationTemplateService(WorkshopDbContext db) => _db = db;

    public async Task<IReadOnlyList<NotificationTemplateDto>> ListAsync(CancellationToken ct = default)
    {
        var rows = await _db.NotificationTemplates.AsNoTracking().OrderBy(t => t.Key).ToListAsync(ct);
        return rows.Select(t => new NotificationTemplateDto(t.Id, t.Key, t.Name, t.Channel, t.Subject, t.Body, t.IsEnabled)).ToList();
    }

    public async Task<NotificationTemplateDto> UpsertAsync(string key, UpsertNotificationTemplateRequest request, CancellationToken ct = default)
    {
        var row = await _db.NotificationTemplates.FirstOrDefaultAsync(t => t.Key == key, ct);
        if (row is null)
        {
            row = new NotificationTemplate { Key = key, Name = request.Name ?? key };
            _db.NotificationTemplates.Add(row);
        }
        row.Name = request.Name ?? row.Name;
        row.Channel = request.Channel;
        row.Subject = request.Subject;
        row.Body = request.Body;
        row.IsEnabled = request.IsEnabled;
        row.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return new NotificationTemplateDto(row.Id, row.Key, row.Name, row.Channel, row.Subject, row.Body, row.IsEnabled);
    }

    public async Task QueueAsync(string templateKey, string to, Guid? customerId, Guid? ticketId, IReadOnlyDictionary<string, string> tokens, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(to)) return;
        var template = await _db.NotificationTemplates.AsNoTracking().FirstOrDefaultAsync(t => t.Key == templateKey, ct);
        var subject = template?.Subject ?? templateKey;
        var body = template?.Body ?? templateKey;
        if (template is { IsEnabled: false }) return;
        foreach (var (k, v) in tokens)
        {
            subject = subject.Replace("{{" + k + "}}", v, StringComparison.OrdinalIgnoreCase);
            body = body.Replace("{{" + k + "}}", v, StringComparison.OrdinalIgnoreCase);
        }

        // Outbound providers (Twilio/SMTP) are configured later — queue for worker delivery.
        _db.NotificationOutboxItems.Add(new NotificationOutbox
        {
            TemplateKey = templateKey,
            Channel = template?.Channel ?? (to.Contains('@') ? "Email" : "Sms"),
            ToAddress = to,
            Subject = subject,
            Body = body,
            Status = "Pending",
            CustomerId = customerId,
            RepairTicketId = ticketId
        });
        await _db.SaveChangesAsync(ct);
    }
}

public sealed class WebsiteIntegrationService : IWebsiteIntegrationService
{
    private readonly WorkshopDbContext _db;
    private readonly IAuditService _audit;
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _http;

    public WebsiteIntegrationService(
        WorkshopDbContext db, IAuditService audit, IConfiguration config, IHttpClientFactory http)
    {
        _db = db;
        _audit = audit;
        _config = config;
        _http = http;
    }

    public async Task EnsureWebsiteDefaultsAsync(CancellationToken ct = default)
    {
        await EnsureExtraStatusesAsync(ct);
        await EnsureNotificationTemplatesAsync(ct);
        await EnsureRefurbChecklistsAsync(ct);
    }

    public async Task<IReadOnlyList<WebsiteApiKeyDto>> ListApiKeysAsync(CancellationToken ct = default)
    {
        var rows = await _db.WebsiteApiKeys.AsNoTracking()
            .Where(k => k.ArchivedAt == null).OrderByDescending(k => k.CreatedAt).ToListAsync(ct);
        return rows.Select(k => new WebsiteApiKeyDto(k.Id, k.Name, k.KeyPrefix, k.IsActive, k.CreatedAt, k.LastUsedAt)).ToList();
    }

    public async Task<CreateWebsiteApiKeyResponse> CreateApiKeyAsync(CreateWebsiteApiKeyRequest request, Guid actorId, CancellationToken ct = default)
    {
        var raw = "wos_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
        var prefix = raw[..12];
        var row = new WebsiteApiKey
        {
            Name = request.Name.Trim(),
            KeyPrefix = prefix,
            KeyHash = WebsiteAccess.HashPin(raw),
            AllowedOrigins = request.AllowedOrigins,
            CreatedById = actorId,
            IsActive = true
        };
        _db.WebsiteApiKeys.Add(row);
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(actorId, "website.api_key.create", "WebsiteApiKey", row.Id.ToString(), ct: ct);
        return new CreateWebsiteApiKeyResponse(row.Id, row.Name, raw, prefix);
    }

    public async Task RevokeApiKeyAsync(Guid id, CancellationToken ct = default)
    {
        var row = await _db.WebsiteApiKeys.FirstOrDefaultAsync(k => k.Id == id, ct)
            ?? throw new AppException("not_found", "API key not found.", 404);
        row.IsActive = false;
        row.ArchivedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<bool> ValidateApiKeyAsync(string apiKey, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey)) return false;
        var hash = WebsiteAccess.HashPin(apiKey.Trim());
        var row = await _db.WebsiteApiKeys.FirstOrDefaultAsync(k => k.KeyHash == hash && k.IsActive && k.ArchivedAt == null, ct);
        if (row is null) return false;
        row.LastUsedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<WooCommerceSettingsDto> GetWooSettingsAsync(CancellationToken ct = default)
    {
        var stored = await DbSeed.GetSettingAsync(_db, "website.woocommerce", new WooStoredSettings(), ct);
        return new WooCommerceSettingsDto(
            stored.Enabled,
            stored.StoreUrl,
            !string.IsNullOrWhiteSpace(stored.ConsumerKey),
            !string.IsNullOrWhiteSpace(stored.ConsumerSecret),
            stored.DefaultWarrantyText ?? "90-day local warranty");
    }

    public async Task<WooCommerceSettingsDto> SaveWooSettingsAsync(UpdateWooCommerceSettingsRequest request, Guid actorId, CancellationToken ct = default)
    {
        var existing = await DbSeed.GetSettingAsync(_db, "website.woocommerce", new WooStoredSettings(), ct);
        var payload = new WooStoredSettings
        {
            Enabled = request.Enabled,
            StoreUrl = request.StoreUrl,
            ConsumerKey = string.IsNullOrWhiteSpace(request.ConsumerKey) ? existing.ConsumerKey : request.ConsumerKey,
            ConsumerSecret = string.IsNullOrWhiteSpace(request.ConsumerSecret) ? existing.ConsumerSecret : request.ConsumerSecret,
            DefaultWarrantyText = request.DefaultWarrantyText ?? existing.DefaultWarrantyText ?? "90-day local warranty"
        };
        await DbSeed.SetSettingAsync(_db, "website.woocommerce", payload, actorId, ct);
        await _audit.WriteAsync(actorId, "website.woo.settings", "Setting", "website.woocommerce", ct: ct);
        return await GetWooSettingsAsync(ct);
    }

    private sealed class WooStoredSettings
    {
        public bool Enabled { get; set; }
        public string? StoreUrl { get; set; }
        public string? ConsumerKey { get; set; }
        public string? ConsumerSecret { get; set; }
        public string? DefaultWarrantyText { get; set; }
    }

    public async Task<PublishRefurbishedResponse> PublishRefurbishedAsync(PublishRefurbishedRequest request, Guid actorId, CancellationToken ct = default)
    {
        var device = await _db.UsedDevices.FirstOrDefaultAsync(u => u.Id == request.UsedDeviceId && u.ArchivedAt == null, ct)
            ?? throw new AppException("not_found", "Refurbished device not found.", 404);
        var stored = await DbSeed.GetSettingAsync(_db, "website.woocommerce", new WooStoredSettings(), ct);
        device.Status = "Listed";
        device.ExpectedResale = request.SalePriceOverride ?? device.ExpectedResale;
        await _db.SaveChangesAsync(ct);

        var stockId = device.Notes?.StartsWith("Stock ") == true
            ? device.Notes.Split(' ', 3)[1].TrimEnd('.')
            : device.Id.ToString("N")[..8].ToUpperInvariant();

        string? externalId = null;
        string message;
        if (stored.Enabled &&
            !string.IsNullOrWhiteSpace(stored.StoreUrl) &&
            !string.IsNullOrWhiteSpace(stored.ConsumerKey) &&
            !string.IsNullOrWhiteSpace(stored.ConsumerSecret))
        {
            try
            {
                externalId = await PushWooProductAsync(device, stockId, stored, ct);
                message = $"Published to WooCommerce as product {externalId}.";
            }
            catch (Exception ex)
            {
                message = $"Marked Listed in WorkshopOS. WooCommerce push failed: {ex.Message}";
            }
        }
        else
        {
            message = "Marked Listed in WorkshopOS. WordPress plugin can pull this product, or enable WooCommerce credentials to push automatically.";
        }

        await _audit.WriteAsync(actorId, "website.woo.publish", "UsedDevice", device.Id.ToString(),
            newValue: new { stockId, stored.Enabled, stored.StoreUrl, externalId }, ct: ct);
        await EmitWebhookAsync("refurb.listed", new
        {
            usedDeviceId = device.Id,
            stockId,
            sku = stockId,
            salePrice = device.ExpectedResale,
            title = device.Summary,
            brand = device.Brand,
            model = device.Model,
            storage = device.StorageCapacity,
            colour = device.Colour,
            condition = device.ConditionGrade,
            externalProductId = externalId
        }, ct);

        return new PublishRefurbishedResponse(device.Id, stockId, externalId, message);
    }

    private async Task<string> PushWooProductAsync(UsedDevice device, string stockId, WooStoredSettings stored, CancellationToken ct)
    {
        var baseUrl = stored.StoreUrl!.TrimEnd('/');
        var client = _http.CreateClient("woocommerce");
        var creds = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{stored.ConsumerKey}:{stored.ConsumerSecret}"));
        using var req = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/wp-json/wc/v3/products");
        req.Headers.Authorization = new AuthenticationHeaderValue("Basic", creds);
        var body = new
        {
            name = device.Summary,
            type = "simple",
            regular_price = device.ExpectedResale.ToString("0.00"),
            sku = stockId,
            manage_stock = true,
            stock_quantity = 1,
            description = string.Join("\n", new[]
            {
                device.Specs,
                device.Faults is null ? null : $"Notes: {device.Faults}",
                stored.DefaultWarrantyText
            }.Where(x => !string.IsNullOrWhiteSpace(x))),
            meta_data = new object[]
            {
                new { key = "workshopos_stock_id", value = stockId },
                new { key = "workshopos_used_device_id", value = device.Id.ToString() },
                new { key = "condition_grade", value = device.ConditionGrade },
                new { key = "storage", value = device.StorageCapacity ?? "" },
                new { key = "colour", value = device.Colour ?? "" }
            }
        };
        req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        using var res = await client.SendAsync(req, ct);
        var text = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException($"WooCommerce HTTP {(int)res.StatusCode}: {text[..Math.Min(200, text.Length)]}");
        using var doc = JsonDocument.Parse(text);
        return doc.RootElement.TryGetProperty("id", out var idEl) ? idEl.ToString() : stockId;
    }

    public async Task<WebsiteWebhookSettingsDto> GetWebhookSettingsAsync(CancellationToken ct = default)
    {
        var s = await DbSeed.GetSettingAsync(_db, "website.webhooks", new WebhookStoredSettings(), ct);
        return new WebsiteWebhookSettingsDto(s.Enabled, s.Url, !string.IsNullOrWhiteSpace(s.Secret));
    }

    public async Task<WebsiteWebhookSettingsDto> SaveWebhookSettingsAsync(UpdateWebsiteWebhookSettingsRequest request, Guid actorId, CancellationToken ct = default)
    {
        var existing = await DbSeed.GetSettingAsync(_db, "website.webhooks", new WebhookStoredSettings(), ct);
        var payload = new WebhookStoredSettings
        {
            Enabled = request.Enabled,
            Url = request.Url?.Trim(),
            Secret = string.IsNullOrWhiteSpace(request.Secret) ? existing.Secret : request.Secret
        };
        await DbSeed.SetSettingAsync(_db, "website.webhooks", payload, actorId, ct);
        await _audit.WriteAsync(actorId, "website.webhook.settings", "Setting", "website.webhooks", ct: ct);
        return await GetWebhookSettingsAsync(ct);
    }

    public async Task EmitWebhookAsync(string eventName, object payload, CancellationToken ct = default)
    {
        var s = await DbSeed.GetSettingAsync(_db, "website.webhooks", new WebhookStoredSettings(), ct);
        if (!s.Enabled || string.IsNullOrWhiteSpace(s.Url)) return;
        try
        {
            var client = _http.CreateClient("woocommerce");
            using var req = new HttpRequestMessage(HttpMethod.Post, s.Url);
            if (!string.IsNullOrWhiteSpace(s.Secret))
                req.Headers.TryAddWithoutValidation("X-WorkshopOS-Webhook-Secret", s.Secret);
            req.Headers.TryAddWithoutValidation("X-WorkshopOS-Event", eventName);
            var envelope = new { id = Guid.NewGuid(), @event = eventName, at = DateTimeOffset.UtcNow, data = payload };
            req.Content = new StringContent(JsonSerializer.Serialize(envelope), Encoding.UTF8, "application/json");
            using var res = await client.SendAsync(req, ct);
            if (!res.IsSuccessStatusCode)
                Console.WriteLine($"Webhook {eventName} failed: {(int)res.StatusCode}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Webhook {eventName} error: {ex.Message}");
        }
    }

    public async Task<BookingAvailabilitySettingsDto> GetBookingAvailabilitySettingsAsync(CancellationToken ct = default) =>
        await DbSeed.GetSettingAsync(_db, "website.booking_availability", new BookingAvailabilitySettingsDto(9, 17, 60, 3), ct);

    public async Task<BookingAvailabilitySettingsDto> SaveBookingAvailabilitySettingsAsync(BookingAvailabilitySettingsDto settings, Guid actorId, CancellationToken ct = default)
    {
        var open = Math.Clamp(settings.OpenHour, 0, 23);
        var close = Math.Clamp(settings.CloseHour, 1, 24);
        if (close <= open) throw new ValidationAppException("CloseHour must be after OpenHour.");
        var payload = new BookingAvailabilitySettingsDto(open, close, Math.Clamp(settings.SlotMinutes, 15, 240), Math.Clamp(settings.Capacity, 1, 50));
        await DbSeed.SetSettingAsync(_db, "website.booking_availability", payload, actorId, ct);
        return payload;
    }

    private sealed class WebhookStoredSettings
    {
        public bool Enabled { get; set; }
        public string? Url { get; set; }
        public string? Secret { get; set; }
    }

    public async Task<WebsiteDashboardCardsDto> GetWebsiteDashboardAsync(CancellationToken ct = default)
    {
        var open = await _db.RepairTickets.AsNoTracking().Include(t => t.Status)
            .Where(t => t.ArchivedAt == null && !t.Status.IsCompleted && !t.Status.IsCancelled).ToListAsync(ct);
        var sell = await _db.SellDeviceRequests.AsNoTracking().ToListAsync(ct);
        var used = await _db.UsedDevices.AsNoTracking().Where(u => u.ArchivedAt == null).ToListAsync(ct);
        var lowStock = await _db.InventoryItems.AsNoTracking()
            .CountAsync(i => i.ArchivedAt == null && (i.QuantityOnHand - i.QuantityReserved) <= i.MinimumStock, ct);
        var today = DateTimeOffset.Now.Date;
        var appointments = await _db.Bookings.AsNoTracking()
            .CountAsync(b => b.Status != "Cancelled" && b.StartsAt >= today && b.StartsAt < today.AddDays(1), ct);
        var outstanding = await _db.Invoices.AsNoTracking()
            .CountAsync(i => i.ArchivedAt == null && i.AmountPaid < i.Total && i.Status != "Cancelled", ct);

        return new WebsiteDashboardCardsDto(
            open.Count(t => t.Status.Key is "diagnosing" or "received" or "booking_received"),
            open.Count(t => t.Status.CountsAsAwaitingApproval || t.Status.Key == "awaiting_approval"),
            open.Count(t => t.Status.Key is "repairing" or "approved" or "parts_arrived"),
            open.Count(t => t.Status.Key is "testing" or "quality_check"),
            open.Count(t => t.Status.CountsAsReadyForPickup),
            sell.Count(s => s.Status is "New" or "Awaiting Review"),
            sell.Count(s => s.Status == "Offer Sent"),
            used.Count(u => u.Status is "Purchased" or "Inspection" or "Parts Required" or "Repair" or "Testing" or "Cleaning" or "Final Quality Check"),
            used.Count(u => u.Status is "Ready for Sale" or "In stock"),
            lowStock,
            appointments,
            outstanding);
    }

    public async Task<QrPayloadDto> BuildQrAsync(string kind, Guid id, CancellationToken ct = default)
    {
        var baseUrl = _config["Website:PublicBaseUrl"] ?? "https://techbenchnq.com.au";
        return kind.ToLowerInvariant() switch
        {
            "repair" or "ticket" => await QrRepairAsync(id, baseUrl, ct),
            "invoice" => await QrInvoiceAsync(id, baseUrl, ct),
            "refurb" or "used" => await QrRefurbAsync(id, baseUrl, ct),
            "part" or "inventory" => await QrPartAsync(id, baseUrl, ct),
            _ => throw new ValidationAppException("Unknown QR kind. Use repair, invoice, refurb, or part.")
        };
    }

    private async Task<QrPayloadDto> QrRepairAsync(Guid id, string baseUrl, CancellationToken ct)
    {
        var t = await _db.RepairTickets.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new AppException("not_found", "Repair not found.", 404);
        var link = string.IsNullOrWhiteSpace(t.PublicAccessToken)
            ? $"{baseUrl}/repair-status?code={Uri.EscapeDataString(t.TicketNumber)}"
            : $"{baseUrl}/repair-status?token={Uri.EscapeDataString(t.PublicAccessToken)}";
        var payload = $"workshopos:repair:{t.Id}:{t.TicketNumber}";
        return new QrPayloadDto("repair", t.Id.ToString(), t.TicketNumber, payload, link);
    }

    private async Task<QrPayloadDto> QrInvoiceAsync(Guid id, string baseUrl, CancellationToken ct)
    {
        var i = await _db.Invoices.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new AppException("not_found", "Invoice not found.", 404);
        return new QrPayloadDto("invoice", i.Id.ToString(), i.Number, $"workshopos:invoice:{i.Id}", $"{baseUrl}/portal/invoices/{i.Id}");
    }

    private async Task<QrPayloadDto> QrRefurbAsync(Guid id, string baseUrl, CancellationToken ct)
    {
        var u = await _db.UsedDevices.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new AppException("not_found", "Device not found.", 404);
        return new QrPayloadDto("refurb", u.Id.ToString(), u.Summary, $"workshopos:refurb:{u.Id}", $"{baseUrl}/shop?refurb={u.Id}");
    }

    private async Task<QrPayloadDto> QrPartAsync(Guid id, string baseUrl, CancellationToken ct)
    {
        var p = await _db.InventoryItems.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new AppException("not_found", "Part not found.", 404);
        return new QrPayloadDto("part", p.Id.ToString(), p.Sku, $"workshopos:part:{p.Id}:{p.Sku}", $"{baseUrl}/parts/{p.Sku}");
    }

    private async Task EnsureExtraStatusesAsync(CancellationToken ct)
    {
        (string Key, string Name, string Colour, int Sort, bool Parts, bool Approval, bool Pickup)[] extra =
        [
            ("booking_received", "Booking Received", "#64748b", 5, false, false, false),
            ("awaiting_device", "Awaiting Device", "#94a3b8", 8, false, false, false),
            ("diagnosis_complete", "Diagnosis Complete", "#2563eb", 25, false, false, false),
            ("approved", "Approved", "#ca8a04", 32, false, false, false),
            ("parts_ordered", "Parts Ordered", "#c2410c", 42, true, false, false),
            ("parts_arrived", "Parts Arrived", "#ea580c", 45, false, false, false),
            ("quality_check", "Quality Check", "#0f766e", 75, false, false, false)
        ];
        foreach (var s in extra)
        {
            if (await _db.RepairStatuses.AnyAsync(x => x.Key == s.Key, ct)) continue;
            _db.RepairStatuses.Add(new RepairStatus
            {
                Key = s.Key,
                Name = s.Name,
                Colour = s.Colour,
                SortOrder = s.Sort,
                IsSystem = true,
                CountsAsWaitingForParts = s.Parts,
                CountsAsAwaitingApproval = s.Approval,
                CountsAsReadyForPickup = s.Pickup
            });
        }
        await _db.SaveChangesAsync(ct);
    }

    private async Task EnsureNotificationTemplatesAsync(CancellationToken ct)
    {
        (string Key, string Name, string Subject, string Body)[] templates =
        [
            ("booking_confirmed", "Booking confirmed", "Booking confirmed — {{tracking}}", "Hi {{customer}}, your booking is confirmed. Tracking {{tracking}}, PIN {{pin}}."),
            ("device_checked_in", "Device checked in", "Device checked in — {{tracking}}", "We have checked in your device {{tracking}}."),
            ("diagnosis_complete", "Diagnosis complete", "Diagnosis complete — {{tracking}}", "Diagnosis is complete for {{tracking}}."),
            ("quote_ready", "Quote ready", "Your quote is ready — {{quote}}", "Your repair quote {{quote}} is ready to review."),
            ("quote_approved", "Quote approved", "Quote approved — {{quote}}", "Thanks — quote {{quote}} was approved."),
            ("parts_ordered", "Parts ordered", "Parts ordered — {{tracking}}", "Parts have been ordered for {{tracking}}."),
            ("repair_started", "Repair started", "Repair started — {{tracking}}", "Repair work has started on {{tracking}}."),
            ("testing_begun", "Testing begun", "Testing — {{tracking}}", "Your device {{tracking}} is in testing."),
            ("ready_for_collection", "Ready for collection", "Ready for collection — {{tracking}}", "Great news — {{tracking}} is ready for collection."),
            ("payment_received", "Payment received", "Payment received", "We have received your payment. Thank you."),
            ("repair_completed", "Repair completed", "Repair completed — {{tracking}}", "Repair {{tracking}} is complete."),
            ("warranty_expiring", "Warranty expiring", "Warranty reminder", "Your warranty for {{device}} expires on {{expires}}."),
            ("sell_request_received", "Sell request received", "Sell Your Tech — {{reference}}", "We received your Sell Your Tech request {{reference}} for {{device}}.")
        ];
        foreach (var t in templates)
        {
            if (await _db.NotificationTemplates.AnyAsync(x => x.Key == t.Key, ct)) continue;
            _db.NotificationTemplates.Add(new NotificationTemplate
            {
                Key = t.Key,
                Name = t.Name,
                Channel = "Both",
                Subject = t.Subject,
                Body = t.Body,
                IsEnabled = true
            });
        }
        await _db.SaveChangesAsync(ct);
    }

    private async Task EnsureRefurbChecklistsAsync(CancellationToken ct)
    {
        if (await _db.RefurbDiagnosticChecklists.AnyAsync(ct)) return;
        string[] phone =
        [
            "IMEI/serial check", "Activation/account lock", "Display", "Touch", "Cameras", "Face ID/Touch ID",
            "Speakers", "Microphones", "Charging", "Wireless charging", "Wi-Fi", "Bluetooth", "Cellular",
            "Battery", "Buttons", "Ports", "Liquid damage indicators", "Cosmetic condition", "Factory reset", "Final diagnostics"
        ];
        string[] laptop =
        [
            "Serial check", "Account lock", "Display", "Keyboard", "Trackpad", "Webcam", "Speakers", "Microphone",
            "Charging", "Wi-Fi", "Bluetooth", "USB/ports", "Battery", "Cosmetic condition", "Factory reset", "Final diagnostics"
        ];
        _db.RefurbDiagnosticChecklists.Add(new RefurbDiagnosticChecklist
        {
            Category = "Phone",
            Name = "Phone / tablet diagnostics",
            ItemsJson = JsonSerializer.Serialize(phone)
        });
        _db.RefurbDiagnosticChecklists.Add(new RefurbDiagnosticChecklist
        {
            Category = "Laptop",
            Name = "Laptop / MacBook diagnostics",
            ItemsJson = JsonSerializer.Serialize(laptop)
        });
        await _db.SaveChangesAsync(ct);
    }
}
