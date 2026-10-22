namespace WorkshopOS.Contracts.Operations;

// —— Refurbished dashboard ——

public sealed record RefurbDashboardDto(
    RefurbPipelineCountsDto Pipeline,
    RefurbFinancialSummaryDto Financials,
    RefurbPerformanceDto Performance,
    IReadOnlyList<NamedCountDto> BrandDistribution,
    IReadOnlyList<NamedCountDto> SourceDistribution,
    IReadOnlyList<NamedMoneyDto> TopModelsByProjectedProfit,
    IReadOnlyList<NamedCountDto> MonthlyAcquisitions,
    IReadOnlyList<NamedMoneyDto> MonthlySalesRevenue,
    DateTimeOffset? From,
    DateTimeOffset? To);

public sealed record RefurbPipelineCountsDto(
    int TotalAcquired,
    int AwaitingInspection,
    int UnderRepair,
    int AwaitingParts,
    int QualityTesting,
    int ReadyForSale,
    int Listed,
    int Sold,
    int Reserved,
    int PartsOnly,
    int WrittenOff,
    int OpenAcquisitionLeads);

public sealed record RefurbFinancialSummaryDto(
    decimal TotalPurchaseExpenditure,
    decimal TotalRefurbishmentCosts,
    decimal CurrentInventoryInvestment,
    decimal EstimatedInventoryResaleValue,
    decimal ProjectedProfitUnsold,
    decimal RealisedProfitSold,
    decimal MonthlySalesRevenue,
    decimal AverageProfitPerSoldDevice,
    decimal ReturnOnInvestmentPercent);

public sealed record RefurbPerformanceDto(
    double? AverageDaysToRefurbish,
    double? AverageDaysToSell,
    int DevicesSoldInPeriod,
    int DevicesAcquiredInPeriod);

// —— Buy / profit calculator ——

public sealed record RefurbBuyCalcRequest(
    decimal EstimatedResalePrice,
    decimal ProposedPurchasePrice,
    decimal ReplacementParts = 0,
    decimal PartsShipping = 0,
    decimal TechnicianLabour = 0,
    decimal MarketplaceFees = 0,
    decimal PaymentProcessingFees = 0,
    decimal Packaging = 0,
    decimal OutboundShipping = 0,
    decimal ExpectedWarrantyAllowance = 0,
    decimal OtherCosts = 0,
    decimal? MinimumProfit = null,
    decimal? MinimumMarginPercent = null,
    decimal? ExcellentBuyMinMarginPercent = null,
    decimal? GoodBuyMinMarginPercent = null,
    decimal? AcceptableMinMarginPercent = null);

public sealed record RefurbBuyCalcResult(
    decimal EstimatedResalePrice,
    decimal ProposedPurchasePrice,
    decimal TotalCostsExcludingPurchase,
    decimal TotalInvestment,
    decimal GrossProfit,
    decimal ProfitMarginPercent,
    decimal ReturnOnInvestmentPercent,
    decimal MaximumRecommendedPurchasePrice,
    decimal MinimumProfitableResalePrice,
    decimal NegotiationAllowance,
    string Recommendation,
    string RecommendationReason);

// —— Acquisition leads ——

public sealed record AcquisitionLeadListItemDto(
    Guid Id,
    string Reference,
    string Status,
    string Brand,
    string Model,
    string? Category,
    string AcquisitionSource,
    string? SellerName,
    decimal? AskingPrice,
    decimal? ProposedOffer,
    decimal? AcceptedPurchasePrice,
    string? BuyRecommendation,
    DateTimeOffset? FollowUpAt,
    Guid? AssignedStaffId,
    Guid? UsedDeviceId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record AcquisitionLeadDetailDto(
    Guid Id,
    string Reference,
    string Status,
    string Brand,
    string Model,
    string? ModelNumber,
    string? Category,
    string? StorageCapacity,
    string? Colour,
    string? Imei,
    string? Serial,
    string? Carrier,
    string? SellerName,
    string? SellerContact,
    string? SellerEmail,
    string? SellerPhone,
    string SellerType,
    string AcquisitionSource,
    string? ListingUrl,
    decimal? AskingPrice,
    decimal? ProposedOffer,
    decimal? AcceptedPurchasePrice,
    string? DeviceCondition,
    string? KnownFaults,
    string? SuspectedFaults,
    string ActivationLockStatus,
    IReadOnlyList<string> PhotoUrls,
    string? Notes,
    DateTimeOffset? PurchaseDate,
    string? PaymentMethod,
    string? ShippingOrCollection,
    Guid? AssignedStaffId,
    DateTimeOffset? FollowUpAt,
    Guid? UsedDeviceId,
    string? UsedDeviceStockId,
    Guid? SellerCustomerId,
    IReadOnlyList<AcquisitionOfferDto> Offers,
    IReadOnlyList<AcquisitionEventDto> Events,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record AcquisitionOfferDto(
    Guid Id,
    decimal Amount,
    string Status,
    string? Message,
    string? Notes,
    decimal? EstimatedResalePrice,
    decimal? EstimatedPartsCost,
    decimal? LabourAllowance,
    decimal? EstimatedProfit,
    decimal? EstimatedMarginPercent,
    decimal? AbsoluteMaxPurchasePrice,
    string? BuyRecommendation,
    DateTimeOffset CreatedAt,
    DateTimeOffset? SentAt,
    DateTimeOffset? RespondedAt);

public sealed record AcquisitionEventDto(
    DateTimeOffset CreatedAt,
    string EventType,
    string Summary);

public sealed record CreateAcquisitionLeadRequest(
    string? Brand,
    string? Model,
    string? ModelNumber,
    string? Category,
    string? StorageCapacity,
    string? Colour,
    string? Imei,
    string? Serial,
    string? Carrier,
    string? SellerName,
    string? SellerContact,
    string? SellerEmail,
    string? SellerPhone,
    string? SellerType,
    string? AcquisitionSource,
    string? ListingUrl,
    decimal? AskingPrice,
    decimal? ProposedOffer,
    string? DeviceCondition,
    string? KnownFaults,
    string? SuspectedFaults,
    string? ActivationLockStatus,
    IReadOnlyList<string>? PhotoUrls,
    string? Notes,
    Guid? AssignedStaffId,
    DateTimeOffset? FollowUpAt,
    Guid? SellerCustomerId);

public sealed record UpdateAcquisitionLeadRequest(
    string? Brand,
    string? Model,
    string? ModelNumber,
    string? Category,
    string? StorageCapacity,
    string? Colour,
    string? Imei,
    string? Serial,
    string? Carrier,
    string? SellerName,
    string? SellerContact,
    string? SellerEmail,
    string? SellerPhone,
    string? SellerType,
    string? AcquisitionSource,
    string? ListingUrl,
    decimal? AskingPrice,
    decimal? ProposedOffer,
    decimal? AcceptedPurchasePrice,
    string? DeviceCondition,
    string? KnownFaults,
    string? SuspectedFaults,
    string? ActivationLockStatus,
    IReadOnlyList<string>? PhotoUrls,
    string? Notes,
    DateTimeOffset? PurchaseDate,
    string? PaymentMethod,
    string? ShippingOrCollection,
    Guid? AssignedStaffId,
    DateTimeOffset? FollowUpAt,
    Guid? SellerCustomerId);

public sealed record UpdateAcquisitionStatusRequest(string Status, string? Notes);

public sealed record CreateAcquisitionOfferRequest(
    decimal Amount,
    string? Message,
    string? Notes,
    decimal? EstimatedResalePrice,
    decimal? EstimatedPartsCost,
    decimal? LabourAllowance,
    bool MarkAsSent = false);

public sealed record ConvertAcquisitionRequest(
    decimal? PurchasePriceOverride,
    decimal? ExpectedResaleOverride,
    decimal? ExpectedPartsCostOverride,
    string? InitialStatus,
    string? LocationBin,
    bool AssignStockId = true);

// —— Extended used-device fields (Phase 1; additive on detail) ——

public sealed record UsedDeviceSecurityDto(
    bool OwnershipDeclared,
    bool OwnershipEvidenceOnFile,
    string? ActivationLockStatus,
    bool FindMyRemoved,
    string? FrpStatus,
    string? CarrierLockStatus,
    string? LostStolenStatus,
    bool FactoryResetConfirmed,
    bool DataSanitisedConfirmed,
    string? SecurityCheckNotes,
    bool ReadyForResaleChecksComplete);

public sealed record UsedDeviceCostBreakdownDto(
    decimal PurchasePrice,
    decimal PartsCost,
    decimal LabourCost,
    decimal ShippingInCost,
    decimal ShippingOutCost,
    decimal PackagingCost,
    decimal MarketplaceFees,
    decimal PaymentFees,
    decimal WarrantyAllowance,
    decimal TotalInvested,
    decimal AskingPrice,
    decimal? ActualSalePrice,
    decimal? ProjectedProfit,
    decimal? RealisedProfit);
