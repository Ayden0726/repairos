namespace WorkshopOS.Contracts.Operations;

public sealed record CatalogueCategoryDto(
    Guid Id,
    string Key,
    string Name,
    string? DeviceType,
    Guid? ParentId,
    int SortOrder,
    bool IsSystem,
    string? Icon,
    int ServiceCount = 0);

public sealed record UpsertCatalogueCategoryRequest(
    Guid? Id,
    string Key,
    string Name,
    string? DeviceType,
    Guid? ParentId,
    int SortOrder,
    string? Icon,
    bool IsActive = true);

public sealed record CatalogueServiceDto(
    Guid Id,
    string Name,
    string? Category,
    string? Description,
    decimal DefaultLabourFee,
    decimal? DefaultPartMarkupPercent,
    bool IsActive,
    int SortOrder,
    string? Code = null,
    string? Subcategory = null,
    string? DeviceType = null,
    IReadOnlyList<string>? CompatibleBrands = null,
    IReadOnlyList<string>? CompatibleModels = null,
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
    string? CategoryKey = null,
    string? CategoryName = null);

public sealed record UpsertCatalogueServiceRequest(
    Guid? Id,
    string Name,
    string? Category,
    string? Description,
    decimal DefaultLabourFee,
    decimal? DefaultPartMarkupPercent,
    bool IsActive,
    int SortOrder,
    string? Code = null,
    string? Subcategory = null,
    string? DeviceType = null,
    IReadOnlyList<string>? CompatibleBrands = null,
    IReadOnlyList<string>? CompatibleModels = null,
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
    string? CategoryKey = null);

public sealed record CatalogueBundleDto(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    decimal? BundlePrice,
    bool IsActive,
    int SortOrder,
    bool IsSystem,
    IReadOnlyList<Guid> ServicePricingIds,
    IReadOnlyList<CatalogueServiceDto>? Services = null);

public sealed record UpsertCatalogueBundleRequest(
    Guid? Id,
    string Code,
    string Name,
    string? Description,
    decimal? BundlePrice,
    bool IsActive,
    int SortOrder,
    IReadOnlyList<Guid> ServicePricingIds);

public sealed record DeviceBrandDto(Guid Id, string Name, IReadOnlyList<string> DeviceTypes, int SortOrder);
public sealed record DeviceModelDto(Guid Id, Guid BrandId, string BrandName, string Name, string? DeviceType, int SortOrder);

public sealed record CatalogueImportResultDto(
    string Version,
    int CategoriesUpserted,
    int BrandsUpserted,
    int ModelsUpserted,
    int ServicesUpserted,
    int BundlesUpserted,
    bool AlreadyCurrent);

public sealed record ReorderCatalogueRequest(IReadOnlyList<Guid> OrderedIds);
