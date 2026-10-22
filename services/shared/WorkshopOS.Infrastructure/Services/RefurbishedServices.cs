using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WorkshopOS.Application.Abstractions;
using WorkshopOS.Application.Common;
using WorkshopOS.Contracts.Operations;
using WorkshopOS.Contracts.Website;
using WorkshopOS.Domain.Entities;
using WorkshopOS.Infrastructure.Persistence;

namespace WorkshopOS.Infrastructure.Services;

public sealed class RefurbBuyCalculatorService : IRefurbBuyCalculatorService
{
    private readonly WorkshopDbContext _db;

    public RefurbBuyCalculatorService(WorkshopDbContext db) => _db = db;

    public async Task<RefurbBuyCalcResult> CalculateAsync(RefurbBuyCalcRequest request, CancellationToken ct = default)
    {
        var defaults = await DbSeed.GetSettingAsync(_db, "website.offer_defaults",
            new OfferCalculatorDefaultsDto(50, 25, 10, 12, 40), ct);

        var minProfit = request.MinimumProfit ?? defaults.MinimumProfit;
        var minMargin = request.MinimumMarginPercent ?? defaults.MinimumMarginPercent;
        var excellentMin = request.ExcellentBuyMinMarginPercent ?? Math.Max(minMargin + 15m, 35m);
        var goodMin = request.GoodBuyMinMarginPercent ?? Math.Max(minMargin + 8m, 25m);
        var acceptableMin = request.AcceptableMinMarginPercent ?? minMargin;

        var marketplaceFees = request.MarketplaceFees > 0
            ? request.MarketplaceFees
            : Math.Round(request.EstimatedResalePrice * (defaults.SellingFeePercent / 100m), 2);
        var warrantyAllowance = request.ExpectedWarrantyAllowance > 0
            ? request.ExpectedWarrantyAllowance
            : Math.Round(request.EstimatedResalePrice * (defaults.RiskPercent / 100m), 2);

        var costsExPurchase = Math.Round(
            request.ReplacementParts + request.PartsShipping + request.TechnicianLabour +
            marketplaceFees + request.PaymentProcessingFees + request.Packaging +
            request.OutboundShipping + warrantyAllowance + request.OtherCosts, 2);

        var investment = Math.Round(request.ProposedPurchasePrice + costsExPurchase, 2);
        var profit = Math.Round(request.EstimatedResalePrice - investment, 2);
        var margin = request.EstimatedResalePrice <= 0
            ? 0m
            : Math.Round(profit / request.EstimatedResalePrice * 100m, 2);
        var roi = request.ProposedPurchasePrice <= 0
            ? 0m
            : Math.Round(profit / request.ProposedPurchasePrice * 100m, 2);

        var maxByProfit = request.EstimatedResalePrice - costsExPurchase - minProfit;
        var maxByMargin = request.EstimatedResalePrice
                         - (request.EstimatedResalePrice * (minMargin / 100m))
                         - costsExPurchase;
        var absoluteMax = Math.Max(0, Math.Round(Math.Min(maxByProfit, maxByMargin), 2));
        var minResale = Math.Round(investment + minProfit, 2);
        var negotiation = Math.Max(0, Math.Round(absoluteMax - request.ProposedPurchasePrice, 2));

        var (recommendation, reason) = Classify(margin, profit, minProfit, excellentMin, goodMin, acceptableMin,
            request.ProposedPurchasePrice, absoluteMax);

        return new RefurbBuyCalcResult(
            request.EstimatedResalePrice,
            request.ProposedPurchasePrice,
            costsExPurchase,
            investment,
            profit,
            margin,
            roi,
            absoluteMax,
            minResale,
            negotiation,
            recommendation,
            reason);
    }

    private static (string Rec, string Reason) Classify(
        decimal margin, decimal profit, decimal minProfit,
        decimal excellentMin, decimal goodMin, decimal acceptableMin,
        decimal purchase, decimal absoluteMax)
    {
        if (purchase > absoluteMax && absoluteMax > 0)
            return ("Avoid", $"Proposed purchase exceeds maximum recommended price of ${absoluteMax:0.00} AUD.");
        if (profit < 0)
            return ("High Risk", "Estimated profit is negative at the proposed purchase price.");
        if (profit < minProfit)
            return ("Low Margin", $"Estimated profit ${profit:0.00} is below the minimum profit target of ${minProfit:0.00}.");
        if (margin >= excellentMin)
            return ("Excellent Buy", $"Estimated margin {margin:0.#}% meets the excellent-buy threshold ({excellentMin:0.#}%).");
        if (margin >= goodMin)
            return ("Good Buy", $"Estimated margin {margin:0.#}% meets the good-buy threshold ({goodMin:0.#}%).");
        if (margin >= acceptableMin)
            return ("Acceptable", $"Estimated margin {margin:0.#}% meets the minimum acceptable threshold ({acceptableMin:0.#}%).");
        return ("Low Margin", $"Estimated margin {margin:0.#}% is below the acceptable threshold ({acceptableMin:0.#}%).");
    }
}

public sealed class RefurbDashboardService : IRefurbDashboardService
{
    private readonly WorkshopDbContext _db;

    public RefurbDashboardService(WorkshopDbContext db) => _db = db;

    public async Task<RefurbDashboardDto> GetAsync(DateTimeOffset? from, DateTimeOffset? to, CancellationToken ct = default)
    {
        var end = to ?? DateTimeOffset.UtcNow;
        var start = from ?? end.AddDays(-90);

        var devices = await _db.UsedDevices.AsNoTracking()
            .Where(d => d.ArchivedAt == null)
            .ToListAsync(ct);

        var leadsOpen = await _db.AcquisitionLeads.AsNoTracking()
            .CountAsync(l => l.ArchivedAt == null &&
                             l.Status != "Purchased" &&
                             l.Status != "Rejected" &&
                             l.Status != "Cancelled" &&
                             l.Status != "Sold Elsewhere" &&
                             l.Status != "Returned to Seller", ct);

        static bool StatusIs(UsedDevice d, params string[] names) =>
            names.Any(n => d.Status.Equals(n, StringComparison.OrdinalIgnoreCase));

        var pipeline = new RefurbPipelineCountsDto(
            TotalAcquired: devices.Count,
            AwaitingInspection: devices.Count(d => StatusIs(d, "Purchased", "Inspection")),
            UnderRepair: devices.Count(d => StatusIs(d, "Repair", "Refurbishing", "Cleaning")),
            AwaitingParts: devices.Count(d => StatusIs(d, "Parts Required")),
            QualityTesting: devices.Count(d => StatusIs(d, "Testing", "Final Quality Check", "Final QC")),
            ReadyForSale: devices.Count(d => StatusIs(d, "Ready for Sale", "In stock")),
            Listed: devices.Count(d => StatusIs(d, "Listed")),
            Sold: devices.Count(d => StatusIs(d, "Sold")),
            Reserved: devices.Count(d => StatusIs(d, "Reserved")),
            PartsOnly: devices.Count(d => d.IsPartsOnly),
            WrittenOff: devices.Count(d => StatusIs(d, "Written Off", "Failed Repair")),
            OpenAcquisitionLeads: leadsOpen);

        var unsold = devices.Where(d => !StatusIs(d, "Sold")).ToList();
        var sold = devices.Where(d => StatusIs(d, "Sold")).ToList();
        var soldInPeriod = sold.Where(d => (d.SoldAt ?? d.UpdatedAt) >= start && (d.SoldAt ?? d.UpdatedAt) <= end).ToList();
        var acquiredInPeriod = devices.Where(d => d.CreatedAt >= start && d.CreatedAt <= end).ToList();

        var purchaseSpend = devices.Sum(d => d.PurchasePrice);
        var refurbCosts = devices.Sum(d => d.ExpectedRepairCost + d.LabourCost + d.ShippingInCost + d.PackagingCost);
        var inventoryInvestment = unsold.Sum(d => d.TotalCost);
        var inventoryResale = unsold.Sum(d => d.ExpectedResale);
        var projectedProfit = inventoryResale - inventoryInvestment;
        var realisedProfit = sold.Where(d => d.ActualSalePrice.HasValue)
            .Sum(d => d.ActualSalePrice!.Value - d.TotalCost - d.MarketplaceFees - d.PaymentFees - d.ShippingOutCost - d.WarrantyAllowance);
        var monthlyRevenue = soldInPeriod.Where(d => d.ActualSalePrice.HasValue).Sum(d => d.ActualSalePrice!.Value);
        var avgProfit = soldInPeriod.Count == 0
            ? 0m
            : Math.Round(soldInPeriod.Where(d => d.ActualSalePrice.HasValue)
                .Select(d => d.ActualSalePrice!.Value - d.TotalCost).DefaultIfEmpty(0).Average(), 2);
        var roi = purchaseSpend <= 0 ? 0m : Math.Round(realisedProfit / purchaseSpend * 100m, 2);

        var financials = new RefurbFinancialSummaryDto(
            Math.Round(purchaseSpend, 2),
            Math.Round(refurbCosts, 2),
            Math.Round(inventoryInvestment, 2),
            Math.Round(inventoryResale, 2),
            Math.Round(projectedProfit, 2),
            Math.Round(realisedProfit, 2),
            Math.Round(monthlyRevenue, 2),
            avgProfit,
            roi);

        double? avgRefurbDays = null;
        var refurbished = devices.Where(d =>
            StatusIs(d, "Ready for Sale", "Listed", "Sold", "Reserved") && d.ReceivedAt.HasValue).ToList();
        if (refurbished.Count > 0)
        {
            avgRefurbDays = refurbished
                .Select(d => ((d.ListedAt ?? d.SoldAt ?? d.UpdatedAt) - d.ReceivedAt!.Value).TotalDays)
                .Where(x => x >= 0)
                .DefaultIfEmpty()
                .Average();
        }

        double? avgSellDays = null;
        var soldWithList = sold.Where(d => d.ListedAt.HasValue && d.SoldAt.HasValue).ToList();
        if (soldWithList.Count > 0)
            avgSellDays = soldWithList.Average(d => (d.SoldAt!.Value - d.ListedAt!.Value).TotalDays);

        var performance = new RefurbPerformanceDto(
            avgRefurbDays.HasValue ? Math.Round(avgRefurbDays.Value, 1) : null,
            avgSellDays.HasValue ? Math.Round(avgSellDays.Value, 1) : null,
            soldInPeriod.Count,
            acquiredInPeriod.Count);

        var brands = devices
            .GroupBy(d => string.IsNullOrWhiteSpace(d.Brand) ? "Unknown" : d.Brand!)
            .Select(g => new NamedCountDto(g.Key, g.Count()))
            .OrderByDescending(x => x.Count)
            .Take(12)
            .ToList();

        var sources = devices
            .GroupBy(d => string.IsNullOrWhiteSpace(d.Source) ? "Unknown" : d.Source!)
            .Select(g => new NamedCountDto(g.Key, g.Count()))
            .OrderByDescending(x => x.Count)
            .Take(12)
            .ToList();

        var topModels = unsold
            .GroupBy(d => $"{d.Brand} {d.Model}".Trim())
            .Select(g => new NamedMoneyDto(
                string.IsNullOrWhiteSpace(g.Key) ? "Unknown" : g.Key,
                Math.Round(g.Sum(d => d.ExpectedResale - d.TotalCost), 2)))
            .OrderByDescending(x => x.Amount)
            .Take(10)
            .ToList();

        var monthlyAcq = acquiredInPeriod
            .GroupBy(d => d.CreatedAt.ToOffset(TimeSpan.FromHours(10)).ToString("yyyy-MM")) // AEST approx for labels
            .Select(g => new NamedCountDto(g.Key, g.Count()))
            .OrderBy(x => x.Name)
            .ToList();

        var monthlySales = soldInPeriod
            .Where(d => d.ActualSalePrice.HasValue)
            .GroupBy(d => (d.SoldAt ?? d.UpdatedAt).ToOffset(TimeSpan.FromHours(10)).ToString("yyyy-MM"))
            .Select(g => new NamedMoneyDto(g.Key, Math.Round(g.Sum(d => d.ActualSalePrice!.Value), 2)))
            .OrderBy(x => x.Name)
            .ToList();

        return new RefurbDashboardDto(
            pipeline, financials, performance,
            brands, sources, topModels, monthlyAcq, monthlySales,
            start, end);
    }
}

public sealed class AcquisitionService : IAcquisitionService
{
    public static readonly string[] AllowedStatuses =
    [
        "New Lead", "Under Evaluation", "Offer Prepared", "Offer Sent", "Negotiating",
        "Accepted", "Purchased", "Awaiting Arrival", "Received", "Inspection",
        "Rejected", "Cancelled", "Seller Unresponsive", "Sold Elsewhere", "Returned to Seller"
    ];

    private static readonly string[] Sources =
    [
        "Facebook Marketplace", "eBay", "Gumtree", "Trade-In", "Walk-In",
        "Wholesale", "Buy/Sell Group", "Other Online", "Other"
    ];

    private readonly WorkshopDbContext _db;
    private readonly IAuditService _audit;
    private readonly IRefurbBuyCalculatorService _calc;
    private readonly IUsedTechService _used;

    public AcquisitionService(
        WorkshopDbContext db, IAuditService audit, IRefurbBuyCalculatorService calc, IUsedTechService used)
    {
        _db = db;
        _audit = audit;
        _calc = calc;
        _used = used;
    }

    public async Task<IReadOnlyList<AcquisitionLeadListItemDto>> ListAsync(
        string? q = null, string? status = null, CancellationToken ct = default)
    {
        var query = _db.AcquisitionLeads.AsNoTracking()
            .Include(l => l.Offers)
            .Where(l => l.ArchivedAt == null);

        if (!string.IsNullOrWhiteSpace(status) && !status.Equals("All", StringComparison.OrdinalIgnoreCase))
            query = query.Where(l => l.Status == status.Trim());

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLowerInvariant();
            query = query.Where(l =>
                l.Reference.ToLower().Contains(term) ||
                l.Brand.ToLower().Contains(term) ||
                l.Model.ToLower().Contains(term) ||
                (l.SellerName != null && l.SellerName.ToLower().Contains(term)) ||
                (l.Imei != null && l.Imei.ToLower().Contains(term)) ||
                (l.ListingUrl != null && l.ListingUrl.ToLower().Contains(term)));
        }

        var rows = await query.OrderByDescending(l => l.UpdatedAt).Take(300).ToListAsync(ct);
        return rows.Select(MapList).ToList();
    }

    public async Task<AcquisitionLeadDetailDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var lead = await LoadAsync(id, ct);
        return await MapDetailAsync(lead, ct);
    }

    public async Task<AcquisitionLeadDetailDto> CreateAsync(
        CreateAcquisitionLeadRequest request, Guid actorId, CancellationToken ct = default)
    {
        var brand = Req(request.Brand, "Brand");
        var model = Req(request.Model, "Model");
        var reference = await NextReferenceAsync(ct);

        var lead = new AcquisitionLead
        {
            Reference = reference,
            Status = "New Lead",
            Brand = brand,
            Model = model,
            ModelNumber = Norm(request.ModelNumber),
            Category = Norm(request.Category) ?? "Phone",
            StorageCapacity = Norm(request.StorageCapacity),
            Colour = Norm(request.Colour),
            Imei = Norm(request.Imei),
            Serial = Norm(request.Serial),
            Carrier = Norm(request.Carrier),
            SellerName = Norm(request.SellerName),
            SellerContact = Norm(request.SellerContact),
            SellerEmail = Norm(request.SellerEmail),
            SellerPhone = Norm(request.SellerPhone),
            SellerType = Norm(request.SellerType) ?? "Individual",
            AcquisitionSource = NormalizeSource(request.AcquisitionSource),
            ListingUrl = Norm(request.ListingUrl),
            AskingPrice = request.AskingPrice,
            ProposedOffer = request.ProposedOffer,
            DeviceCondition = Norm(request.DeviceCondition),
            KnownFaults = Norm(request.KnownFaults),
            SuspectedFaults = Norm(request.SuspectedFaults),
            ActivationLockStatus = Norm(request.ActivationLockStatus) ?? "Unknown",
            PhotoUrlsJson = JsonSerializer.Serialize(request.PhotoUrls ?? Array.Empty<string>()),
            Notes = Norm(request.Notes),
            AssignedStaffId = request.AssignedStaffId,
            FollowUpAt = request.FollowUpAt,
            SellerCustomerId = request.SellerCustomerId
        };
        _db.AcquisitionLeads.Add(lead);
        await _db.SaveChangesAsync(ct);

        _db.AcquisitionEvents.Add(new AcquisitionEvent
        {
            AcquisitionLeadId = lead.Id,
            ActorUserId = actorId,
            EventType = "created",
            Summary = $"Lead created from {lead.AcquisitionSource}"
        });

        // Duplicate listing / IMEI hint (non-blocking)
        if (!string.IsNullOrWhiteSpace(lead.ListingUrl) || !string.IsNullOrWhiteSpace(lead.Imei))
        {
            var dup = await FindDuplicateHintAsync(lead.ListingUrl, lead.Imei, lead.Id, ct);
            if (dup is not null)
                _db.AcquisitionEvents.Add(new AcquisitionEvent
                {
                    AcquisitionLeadId = lead.Id,
                    ActorUserId = actorId,
                    EventType = "duplicate_hint",
                    Summary = dup
                });
        }

        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(actorId, "acquisition.create", "AcquisitionLead", lead.Id.ToString(), ct: ct);
        return await MapDetailAsync(await LoadAsync(lead.Id, ct), ct);
    }

    public async Task<AcquisitionLeadDetailDto> UpdateAsync(
        Guid id, UpdateAcquisitionLeadRequest request, Guid actorId, CancellationToken ct = default)
    {
        var lead = await _db.AcquisitionLeads
            .FirstOrDefaultAsync(l => l.Id == id && l.ArchivedAt == null, ct)
            ?? throw new AppException("not_found", "Acquisition lead was not found.", 404);

        lead.Brand = Req(request.Brand, "Brand");
        lead.Model = Req(request.Model, "Model");
        lead.ModelNumber = Norm(request.ModelNumber);
        lead.Category = Norm(request.Category) ?? lead.Category;
        lead.StorageCapacity = Norm(request.StorageCapacity);
        lead.Colour = Norm(request.Colour);
        lead.Imei = Norm(request.Imei);
        lead.Serial = Norm(request.Serial);
        lead.Carrier = Norm(request.Carrier);
        lead.SellerName = Norm(request.SellerName);
        lead.SellerContact = Norm(request.SellerContact);
        lead.SellerEmail = Norm(request.SellerEmail);
        lead.SellerPhone = Norm(request.SellerPhone);
        lead.SellerType = Norm(request.SellerType) ?? lead.SellerType;
        lead.AcquisitionSource = NormalizeSource(request.AcquisitionSource);
        lead.ListingUrl = Norm(request.ListingUrl);
        lead.AskingPrice = request.AskingPrice;
        lead.ProposedOffer = request.ProposedOffer;
        lead.AcceptedPurchasePrice = request.AcceptedPurchasePrice;
        lead.DeviceCondition = Norm(request.DeviceCondition);
        lead.KnownFaults = Norm(request.KnownFaults);
        lead.SuspectedFaults = Norm(request.SuspectedFaults);
        lead.ActivationLockStatus = Norm(request.ActivationLockStatus) ?? lead.ActivationLockStatus;
        if (request.PhotoUrls is not null)
            lead.PhotoUrlsJson = JsonSerializer.Serialize(request.PhotoUrls);
        lead.Notes = Norm(request.Notes);
        lead.PurchaseDate = request.PurchaseDate;
        lead.PaymentMethod = Norm(request.PaymentMethod);
        lead.ShippingOrCollection = Norm(request.ShippingOrCollection);
        lead.AssignedStaffId = request.AssignedStaffId;
        lead.FollowUpAt = request.FollowUpAt;
        lead.SellerCustomerId = request.SellerCustomerId;
        lead.UpdatedAt = DateTimeOffset.UtcNow;
        _db.AcquisitionEvents.Add(new AcquisitionEvent
        {
            AcquisitionLeadId = lead.Id,
            ActorUserId = actorId,
            EventType = "updated",
            Summary = "Lead details updated"
        });

        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(actorId, "acquisition.update", "AcquisitionLead", id.ToString(), ct: ct);
        return await MapDetailAsync(await LoadAsync(id, ct), ct);
    }

    public async Task<AcquisitionLeadDetailDto> UpdateStatusAsync(
        Guid id, UpdateAcquisitionStatusRequest request, Guid actorId, CancellationToken ct = default)
    {
        var lead = await _db.AcquisitionLeads
            .FirstOrDefaultAsync(l => l.Id == id && l.ArchivedAt == null, ct)
            ?? throw new AppException("not_found", "Acquisition lead was not found.", 404);

        var next = AllowedStatuses.FirstOrDefault(s => s.Equals(request.Status.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? throw new ValidationAppException($"Status must be one of: {string.Join(", ", AllowedStatuses)}");

        var previous = lead.Status;
        lead.Status = next;
        if (!string.IsNullOrWhiteSpace(request.Notes))
            lead.Notes = string.IsNullOrWhiteSpace(lead.Notes) ? request.Notes.Trim() : $"{lead.Notes}\n{request.Notes.Trim()}";
        lead.UpdatedAt = DateTimeOffset.UtcNow;
        _db.AcquisitionEvents.Add(new AcquisitionEvent
        {
            AcquisitionLeadId = lead.Id,
            ActorUserId = actorId,
            EventType = "status",
            Summary = $"Status {previous} → {next}"
        });
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(actorId, "acquisition.status", "AcquisitionLead", id.ToString(), newValue: next, ct: ct);
        return await MapDetailAsync(await LoadAsync(id, ct), ct);
    }

    public async Task<AcquisitionOfferDto> AddOfferAsync(
        Guid id, CreateAcquisitionOfferRequest request, Guid actorId, CancellationToken ct = default)
    {
        if (request.Amount < 0)
            throw new ValidationAppException("Offer amount cannot be negative.");

        var lead = await _db.AcquisitionLeads
            .FirstOrDefaultAsync(l => l.Id == id && l.ArchivedAt == null, ct)
            ?? throw new AppException("not_found", "Acquisition lead was not found.", 404);

        RefurbBuyCalcResult? calc = null;
        if (request.EstimatedResalePrice is decimal resale && resale > 0)
        {
            calc = await _calc.CalculateAsync(new RefurbBuyCalcRequest(
                resale,
                request.Amount,
                request.EstimatedPartsCost ?? 0,
                TechnicianLabour: request.LabourAllowance ?? 0), ct);
        }

        var openOffers = await _db.AcquisitionOffers
            .Where(o => o.AcquisitionLeadId == id && (o.Status == "Draft" || o.Status == "Prepared" || o.Status == "Sent"))
            .ToListAsync(ct);
        foreach (var existing in openOffers)
            existing.Status = "Superseded";

        var offer = new AcquisitionOffer
        {
            AcquisitionLeadId = lead.Id,
            Amount = request.Amount,
            Status = request.MarkAsSent ? "Sent" : "Prepared",
            Message = Norm(request.Message),
            Notes = Norm(request.Notes),
            CreatedById = actorId,
            SentAt = request.MarkAsSent ? DateTimeOffset.UtcNow : null,
            EstimatedResalePrice = request.EstimatedResalePrice,
            EstimatedPartsCost = request.EstimatedPartsCost,
            LabourAllowance = request.LabourAllowance,
            EstimatedProfit = calc?.GrossProfit,
            EstimatedMarginPercent = calc?.ProfitMarginPercent,
            AbsoluteMaxPurchasePrice = calc?.MaximumRecommendedPurchasePrice,
            BuyRecommendation = calc?.Recommendation
        };
        _db.AcquisitionOffers.Add(offer);
        _db.AcquisitionEvents.Add(new AcquisitionEvent
        {
            AcquisitionLeadId = lead.Id,
            ActorUserId = actorId,
            EventType = request.MarkAsSent ? "offer_sent" : "offer_prepared",
            Summary = $"Offer ${request.Amount:0.00} AUD ({offer.BuyRecommendation ?? "unscored"})"
        });

        lead.ProposedOffer = request.Amount;
        lead.Status = request.MarkAsSent ? "Offer Sent" : "Offer Prepared";
        lead.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(actorId, "acquisition.offer", "AcquisitionLead", id.ToString(), ct: ct);
        return MapOffer(offer);
    }

    public async Task<UsedDeviceDetailDto> ConvertToInventoryAsync(
        Guid id, ConvertAcquisitionRequest request, Guid actorId, CancellationToken ct = default)
    {
        var lead = await _db.AcquisitionLeads
            .FirstOrDefaultAsync(l => l.Id == id && l.ArchivedAt == null, ct)
            ?? throw new AppException("not_found", "Acquisition lead was not found.", 404);

        if (lead.UsedDeviceId is Guid existingId)
            return await _used.GetAsync(existingId, ct);

        var purchase = request.PurchasePriceOverride
            ?? lead.AcceptedPurchasePrice
            ?? lead.ProposedOffer
            ?? lead.AskingPrice
            ?? 0m;

        var stockId = request.AssignStockId ? await NextRefurbStockIdAsync(ct) : null;
        var status = string.IsNullOrWhiteSpace(request.InitialStatus) ? "Purchased" : request.InitialStatus!;

        var device = new UsedDevice
        {
            Summary = $"{lead.Brand} {lead.Model}".Trim(),
            Brand = lead.Brand,
            Model = lead.Model,
            ModelNumber = lead.ModelNumber,
            Category = lead.Category,
            Serial = lead.Serial,
            Imei = lead.Imei,
            Colour = lead.Colour,
            StorageCapacity = lead.StorageCapacity,
            Carrier = lead.Carrier,
            Status = status,
            ConditionGrade = "Good",
            PurchasePrice = purchase,
            ExpectedRepairCost = request.ExpectedPartsCostOverride ?? 0,
            ExpectedResale = request.ExpectedResaleOverride ?? 0,
            Faults = string.Join(" · ", new[] { lead.KnownFaults, lead.SuspectedFaults }.Where(x => !string.IsNullOrWhiteSpace(x))),
            Notes = $"From acquisition {lead.Reference}. {lead.Notes}".Trim(),
            Source = lead.AcquisitionSource,
            SellerCustomerId = lead.SellerCustomerId,
            StockId = stockId,
            Sku = stockId,
            LocationBin = Norm(request.LocationBin),
            AcquisitionLeadId = lead.Id,
            PurchaseDate = lead.PurchaseDate ?? DateTimeOffset.UtcNow,
            ReceivedAt = lead.Status is "Received" or "Inspection" ? DateTimeOffset.UtcNow : null,
            ActivationLockStatus = lead.ActivationLockStatus
        };

        _db.UsedDevices.Add(device);
        await _db.SaveChangesAsync(ct);

        lead.UsedDeviceId = device.Id;
        lead.AcceptedPurchasePrice = purchase;
        lead.Status = "Purchased";
        lead.PurchaseDate ??= DateTimeOffset.UtcNow;
        lead.UpdatedAt = DateTimeOffset.UtcNow;
        _db.AcquisitionEvents.Add(new AcquisitionEvent
        {
            AcquisitionLeadId = lead.Id,
            ActorUserId = actorId,
            EventType = "converted",
            Summary = stockId is null
                ? "Converted to refurbished inventory"
                : $"Converted to inventory {stockId}"
        });
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(actorId, "acquisition.convert", "UsedDevice", device.Id.ToString(), ct: ct);
        return await _used.GetAsync(device.Id, ct);
    }

    private async Task<AcquisitionLead> LoadAsync(Guid id, CancellationToken ct) =>
        await _db.AcquisitionLeads.AsNoTracking()
            .Include(l => l.Offers).Include(l => l.Events)
            .FirstOrDefaultAsync(l => l.Id == id && l.ArchivedAt == null, ct)
        ?? throw new AppException("not_found", "Acquisition lead was not found.", 404);

    private async Task<string?> FindDuplicateHintAsync(string? url, string? imei, Guid? excludeId, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(url))
        {
            var urlHit = await _db.AcquisitionLeads.AsNoTracking()
                .Where(l => l.ArchivedAt == null && l.ListingUrl == url && (excludeId == null || l.Id != excludeId))
                .Select(l => l.Reference)
                .FirstOrDefaultAsync(ct);
            if (urlHit is not null)
                return $"Possible duplicate listing URL — matches lead {urlHit}.";
        }

        if (!string.IsNullOrWhiteSpace(imei))
        {
            var imeiHit = await _db.AcquisitionLeads.AsNoTracking()
                .Where(l => l.ArchivedAt == null && l.Imei == imei && (excludeId == null || l.Id != excludeId))
                .Select(l => l.Reference)
                .FirstOrDefaultAsync(ct);
            if (imeiHit is not null)
                return $"Possible duplicate IMEI — matches lead {imeiHit}.";

            var deviceHit = await _db.UsedDevices.AsNoTracking()
                .Where(d => d.ArchivedAt == null && d.Imei == imei)
                .Select(d => d.StockId ?? d.Summary)
                .FirstOrDefaultAsync(ct);
            if (deviceHit is not null)
                return $"IMEI already exists on refurbished inventory ({deviceHit}).";
        }

        return null;
    }

    private async Task<string> NextReferenceAsync(CancellationToken ct)
    {
        var key = "doc:ACQ:lead";
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
        return $"TB-ACQ-{value:D5}";
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

    private static AcquisitionLeadListItemDto MapList(AcquisitionLead l)
    {
        var latest = l.Offers.OrderByDescending(o => o.CreatedAt).FirstOrDefault();
        return new AcquisitionLeadListItemDto(
            l.Id, l.Reference, l.Status, l.Brand, l.Model, l.Category, l.AcquisitionSource,
            l.SellerName, l.AskingPrice, l.ProposedOffer, l.AcceptedPurchasePrice,
            latest?.BuyRecommendation, l.FollowUpAt, l.AssignedStaffId, l.UsedDeviceId,
            l.CreatedAt, l.UpdatedAt);
    }

    private async Task<AcquisitionLeadDetailDto> MapDetailAsync(AcquisitionLead l, CancellationToken ct)
    {
        string? stockId = null;
        if (l.UsedDeviceId is Guid uid)
            stockId = await _db.UsedDevices.AsNoTracking()
                .Where(d => d.Id == uid).Select(d => d.StockId).FirstOrDefaultAsync(ct);

        var photos = Array.Empty<string>();
        try { photos = JsonSerializer.Deserialize<string[]>(l.PhotoUrlsJson ?? "[]") ?? Array.Empty<string>(); }
        catch { /* ignore malformed */ }

        return new AcquisitionLeadDetailDto(
            l.Id, l.Reference, l.Status, l.Brand, l.Model, l.ModelNumber, l.Category,
            l.StorageCapacity, l.Colour, l.Imei, l.Serial, l.Carrier,
            l.SellerName, l.SellerContact, l.SellerEmail, l.SellerPhone, l.SellerType,
            l.AcquisitionSource, l.ListingUrl, l.AskingPrice, l.ProposedOffer, l.AcceptedPurchasePrice,
            l.DeviceCondition, l.KnownFaults, l.SuspectedFaults, l.ActivationLockStatus,
            photos, l.Notes, l.PurchaseDate, l.PaymentMethod, l.ShippingOrCollection,
            l.AssignedStaffId, l.FollowUpAt, l.UsedDeviceId, stockId, l.SellerCustomerId,
            l.Offers.OrderByDescending(o => o.CreatedAt).Select(MapOffer).ToList(),
            l.Events.OrderBy(e => e.CreatedAt).Select(e => new AcquisitionEventDto(e.CreatedAt, e.EventType, e.Summary)).ToList(),
            l.CreatedAt, l.UpdatedAt);
    }

    private static AcquisitionOfferDto MapOffer(AcquisitionOffer o) => new(
        o.Id, o.Amount, o.Status, o.Message, o.Notes,
        o.EstimatedResalePrice, o.EstimatedPartsCost, o.LabourAllowance,
        o.EstimatedProfit, o.EstimatedMarginPercent, o.AbsoluteMaxPurchasePrice, o.BuyRecommendation,
        o.CreatedAt, o.SentAt, o.RespondedAt);

    private static string NormalizeSource(string? source)
    {
        if (string.IsNullOrWhiteSpace(source)) return "Other";
        var match = Sources.FirstOrDefault(s => s.Equals(source.Trim(), StringComparison.OrdinalIgnoreCase));
        return match ?? source.Trim();
    }

    private static string Req(string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ValidationAppException($"{field} is required.");
        return value.Trim();
    }

    private static string? Norm(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
