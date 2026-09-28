using WorkshopOS.Contracts.Operations;

namespace WorkshopOS.Infrastructure.Services;

/// <summary>
/// Decimal-safe pricing engine. Server is source of truth for quote totals.
/// Labour is one fee for the whole job. Markup is applied once to Σ landed part costs.
/// </summary>
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

        var rawLines = request.Lines ?? Array.Empty<QuoteLineCalcInput>();
        var prepared = new List<(QuoteLineCalcInput Input, decimal Qty, decimal Landed, decimal LineCost, decimal Additional, decimal Discount, decimal? SellOverride)>();

        foreach (var input in rawLines)
        {
            var qty = input.Quantity <= 0 ? 1m : input.Quantity;
            var landed = RoundMoney(input.PartCost + input.ShippingCost + input.OtherCost);
            var sellOverride = input.PartSellOverride ?? input.UnitPriceOverride;
            // Simple/legacy lines with a sell override and no cost: treat UnitPrice as landed cost so markup still applies.
            if (sellOverride is decimal so && landed <= 0m && so > 0m)
                landed = RoundMoney(so);
            var lineCost = RoundMoney(landed * qty);
            prepared.Add((input, qty, landed, lineCost,
                RoundMoney(input.AdditionalAmount),
                RoundMoney(Math.Max(0m, input.DiscountAmount)),
                null)); // sell overrides no longer bypass job markup; cost-driven only
        }

        var partsCostTotal = RoundMoney(prepared.Sum(p => p.LineCost));
        var additionalTotal = RoundMoney(prepared.Sum(p => p.Additional));
        var lineDiscounts = RoundMoney(prepared.Sum(p => p.Discount));

        var jobService = request.ServicePricingId is Guid sid
            ? services.FirstOrDefault(s => s.Id == sid)
            : null;
        // Prefer explicit job service; else first line service if any.
        if (jobService is null)
        {
            var lineSid = prepared.Select(p => p.Input.ServicePricingId).FirstOrDefault(id => id is not null);
            if (lineSid is Guid ls)
                jobService = services.FirstOrDefault(s => s.Id == ls);
        }

        decimal markupPercent;
        decimal markupAmount;

        if (request.MarkupAmount is decimal amtOverride)
        {
            markupAmount = RoundMoney(amtOverride);
            markupPercent = partsCostTotal > 0 ? RoundMoney(markupAmount / partsCostTotal * 100m) : 0m;
        }
        else
        {
            markupPercent = request.MarkupPercent
                ?? jobService?.DefaultPartMarkupPercent
                ?? ResolveMarkupPercent(partsCostTotal, settings.Parts, tiers);
            markupAmount = RoundMoney(partsCostTotal * (markupPercent / 100m));

            if (settings.Parts.MarkupMethod.Equals("Fixed", StringComparison.OrdinalIgnoreCase))
            {
                markupAmount = RoundMoney(settings.Parts.FixedMarkupAmount);
                markupPercent = partsCostTotal > 0 ? RoundMoney(markupAmount / partsCostTotal * 100m) : 0m;
            }
            else if (settings.Parts.MarkupMethod.Equals("Hybrid", StringComparison.OrdinalIgnoreCase))
            {
                var percentAmt = RoundMoney(partsCostTotal * (markupPercent / 100m));
                markupAmount = RoundMoney(percentAmt + settings.Parts.FixedMarkupAmount);
                markupPercent = partsCostTotal > 0 ? RoundMoney(markupAmount / partsCostTotal * 100m) : 0m;
            }

            if (settings.Parts.MinimumPartProfit > 0 && markupAmount < settings.Parts.MinimumPartProfit)
            {
                markupAmount = RoundMoney(settings.Parts.MinimumPartProfit);
                markupPercent = partsCostTotal > 0 ? RoundMoney(markupAmount / partsCostTotal * 100m) : 0m;
            }
        }

        var partsSellTotal = RoundMoney(partsCostTotal + markupAmount);

        var difficultyKey = request.DifficultyLevelKey
            ?? prepared.Select(p => p.Input.DifficultyLevelKey).FirstOrDefault(k => !string.IsNullOrWhiteSpace(k));

        var labourFee = request.LabourFee
            ?? ResolveLabour(difficultyKey, settings, jobService);
        labourFee = RoundMoney(Math.Max(labourFee, settings.Labour.MinimumLabourFee));

        var lineResults = new List<QuoteLineCalcResult>();
        foreach (var p in prepared)
        {
            // Lines list at cost; job markup/labour live on the summary.
            // UnitPrice/PartSell = landed so customer print can still show a unit figure;
            // proportional sell share is applied so line totals sum to PartsSellTotal.
            decimal unitSell;
            decimal lineSell;
            decimal lineMarkupAmt;
            if (partsCostTotal > 0 && p.LineCost > 0)
            {
                lineSell = RoundMoney(partsSellTotal * (p.LineCost / partsCostTotal));
                unitSell = RoundMoney(lineSell / p.Qty);
                lineMarkupAmt = RoundMoney(lineSell - p.LineCost);
            }
            else
            {
                unitSell = p.Landed;
                lineSell = RoundMoney(p.Landed * p.Qty);
                lineMarkupAmt = 0m;
            }

            var lineSubtotal = RoundMoney(lineSell + p.Additional - p.Discount);
            var lineProfit = RoundMoney(lineSubtotal - p.LineCost);

            lineResults.Add(new QuoteLineCalcResult(
                string.IsNullOrWhiteSpace(p.Input.Type) ? "PART" : p.Input.Type.Trim(),
                p.Input.Description?.Trim() ?? string.Empty,
                p.Input.ServiceName, p.Input.PartName, p.Input.SupplierName, p.Input.Sku,
                p.Input.InventoryItemId, p.Input.ServicePricingId, p.Input.DifficultyLevelKey,
                p.Qty, RoundMoney(p.Input.PartCost), RoundMoney(p.Input.ShippingCost), RoundMoney(p.Input.OtherCost),
                p.Landed, markupPercent, lineMarkupAmt, unitSell,
                0m, // labour is job-level only
                p.Additional, p.Discount,
                unitSell, lineSubtotal, lineSubtotal, lineProfit, p.LineCost));
        }

        // Fix rounding drift so Σ line sells == partsSellTotal
        var allocatedSell = RoundMoney(lineResults.Sum(l => l.LineSubtotal - l.AdditionalAmount + l.DiscountAmount));
        if (lineResults.Count > 0 && allocatedSell != partsSellTotal)
        {
            var drift = RoundMoney(partsSellTotal - allocatedSell);
            var last = lineResults[^1];
            var newSub = RoundMoney(last.LineSubtotal + drift);
            var newUnit = last.Quantity > 0 ? RoundMoney((newSub - last.AdditionalAmount + last.DiscountAmount) / last.Quantity) : last.UnitPrice;
            var newMarkup = RoundMoney((newSub - last.AdditionalAmount + last.DiscountAmount) - last.LineCost);
            lineResults[^1] = last with
            {
                PartSell = newUnit,
                UnitPrice = newUnit,
                LineSubtotal = newSub,
                LineTotal = newSub,
                MarkupAmount = newMarkup,
                LineProfit = RoundMoney(newSub - last.LineCost)
            };
        }

        var quoteDiscount = 0m;
        var preDiscount = RoundMoney(partsSellTotal + labourFee + additionalTotal - lineDiscounts);
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

        var profit = RoundMoney(total - partsCostTotal);
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
            lineResults,
            partsSellTotal,
            labourFee,
            discountTotal,
            partsCostTotal,
            preRound, total, subtotal, gst, profit, margin, belowMin, requiresApproval,
            roundingMethod, warning,
            partsCostTotal, partsSellTotal, markupPercent, markupAmount, labourFee, additionalTotal);
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
