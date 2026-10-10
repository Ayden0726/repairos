namespace WorkshopOS.Contracts.Operations;

/// <summary>Admin settings for automatic IMEI / device identification.</summary>
public sealed record DeviceIdentificationSettingsDto(
    bool Enabled,
    string Provider,
    bool ApiKeyConfigured,
    bool BasicLookupEnabled,
    bool AdvancedLookupEnabled,
    int BasicServiceId,
    int? AdvancedServiceId,
    bool CacheEnabled,
    int CacheTtlDays,
    int DailyLookupLimit,
    int LookupsToday,
    decimal? EstimatedBasicCost,
    decimal? EstimatedAdvancedCost,
    string? Notes);

public sealed record UpdateDeviceIdentificationSettingsRequest(
    bool Enabled,
    string Provider,
    string? ApiKey,
    bool ClearApiKey,
    bool BasicLookupEnabled,
    bool AdvancedLookupEnabled,
    int BasicServiceId,
    int? AdvancedServiceId,
    bool CacheEnabled,
    int CacheTtlDays,
    int DailyLookupLimit,
    decimal? EstimatedBasicCost,
    decimal? EstimatedAdvancedCost,
    string? Notes);

public sealed record ImeiLookupRequest(
    string Imei,
    bool Advanced = false,
    bool ForceRefresh = false);

/// <summary>
/// Lookup result for intake. Model-level fields are never presented as verified device-specific facts
/// (exact storage, colour, battery health, lock status are only filled when the provider returns them).
/// </summary>
public sealed record ImeiLookupResultDto(
    bool Success,
    string? ErrorCode,
    string Message,
    string? ImeiMasked,
    string? Tac,
    string? Brand,
    string? Manufacturer,
    string? Model,
    string? ModelNumber,
    string? DeviceCategory,
    string? Display,
    string? Processor,
    string? Chipset,
    string? BatteryCapacity,
    string? Network,
    string? Storage,
    string? Ram,
    string? Colour,
    string? Warranty,
    bool StorageIsDeviceSpecific,
    bool ColourIsDeviceSpecific,
    bool WarrantyIsDeviceSpecific,
    string VerificationStatus,
    string Provider,
    DateTimeOffset? LookedUpAt,
    bool FromCache,
    bool ApiConfigured,
    bool UsedManualFallback,
    decimal? Cost,
    Guid? CatalogueBrandId,
    Guid? CatalogueModelId,
    string? CatalogueModelName,
    IReadOnlyList<DeviceModelDto>? CatalogueMatches,
    IReadOnlyList<ImeiDuplicateHitDto>? Duplicates,
    IReadOnlyDictionary<string, string>? RawFields);

public sealed record ImeiDuplicateHitDto(
    string Source,
    Guid Id,
    string Label,
    string? TicketNumber,
    DateTimeOffset? CreatedAt);

public sealed record ImeiValidateRequest(string Imei);

public sealed record ImeiValidateResultDto(
    bool IsValid,
    string? NormalizedImei,
    string? Tac,
    string? Message,
    IReadOnlyList<ImeiDuplicateHitDto>? Duplicates);
