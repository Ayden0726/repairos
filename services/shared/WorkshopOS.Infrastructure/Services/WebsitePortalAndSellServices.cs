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

public sealed class CustomerPortalService : ICustomerPortalService
{
    private readonly WorkshopDbContext _db;
    private readonly TokenService _tokens;
    private readonly IOptions<JwtOptions> _jwt;
    private readonly PasswordHasher<CustomerPortalAccount> _hasher = new();

    public CustomerPortalService(WorkshopDbContext db, TokenService tokens, IOptions<JwtOptions> jwt)
    {
        _db = db;
        _tokens = tokens;
        _jwt = jwt;
    }

    public async Task<CustomerPortalAuthResponse> LoginAsync(CustomerPortalLoginRequest request, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var account = await _db.CustomerPortalAccounts.Include(a => a.Customer)
            .FirstOrDefaultAsync(a => a.NormalizedEmail == email && a.ArchivedAt == null, ct)
            ?? throw new AppException("invalid_credentials", "Invalid email or password.", 401);
        if (_hasher.VerifyHashedPassword(account, account.PasswordHash, request.Password) == PasswordVerificationResult.Failed)
            throw new AppException("invalid_credentials", "Invalid email or password.", 401);
        account.LastLoginAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Issue(account);
    }

    public async Task<CustomerPortalAuthResponse> RegisterAsync(CustomerPortalRegisterRequest request, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (await _db.CustomerPortalAccounts.AnyAsync(a => a.NormalizedEmail == email && a.ArchivedAt == null, ct))
            throw new ValidationAppException("An account with that email already exists.");

        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Email != null && c.Email.ToLower() == email && c.ArchivedAt == null, ct);
        if (customer is null)
        {
            customer = new Customer
            {
                Type = CustomerType.Individual,
                FirstName = request.FirstName.Trim(),
                LastName = request.LastName.Trim(),
                DisplayName = $"{request.FirstName.Trim()} {request.LastName.Trim()}".Trim(),
                Email = request.Email.Trim(),
                Phone = request.Phone?.Trim()
            };
            _db.Customers.Add(customer);
            await _db.SaveChangesAsync(ct);
        }

        var account = new CustomerPortalAccount
        {
            CustomerId = customer.Id,
            Email = request.Email.Trim(),
            NormalizedEmail = email
        };
        account.PasswordHash = _hasher.HashPassword(account, request.Password);
        _db.CustomerPortalAccounts.Add(account);
        await _db.SaveChangesAsync(ct);
        account.Customer = customer;
        return Issue(account);
    }

    public async Task<IReadOnlyList<CustomerRepairListItemDto>> ListRepairsAsync(Guid customerId, CancellationToken ct = default)
    {
        var rows = await _db.RepairTickets.AsNoTracking()
            .Include(t => t.Status).Include(t => t.Device)
            .Where(t => t.CustomerId == customerId && t.ArchivedAt == null)
            .OrderByDescending(t => t.CreatedAt).Take(100).ToListAsync(ct);
        return rows.Select(t => new CustomerRepairListItemDto(
            t.Id, t.TicketNumber, t.Status.Name,
            t.Device is null ? "Device" : $"{t.Device.Brand} {t.Device.Model}",
            t.CreatedAt, t.EstimatedCompletionAt ?? t.DueAt)).ToList();
    }

    public async Task<IReadOnlyList<CustomerQuoteListItemDto>> ListQuotesAsync(Guid customerId, CancellationToken ct = default)
    {
        var rows = await _db.Quotes.AsNoTracking()
            .Include(q => q.RepairTicket)
            .Where(q => q.CustomerId == customerId && q.ArchivedAt == null)
            .OrderByDescending(q => q.CreatedAt).Take(100).ToListAsync(ct);
        return rows.Select(q => new CustomerQuoteListItemDto(
            q.Id, q.Number, q.Status, q.Total, q.CreatedAt, q.RepairTicket?.TicketNumber)).ToList();
    }

    public async Task<IReadOnlyList<CustomerDeviceListItemDto>> ListDevicesAsync(Guid customerId, CancellationToken ct = default)
    {
        var rows = await _db.Devices.AsNoTracking()
            .Where(d => d.CustomerId == customerId && d.ArchivedAt == null)
            .OrderByDescending(d => d.CreatedAt).Take(100).ToListAsync(ct);
        return rows.Select(d => new CustomerDeviceListItemDto(
            d.Id, d.Brand, d.Model, d.Category.ToString(), WebsiteAccess.MaskSerial(d.Serial ?? d.Imei))).ToList();
    }

    public async Task<IReadOnlyList<CustomerInvoiceListItemDto>> ListInvoicesAsync(Guid customerId, CancellationToken ct = default)
    {
        var rows = await _db.Invoices.AsNoTracking()
            .Where(i => i.CustomerId == customerId && i.ArchivedAt == null)
            .OrderByDescending(i => i.CreatedAt).Take(100).ToListAsync(ct);
        return rows.Select(i => new CustomerInvoiceListItemDto(
            i.Id, i.Number, i.Status, i.Total, Math.Max(0, i.Total - i.AmountPaid), i.CreatedAt)).ToList();
    }

    public async Task<IReadOnlyList<CustomerWarrantyListItemDto>> ListWarrantiesAsync(Guid customerId, CancellationToken ct = default)
    {
        var rows = await _db.Warranties.AsNoTracking().Include(w => w.Device)
            .Where(w => w.CustomerId == customerId)
            .OrderByDescending(w => w.ExpiresAt).Take(100).ToListAsync(ct);
        return rows.Select(w => new CustomerWarrantyListItemDto(
            w.Id, w.CoverageSummary, w.Status, w.StartsAt, w.ExpiresAt,
            w.Device is null ? null : $"{w.Device.Brand} {w.Device.Model}")).ToList();
    }

    public async Task<IReadOnlyList<CustomerMessageListItemDto>> ListThreadsAsync(Guid customerId, CancellationToken ct = default)
    {
        var rows = await _db.ConversationThreads.AsNoTracking()
            .Include(t => t.Messages)
            .Where(t => t.CustomerId == customerId)
            .OrderByDescending(t => t.UpdatedAt).Take(50).ToListAsync(ct);
        return rows.Select(t =>
        {
            var last = t.Messages.Where(m => m.Visibility != "Internal").OrderByDescending(m => m.CreatedAt).FirstOrDefault();
            return new CustomerMessageListItemDto(t.Id, t.Id, t.Subject, last?.Body ?? "", t.UpdatedAt, false);
        }).ToList();
    }

    public async Task<IReadOnlyList<CustomerMessageDto>> ListMessagesAsync(Guid customerId, Guid threadId, CancellationToken ct = default)
    {
        var thread = await _db.ConversationThreads.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == threadId && t.CustomerId == customerId, ct)
            ?? throw new AppException("not_found", "Conversation not found.", 404);
        var messages = await _db.ConversationMessages.AsNoTracking()
            .Where(m => m.ThreadId == thread.Id && m.Visibility != "Internal")
            .OrderBy(m => m.CreatedAt).ToListAsync(ct);
        return messages.Select(m => new CustomerMessageDto(
            m.Id, m.Body, m.Channel, m.AuthorName, m.CreatedAt, m.AuthorCustomerId == customerId)).ToList();
    }

    public async Task<CustomerMessageListItemDto> PostMessageAsync(Guid customerId, CustomerPostMessageRequest request, CancellationToken ct = default)
    {
        ConversationThread? thread = null;
        if (request.ThreadId is Guid tid)
            thread = await _db.ConversationThreads.FirstOrDefaultAsync(t => t.Id == tid && t.CustomerId == customerId, ct);
        if (thread is null && request.RepairTicketId is Guid rid)
        {
            thread = await _db.ConversationThreads.FirstOrDefaultAsync(t => t.RepairTicketId == rid && t.CustomerId == customerId, ct);
            if (thread is null)
            {
                var ticket = await _db.RepairTickets.FirstOrDefaultAsync(t => t.Id == rid && t.CustomerId == customerId, ct)
                    ?? throw new AppException("not_found", "Repair not found.", 404);
                thread = new ConversationThread
                {
                    Subject = string.IsNullOrWhiteSpace(request.Subject) ? $"Repair {ticket.TicketNumber}" : request.Subject.Trim(),
                    CustomerId = customerId,
                    RepairTicketId = ticket.Id
                };
                _db.ConversationThreads.Add(thread);
            }
        }
        thread ??= new ConversationThread
        {
            Subject = string.IsNullOrWhiteSpace(request.Subject) ? "Message" : request.Subject.Trim(),
            CustomerId = customerId
        };
        if (thread.Id == Guid.Empty) _db.ConversationThreads.Add(thread);

        var customer = await _db.Customers.FirstAsync(c => c.Id == customerId, ct);
        var msg = new ConversationMessage
        {
            Thread = thread,
            Visibility = "CustomerVisible",
            Channel = "Portal",
            Body = request.Body.Trim(),
            AuthorCustomerId = customerId,
            AuthorName = customer.DisplayName
        };
        thread.UpdatedAt = DateTimeOffset.UtcNow;
        _db.ConversationMessages.Add(msg);
        await _db.SaveChangesAsync(ct);
        return new CustomerMessageListItemDto(thread.Id, thread.Id, thread.Subject, msg.Body, thread.UpdatedAt, false);
    }

    public async Task<IReadOnlyList<CustomerSellRequestListItemDto>> ListSellRequestsAsync(Guid customerId, CancellationToken ct = default)
    {
        var rows = await _db.SellDeviceRequests.AsNoTracking().Include(r => r.Offer)
            .Where(r => r.CustomerId == customerId)
            .OrderByDescending(r => r.CreatedAt).Take(100).ToListAsync(ct);
        return rows.Select(r => new CustomerSellRequestListItemDto(
            r.Id, r.Reference, r.Status, $"{r.Brand} {r.Model}",
            r.Offer?.CustomerFacingOffer ?? r.Offer?.RecommendedOffer, r.CreatedAt)).ToList();
    }

    private CustomerPortalAuthResponse Issue(CustomerPortalAccount account)
    {
        var expires = DateTimeOffset.UtcNow.AddMinutes(Math.Max(15, _jwt.Value.AccessTokenMinutes));
        var token = _tokens.CreateCustomerAccessToken(account.CustomerId, account.Email, account.Customer.DisplayName, expires);
        return new CustomerPortalAuthResponse(token, expires, account.CustomerId, account.Customer.DisplayName, account.Email);
    }
}

public sealed class SellTechService : ISellTechService
{
    private readonly WorkshopDbContext _db;
    private readonly IAuditService _audit;

    public SellTechService(WorkshopDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<IReadOnlyList<SellDeviceListItemDto>> ListAsync(string? q, string? status, CancellationToken ct = default)
    {
        var query = _db.SellDeviceRequests.AsNoTracking().Include(r => r.Offer).AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(r => r.Status == status);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLowerInvariant();
            query = query.Where(r =>
                r.Reference.ToLower().Contains(term) ||
                r.Brand.ToLower().Contains(term) ||
                r.Model.ToLower().Contains(term) ||
                r.ContactName.ToLower().Contains(term));
        }
        var rows = await query.OrderByDescending(r => r.CreatedAt).Take(200).ToListAsync(ct);
        return rows.Select(r => new SellDeviceListItemDto(
            r.Id, r.Reference, r.Status, r.Category, r.Brand, r.Model, r.ContactName, r.Suburb, r.CreatedAt,
            r.Offer?.CustomerFacingOffer ?? r.Offer?.RecommendedOffer)).ToList();
    }

    public async Task<SellDeviceDetailDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var r = await _db.SellDeviceRequests.AsNoTracking()
            .Include(x => x.Offer).Include(x => x.Events)
            .FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new AppException("not_found", "Sell request not found.", 404);
        return MapDetail(r);
    }

    private static readonly string[] SellStatuses =
    [
        "New", "Awaiting Review", "Offer Being Prepared", "Offer Sent", "Customer Accepted", "Customer Declined",
        "Awaiting Device", "Device Received", "Inspection", "Offer Adjusted", "Purchased", "Declined", "Archived"
    ];

    public async Task<SellDeviceDetailDto> UpdateStatusAsync(Guid id, UpdateSellDeviceStatusRequest request, Guid actorId, CancellationToken ct = default)
    {
        var r = await _db.SellDeviceRequests.Include(x => x.Offer).Include(x => x.Events)
            .FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new AppException("not_found", "Sell request not found.", 404);
        var next = SellStatuses.FirstOrDefault(s => s.Equals(request.Status.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? throw new ValidationAppException($"Invalid Sell Your Tech status. Use: {string.Join(", ", SellStatuses)}");
        r.Status = next;
        if (request.InternalNotes is not null) r.InternalNotes = request.InternalNotes;
        r.UpdatedAt = DateTimeOffset.UtcNow;
        r.Events.Add(new SellDeviceEvent { ActorUserId = actorId, EventType = "status", Summary = $"Status → {r.Status}" });
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(actorId, "selltech.status", "SellDeviceRequest", id.ToString(), ct: ct);
        return MapDetail(r);
    }

    public async Task<SellDeviceOfferDto> UpsertOfferAsync(Guid requestId, UpsertSellDeviceOfferRequest request, Guid actorId, CancellationToken ct = default)
    {
        var row = await _db.SellDeviceRequests.Include(r => r.Offer)
            .FirstOrDefaultAsync(r => r.Id == requestId, ct)
            ?? throw new AppException("not_found", "Sell request not found.", 404);
        var defaults = await GetOfferDefaultsAsync(ct);
        var calc = OfferCalculator.Calculate(
            request.EstimatedResalePrice,
            request.EstimatedPartsCost ?? 0,
            request.LabourAllowance ?? defaults.LabourAllowance,
            defaults.SellingFeePercent,
            request.ShippingPacking ?? 0,
            defaults.RiskPercent,
            request.DesiredMinProfit ?? defaults.MinimumProfit,
            request.DesiredMinMarginPercent ?? defaults.MinimumMarginPercent,
            request.PurchaseCost);

        var offer = row.Offer ?? new SellDeviceOffer { RequestId = row.Id };
        if (row.Offer is null) { _db.SellDeviceOffers.Add(offer); row.Offer = offer; }

        offer.EstimatedResalePrice = calc.EstimatedResalePrice;
        offer.PurchaseCost = calc.PurchaseCost;
        offer.EstimatedPartsCost = calc.EstimatedPartsCost;
        offer.LabourAllowance = calc.LabourAllowance;
        offer.SellingPlatformFees = calc.SellingPlatformFees;
        offer.ShippingPacking = calc.ShippingPacking;
        offer.RiskAllowance = calc.RiskAllowance;
        offer.DesiredMinProfit = calc.DesiredMinProfit;
        offer.DesiredMinMarginPercent = calc.DesiredMinMarginPercent;
        offer.RecommendedOfferMin = calc.RecommendedOfferMin;
        offer.RecommendedOffer = calc.RecommendedOffer;
        offer.RecommendedOfferMax = calc.RecommendedOfferMax;
        offer.AbsoluteMaxPurchasePrice = calc.AbsoluteMaxPurchasePrice;
        offer.EstimatedTotalInvestment = calc.EstimatedTotalInvestment;
        offer.EstimatedResaleProfit = calc.EstimatedResaleProfit;
        offer.EstimatedMarginPercent = calc.EstimatedMarginPercent;
        offer.CustomerFacingOffer = request.CustomerFacingOffer ?? calc.RecommendedOffer;
        offer.Notes = request.Notes;
        offer.Status = "Prepared";
        row.Status = "Offer Being Prepared";
        row.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(actorId, "selltech.offer", "SellDeviceRequest", requestId.ToString(), ct: ct);
        return MapOffer(offer);
    }

    public async Task<SellDeviceOfferDto> SendOfferAsync(Guid requestId, Guid actorId, CancellationToken ct = default)
    {
        var row = await _db.SellDeviceRequests.Include(r => r.Offer)
            .FirstOrDefaultAsync(r => r.Id == requestId, ct)
            ?? throw new AppException("not_found", "Sell request not found.", 404);
        if (row.Offer is null) throw new ValidationAppException("Prepare an offer first.");
        row.Offer.Status = "Sent";
        row.Offer.SentAt = DateTimeOffset.UtcNow;
        row.Status = "Offer Sent";
        row.UpdatedAt = DateTimeOffset.UtcNow;
        row.Events.Add(new SellDeviceEvent { ActorUserId = actorId, EventType = "offer_sent", Summary = $"Offer ${row.Offer.CustomerFacingOffer:0.00} sent" });
        await _db.SaveChangesAsync(ct);
        return MapOffer(row.Offer);
    }

    public async Task<UsedDeviceDetailDto> CreateRefurbFromPurchaseAsync(Guid requestId, Guid actorId, CancellationToken ct = default)
    {
        var row = await _db.SellDeviceRequests.Include(r => r.Offer)
            .FirstOrDefaultAsync(r => r.Id == requestId, ct)
            ?? throw new AppException("not_found", "Sell request not found.", 404);
        if (row.UsedDeviceId is Guid existing)
        {
            var existingDto = await _db.UsedDevices.AsNoTracking().FirstAsync(u => u.Id == existing, ct);
            return MapUsed(existingDto, row);
        }

        var stockId = await DocumentNumbering.NextAsync(_db, "TB-RFB", ct, digits: 5);
        // DocumentNumbering formats as TB-RFB-2026-00001 — user wanted TB-RFB-00382. Use custom RFB seq:
        stockId = await NextRefurbStockIdAsync(ct);

        var used = new UsedDevice
        {
            Summary = $"{row.Brand} {row.Model}".Trim(),
            Brand = row.Brand,
            Model = row.Model,
            Category = row.Category,
            Serial = row.SerialOrImei,
            Imei = row.SerialOrImei,
            Colour = row.Colour,
            StorageCapacity = row.Storage,
            Specs = string.Join(" · ", new[] { row.Ram, row.Cpu, row.Gpu }.Where(x => !string.IsNullOrWhiteSpace(x))),
            Status = "Purchased",
            ConditionGrade = "Good",
            PurchasePrice = row.Offer?.CustomerFacingOffer ?? row.Offer?.RecommendedOffer ?? row.Offer?.PurchaseCost ?? 0,
            ExpectedRepairCost = row.Offer?.EstimatedPartsCost ?? 0,
            ExpectedResale = row.Offer?.EstimatedResalePrice ?? 0,
            Faults = row.KnownFaults,
            Notes = $"From Sell Your Tech {row.Reference}. {row.CustomerNotes}".Trim(),
            Source = "SellYourTech",
            SellerCustomerId = row.CustomerId,
            StockId = stockId,
            Sku = stockId,
            PurchaseDate = DateTimeOffset.UtcNow,
            ActivationLockStatus = row.FindMyEnabled == true ? "Locked" : row.AccountLocked == true ? "Suspected Locked" : "Unknown"
        };
        _db.UsedDevices.Add(used);
        row.UsedDevice = used;
        row.Status = "Purchased";
        row.UpdatedAt = DateTimeOffset.UtcNow;
        row.Events.Add(new SellDeviceEvent { ActorUserId = actorId, EventType = "refurb_created", Summary = $"Refurb stock {stockId} created" });
        await _db.SaveChangesAsync(ct);
        row.UsedDeviceId = used.Id;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(actorId, "selltech.refurb", "UsedDevice", used.Id.ToString(), ct: ct);
        return MapUsed(used, row);
    }

    public async Task<OfferCalculatorDefaultsDto> GetOfferDefaultsAsync(CancellationToken ct = default) =>
        await DbSeed.GetSettingAsync(_db, "website.offer_defaults", new OfferCalculatorDefaultsDto(50, 25, 10, 12, 40), ct);

    public async Task<OfferCalculatorDefaultsDto> SaveOfferDefaultsAsync(OfferCalculatorDefaultsDto defaults, Guid actorId, CancellationToken ct = default)
    {
        await DbSeed.SetSettingAsync(_db, "website.offer_defaults", defaults, actorId, ct);
        return defaults;
    }

    private async Task<string> NextRefurbStockIdAsync(CancellationToken ct)
    {
        var key = "doc:RFB:stock";
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        var seq = await _db.DocumentSequences.FirstOrDefaultAsync(s => s.Key == key, ct);
        if (seq is null)
        {
            seq = new DocumentSequence { Key = key, Year = DateTime.UtcNow.Year, NextValue = 1 };
            _db.DocumentSequences.Add(seq);
        }
        var value = seq.NextValue++;
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        _db.Entry(seq).State = EntityState.Detached;
        return $"TB-RFB-{value:D5}";
    }

    private static SellDeviceDetailDto MapDetail(SellDeviceRequest r)
    {
        var photos = Array.Empty<string>();
        try { photos = JsonSerializer.Deserialize<string[]>(r.PhotoUrlsJson ?? "[]") ?? Array.Empty<string>(); } catch { /* ignore */ }
        return new SellDeviceDetailDto(
            r.Id, r.Reference, r.Status, r.Category, r.Brand, r.Model, r.Storage, r.Ram, r.Cpu, r.Gpu, r.Colour,
            WebsiteAccess.MaskSerial(r.SerialOrImei), r.BatteryHealth, r.CarrierStatus, r.PowersOn, r.Charges,
            r.ScreenCondition, r.BodyCondition, r.CameraOk, r.SpeakerMicOk, r.ButtonsOk, r.BiometricsOk,
            r.LiquidDamage, r.KnownFaults, r.AccountLocked, r.FindMyEnabled, r.GoogleSamsungRemoved,
            r.CustomerNotes, r.ContactName, r.ContactPhone, r.ContactEmail, r.Suburb, r.PreferredContact,
            photos, r.InternalNotes, r.CustomerId, r.UsedDeviceId,
            r.Offer is null ? null : MapOffer(r.Offer),
            r.Events.OrderBy(e => e.CreatedAt).Select(e => new SellDeviceEventDto(e.CreatedAt, e.EventType, e.Summary)).ToList());
    }

    private static SellDeviceOfferDto MapOffer(SellDeviceOffer o) => new(
        o.Id, o.EstimatedResalePrice, o.PurchaseCost, o.EstimatedPartsCost, o.LabourAllowance,
        o.SellingPlatformFees, o.ShippingPacking, o.RiskAllowance, o.DesiredMinProfit, o.DesiredMinMarginPercent,
        o.RecommendedOfferMin, o.RecommendedOffer, o.RecommendedOfferMax, o.AbsoluteMaxPurchasePrice,
        o.EstimatedTotalInvestment, o.EstimatedResaleProfit, o.EstimatedMarginPercent,
        o.CustomerFacingOffer, o.Status, o.Notes, o.CreatedAt, o.SentAt);

    private static UsedDeviceDetailDto MapUsed(UsedDevice u, SellDeviceRequest r) => new(
        u.Id, u.Summary, u.Brand, u.Model, u.Category, u.Serial, u.Imei, u.Colour, u.StorageCapacity, u.Specs,
        u.Status, u.ConditionGrade, u.PurchasePrice, u.ExpectedRepairCost, u.TotalCost, u.ExpectedResale,
        u.ActualSalePrice, u.ActualSalePrice is decimal sale ? sale - u.TotalCost : null,
        u.Faults, u.Notes, u.Source, u.SellerCustomerId, r.ContactName, u.CustomerId, null, u.RepairTicketId, null,
        u.CreatedAt, u.UpdatedAt);
}
