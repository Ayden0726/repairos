namespace WorkshopOS.Contracts.Operations;

public sealed record DifficultyLevelDto(string Key, string Name, decimal LabourFee, int SortOrder);

public sealed record LabourPricingDto(
    decimal DefaultLabourFee,
    decimal MinimumLabourFee,
    bool DifficultyPricingEnabled,
    IReadOnlyList<DifficultyLevelDto> DifficultyLevels);

public sealed record PartsPricingDto(
    string MarkupMethod, // FlatPercent | Tiered | Fixed | Hybrid
    decimal DefaultMarkupPercent,
    decimal FixedMarkupAmount,
    decimal MinimumPartProfit);

public sealed record ProfitabilityPricingDto(
    decimal MinimumGrossMarginPercent,
    decimal WarnBelowMarginPercent,
    bool ManagerApprovalRequired);

public sealed record RoundingPricingDto(
    string Method, // None | Nearest1 | Nearest5 | Nearest10 | End9 | End995 | Custom
    decimal? CustomIncrement);

public sealed record DiscountPricingDto(
    decimal MaxTechDiscountPercent,
    decimal ManagerAbovePercent);

public sealed record QuoteDefaultsDto(
    int DefaultValidityDays,
    bool AutoExpire);

public sealed record TaxPricingDto(
    bool Enabled,
    decimal Rate,
    bool Inclusive);

public sealed record PricingSettingsDto(
    LabourPricingDto Labour,
    PartsPricingDto Parts,
    ProfitabilityPricingDto Profitability,
    RoundingPricingDto Rounding,
    DiscountPricingDto Discounts,
    QuoteDefaultsDto Quote,
    TaxPricingDto? Tax);

public sealed record MarkupTierDto(Guid Id, decimal MinCost, decimal? MaxCost, decimal MarkupPercent, int SortOrder);
public sealed record UpsertMarkupTierRequest(Guid? Id, decimal MinCost, decimal? MaxCost, decimal MarkupPercent, int SortOrder);

public sealed record ServicePricingDto(
    Guid Id, string Name, string? Category, string? Description,
    decimal DefaultLabourFee, decimal? DefaultPartMarkupPercent, bool IsActive, int SortOrder);
public sealed record UpsertServicePricingRequest(
    Guid? Id, string Name, string? Category, string? Description,
    decimal DefaultLabourFee, decimal? DefaultPartMarkupPercent, bool IsActive, int SortOrder);

public sealed record QuoteLineCalcInput(
    string Type,
    string Description,
    string? ServiceName,
    string? PartName,
    string? SupplierName,
    string? Sku,
    Guid? InventoryItemId,
    Guid? ServicePricingId,
    string? DifficultyLevelKey,
    decimal Quantity,
    decimal PartCost,
    decimal ShippingCost,
    decimal OtherCost,
    decimal? MarkupPercentOverride,
    decimal? MarkupAmountOverride,
    decimal? PartSellOverride,
    decimal? LabourOverride,
    decimal AdditionalAmount,
    decimal DiscountAmount,
    decimal? UnitPriceOverride);

public sealed record QuoteLineCalcResult(
    string Type,
    string Description,
    string? ServiceName,
    string? PartName,
    string? SupplierName,
    string? Sku,
    Guid? InventoryItemId,
    Guid? ServicePricingId,
    string? DifficultyLevelKey,
    decimal Quantity,
    decimal PartCost,
    decimal ShippingCost,
    decimal OtherCost,
    decimal LandedCost,
    decimal MarkupPercent,
    decimal MarkupAmount,
    decimal PartSell,
    decimal LabourAmount,
    decimal AdditionalAmount,
    decimal DiscountAmount,
    decimal UnitPrice,
    decimal LineSubtotal,
    decimal LineTotal,
    decimal LineProfit,
    decimal LineCost);

public sealed record PricingPreviewRequest(
    IReadOnlyList<QuoteLineCalcInput> Lines,
    decimal? DiscountPercent,
    decimal? DiscountAmount,
    bool? OverrideRounding,
    string? RoundingMethodOverride);

public sealed record PricingPreviewResponse(
    IReadOnlyList<QuoteLineCalcResult> Lines,
    decimal PartsSubtotal,
    decimal LabourSubtotal,
    decimal DiscountTotal,
    decimal CostTotal,
    decimal PreRoundTotal,
    decimal Total,
    decimal Subtotal,
    decimal GstAmount,
    decimal ProfitTotal,
    decimal MarginPercent,
    bool BelowMinimumMargin,
    bool RequiresApproval,
    string RoundingMethod,
    string? Warning);
