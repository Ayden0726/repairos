using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WorkshopOS.Application.Abstractions;
using WorkshopOS.Application.Common;
using WorkshopOS.Contracts.Auth;
using WorkshopOS.Contracts.Common;
using WorkshopOS.Contracts.Operations;
using WorkshopOS.Contracts.Workshop;
using WorkshopOS.Domain.Entities;
using WorkshopOS.Infrastructure.Persistence;

namespace WorkshopOS.Infrastructure.Services;

public sealed class PricingSettingsService : IPricingSettingsService
{
    private readonly WorkshopDbContext _db;
    private readonly IAuditService _audit;

    public PricingSettingsService(WorkshopDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<PricingSettingsDto> GetAsync(CancellationToken ct = default)
    {
        var settings = await DbSeed.GetSettingAsync<PricingSettingsDto?>(_db, SettingKeys.PricingSettings, null, ct)
            ?? PricingCalculator.DefaultSettings();
        var business = await DbSeed.GetSettingAsync<BusinessProfileDto?>(_db, SettingKeys.BusinessProfile, null, ct);
        var tax = new TaxPricingDto(
            business?.GstRegistered ?? settings.Tax?.Enabled ?? true,
            business is null || business.GstRate <= 0 ? (settings.Tax?.Rate ?? 0.10m) : business.GstRate,
            business?.GstInclusive ?? settings.Tax?.Inclusive ?? true);
        return settings with { Tax = tax };
    }

    public async Task<PricingSettingsDto> UpdateAsync(PricingSettingsDto settings, Guid actorId, CancellationToken ct = default)
    {
        var cleaned = Normalize(settings);
        await DbSeed.SetSettingAsync(_db, SettingKeys.PricingSettings, cleaned, actorId, ct);
        if (cleaned.Tax is { } tax)
        {
            var business = await DbSeed.GetSettingAsync(_db, SettingKeys.BusinessProfile,
                new BusinessProfileDto("Workshop", null, null, null, null, null, null, null, null, "#0F766E", true, 0.10m, "AUD", 110m), ct);
            business = business with
            {
                GstRegistered = tax.Enabled,
                GstRate = tax.Rate <= 0 ? 0.10m : tax.Rate,
                GstInclusive = tax.Inclusive
            };
            await DbSeed.SetSettingAsync(_db, SettingKeys.BusinessProfile, business, actorId, ct);
            await DbSeed.SetSettingAsync(_db, SettingKeys.Gst, new { registered = tax.Enabled, rate = tax.Rate, inclusive = tax.Inclusive }, actorId, ct);
        }
        await _audit.WriteAsync(actorId, "pricing.settings.update", "Setting", SettingKeys.PricingSettings, newValue: cleaned, ct: ct);
        return await GetAsync(ct);
    }

    public async Task<IReadOnlyList<MarkupTierDto>> ListTiersAsync(CancellationToken ct = default) =>
        await _db.MarkupTiers.AsNoTracking()
            .Where(t => t.ArchivedAt == null)
            .OrderBy(t => t.SortOrder).ThenBy(t => t.MinCost)
            .Select(t => new MarkupTierDto(t.Id, t.MinCost, t.MaxCost, t.MarkupPercent, t.SortOrder))
            .ToListAsync(ct);

    public async Task<MarkupTierDto> UpsertTierAsync(UpsertMarkupTierRequest request, Guid actorId, CancellationToken ct = default)
    {
        MarkupTier tier;
        if (request.Id is Guid id)
        {
            tier = await _db.MarkupTiers.FirstOrDefaultAsync(t => t.Id == id && t.ArchivedAt == null, ct)
                ?? throw new AppException("not_found", "Markup tier was not found.", 404);
        }
        else
        {
            tier = new MarkupTier();
            _db.MarkupTiers.Add(tier);
        }
        tier.MinCost = PricingCalculator.RoundMoney(request.MinCost);
        tier.MaxCost = request.MaxCost is null ? null : PricingCalculator.RoundMoney(request.MaxCost.Value);
        tier.MarkupPercent = PricingCalculator.RoundMoney(request.MarkupPercent);
        tier.SortOrder = request.SortOrder;
        tier.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(actorId, "pricing.tier.upsert", "MarkupTier", tier.Id.ToString(), ct: ct);
        return new MarkupTierDto(tier.Id, tier.MinCost, tier.MaxCost, tier.MarkupPercent, tier.SortOrder);
    }

    public async Task DeleteTierAsync(Guid id, Guid actorId, CancellationToken ct = default)
    {
        var tier = await _db.MarkupTiers.FirstOrDefaultAsync(t => t.Id == id && t.ArchivedAt == null, ct)
            ?? throw new AppException("not_found", "Markup tier was not found.", 404);
        tier.ArchivedAt = DateTimeOffset.UtcNow;
        tier.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(actorId, "pricing.tier.delete", "MarkupTier", id.ToString(), ct: ct);
    }

    public async Task<IReadOnlyList<ServicePricingDto>> ListServicesAsync(CancellationToken ct = default) =>
        await _db.ServicePricings.AsNoTracking()
            .Where(s => s.ArchivedAt == null)
            .OrderBy(s => s.SortOrder).ThenBy(s => s.Name)
            .Select(s => new ServicePricingDto(
                s.Id, s.Name, s.Category, s.Description, s.DefaultLabourFee, s.DefaultPartMarkupPercent,
                s.IsActive, s.SortOrder, s.Code, s.Subcategory, s.DeviceType, s.ServiceFee, s.EstimatedMinutes,
                s.MinCharge, s.DiagnosticFee, s.PartsRequired, s.SerialRequired, s.WarrantyDays, s.TechNotes,
                s.CustomerDescription, s.IsSystem, s.CategoryId, null, null))
            .ToListAsync(ct);

    public async Task<ServicePricingDto> UpsertServiceAsync(UpsertServicePricingRequest request, Guid actorId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ValidationAppException("Service name is required.");
        ServicePricing svc;
        if (request.Id is Guid id)
        {
            svc = await _db.ServicePricings.FirstOrDefaultAsync(s => s.Id == id && s.ArchivedAt == null, ct)
                ?? throw new AppException("not_found", "Service was not found.", 404);
        }
        else
        {
            svc = new ServicePricing();
            _db.ServicePricings.Add(svc);
        }
        svc.Name = request.Name.Trim();
        svc.Category = string.IsNullOrWhiteSpace(request.Category) ? null : request.Category.Trim();
        svc.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        svc.DefaultLabourFee = PricingCalculator.RoundMoney(request.DefaultLabourFee);
        svc.DefaultPartMarkupPercent = request.DefaultPartMarkupPercent is null
            ? null : PricingCalculator.RoundMoney(request.DefaultPartMarkupPercent.Value);
        svc.IsActive = request.IsActive;
        svc.SortOrder = request.SortOrder;
        if (!string.IsNullOrWhiteSpace(request.Code)) svc.Code = request.Code.Trim().ToUpperInvariant();
        svc.Subcategory = string.IsNullOrWhiteSpace(request.Subcategory) ? svc.Subcategory : request.Subcategory.Trim();
        svc.DeviceType = string.IsNullOrWhiteSpace(request.DeviceType) ? svc.DeviceType : request.DeviceType.Trim();
        svc.ServiceFee = PricingCalculator.RoundMoney(request.ServiceFee);
        if (request.EstimatedMinutes is not null) svc.EstimatedMinutes = request.EstimatedMinutes;
        if (request.MinCharge is not null) svc.MinCharge = PricingCalculator.RoundMoney(request.MinCharge.Value);
        if (request.DiagnosticFee is not null) svc.DiagnosticFee = PricingCalculator.RoundMoney(request.DiagnosticFee.Value);
        svc.PartsRequired = request.PartsRequired;
        svc.SerialRequired = request.SerialRequired;
        if (request.WarrantyDays is not null) svc.WarrantyDays = request.WarrantyDays;
        if (request.TechNotes is not null) svc.TechNotes = request.TechNotes.Trim();
        if (request.CustomerDescription is not null) svc.CustomerDescription = request.CustomerDescription.Trim();
        if (request.CategoryId is not null) svc.CategoryId = request.CategoryId;
        if (request.CompatibleBrands is not null)
            svc.CompatibleBrandsJson = System.Text.Json.JsonSerializer.Serialize(request.CompatibleBrands);
        if (request.CompatibleModels is not null)
            svc.CompatibleModelsJson = System.Text.Json.JsonSerializer.Serialize(request.CompatibleModels);
        svc.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(actorId, "pricing.service.upsert", "ServicePricing", svc.Id.ToString(), ct: ct);
        return ServiceCatalogueService.MapThin(svc);
    }

    public async Task DeleteServiceAsync(Guid id, Guid actorId, CancellationToken ct = default)
    {
        var svc = await _db.ServicePricings.FirstOrDefaultAsync(s => s.Id == id && s.ArchivedAt == null, ct)
            ?? throw new AppException("not_found", "Service was not found.", 404);
        svc.ArchivedAt = DateTimeOffset.UtcNow;
        svc.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(actorId, "pricing.service.delete", "ServicePricing", id.ToString(), ct: ct);
    }

    public async Task<PricingPreviewResponse> PreviewAsync(PricingPreviewRequest request, CancellationToken ct = default)
    {
        var settings = await GetAsync(ct);
        var tiers = await ListTiersAsync(ct);
        var services = await ListServicesAsync(ct);
        return PricingCalculator.Calculate(request, settings, tiers, services, settings.Tax);
    }

    private static PricingSettingsDto Normalize(PricingSettingsDto s)
    {
        var labour = s.Labour with
        {
            DefaultLabourFee = PricingCalculator.RoundMoney(s.Labour.DefaultLabourFee),
            MinimumLabourFee = PricingCalculator.RoundMoney(s.Labour.MinimumLabourFee),
            DifficultyLevels = (s.Labour.DifficultyLevels ?? Array.Empty<DifficultyLevelDto>())
                .Select(d => d with { LabourFee = PricingCalculator.RoundMoney(d.LabourFee) }).ToList()
        };
        var parts = s.Parts with
        {
            MarkupMethod = string.IsNullOrWhiteSpace(s.Parts.MarkupMethod) ? "FlatPercent" : s.Parts.MarkupMethod.Trim(),
            DefaultMarkupPercent = PricingCalculator.RoundMoney(s.Parts.DefaultMarkupPercent),
            FixedMarkupAmount = PricingCalculator.RoundMoney(s.Parts.FixedMarkupAmount),
            MinimumPartProfit = PricingCalculator.RoundMoney(s.Parts.MinimumPartProfit)
        };
        var quote = s.Quote with
        {
            DefaultValidityDays = s.Quote.DefaultValidityDays <= 0 ? 14 : s.Quote.DefaultValidityDays
        };
        var tax = s.Tax is null ? null : s.Tax with { Rate = s.Tax.Rate <= 0 ? 0.10m : s.Tax.Rate };
        return s with { Labour = labour, Parts = parts, Quote = quote, Tax = tax };
    }
}

public sealed class QuoteService : IQuoteService
{
    private static readonly string[] Statuses =
        ["Draft", "Sent", "Viewed", "Accepted", "Declined", "Expired", "Converted", "Cancelled"];

    private readonly WorkshopDbContext _db;
    private readonly IAuditService _audit;
    private readonly IPricingSettingsService _pricing;
    private readonly IRepairService _repairs;

    public QuoteService(WorkshopDbContext db, IAuditService audit, IPricingSettingsService pricing, IRepairService repairs)
    {
        _db = db;
        _audit = audit;
        _pricing = pricing;
        _repairs = repairs;
    }

    public async Task<IReadOnlyList<QuoteListItemDto>> ListAsync(string? q, string? status, Guid? customerId, Guid? repairTicketId, CancellationToken ct = default)
    {
        await ExpireOverdueAsync(ct);
        var query = _db.Quotes.AsNoTracking().Include(x => x.Customer)
            .Where(x => x.ArchivedAt == null);
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(x => x.Status == status);
        if (customerId is Guid cid)
            query = query.Where(x => x.CustomerId == cid);
        if (repairTicketId is Guid rid)
            query = query.Where(x => x.RepairTicketId == rid);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLowerInvariant();
            query = query.Where(x =>
                x.Number.ToLower().Contains(term) ||
                x.Customer.DisplayName.ToLower().Contains(term) ||
                (x.Issue != null && x.Issue.ToLower().Contains(term)) ||
                (x.DeviceBrand != null && x.DeviceBrand.ToLower().Contains(term)) ||
                (x.DeviceModel != null && x.DeviceModel.ToLower().Contains(term)));
        }
        return await query.OrderByDescending(x => x.CreatedAt)
            .Select(x => new QuoteListItemDto(
                x.Id, x.Number, x.Customer.DisplayName, x.Status, x.Total, x.CreatedAt,
                x.ExpiresAt, x.RepairTicketId, x.RevisionNumber, x.MarginPercent, x.RequiresApproval))
            .ToListAsync(ct);
    }

    public async Task<QuoteDetailDto> GetAsync(Guid id, bool includeInternalFinancials, CancellationToken ct = default)
    {
        await ExpireOverdueAsync(ct);
        var quote = await LoadAsync(id, ct);
        return Map(quote, includeInternalFinancials);
    }

    public async Task<QuoteDetailDto> CreateAsync(CreateQuoteRequest request, Guid actorId, IReadOnlySet<string> permissions, CancellationToken ct = default)
    {
        _ = await _db.Customers.FirstOrDefaultAsync(c => c.Id == request.CustomerId && c.ArchivedAt == null, ct)
            ?? throw new AppException("not_found", "Customer was not found.", 404);

        if (request.RepairTicketId is Guid ticketId)
        {
            _ = await _db.RepairTickets.FirstOrDefaultAsync(t => t.Id == ticketId && t.ArchivedAt == null, ct)
                ?? throw new AppException("not_found", "Repair ticket was not found.", 404);
        }

        var lineInputs = await ResolveLineInputsAsync(request, ct);
        if (lineInputs.Count == 0)
            throw new ValidationAppException("At least one line is required.");

        EnsurePricingPermissions(lineInputs, request.LabourFee, request.MarkupPercent, request.MarkupAmount, request.DiscountAmount, request.DiscountPercent, permissions);

        var settings = await _pricing.GetAsync(ct);
        var preview = await _pricing.PreviewAsync(new PricingPreviewRequest(
            lineInputs, request.DiscountPercent, request.DiscountAmount, null, null,
            request.LabourFee, request.MarkupPercent, request.MarkupAmount,
            request.DifficultyLevelKey, request.ServicePricingId), ct);
        if (preview.RequiresApproval && !Has(permissions, "pricing.approve_low_margin") && !Has(permissions, "pricing.edit_settings"))
            throw new ValidationAppException(preview.Warning ?? "Quote margin requires manager approval.");

        var validity = request.ValidityDays ?? settings.Quote.DefaultValidityDays;
        if (validity <= 0) validity = 14;

        var number = await DocumentNumbering.NextAsync(_db, "QTE", ct);
        var quote = new Quote
        {
            Number = number,
            CustomerId = request.CustomerId,
            RepairTicketId = request.RepairTicketId,
            Status = "Draft",
            Issue = request.Issue?.Trim(),
            CustomerNotes = request.CustomerNotes?.Trim(),
            InternalNotes = request.InternalNotes?.Trim(),
            DeviceBrand = request.DeviceBrand?.Trim(),
            DeviceModel = request.DeviceModel?.Trim(),
            DeviceSerial = request.DeviceSerial?.Trim(),
            DeviceCategory = request.DeviceCategory?.Trim(),
            CreatedById = actorId,
            ValidityDays = validity,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(validity),
            RevisionNumber = 1
        };

        if (quote.RepairTicketId is Guid rid && (string.IsNullOrWhiteSpace(quote.DeviceBrand) || string.IsNullOrWhiteSpace(quote.DeviceModel)))
            await FillDeviceFromTicketAsync(quote, rid, ct);

        ApplyPreview(quote, preview);
        AddLines(quote, preview.Lines);
        _db.Quotes.Add(quote);
        AddAudit(quote, actorId, "create", $"Created {quote.Number}");
        await _db.SaveChangesAsync(ct);
        var createdId = quote.Id;
        var createdNumber = quote.Number;
        _db.ChangeTracker.Clear();
        await SaveRevisionSnapshotAsync(createdId, 1, actorId, "Initial", ct);
        await _audit.WriteAsync(actorId, "quote.create", "Quote", createdId.ToString(), newValue: new { Number = createdNumber }, ct: ct);
        return await GetAsync(createdId, true, ct);
    }

    public async Task<QuoteDetailDto> UpdateAsync(Guid id, UpdateQuoteRequest request, Guid actorId, IReadOnlySet<string> permissions, CancellationToken ct = default)
    {
        var quote = await _db.Quotes.Include(q => q.Lines).FirstOrDefaultAsync(q => q.Id == id && q.ArchivedAt == null, ct)
            ?? throw new AppException("not_found", "Quote was not found.", 404);
        if (quote.IsFrozen || quote.Status is "Accepted" or "Converted")
            throw new ValidationAppException("Accepted quotes are frozen and cannot be edited. Create a revision instead.");
        if (quote.Status is "Cancelled")
            throw new ValidationAppException("Cancelled quotes cannot be edited.");

        var calcInputs = ToCalcInputs(request.Lines);
        EnsurePricingPermissions(calcInputs, request.LabourFee, request.MarkupPercent, request.MarkupAmount, request.DiscountAmount, request.DiscountPercent, permissions);
        var preview = await _pricing.PreviewAsync(new PricingPreviewRequest(
            calcInputs, request.DiscountPercent, request.DiscountAmount, null, null,
            request.LabourFee, request.MarkupPercent, request.MarkupAmount,
            request.DifficultyLevelKey, request.ServicePricingId), ct);
        if (preview.RequiresApproval && !Has(permissions, "pricing.approve_low_margin") && !Has(permissions, "pricing.edit_settings"))
            throw new ValidationAppException(preview.Warning ?? "Quote margin requires manager approval.");

        quote.Issue = request.Issue?.Trim() ?? quote.Issue;
        quote.CustomerNotes = request.CustomerNotes?.Trim() ?? quote.CustomerNotes;
        quote.InternalNotes = request.InternalNotes?.Trim() ?? quote.InternalNotes;
        quote.DeviceBrand = request.DeviceBrand?.Trim() ?? quote.DeviceBrand;
        quote.DeviceModel = request.DeviceModel?.Trim() ?? quote.DeviceModel;
        quote.DeviceSerial = request.DeviceSerial?.Trim() ?? quote.DeviceSerial;
        quote.DeviceCategory = request.DeviceCategory?.Trim() ?? quote.DeviceCategory;
        if (request.ValidityDays is int days && days > 0)
        {
            quote.ValidityDays = days;
            quote.ExpiresAt = DateTimeOffset.UtcNow.AddDays(days);
        }

        quote.RevisionNumber += 1;
        _db.QuoteLines.RemoveRange(quote.Lines);
        quote.Lines.Clear();
        ApplyPreview(quote, preview);
        AddLines(quote, preview.Lines);
        quote.UpdatedAt = DateTimeOffset.UtcNow;
        _db.QuoteAuditEntries.Add(new QuoteAuditEntry
        {
            QuoteId = quote.Id,
            Action = "update",
            Detail = request.ReviseReason ?? $"Revision {quote.RevisionNumber}",
            ActorUserId = actorId
        });
        await _db.SaveChangesAsync(ct);
        var revision = quote.RevisionNumber;
        var quoteId = quote.Id;
        _db.ChangeTracker.Clear();
        await SaveRevisionSnapshotAsync(quoteId, revision, actorId, request.ReviseReason ?? "Update", ct);
        await _audit.WriteAsync(actorId, "quote.update", "Quote", quoteId.ToString(), newValue: new { RevisionNumber = revision }, ct: ct);
        return await GetAsync(quoteId, true, ct);
    }

    public async Task<QuoteDetailDto> SetStatusAsync(Guid id, string status, Guid actorId, IReadOnlySet<string> permissions, CancellationToken ct = default)
    {
        if (!Statuses.Contains(status, StringComparer.OrdinalIgnoreCase))
            throw new ValidationAppException("Invalid quote status.");
        var normalized = Statuses.First(a => a.Equals(status, StringComparison.OrdinalIgnoreCase));
        var quote = await _db.Quotes.FirstOrDefaultAsync(q => q.Id == id && q.ArchivedAt == null, ct)
            ?? throw new AppException("not_found", "Quote was not found.", 404);

        if (quote.IsFrozen && normalized is not ("Cancelled" or "Converted"))
            throw new ValidationAppException("Frozen quotes cannot change status except Converted/Cancelled.");

        if (normalized == "Accepted")
        {
            if (quote.RequiresApproval && !Has(permissions, "pricing.approve_low_margin") && !Has(permissions, "pricing.edit_settings"))
                throw new ValidationAppException("Low-margin quote requires manager approval before accept.");
            quote.AcceptedAt = DateTimeOffset.UtcNow;
            quote.AcceptedById = actorId;
            quote.AcceptedTotal = quote.Total;
            quote.AcceptedVersion = quote.RevisionNumber;
            quote.IsFrozen = true;
        }

        var old = quote.Status;
        quote.Status = normalized;
        quote.UpdatedAt = DateTimeOffset.UtcNow;
        _db.QuoteAuditEntries.Add(new QuoteAuditEntry
        {
            QuoteId = quote.Id,
            Action = "status",
            Detail = $"{old} → {normalized}",
            ActorUserId = actorId
        });
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(actorId, "quote.status", "Quote", id.ToString(), oldValue: old, newValue: normalized, ct: ct);
        return await GetAsync(id, true, ct);
    }

    public async Task<QuoteDetailDto> ReviseAsync(Guid id, UpdateQuoteRequest request, Guid actorId, IReadOnlySet<string> permissions, CancellationToken ct = default)
    {
        var quote = await _db.Quotes.Include(q => q.Lines).FirstOrDefaultAsync(q => q.Id == id && q.ArchivedAt == null, ct)
            ?? throw new AppException("not_found", "Quote was not found.", 404);

        // Revising an accepted quote creates an editable draft copy path: unfreeze into new revision as Draft.
        if (quote.IsFrozen || quote.Status is "Accepted" or "Converted")
        {
            quote.IsFrozen = false;
            quote.Status = "Draft";
            quote.AcceptedAt = null;
            quote.AcceptedById = null;
            quote.AcceptedTotal = null;
            quote.AcceptedVersion = null;
        }
        request = request with { ReviseReason = request.ReviseReason ?? "Revision" };
        return await UpdateAsync(id, request, actorId, permissions, ct);
    }

    public async Task<QuoteDetailDto> ConvertToRepairAsync(Guid id, ConvertQuoteToRepairRequest request, Guid actorId, CancellationToken ct = default)
    {
        var quote = await _db.Quotes.Include(q => q.Customer).Include(q => q.Lines)
            .FirstOrDefaultAsync(q => q.Id == id && q.ArchivedAt == null, ct)
            ?? throw new AppException("not_found", "Quote was not found.", 404);

        if (quote.Status is not ("Accepted" or "Sent" or "Viewed" or "Draft"))
            throw new ValidationAppException("Only draft/sent/viewed/accepted quotes can convert to a repair.");

        if (request.RepairTicketId is Guid existingId)
        {
            _ = await _db.RepairTickets.FirstOrDefaultAsync(t => t.Id == existingId && t.ArchivedAt == null, ct)
                ?? throw new AppException("not_found", "Repair ticket was not found.", 404);
            quote.RepairTicketId = existingId;
        }
        else if (quote.RepairTicketId is null)
        {
            DeviceCategory? cat = null;
            if (!string.IsNullOrWhiteSpace(quote.DeviceCategory) &&
                Enum.TryParse<DeviceCategory>(quote.DeviceCategory, true, out var parsed))
                cat = parsed;

            var repair = await _repairs.CreateAsync(new CreateRepairRequest(
                quote.CustomerId, null, null, null, null,
                string.IsNullOrWhiteSpace(quote.Issue) ? $"Quote {quote.Number}" : quote.Issue!,
                null, null, null, null, null, false, false, false, false, null,
                quote.Total, null, null,
                cat, quote.DeviceBrand, quote.DeviceModel, quote.DeviceSerial, null), actorId, ct);
            quote.RepairTicketId = repair.Id;
        }

        quote.Status = "Converted";
        quote.IsFrozen = true;
        quote.UpdatedAt = DateTimeOffset.UtcNow;
        if (quote.AcceptedAt is null)
        {
            quote.AcceptedAt = DateTimeOffset.UtcNow;
            quote.AcceptedById = actorId;
            quote.AcceptedTotal = quote.Total;
            quote.AcceptedVersion = quote.RevisionNumber;
        }
        AddAudit(quote, actorId, "convert", $"Linked to repair {quote.RepairTicketId}");
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(actorId, "quote.convert", "Quote", id.ToString(), newValue: quote.RepairTicketId, ct: ct);
        return await GetAsync(id, true, ct);
    }

    public async Task<string> BuildCustomerPrintHtmlAsync(Guid id, CancellationToken ct = default)
    {
        var q = await LoadAsync(id, ct);
        var business = await DbSeed.GetSettingAsync<BusinessProfileDto?>(_db, SettingKeys.BusinessProfile, null, ct);
        static string Esc(string? s) => System.Net.WebUtility.HtmlEncode(s ?? "");
        var biz = business?.Name ?? "Workshop";

        // HARDCODED `$` + 2 decimals for customer print. Do NOT use CultureInfo / ToString("C")
        // (InvariantCulture yields ¤) and do NOT rely on MoneyDisplay here — print must never fail.
        // AUD/USD/CAD/NZD all use `$` for this workshop product; map via literal only.
        static string Money(decimal amount) =>
            "$" + amount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);

        var partsSell = q.PartsSellTotal != 0m ? q.PartsSellTotal : q.PartsSubtotal;
        // Job-level labour only (never sum of per-line LabourAmount — that double-counts legacy quotes).
        var labour = q.LabourFee != 0m ? q.LabourFee : q.LabourSubtotal;

        var device = string.Join(" ", new[] { q.DeviceBrand, q.DeviceModel }.Where(x => !string.IsNullOrWhiteSpace(x)));
        if (string.IsNullOrWhiteSpace(device)) device = "—";

        // Customer lines: parts at sell only (job markup already in PartSell/UnitPrice). Never bake labour into a line.
        // Legacy quotes may still store LabourAmount on each line — strip it from the amount shown.
        static decimal CustomerPartAmount(QuoteLine l)
        {
            if (l.PartSell > 0m)
                return PricingCalculator.RoundMoney(l.PartSell * l.Quantity + l.AdditionalAmount - l.DiscountAmount);
            // UnitPrice is the customer unit when PartSell was not populated; exclude LabourAmount.
            if (l.UnitPrice > 0m)
                return PricingCalculator.RoundMoney(l.UnitPrice * l.Quantity + l.AdditionalAmount - l.DiscountAmount);
            return PricingCalculator.RoundMoney(Math.Max(0m, l.LineTotal - l.LabourAmount));
        }

        var lineHtml = new System.Text.StringBuilder();
        foreach (var l in q.Lines.OrderBy(x => x.SortOrder))
        {
            var amount = CustomerPartAmount(l);
            lineHtml.Append("<tr><td>").Append(Esc(l.Description)).Append("</td>")
                .Append("<td class=\"num\">").Append(Esc(l.Quantity.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture))).Append("</td>")
                .Append("<td class=\"num\">").Append(Money(amount)).Append("</td></tr>");
        }
        if (labour > 0m)
        {
            lineHtml.Append("<tr><td>Labour / service</td><td class=\"num\">1</td><td class=\"num\">")
                .Append(Money(labour)).Append("</td></tr>");
        }

        var sb = new System.Text.StringBuilder();
        sb.Append("<!DOCTYPE html><html><head><meta charset=\"utf-8\"/>")
          .Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\"/>")
          // Deploy fingerprint: grep print HTML for workshopos-currency:$ — proves new API binary.
          .Append("<!-- workshopos-currency:$ -->")
          .Append("<title>").Append(Esc(q.Number)).Append("</title><style>")
          // A4 customer quote — WinUI print opens this HTML in the browser
          .Append("@page{size:A4;margin:12mm}")
          .Append("*{box-sizing:border-box}")
          .Append("html,body{margin:0;padding:0;color:#111;background:#fff}")
          .Append("body{font-family:Segoe UI,Arial,Helvetica,sans-serif;font-size:11pt;line-height:1.35;")
          .Append("width:100%;max-width:190mm;margin:0 auto;padding:12px 14px}")
          .Append(".sheet{width:100%;max-width:190mm}")
          .Append("h1{margin:0 0 4px;font-size:18pt;font-weight:700}")
          .Append(".meta{color:#444;margin:0 0 14px;font-size:10pt}")
          .Append("table{width:100%;border-collapse:collapse;margin:10px 0;table-layout:fixed}")
          .Append("th,td{border-bottom:1px solid #ccc;padding:7px 6px;text-align:left;vertical-align:top;")
          .Append("word-wrap:break-word;overflow-wrap:anywhere;font-size:10pt}")
          .Append("th{font-size:9pt;text-transform:uppercase;letter-spacing:.03em;color:#555}")
          .Append("th.num,td.num{text-align:right;white-space:nowrap}")
          .Append("th:nth-child(1),td:nth-child(1){width:62%}")
          .Append("th:nth-child(2),td:nth-child(2){width:12%}")
          .Append("th:nth-child(3),td:nth-child(3){width:26%}")
          .Append(".box{border:1px solid #bbb;border-radius:4px;padding:10px 12px;margin:10px 0}")
          .Append(".label{font-size:8pt;text-transform:uppercase;letter-spacing:.04em;color:#666;margin-bottom:4px}")
          .Append(".totals{margin:12px 0 0 auto;width:100%;max-width:70mm}")
          .Append(".totals div{display:flex;justify-content:space-between;gap:12px;margin:3px 0;font-size:10pt}")
          .Append(".totals .grand{font-weight:700;font-size:12pt;margin-top:6px;padding-top:6px;border-top:1px solid #333}")
          .Append("button{margin:0 0 12px;padding:8px 14px;font-size:11pt}")
          .Append("@media print{")
          .Append("button{display:none!important}")
          .Append("body{padding:0;max-width:190mm;width:auto;font-size:10pt}")
          .Append(".sheet{max-width:190mm}")
          .Append("a{color:inherit;text-decoration:none}")
          .Append("}")
          .Append("</style></head><body data-currency=\"$\"><div class=\"sheet\">")
          .Append("<button type=\"button\" onclick=\"window.print()\">Print</button>")
          .Append("<h1>").Append(Esc(biz)).Append("</h1>")
          .Append("<div class=\"meta\">Quote ").Append(Esc(q.Number)).Append(" · ").Append(Esc(q.Status))
          .Append(q.ExpiresAt is null ? "" : " · Valid until " + Esc(q.ExpiresAt.Value.ToLocalTime().ToString("d")))
          .Append("</div>")
          .Append("<div class=\"box\"><div class=\"label\">Customer</div>").Append(Esc(q.Customer.DisplayName)).Append("</div>")
          .Append("<div class=\"box\"><div class=\"label\">Device</div>").Append(Esc(device));
        if (!string.IsNullOrWhiteSpace(q.DeviceSerial))
            sb.Append("<div>Serial: ").Append(Esc(q.DeviceSerial)).Append("</div>");
        sb.Append("</div>");
        if (!string.IsNullOrWhiteSpace(q.Issue))
            sb.Append("<div class=\"box\"><div class=\"label\">Issue</div><pre style=\"white-space:pre-wrap;font-family:inherit;margin:0\">")
              .Append(Esc(q.Issue)).Append("</pre></div>");
        // Job-level layout: Description / Qty / Amount — NOT Price+Total (that baked labour into line totals).
        sb.Append("<table><thead><tr><th>Description</th><th class=\"num\">Qty</th><th class=\"num\">Amount</th></tr></thead><tbody>")
          .Append(lineHtml).Append("</tbody></table>")
          .Append("<div class=\"totals\">")
          .Append("<div><span>Parts</span><span>").Append(Money(partsSell)).Append("</span></div>")
          .Append("<div><span>Labour</span><span>").Append(Money(labour)).Append("</span></div>");
        if (q.DiscountTotal > 0)
            sb.Append("<div><span>Discount</span><span>-").Append(Money(q.DiscountTotal)).Append("</span></div>");
        sb.Append("<div><span>Subtotal</span><span>").Append(Money(q.Subtotal)).Append("</span></div>")
          .Append("<div><span>Tax</span><span>").Append(Money(q.GstAmount)).Append("</span></div>")
          .Append("<div class=\"grand\"><span>Total</span><span>").Append(Money(q.Total)).Append("</span></div>")
          .Append("</div>");

        var warrantyHtml = new System.Text.StringBuilder();
        var svcIds = q.Lines.Where(l => l.ServicePricingId != null).Select(l => l.ServicePricingId!.Value).Distinct().ToList();
        if (svcIds.Count > 0)
        {
            var warranties = await _db.ServicePricings.AsNoTracking()
                .Where(s => svcIds.Contains(s.Id) && s.WarrantyDays != null && s.WarrantyDays > 0)
                .Select(s => new { s.Name, s.WarrantyDays })
                .ToListAsync(ct);
            foreach (var w in warranties)
                warrantyHtml.Append("<li>").Append(Esc(w.Name)).Append(": ").Append(w.WarrantyDays).Append(" days</li>");
        }
        if (warrantyHtml.Length > 0)
        {
            sb.Append("<div class=\"box\"><div class=\"label\">Warranty</div><ul style=\"margin:0;padding-left:18px\">")
              .Append(warrantyHtml).Append("</ul></div>");
        }

        if (!string.IsNullOrWhiteSpace(q.CustomerNotes))
            sb.Append("<div class=\"box\"><div class=\"label\">Notes</div>").Append(Esc(q.CustomerNotes)).Append("</div>");
        sb.Append("</div><script>window.onload=function(){setTimeout(function(){window.print()},300);}</script>")
          .Append("</body></html>");
        return sb.ToString();
    }

    private async Task<List<QuoteLineCalcInput>> ResolveLineInputsAsync(CreateQuoteRequest request, CancellationToken ct)
    {
        if (request.Lines is { Count: > 0 })
            return ToCalcInputs(request.Lines).ToList();

        if (request.SimpleLines is { Count: > 0 })
        {
            // Legacy simple lines: UnitPrice is treated as part cost; job markup + labour apply once.
            return request.SimpleLines.Select(l => new QuoteLineCalcInput(
                l.Type, l.Description, null, null, null, null, null, null, null,
                l.Quantity, l.UnitPrice, 0m, 0m, null, null, null, null, 0m, 0m, null)).ToList();
        }
        await Task.CompletedTask;
        return new List<QuoteLineCalcInput>();
    }

    private static List<QuoteLineCalcInput> ToCalcInputs(IReadOnlyList<QuoteLineInputDto> lines) =>
        lines.Select(l => new QuoteLineCalcInput(
            l.Type, l.Description, l.ServiceName, l.PartName, l.SupplierName, l.Sku,
            l.InventoryItemId, l.ServicePricingId, l.DifficultyLevelKey, l.Quantity,
            l.PartCost, l.ShippingCost, l.OtherCost,
            null, null, // per-line markup ignored — job-level only
            l.PartSell,
            null, // per-line labour ignored — job-level only
            l.AdditionalAmount, l.DiscountAmount, l.UnitPrice)).ToList();

    private static void EnsurePricingPermissions(
        IReadOnlyList<QuoteLineCalcInput> lines,
        decimal? labourFee,
        decimal? markupPercent,
        decimal? markupAmount,
        decimal? discountAmount,
        decimal? discountPercent,
        IReadOnlySet<string> permissions)
    {
        if (markupPercent is not null || markupAmount is not null)
        {
            if (!Has(permissions, "pricing.change_markup") && !Has(permissions, "pricing.edit")
                && !Has(permissions, "pricing.edit_settings") && !Has(permissions, "quotes.manage"))
                throw new ValidationAppException("You do not have permission to change markup.");
        }
        if (labourFee is not null)
        {
            if (!Has(permissions, "pricing.override_labour") && !Has(permissions, "pricing.edit_settings")
                && !Has(permissions, "quotes.manage"))
                throw new ValidationAppException("You do not have permission to override labour.");
        }
        if (discountAmount is > 0 || discountPercent is > 0 || lines.Any(l => l.DiscountAmount > 0))
        {
            if (!Has(permissions, "pricing.apply_discount") && !Has(permissions, "quotes.manage"))
                throw new ValidationAppException("You do not have permission to apply discounts.");
        }
        foreach (var line in lines)
        {
            if (line.PartSellOverride is not null &&
                !Has(permissions, "pricing.override_price") && !Has(permissions, "pricing.edit_settings") && !Has(permissions, "quotes.manage"))
                throw new ValidationAppException("You do not have permission to override recommended price.");
        }
    }

    private static bool Has(IReadOnlySet<string> permissions, string key) =>
        permissions.Contains(key) || permissions.Contains("*");

    private static void ApplyPreview(Quote quote, PricingPreviewResponse preview)
    {
        quote.PartsCostTotal = preview.PartsCostTotal;
        quote.PartsSellTotal = preview.PartsSellTotal;
        quote.MarkupPercent = preview.MarkupPercent;
        quote.MarkupAmount = preview.MarkupAmount;
        quote.LabourFee = preview.LabourFee;
        quote.AdditionalTotal = preview.AdditionalTotal;
        // Legacy aliases kept in sync for older clients / reports.
        quote.PartsSubtotal = preview.PartsSellTotal;
        quote.LabourSubtotal = preview.LabourFee;
        quote.DiscountTotal = preview.DiscountTotal;
        quote.CostTotal = preview.PartsCostTotal;
        quote.ProfitTotal = preview.ProfitTotal;
        quote.MarginPercent = preview.MarginPercent;
        quote.RequiresApproval = preview.RequiresApproval;
        quote.RoundingMethod = preview.RoundingMethod;
        quote.PreRoundTotal = preview.PreRoundTotal;
        quote.Subtotal = preview.Subtotal;
        quote.GstAmount = preview.GstAmount;
        quote.Total = preview.Total;
    }

    private static void AddLines(Quote quote, IReadOnlyList<QuoteLineCalcResult> lines)
    {
        var order = 0;
        foreach (var line in lines)
        {
            quote.Lines.Add(new QuoteLine
            {
                Type = line.Type,
                Description = line.Description,
                ServiceName = line.ServiceName,
                PartName = line.PartName,
                SupplierName = line.SupplierName,
                Sku = line.Sku,
                InventoryItemId = line.InventoryItemId,
                ServicePricingId = line.ServicePricingId,
                DifficultyLevelKey = line.DifficultyLevelKey,
                Quantity = line.Quantity,
                PartCost = line.PartCost,
                ShippingCost = line.ShippingCost,
                OtherCost = line.OtherCost,
                LandedCost = line.LandedCost,
                MarkupPercent = line.MarkupPercent,
                MarkupAmount = line.MarkupAmount,
                PartSell = line.PartSell,
                LabourAmount = line.LabourAmount,
                AdditionalAmount = line.AdditionalAmount,
                DiscountAmount = line.DiscountAmount,
                UnitPrice = line.UnitPrice,
                LineSubtotal = line.LineSubtotal,
                LineTotal = line.LineTotal,
                LineProfit = line.LineProfit,
                SortOrder = order++
            });
        }
    }

    private void AddAudit(Quote quote, Guid actorId, string action, string? detail)
    {
        quote.AuditEntries.Add(new QuoteAuditEntry
        {
            Action = action,
            Detail = detail,
            ActorUserId = actorId
        });
    }

    private async Task SaveRevisionSnapshotAsync(Guid quoteId, int revision, Guid actorId, string? reason, CancellationToken ct)
    {
        var quote = await LoadAsync(quoteId, ct);
        var snapshot = JsonSerializer.Serialize(Map(quote, true));
        _db.QuoteRevisions.Add(new QuoteRevision
        {
            QuoteId = quoteId,
            RevisionNumber = revision,
            SnapshotJson = snapshot,
            CreatedById = actorId,
            Reason = reason
        });
        await _db.SaveChangesAsync(ct);
    }

    private async Task FillDeviceFromTicketAsync(Quote quote, Guid ticketId, CancellationToken ct)
    {
        var ticket = await _db.RepairTickets.AsNoTracking().Include(t => t.Device)
            .FirstOrDefaultAsync(t => t.Id == ticketId, ct);
        if (ticket?.Device is null) return;
        quote.DeviceBrand ??= ticket.Device.Brand;
        quote.DeviceModel ??= ticket.Device.Model;
        quote.DeviceSerial ??= ticket.Device.Serial;
        quote.DeviceCategory ??= ticket.Device.Category.ToString();
        quote.Issue ??= ticket.ReportedIssue;
    }

    private async Task ExpireOverdueAsync(CancellationToken ct)
    {
        var settings = await _pricing.GetAsync(ct);
        if (!settings.Quote.AutoExpire) return;
        var now = DateTimeOffset.UtcNow;
        var overdue = await _db.Quotes
            .Where(q => q.ArchivedAt == null && !q.IsFrozen &&
                        q.ExpiresAt != null && q.ExpiresAt < now &&
                        (q.Status == "Sent" || q.Status == "Viewed" || q.Status == "Draft"))
            .ToListAsync(ct);
        foreach (var q in overdue)
        {
            q.Status = "Expired";
            q.UpdatedAt = now;
        }
        if (overdue.Count > 0)
            await _db.SaveChangesAsync(ct);
    }

    private async Task<Quote> LoadAsync(Guid id, CancellationToken ct) =>
        await _db.Quotes.AsNoTracking()
            .Include(x => x.Customer)
            .Include(x => x.Lines)
            .Include(x => x.Revisions)
            .FirstOrDefaultAsync(x => x.Id == id && x.ArchivedAt == null, ct)
        ?? throw new AppException("not_found", "Quote was not found.", 404);

    private static QuoteDetailDto Map(Quote q, bool includeInternal)
    {
        var lines = q.Lines.OrderBy(l => l.SortOrder).Select(l => new QuoteLineDetailDto(
            l.Id, l.Type, l.Description, l.ServiceName, l.PartName, l.SupplierName, l.Sku,
            l.InventoryItemId, l.ServicePricingId, l.DifficultyLevelKey, l.Quantity,
            includeInternal ? l.PartCost : 0m,
            includeInternal ? l.ShippingCost : 0m,
            includeInternal ? l.OtherCost : 0m,
            includeInternal ? l.LandedCost : 0m,
            includeInternal ? l.MarkupPercent : 0m,
            includeInternal ? l.MarkupAmount : 0m,
            l.PartSell, l.LabourAmount, l.AdditionalAmount, l.DiscountAmount,
            l.UnitPrice, l.LineSubtotal, l.LineTotal,
            includeInternal ? l.LineProfit : 0m)).ToList();

        // Historical accepted quotes may predate job-level columns — fall back to aliases.
        var partsCost = q.PartsCostTotal != 0m ? q.PartsCostTotal : q.CostTotal;
        var partsSell = q.PartsSellTotal != 0m ? q.PartsSellTotal : q.PartsSubtotal;
        var labourFee = q.LabourFee != 0m ? q.LabourFee : q.LabourSubtotal;
        var markupPercent = q.MarkupPercent;
        var markupAmount = q.MarkupAmount != 0m
            ? q.MarkupAmount
            : (partsCost > 0 && partsSell > partsCost ? partsSell - partsCost : 0m);

        return new QuoteDetailDto(
            q.Id, q.Number, q.CustomerId, q.Customer.DisplayName, q.RepairTicketId,
            q.Status, q.Issue, q.CustomerNotes, includeInternal ? q.InternalNotes : null,
            q.DeviceBrand, q.DeviceModel, q.DeviceSerial, q.DeviceCategory,
            q.ValidityDays, q.RevisionNumber, q.ExpiresAt,
            q.AcceptedAt, q.AcceptedById, q.AcceptedTotal, q.AcceptedVersion, q.IsFrozen,
            partsSell, labourFee, q.DiscountTotal,
            includeInternal ? partsCost : 0m,
            includeInternal ? q.ProfitTotal : 0m,
            includeInternal ? q.MarginPercent : 0m,
            q.RequiresApproval, q.RoundingMethod, q.PreRoundTotal,
            q.Subtotal, q.GstAmount, q.Total, lines,
            q.Revisions.OrderByDescending(r => r.RevisionNumber)
                .Select(r => new QuoteRevisionDto(r.Id, r.RevisionNumber, r.CreatedAt, r.Reason)).ToList(),
            includeInternal,
            includeInternal ? partsCost : 0m,
            partsSell,
            includeInternal ? markupPercent : 0m,
            includeInternal ? markupAmount : 0m,
            labourFee,
            q.AdditionalTotal);
    }
}
