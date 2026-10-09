using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using WorkshopOS.Application.Abstractions;
using WorkshopOS.Application.Common;
using WorkshopOS.Contracts.Operations;
using WorkshopOS.Contracts.Website;
using WorkshopOS.Contracts.Workshop;
using WorkshopOS.Domain.Entities;
using WorkshopOS.Infrastructure.Persistence;
using WorkshopOS.Infrastructure.Security;

namespace WorkshopOS.Infrastructure.Services;

internal static class WebsiteAccess
{
    public static string GeneratePin() => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

    public static string HashPin(string pin)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(pin.Trim()));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static bool VerifyPin(string pin, string? hash) =>
        !string.IsNullOrWhiteSpace(hash) &&
        string.Equals(HashPin(pin), hash, StringComparison.OrdinalIgnoreCase);

    public static string GenerateToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
        .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static string MaskSerial(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length < 4) return "••••";
        return new string('•', Math.Max(0, value.Length - 4)) + value[^4..];
    }
}

public sealed class OfferCalculator
{
    public static SellDeviceOfferDto Calculate(
        decimal estimatedResale,
        decimal parts,
        decimal labour,
        decimal sellingFeePercent,
        decimal shipping,
        decimal riskPercent,
        decimal minProfit,
        decimal minMarginPercent,
        decimal? purchaseOverride = null)
    {
        var fees = Math.Round(estimatedResale * (sellingFeePercent / 100m), 2);
        var risk = Math.Round(estimatedResale * (riskPercent / 100m), 2);
        var investmentBase = parts + labour + fees + shipping + risk;
        var maxByProfit = estimatedResale - investmentBase - minProfit;
        var maxByMargin = estimatedResale - (estimatedResale * (minMarginPercent / 100m)) - investmentBase;
        var absoluteMax = Math.Max(0, Math.Round(Math.Min(maxByProfit, maxByMargin), 2));
        var recommended = Math.Max(0, Math.Round(absoluteMax * 0.85m, 2));
        var recommendedMin = Math.Max(0, Math.Round(absoluteMax * 0.75m, 2));
        var purchase = purchaseOverride ?? recommended;
        var totalInvestment = purchase + parts + labour + fees + shipping + risk;
        var profit = estimatedResale - totalInvestment;
        var margin = estimatedResale <= 0 ? 0 : Math.Round(profit / estimatedResale * 100m, 2);

        return new SellDeviceOfferDto(
            Guid.Empty, estimatedResale, purchase, parts, labour, fees, shipping, risk,
            minProfit, minMarginPercent, recommendedMin, recommended, absoluteMax, absoluteMax,
            totalInvestment, profit, margin, null, "Draft", null, DateTimeOffset.UtcNow, null);
    }
}

public sealed class WebsitePublicService : IWebsitePublicService
{
    private readonly WorkshopDbContext _db;
    private readonly IAuditService _audit;
    private readonly INotificationTemplateService _notify;
    private readonly IConfiguration _config;

    public WebsitePublicService(
        WorkshopDbContext db, IAuditService audit, INotificationTemplateService notify, IConfiguration config)
    {
        _db = db;
        _audit = audit;
        _notify = notify;
        _config = config;
    }

    public async Task<PublicRepairQuoteResponse> SubmitRepairQuoteAsync(PublicRepairQuoteRequest request, string? ip, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.ContactName) || string.IsNullOrWhiteSpace(request.FaultDescription))
            throw new ValidationAppException("Name and fault description are required.");
        if (string.IsNullOrWhiteSpace(request.Phone) && string.IsNullOrWhiteSpace(request.Email))
            throw new ValidationAppException("Phone or email is required.");

        var customer = await FindOrCreateCustomerAsync(request.ContactName, request.Phone, request.Email, request.Suburb, request.PreferredContact, ct);
        var ownerId = await SystemActorIdAsync(ct);
        var pin = WebsiteAccess.GeneratePin();
        var token = WebsiteAccess.GenerateToken();
        var type = await _db.RepairTypes.FirstAsync(t => t.Key == "repair", ct);
        var status = await _db.RepairStatuses.FirstAsync(s => s.Key == "booking_received" || s.Key == "received", ct);
        var priority = await _db.RepairPriorities.FirstAsync(p => p.Key == "normal", ct);
        var tracking = await DocumentNumbering.NextAsync(_db, "TB", ct, digits: 6);

        var device = new Device
        {
            CustomerId = customer.Id,
            Category = ParseCategory(request.DeviceCategory),
            Brand = request.Brand.Trim(),
            Model = request.Model.Trim()
        };
        _db.Devices.Add(device);

        var ticket = new RepairTicket
        {
            TicketNumber = tracking,
            CustomerId = customer.Id,
            Device = device,
            TypeId = type.Id,
            StatusId = status.Id,
            PriorityId = priority.Id,
            CreatedById = ownerId,
            ReportedIssue = request.FaultDescription.Trim(),
            AccessPinHash = WebsiteAccess.HashPin(pin),
            PublicAccessToken = token,
            StartedAt = DateTimeOffset.UtcNow
        };
        _db.RepairTickets.Add(ticket);

        // Lightweight quote shell for online approval workflow
        var quoteNumber = await DocumentNumbering.NextAsync(_db, "QTE", ct);
        var quote = new Quote
        {
            Number = quoteNumber,
            CustomerId = customer.Id,
            RepairTicket = ticket,
            Status = "Draft",
            Issue = request.FaultDescription.Trim(),
            CustomerNotes = request.Notes?.Trim(),
            CreatedById = ownerId,
            RevisionNumber = 1
        };
        _db.Quotes.Add(quote);
        _db.RepairEvents.Add(new RepairEvent
        {
            Ticket = ticket,
            EventType = "website.quote_request",
            Summary = "Website repair quote request received"
        });

        await _db.SaveChangesAsync(ct);
        ticket.DeviceId = device.Id;
        quote.RepairTicketId = ticket.Id;
        await _db.SaveChangesAsync(ct);

        await _audit.WriteAsync(ownerId, "website.repair_quote", "RepairTicket", ticket.Id.ToString(),
            newValue: new { ip, tracking }, ct: ct);
        await _notify.QueueAsync("booking_confirmed", request.Email ?? request.Phone ?? "", customer.Id, ticket.Id,
            new Dictionary<string, string>
            {
                ["tracking"] = tracking,
                ["pin"] = pin,
                ["customer"] = customer.DisplayName
            }, ct);

        return new PublicRepairQuoteResponse(quote.Id, quote.Number, tracking, pin, status.Name,
            "Quote request received. Use your tracking code and PIN to check status.");
    }

    public async Task<PublicRepairBookingResponse> SubmitRepairBookingAsync(PublicRepairBookingRequest request, string? ip, CancellationToken ct = default)
    {
        if (request.PreferredEnd is DateTimeOffset end && end <= request.PreferredStart)
            throw new ValidationAppException("Preferred end must be after start.");
        var ends = request.PreferredEnd ?? request.PreferredStart.AddHours(1);
        var customer = await FindOrCreateCustomerAsync(request.ContactName, request.Phone, request.Email, null, null, ct);
        var ownerId = await SystemActorIdAsync(ct);
        var pin = WebsiteAccess.GeneratePin();
        var token = WebsiteAccess.GenerateToken();
        var type = await _db.RepairTypes.FirstAsync(t => t.Key == "repair", ct);
        var status = await _db.RepairStatuses.FirstAsync(s => s.Key == "booking_received" || s.Key == "awaiting_device" || s.Key == "received", ct);
        var priority = await _db.RepairPriorities.FirstAsync(p => p.Key == "normal", ct);
        var tracking = await DocumentNumbering.NextAsync(_db, "TB", ct, digits: 6);

        var device = new Device
        {
            CustomerId = customer.Id,
            Category = ParseCategory(request.DeviceCategory),
            Brand = request.Brand.Trim(),
            Model = request.Model.Trim()
        };
        _db.Devices.Add(device);

        var ticket = new RepairTicket
        {
            TicketNumber = tracking,
            CustomerId = customer.Id,
            Device = device,
            TypeId = type.Id,
            StatusId = status.Id,
            PriorityId = priority.Id,
            CreatedById = ownerId,
            ReportedIssue = request.FaultOrService.Trim(),
            AccessPinHash = WebsiteAccess.HashPin(pin),
            PublicAccessToken = token,
            DueAt = request.PreferredStart,
            StartedAt = DateTimeOffset.UtcNow
        };
        _db.RepairTickets.Add(ticket);

        var booking = new Booking
        {
            CustomerId = customer.Id,
            Type = "Intake",
            Status = "Confirmed",
            StartsAt = request.PreferredStart,
            EndsAt = ends,
            Notes = $"Website booking · {request.DeviceCategory} {request.Brand} {request.Model} · {request.FaultOrService}. {request.Notes}".Trim()
        };
        _db.Bookings.Add(booking);
        _db.RepairEvents.Add(new RepairEvent
        {
            Ticket = ticket,
            EventType = "website.booking",
            Summary = $"Booking confirmed for {request.PreferredStart.LocalDateTime:g}"
        });
        await _db.SaveChangesAsync(ct);

        await _notify.QueueAsync("booking_confirmed", request.Email ?? request.Phone ?? "", customer.Id, ticket.Id,
            new Dictionary<string, string>
            {
                ["tracking"] = tracking,
                ["pin"] = pin,
                ["when"] = request.PreferredStart.ToLocalTime().ToString("g")
            }, ct);

        return new PublicRepairBookingResponse(booking.Id, ticket.Id, tracking, pin, "Confirmed",
            booking.StartsAt, booking.EndsAt, "Booking confirmed.");
    }

    public async Task<PublicSellDeviceResponse> SubmitSellDeviceAsync(PublicSellDeviceRequest request, string? ip, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.ContactName) || string.IsNullOrWhiteSpace(request.Brand) || string.IsNullOrWhiteSpace(request.Model))
            throw new ValidationAppException("Contact name, brand, and model are required.");
        if (string.IsNullOrWhiteSpace(request.ContactPhone) && string.IsNullOrWhiteSpace(request.ContactEmail))
            throw new ValidationAppException("Phone or email is required.");

        var customer = await FindOrCreateCustomerAsync(request.ContactName, request.ContactPhone, request.ContactEmail, request.Suburb, request.PreferredContact, ct);
        var reference = await DocumentNumbering.NextAsync(_db, "SYT", ct, digits: 6);
        var row = new SellDeviceRequest
        {
            Reference = reference,
            CustomerId = customer.Id,
            Status = "New",
            Category = string.IsNullOrWhiteSpace(request.Category) ? "Phone" : request.Category.Trim(),
            Brand = request.Brand.Trim(),
            Model = request.Model.Trim(),
            Storage = request.Storage,
            Ram = request.Ram,
            Cpu = request.Cpu,
            Gpu = request.Gpu,
            Colour = request.Colour,
            SerialOrImei = request.SerialOrImei,
            BatteryHealth = request.BatteryHealth,
            CarrierStatus = request.CarrierStatus,
            PowersOn = request.PowersOn,
            Charges = request.Charges,
            ScreenCondition = request.ScreenCondition,
            BodyCondition = request.BodyCondition,
            CameraOk = request.CameraOk,
            SpeakerMicOk = request.SpeakerMicOk,
            ButtonsOk = request.ButtonsOk,
            BiometricsOk = request.BiometricsOk,
            LiquidDamage = request.LiquidDamage,
            KnownFaults = request.KnownFaults,
            AccountLocked = request.AccountLocked,
            FindMyEnabled = request.FindMyEnabled,
            GoogleSamsungRemoved = request.GoogleSamsungRemoved,
            CustomerNotes = request.CustomerNotes,
            ContactName = request.ContactName.Trim(),
            ContactPhone = request.ContactPhone,
            ContactEmail = request.ContactEmail,
            Suburb = request.Suburb,
            PreferredContact = request.PreferredContact ?? "Phone",
            PhotoUrlsJson = JsonSerializer.Serialize(request.PhotoUrls ?? Array.Empty<string>()),
            Source = "website"
        };
        row.Events.Add(new SellDeviceEvent { EventType = "created", Summary = "Website Sell Your Tech submission" });
        _db.SellDeviceRequests.Add(row);
        await _db.SaveChangesAsync(ct);

        await _notify.QueueAsync("sell_request_received", request.ContactEmail ?? request.ContactPhone ?? "", customer.Id, null,
            new Dictionary<string, string> { ["reference"] = reference, ["device"] = $"{row.Brand} {row.Model}" }, ct);

        return new PublicSellDeviceResponse(row.Id, reference, row.Status, "Thanks — we will review your device and send an offer.");
    }

    public async Task<PublicCustomPcResponse> SubmitCustomPcAsync(PublicCustomPcRequest request, string? ip, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.ContactName))
            throw new ValidationAppException("Contact name is required.");
        var customer = await FindOrCreateCustomerAsync(request.ContactName, request.ContactPhone, request.ContactEmail, null, null, ct);
        var reference = await DocumentNumbering.NextAsync(_db, "PCR", ct, digits: 5);
        var row = new CustomPcRequest
        {
            Reference = reference,
            CustomerId = customer.Id,
            Status = "New",
            Budget = request.Budget,
            Usage = request.Usage,
            PreferredGamesSoftware = request.PreferredGamesSoftware,
            MonitorResolution = request.MonitorResolution,
            PerformanceTarget = request.PerformanceTarget,
            NeedsWifiBluetooth = request.NeedsWifiBluetooth,
            RgbPreferences = request.RgbPreferences,
            CaseSize = request.CaseSize,
            Colour = request.Colour,
            StorageRequirements = request.StorageRequirements,
            Upgradeability = request.Upgradeability,
            ContactName = request.ContactName.Trim(),
            ContactPhone = request.ContactPhone,
            ContactEmail = request.ContactEmail,
            CustomerNotes = request.CustomerNotes
        };
        _db.CustomPcRequests.Add(row);
        await _db.SaveChangesAsync(ct);
        return new PublicCustomPcResponse(row.Id, reference, row.Status, "Custom PC request received.");
    }

    public Task<PublicRepairStatusDto> GetRepairStatusAsync(string trackingCode, string accessPin, CancellationToken ct = default) =>
        LoadStatusAsync(trackingCode, accessPin, null, ct);

    public Task<PublicRepairStatusDto> GetRepairStatusByTokenAsync(string accessToken, CancellationToken ct = default) =>
        LoadStatusAsync(null, null, accessToken, ct);

    public async Task<PublicQuoteDecisionResponse> ApproveQuoteAsync(Guid quoteId, PublicQuoteDecisionRequest request, string? ip, string? userAgent, CancellationToken ct = default) =>
        await DecideQuoteAsync(quoteId, request, "Approved", ip, userAgent, ct);

    public async Task<PublicQuoteDecisionResponse> DeclineQuoteAsync(Guid quoteId, PublicQuoteDecisionRequest request, string? ip, string? userAgent, CancellationToken ct = default) =>
        await DecideQuoteAsync(quoteId, request, "Declined", ip, userAgent, ct);

    public async Task<IReadOnlyList<PublicAvailabilitySlotDto>> GetBookingAvailabilityAsync(DateTimeOffset day, CancellationToken ct = default)
    {
        var settings = await DbSeed.GetSettingAsync(_db, "website.booking_availability",
            new BookingAvailabilitySettingsDto(9, 17, 60, 3), ct);
        var localDay = day.ToLocalTime().Date;
        var open = new TimeSpan(settings.OpenHour, 0, 0);
        var close = new TimeSpan(settings.CloseHour, 0, 0);
        var slotMins = settings.SlotMinutes;
        var capacity = settings.Capacity;
        var existing = await _db.Bookings.AsNoTracking()
            .Where(b => b.Status != "Cancelled" && b.StartsAt < localDay.AddDays(1) && b.EndsAt > localDay)
            .ToListAsync(ct);

        var slots = new List<PublicAvailabilitySlotDto>();
        for (var t = open; t + TimeSpan.FromMinutes(slotMins) <= close; t += TimeSpan.FromMinutes(slotMins))
        {
            var start = new DateTimeOffset(DateTime.SpecifyKind(localDay + t, DateTimeKind.Local));
            var end = start.AddMinutes(slotMins);
            var conflict = existing.Count(b => b.StartsAt < end && b.EndsAt > start);
            var available = conflict < capacity && start > DateTimeOffset.Now;
            slots.Add(new PublicAvailabilitySlotDto(start, end, available,
                available ? null : (conflict >= capacity ? "Fully booked" : "Past")));
        }
        return slots;
    }

    public async Task<PublicRepairMessageResponse> PostRepairMessageAsync(string trackingCode, PublicRepairMessageRequest request, string? ip, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Body))
            throw new ValidationAppException("Message body is required.");
        var ticket = await _db.RepairTickets
            .FirstOrDefaultAsync(t => t.TicketNumber == trackingCode.Trim() && t.ArchivedAt == null, ct)
            ?? throw new AppException("not_found", "Repair not found or PIN incorrect.", 404);
        if (!WebsiteAccess.VerifyPin(request.AccessPin, ticket.AccessPinHash))
            throw new AppException("not_found", "Repair not found or PIN incorrect.", 404);

        var thread = await _db.ConversationThreads.FirstOrDefaultAsync(t => t.RepairTicketId == ticket.Id, ct);
        if (thread is null)
        {
            thread = new ConversationThread
            {
                Subject = string.IsNullOrWhiteSpace(request.Subject) ? $"Repair {ticket.TicketNumber}" : request.Subject.Trim(),
                CustomerId = ticket.CustomerId,
                RepairTicketId = ticket.Id
            };
            _db.ConversationThreads.Add(thread);
            await _db.SaveChangesAsync(ct);
        }

        var msg = new ConversationMessage
        {
            ThreadId = thread.Id,
            Visibility = "CustomerVisible",
            Channel = "Portal",
            Body = request.Body.Trim(),
            AuthorCustomerId = ticket.CustomerId,
            AuthorName = "Customer"
        };
        thread.UpdatedAt = DateTimeOffset.UtcNow;
        _db.ConversationMessages.Add(msg);
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(null, "website.repair_message", "ConversationThread", thread.Id.ToString(),
            newValue: new { ticket.TicketNumber, ip }, ct: ct);
        return new PublicRepairMessageResponse(thread.Id, msg.Id, "Message sent to TechBench.");
    }

    public async Task<SyncRefurbishedStatusResponse> SyncRefurbishedStatusAsync(SyncRefurbishedStatusRequest request, CancellationToken ct = default)
    {
        var key = request.StockIdOrSku.Trim();
        var status = request.Status.Trim();
        if (status is not ("Reserved" or "Sold" or "Listed" or "Ready for Sale"))
            throw new ValidationAppException("Status must be Reserved, Sold, Listed, or Ready for Sale.");

        var device = await _db.UsedDevices.FirstOrDefaultAsync(u =>
            u.ArchivedAt == null &&
            (u.Notes != null && u.Notes.Contains(key) || u.Id.ToString() == key), ct);
        if (device is null && Guid.TryParse(key, out var gid))
            device = await _db.UsedDevices.FirstOrDefaultAsync(u => u.Id == gid && u.ArchivedAt == null, ct);
        if (device is null)
            throw new AppException("not_found", "Refurbished device not found.", 404);

        device.Status = status;
        if (request.ActualSalePrice is decimal sale) device.ActualSalePrice = sale;
        device.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(null, "website.refurb.sync", "UsedDevice", device.Id.ToString(),
            newValue: new { status, key }, ct: ct);
        return new SyncRefurbishedStatusResponse(device.Id, device.Status, "Status synchronised into WorkshopOS.");
    }

    public async Task<PublicSameDayHintDto> GetSameDayHintAsync(string? serviceCode, string? category, CancellationToken ct = default)
    {
        var q = _db.InventoryItems.AsNoTracking().Where(i => i.ArchivedAt == null);
        if (!string.IsNullOrWhiteSpace(serviceCode))
            q = q.Where(i => i.Sku.Contains(serviceCode) || i.Name.Contains(serviceCode));
        else if (!string.IsNullOrWhiteSpace(category))
            q = q.Where(i => i.Category.Contains(category));
        else
            return new PublicSameDayHintDto(false, "Select a service to check same-day availability.", null);

        var items = await q.Take(20).ToListAsync(ct);
        if (items.Count == 0)
            return new PublicSameDayHintDto(false, "Part order required.", 0);
        var stock = items.Sum(i => i.QuantityOnHand - i.QuantityReserved);
        if (stock <= 0)
            return new PublicSameDayHintDto(false, "Part order required.", 0);
        if (stock <= 2)
            return new PublicSameDayHintDto(true, "Same-day repair may be available (low stock).", stock);
        return new PublicSameDayHintDto(true, "Same-day repair may be available.", stock);
    }

    private async Task<PublicQuoteDecisionResponse> DecideQuoteAsync(
        Guid quoteId, PublicQuoteDecisionRequest request, string action, string? ip, string? userAgent, CancellationToken ct)
    {
        var quote = await _db.Quotes.Include(q => q.RepairTicket).ThenInclude(t => t!.Status)
            .FirstOrDefaultAsync(q => q.Id == quoteId && q.ArchivedAt == null, ct)
            ?? throw new AppException("not_found", "Quote was not found.", 404);
        if (quote.RepairTicket is null || !WebsiteAccess.VerifyPin(request.AccessPin, quote.RepairTicket.AccessPinHash))
            throw new AppException("forbidden", "Invalid access PIN.", 403);
        if (quote.Status is "Accepted" or "Declined")
            throw new ValidationAppException($"Quote already {quote.Status.ToLowerInvariant()}. If the quote was edited, ask the shop to resend the new revision.");
        if (quote.Status is not ("Sent" or "Draft" or "Viewed"))
            throw new ValidationAppException("This quote cannot be decided in its current status.");
        if (request.ExpectedRevisionNumber is int expected && expected != quote.RevisionNumber)
            throw new ValidationAppException("This quote was updated. Refresh the page and approve the latest revision.");
        if (quote.AcceptedVersion is int acceptedRev && acceptedRev == quote.RevisionNumber && quote.Status == "Accepted")
            throw new ValidationAppException("This quote revision was already approved.");

        var priorSameRev = await _db.QuoteApprovals
            .Where(a => a.QuoteId == quote.Id && a.QuoteRevisionNumber == quote.RevisionNumber)
            .OrderByDescending(a => a.CreatedAt).FirstOrDefaultAsync(ct);
        if (priorSameRev is not null)
            throw new ValidationAppException($"This quote revision was already {priorSameRev.Action.ToLowerInvariant()}.");

        quote.Status = action == "Approved" ? "Accepted" : "Declined";
        if (action == "Approved")
        {
            quote.AcceptedAt = DateTimeOffset.UtcNow;
            quote.AcceptedTotal = quote.Total;
            quote.AcceptedVersion = quote.RevisionNumber;
            quote.IsFrozen = true;
        }
        _db.QuoteApprovals.Add(new QuoteApprovalRecord
        {
            QuoteId = quote.Id,
            RepairTicketId = quote.RepairTicketId,
            CustomerId = quote.CustomerId,
            QuoteRevisionNumber = quote.RevisionNumber,
            Action = action,
            Comment = request.Comment,
            IpAddress = ip,
            UserAgent = userAgent,
            AccessMethod = "PublicPin"
        });

        if (action == "Approved")
        {
            var approved = await _db.RepairStatuses.FirstOrDefaultAsync(s => s.Key == "approved", ct)
                ?? await _db.RepairStatuses.FirstAsync(s => s.Key == "repairing" || s.Key == "awaiting_approval", ct);
            if (quote.RepairTicket is not null)
            {
                quote.RepairTicket.StatusId = approved.Id;
                _db.RepairEvents.Add(new RepairEvent
                {
                    TicketId = quote.RepairTicket.Id,
                    EventType = "quote.approved",
                    Summary = $"Customer approved quote {quote.Number} (rev {quote.RevisionNumber})"
                });
            }
            await _notify.QueueAsync("quote_approved", quote.RepairTicket?.Customer?.Email ?? "", quote.CustomerId, quote.RepairTicketId,
                new Dictionary<string, string> { ["quote"] = quote.Number }, ct);
        }
        else if (quote.RepairTicket is not null)
        {
            _db.RepairEvents.Add(new RepairEvent
            {
                TicketId = quote.RepairTicket.Id,
                EventType = "quote.declined",
                Summary = $"Customer declined quote {quote.Number} (rev {quote.RevisionNumber})"
            });
        }

        await _db.SaveChangesAsync(ct);
        return new PublicQuoteDecisionResponse(action, quote.Number, quote.RevisionNumber, $"Quote {action.ToLowerInvariant()}.");
    }

    private async Task<PublicRepairStatusDto> LoadStatusAsync(string? trackingCode, string? pin, string? accessToken, CancellationToken ct)
    {
        RepairTicket? ticket = null;
        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            ticket = await _db.RepairTickets.AsNoTracking()
                .Include(t => t.Status).Include(t => t.Device).Include(t => t.Customer)
                .Include(t => t.Events).Include(t => t.Notes)
                .FirstOrDefaultAsync(t => t.PublicAccessToken == accessToken && t.ArchivedAt == null, ct);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(trackingCode) || string.IsNullOrWhiteSpace(pin))
                throw new ValidationAppException("Tracking code and PIN are required.");
            ticket = await _db.RepairTickets.AsNoTracking()
                .Include(t => t.Status).Include(t => t.Device).Include(t => t.Customer)
                .Include(t => t.Events).Include(t => t.Notes)
                .FirstOrDefaultAsync(t => t.TicketNumber == trackingCode.Trim() && t.ArchivedAt == null, ct);
            if (ticket is null || !WebsiteAccess.VerifyPin(pin, ticket.AccessPinHash))
                throw new AppException("not_found", "Repair not found or PIN incorrect.", 404);
        }
        if (ticket is null)
            throw new AppException("not_found", "Repair not found.", 404);

        var quote = await _db.Quotes.AsNoTracking()
            .Where(q => q.RepairTicketId == ticket.Id && q.ArchivedAt == null)
            .OrderByDescending(q => q.RevisionNumber).ThenByDescending(q => q.CreatedAt)
            .FirstOrDefaultAsync(ct);
        var invoice = await _db.Invoices.AsNoTracking()
            .Where(i => i.RepairTicketId == ticket.Id && i.ArchivedAt == null)
            .OrderByDescending(i => i.CreatedAt).FirstOrDefaultAsync(ct);
        var warranty = await _db.Warranties.AsNoTracking()
            .Where(w => w.RepairTicketId == ticket.Id)
            .OrderByDescending(w => w.ExpiresAt).FirstOrDefaultAsync(ct);

        var deposit = ticket.DepositAmount ?? 0;
        var quoteTotal = quote?.Total;
        var balance = invoice is null
            ? Math.Max(0, (quoteTotal ?? ticket.EstimatedPrice ?? 0) - deposit)
            : Math.Max(0, invoice.Total - invoice.AmountPaid);
        var canDecide = quote is not null && quote.Status is "Sent" or "Draft" or "Viewed";
        var notes = string.Join("\n", ticket.Notes.Where(n => !n.IsInternal).OrderBy(n => n.CreatedAt).Select(n => n.Body).TakeLast(3));
        var timeline = ticket.Events.OrderBy(e => e.CreatedAt)
            .Select(e => new PublicTimelineItemDto(e.CreatedAt, e.Summary, e.EventType))
            .ToList();

        return new PublicRepairStatusDto(
            ticket.TicketNumber,
            ticket.Device is null ? "Device" : $"{ticket.Device.Brand} {ticket.Device.Model}",
            ticket.Status.Key,
            MapPublicStatusName(ticket.Status.Key, ticket.Status.Name),
            ticket.Status.Colour,
            ticket.EstimatedCompletionAt ?? ticket.DueAt,
            string.IsNullOrWhiteSpace(notes) ? null : notes,
            quoteTotal,
            deposit,
            balance,
            warranty is null ? null : $"{warranty.Status} until {warranty.ExpiresAt:d}",
            canDecide && quote!.Status != "Accepted",
            canDecide && quote!.Status != "Declined",
            quote?.Id,
            quote?.Number,
            quote?.RevisionNumber,
            timeline,
            invoice?.Number,
            true);
    }

    private async Task<Customer> FindOrCreateCustomerAsync(string name, string? phone, string? email, string? suburb, string? preferred, CancellationToken ct)
    {
        var normalizedEmail = email?.Trim().ToLowerInvariant();
        Customer? existing = null;
        if (!string.IsNullOrWhiteSpace(normalizedEmail))
            existing = await _db.Customers.FirstOrDefaultAsync(c => c.Email != null && c.Email.ToLower() == normalizedEmail && c.ArchivedAt == null, ct);
        if (existing is null && !string.IsNullOrWhiteSpace(phone))
        {
            var p = phone.Trim();
            existing = await _db.Customers.FirstOrDefaultAsync(c => c.Phone == p && c.ArchivedAt == null, ct);
        }
        if (existing is not null) return existing;

        var parts = name.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var customer = new Customer
        {
            Type = CustomerType.Individual,
            FirstName = parts.ElementAtOrDefault(0),
            LastName = parts.ElementAtOrDefault(1),
            DisplayName = name.Trim(),
            Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim(),
            Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim(),
            Suburb = suburb,
            PreferredContact = string.Equals(preferred, "Email", StringComparison.OrdinalIgnoreCase)
                ? PreferredContact.Email
                : PreferredContact.Sms
        };
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync(ct);
        return customer;
    }

    private async Task<Guid> SystemActorIdAsync(CancellationToken ct)
    {
        var owner = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.IsOwner && u.ArchivedAt == null, ct)
            ?? await _db.Users.AsNoTracking().FirstAsync(u => u.ArchivedAt == null, ct);
        return owner.Id;
    }

    private static DeviceCategory ParseCategory(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "phone" or "iphone" or "mobile" => DeviceCategory.Phone,
        "tablet" or "ipad" => DeviceCategory.Tablet,
        "laptop" or "macbook" => DeviceCategory.Laptop,
        "desktop" or "pc" => DeviceCategory.Desktop,
        "console" or "gaming console" => DeviceCategory.Console,
        "watch" or "smartwatch" => DeviceCategory.Other,
        _ => DeviceCategory.Other
    };

    private static string MapPublicStatusName(string key, string name) => key switch
    {
        "booking_received" => "Booking Received",
        "awaiting_device" => "Awaiting Device",
        "received" => "Checked In",
        "diagnosing" => "Diagnosis In Progress",
        "diagnosis_complete" => "Diagnosis Complete",
        "awaiting_approval" => "Awaiting Customer Approval",
        "approved" => "Approved",
        "waiting_parts" => "Parts Required",
        "parts_ordered" => "Parts Ordered",
        "parts_arrived" => "Parts Arrived",
        "repairing" => "Repair In Progress",
        "testing" => "Testing",
        "quality_check" => "Quality Check",
        "ready_pickup" => "Ready for Collection",
        "completed" => "Completed",
        "cancelled" => "Cancelled",
        _ => name
    };
}
