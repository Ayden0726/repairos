using WorkshopOS.Contracts.Operations;

namespace WorkshopOS.Infrastructure.Services;

/// <summary>Decimal-safe pricing engine. Server is source of truth for quote totals.</summary>
public static class PricingCalculator
{
    public static PricingSettingsDto DefaultSettings() => new(
        new LabourPricingDto(50m, 0m, false,
        [
            new DifficultyLevelDto("easy", "Easy", 40m, 1),
            new DifficultyLevelDto("standard", "Standard", 50m, 2),
            new DifficultyLevelDto("hard", "Hard", 80m, 3),
            new DifficultyLevelDto("complex", "Complex", 120m, 4)
        ]),
        new PartsPricingDto("FlatPercent", 20m, 0m, 0m),
        new ProfitabilityPricingDto(25m, 30m, true),
        new RoundingPricingDto("End9", null),
        new DiscountPricingDto(10m, 10m),
        new QuoteDefaultsDto(14, true),
        new TaxPricingDto(true, 0.10m, true));

    public static PricingPreviewResponse Calculate(
        PricingPreviewRequest request,
        PricingSettingsDto settings,
        IReadOnlyList<MarkupTierDto>? tiers = null,
        IReadOnlyList<ServicePricingDto>? services = null,
        TaxPricingDto? taxOverride = null)
    {
        taxOverride ??= settings.Tax ?? new TaxPricingDto(true, 0.10m, true);
        tiers ??= Array.Empty<MarkupTierDto>();
        services ??= Array.Empty<ServicePricingDto>();

        var lineResults = new List<QuoteLineCalcResult>();
        foreach (var input in request.Lines ?? Array.Empty<QuoteLineCalcInput>())
            lineResults.Add(CalculateLine(input, settings, tiers, services));

        var partsSubtotal = RoundMoney(lineResults.Sum(l => l.PartSell * l.Quantity));
        var labourSubtotal = RoundMoney(lineResults.Sum(l => l.LabourAmount));
        var additional = RoundMoney(lineResults.Sum(l => l.AdditionalAmount));
        var lineDiscounts = RoundMoney(lineResults.Sum(l => l.DiscountAmount));
        var costTotal = RoundMoney(lineResults.Sum(l => l.LineCost));

        var preDiscount = RoundMoney(lineResults.Sum(l => l.LineSubtotal) + additional);
        var quoteDiscount = 0m;
        if (request.DiscountAmount is > 0)
            quoteDiscount = RoundMoney(request.DiscountAmount.Value);
        else if (request.DiscountPercent is > 0)
            quoteDiscount = RoundMoney(preDiscount * (request.DiscountPercent.Value / 100m));

        var discountTotal = RoundMoney(lineDiscounts + quoteDiscount);
        var preRound = RoundMoney(Math.Max(0m, preDiscount - quoteDiscount));

        var roundingMethod = request.OverrideRounding == true && !string.IsNullOrWhiteSpace(request.RoundingMethodOverride)
            ? request.RoundingMethodOverride!
            : settings.Rounding.Method;
        var total = ApplyRounding(preRound, roundingMethod, settings.Rounding.CustomIncrement);

        decimal subtotal;
        decimal gst;
        if (taxOverride.Enabled)
        {
            if (taxOverride.Inclusive)
            {
                (subtotal, gst, _) = MoneyGst.FromInclusiveTotal(total, taxOverride.Rate);
            }
            else
            {
                (subtotal, gst, total) = MoneyGst.FromExclusiveTotal(total, taxOverride.Rate);
            }
        }
        else
        {
            subtotal = total;
            gst = 0m;
        }

        // Gross profit vs customer total (labour cost treated as 0 unless embedded in landed).
        var profit = RoundMoney(total - costTotal);
        var margin = total > 0 ? Math.Round(profit / total * 100m, 2, MidpointRounding.AwayFromZero) : 0m;

        var belowMin = margin < settings.Profitability.MinimumGrossMarginPercent;
        var warn = margin < settings.Profitability.WarnBelowMarginPercent;
        var requiresApproval = belowMin && settings.Profitability.ManagerApprovalRequired;

        string? warning = null;
        if (belowMin)
            warning = $"Margin {margin:0.##}% is below minimum {settings.Profitability.MinimumGrossMarginPercent:0.##}%.";
        else if (warn)
            warning = $"Margin {margin:0.##}% is below warning threshold {settings.Profitability.WarnBelowMarginPercent:0.##}%.";

        return new PricingPreviewResponse(
            lineResults, partsSubtotal, labourSubtotal, discountTotal, costTotal,
            preRound, total, subtotal, gst, profit, margin, belowMin, requiresApproval,
            roundingMethod, warning);
    }

    public static QuoteLineCalcResult CalculateLine(
        QuoteLineCalcInput input,
        PricingSettingsDto settings,
        IReadOnlyList<MarkupTierDto> tiers,
        IReadOnlyList<ServicePricingDto> services)
    {
        var qty = input.Quantity <= 0 ? 1m : input.Quantity;
        var landed = RoundMoney(input.PartCost + input.ShippingCost + input.OtherCost);

        var service = input.ServicePricingId is Guid sid
            ? services.FirstOrDefault(s => s.Id == sid)
            : null;

        decimal markupPercent;
        decimal markupAmount;
        decimal partSell;

        if (input.PartSellOverride is decimal sellOverride)
        {
            partSell = RoundMoney(sellOverride);
            markupAmount = RoundMoney(partSell - landed);
            markupPercent = landed > 0 ? RoundMoney(markupAmount / landed * 100m) : 0m;
        }
        else if (input.MarkupAmountOverride is decimal amtOverride)
        {
            markupAmount = RoundMoney(amtOverride);
            partSell = RoundMoney(landed + markupAmount);
            markupPercent = landed > 0 ? RoundMoney(markupAmount / landed * 100m) : 0m;
        }
        else
        {
            markupPercent = input.MarkupPercentOverride
                ?? service?.DefaultPartMarkupPercent
                ?? ResolveMarkupPercent(landed, settings.Parts, tiers);
            markupAmount = RoundMoney(landed * (markupPercent / 100m));
            if (settings.Parts.MarkupMethod.Equals("Fixed", StringComparison.OrdinalIgnoreCase))
            {
                markupAmount = RoundMoney(settings.Parts.FixedMarkupAmount);
                markupPercent = landed > 0 ? RoundMoney(markupAmount / landed * 100m) : 0m;
            }
            else if (settings.Parts.MarkupMethod.Equals("Hybrid", StringComparison.OrdinalIgnoreCase))
            {
                var percentAmt = RoundMoney(landed * (markupPercent / 100m));
                markupAmount = RoundMoney(percentAmt + settings.Parts.FixedMarkupAmount);
                markupPercent = landed > 0 ? RoundMoney(markupAmount / landed * 100m) : 0m;
            }

            partSell = RoundMoney(landed + markupAmount);
            if (settings.Parts.MinimumPartProfit > 0 && markupAmount < settings.Parts.MinimumPartProfit)
            {
                markupAmount = RoundMoney(settings.Parts.MinimumPartProfit);
                partSell = RoundMoney(landed + markupAmount);
                markupPercent = landed > 0 ? RoundMoney(markupAmount / landed * 100m) : 0m;
            }
        }

        var labour = input.LabourOverride
            ?? ResolveLabour(input.DifficultyLevelKey, settings, service);
        labour = RoundMoney(Math.Max(labour, settings.Labour.MinimumLabourFee));

        var additional = RoundMoney(input.AdditionalAmount);
        var discount = RoundMoney(Math.Max(0m, input.DiscountAmount));

        // Unit price is per-unit customer-facing sell for the line (parts portion); labour/additional are line-level.
        var unitPrice = input.UnitPriceOverride ?? partSell;
        if (input.UnitPriceOverride is decimal)
        {
            partSell = RoundMoney(input.UnitPriceOverride.Value);
            markupAmount = RoundMoney(partSell - landed);
            markupPercent = landed > 0 ? RoundMoney(markupAmount / landed * 100m) : 0m;
        }

        var lineSubtotal = RoundMoney((unitPrice * qty) + labour + additional - discount);
        var lineCost = RoundMoney(landed * qty);
        var lineProfit = RoundMoney(lineSubtotal - lineCost);
        var lineTotal = lineSubtotal;

        return new QuoteLineCalcResult(
            string.IsNullOrWhiteSpace(input.Type) ? "PART" : input.Type.Trim(),
            input.Description?.Trim() ?? string.Empty,
            input.ServiceName, input.PartName, input.SupplierName, input.Sku,
            input.InventoryItemId, input.ServicePricingId, input.DifficultyLevelKey,
            qty, RoundMoney(input.PartCost), RoundMoney(input.ShippingCost), RoundMoney(input.OtherCost),
            landed, markupPercent, markupAmount, partSell, labour, additional, discount,
            unitPrice, lineSubtotal, lineTotal, lineProfit, lineCost);
    }

    public static decimal ResolveMarkupPercent(decimal landedCost, PartsPricingDto parts, IReadOnlyList<MarkupTierDto> tiers)
    {
        if (parts.MarkupMethod.Equals("Tiered", StringComparison.OrdinalIgnoreCase) && tiers.Count > 0)
        {
            var tier = tiers
                .OrderBy(t => t.SortOrder)
                .ThenBy(t => t.MinCost)
                .FirstOrDefault(t => landedCost >= t.MinCost && (t.MaxCost is null || landedCost <= t.MaxCost.Value));
            if (tier is not null) return tier.MarkupPercent;
        }
        return parts.DefaultMarkupPercent;
    }

    public static decimal ResolveLabour(string? difficultyKey, PricingSettingsDto settings, ServicePricingDto? service)
    {
        if (settings.Labour.DifficultyPricingEnabled && !string.IsNullOrWhiteSpace(difficultyKey))
        {
            var level = settings.Labour.DifficultyLevels
                .FirstOrDefault(d => d.Key.Equals(difficultyKey, StringComparison.OrdinalIgnoreCase));
            if (level is not null) return level.LabourFee;
        }
        if (service is not null) return service.DefaultLabourFee;
        return settings.Labour.DefaultLabourFee;
    }

    public static decimal ApplyRounding(decimal amount, string method, decimal? customIncrement)
    {
        amount = RoundMoney(amount);
        return method?.Trim() switch
        {
            "None" or null or "" => amount,
            "Nearest1" => Math.Round(amount, 0, MidpointRounding.AwayFromZero),
            "Nearest5" => RoundToNearest(amount, 5m),
            "Nearest10" => RoundToNearest(amount, 10m),
            "End9" => RoundEndIn9(amount),
            "End995" => RoundEndIn995(amount),
            "Custom" when customIncrement is > 0 => RoundToNearest(amount, customIncrement.Value),
            _ => amount
        };
    }

    /// <summary>Round up to the next whole number ending in 9 (e.g. 237.20 → 239).</summary>
    public static decimal RoundEndIn9(decimal amount)
    {
        var ceil = Math.Ceiling(amount);
        if (ceil <= 0) return 0m;
        var lastDigit = (int)(ceil % 10);
        if (lastDigit == 9 && ceil >= amount) return ceil;
        var add = (9 - lastDigit + 10) % 10;
        if (add == 0) add = 10;
        return ceil + add;
    }

    public static decimal RoundEndIn995(decimal amount)
    {
        // Round up to x9.95
        var dollars = Math.Floor(amount);
        var candidate = dollars - (dollars % 10) + 9.95m;
        if (candidate < amount) candidate += 10m;
        return RoundMoney(candidate);
    }

    public static decimal RoundToNearest(decimal amount, decimal increment)
    {
        if (increment <= 0) return RoundMoney(amount);
        return RoundMoney(Math.Round(amount / increment, MidpointRounding.AwayFromZero) * increment);
    }

    public static decimal RoundMoney(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);
}

/// <summary>GST helpers driven by business tax rate (no hardcoded currency).</summary>
public static class MoneyGst
{
    public const decimal DefaultRate = 0.10m;

    public static (decimal Subtotal, decimal Gst, decimal Total) FromInclusiveTotal(decimal inclusiveTotal, decimal rate = DefaultRate)
    {
        var total = PricingCalculator.RoundMoney(inclusiveTotal);
        if (rate <= 0) return (total, 0m, total);
        var divisor = 1m + rate;
        var subtotal = PricingCalculator.RoundMoney(total / divisor);
        var gst = total - subtotal;
        return (subtotal, gst, total);
    }

    public static (decimal Subtotal, decimal Gst, decimal Total) FromExclusiveTotal(decimal exclusiveTotal, decimal rate = DefaultRate)
    {
        var subtotal = PricingCalculator.RoundMoney(exclusiveTotal);
        if (rate <= 0) return (subtotal, 0m, subtotal);
        var gst = PricingCalculator.RoundMoney(subtotal * rate);
        return (subtotal, gst, subtotal + gst);
    }

    public static (decimal Subtotal, decimal Gst, decimal Total) FromInclusiveLines(
        IEnumerable<(decimal Qty, decimal UnitPrice)> lines, decimal rate = DefaultRate)
    {
        var inclusive = lines.Sum(l => PricingCalculator.RoundMoney(l.Qty * l.UnitPrice));
        return FromInclusiveTotal(inclusive, rate);
    }
}
