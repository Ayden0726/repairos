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
    decimal DefaultLabourFee, decimal? DefaultPartMarkupPercent, bool IsActive, int SortOrder,
    string? Code = null,
    string? Subcategory = null,
    string? DeviceType = null,
    decimal ServiceFee = 0m,
    int? EstimatedMinutes = null,
    decimal? MinCharge = null,
    decimal? DiagnosticFee = null,
    bool PartsRequired = false,
    bool SerialRequired = false,
    int? WarrantyDays = null,
    string? TechNotes = null,
    string? CustomerDescription = null,
    bool IsSystem = false,
    Guid? CategoryId = null,
    IReadOnlyList<string>? CompatibleBrands = null,
    IReadOnlyList<string>? CompatibleModels = null);
public sealed record UpsertServicePricingRequest(
    Guid? Id, string Name, string? Category, string? Description,
    decimal DefaultLabourFee, decimal? DefaultPartMarkupPercent, bool IsActive, int SortOrder,
    string? Code = null,
    string? Subcategory = null,
    string? DeviceType = null,
    decimal ServiceFee = 0m,
    int? EstimatedMinutes = null,
    decimal? MinCharge = null,
    decimal? DiagnosticFee = null,
    bool PartsRequired = false,
    bool SerialRequired = false,
    int? WarrantyDays = null,
    string? TechNotes = null,
    string? CustomerDescription = null,
    Guid? CategoryId = null,
    IReadOnlyList<string>? CompatibleBrands = null,
    IReadOnlyList<string>? CompatibleModels = null);

/// <summary>
/// Line input for quote pricing. Lines carry part costs only; job labour + markup
/// are applied once at the quote level via <see cref="PricingPreviewRequest"/>.
/// Legacy per-line markup/labour override fields are ignored by the calculator.
/// </summary>
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

/// <summary>
/// Line result: part costs (and optional cost-share of job parts sell for print).
/// Labour and job markup live on the preview/quote summary, not per line.
/// </summary>
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
    string? RoundingMethodOverride,
    /// <summary>Job-level labour fee override. Null → settings / difficulty / service default.</summary>
    decimal? LabourFee = null,
    /// <summary>Job-level markup % override applied once to Σ landed part costs.</summary>
    decimal? MarkupPercent = null,
    /// <summary>Job-level fixed markup amount override (instead of %).</summary>
    decimal? MarkupAmount = null,
    string? DifficultyLevelKey = null,
    Guid? ServicePricingId = null);

public sealed record PricingPreviewResponse(
    IReadOnlyList<QuoteLineCalcResult> Lines,
    /// <summary>Parts sell total after job markup (alias of PartsSellTotal).</summary>
    decimal PartsSubtotal,
    /// <summary>Job labour fee (alias of LabourFee).</summary>
    decimal LabourSubtotal,
    decimal DiscountTotal,
    /// <summary>Σ landed part costs (alias of PartsCostTotal).</summary>
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
    string? Warning,
    decimal PartsCostTotal = 0m,
    decimal PartsSellTotal = 0m,
    decimal MarkupPercent = 0m,
    decimal MarkupAmount = 0m,
    decimal LabourFee = 0m,
    decimal AdditionalTotal = 0m);
