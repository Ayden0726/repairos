using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using WorkshopOS.Application.Abstractions;
using WorkshopOS.Application.Common;
using WorkshopOS.Contracts.Operations;
using WorkshopOS.Domain.Entities;
using WorkshopOS.Infrastructure.Persistence;

namespace WorkshopOS.Infrastructure.Services;

public sealed class ServiceCatalogueService : IServiceCatalogueService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly WorkshopDbContext _db;
    private readonly IAuditService _audit;

    public ServiceCatalogueService(WorkshopDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task EnsureSeededAsync(CancellationToken ct = default)
    {
        var current = await DbSeed.GetSettingAsync(_db, SettingKeys.ServiceCatalogueVersion, "", ct);
        var seed = LoadSeedDocument();
        if (string.Equals(current, seed.Version, StringComparison.OrdinalIgnoreCase))
            return;
        await ImportSeedAsync(seed, force: false, actorId: null, ct);
    }

    public async Task<CatalogueImportResultDto> ImportAsync(bool force, Guid? actorId, CancellationToken ct = default)
    {
        var seed = LoadSeedDocument();
        var current = await DbSeed.GetSettingAsync(_db, SettingKeys.ServiceCatalogueVersion, "", ct);
        if (!force && string.Equals(current, seed.Version, StringComparison.OrdinalIgnoreCase))
        {
            return new CatalogueImportResultDto(seed.Version, 0, 0, 0, 0, 0, true);
        }
        return await ImportSeedAsync(seed, force, actorId, ct);
    }

    public async Task<IReadOnlyList<CatalogueCategoryDto>> ListCategoriesAsync(CancellationToken ct = default)
    {
        var cats = await _db.ServiceCategories.AsNoTracking()
            .Where(c => c.ArchivedAt == null)
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .ToListAsync(ct);
        var counts = await _db.ServicePricings.AsNoTracking()
            .Where(s => s.ArchivedAt == null && s.CategoryId != null)
            .GroupBy(s => s.CategoryId!.Value)
            .Select(g => new { Id = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Id, x => x.Count, ct);
        return cats.Select(c => new CatalogueCategoryDto(
            c.Id, c.Key, c.Name, c.DeviceType, c.ParentId, c.SortOrder, c.IsSystem, c.Icon,
            counts.GetValueOrDefault(c.Id))).ToList();
    }

    public async Task<CatalogueCategoryDto> UpsertCategoryAsync(UpsertCatalogueCategoryRequest request, Guid actorId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Key) || string.IsNullOrWhiteSpace(request.Name))
            throw new ValidationAppException("Category key and name are required.");
        ServiceCategory cat;
        if (request.Id is Guid id)
        {
            cat = await _db.ServiceCategories.FirstOrDefaultAsync(c => c.Id == id && c.ArchivedAt == null, ct)
                ?? throw new AppException("not_found", "Category was not found.", 404);
        }
        else
        {
            cat = new ServiceCategory();
            _db.ServiceCategories.Add(cat);
        }
        cat.Key = request.Key.Trim().ToLowerInvariant();
        cat.Name = request.Name.Trim();
        cat.DeviceType = string.IsNullOrWhiteSpace(request.DeviceType) ? null : request.DeviceType.Trim();
        cat.ParentId = request.ParentId;
        cat.SortOrder = request.SortOrder;
        cat.Icon = string.IsNullOrWhiteSpace(request.Icon) ? null : request.Icon.Trim();
        cat.UpdatedAt = DateTimeOffset.UtcNow;
        if (!request.IsActive) cat.ArchivedAt ??= DateTimeOffset.UtcNow;
        else cat.ArchivedAt = null;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(actorId, "catalogue.category.upsert", "ServiceCategory", cat.Id.ToString(), ct: ct);
        return new CatalogueCategoryDto(cat.Id, cat.Key, cat.Name, cat.DeviceType, cat.ParentId, cat.SortOrder, cat.IsSystem, cat.Icon);
    }

    public async Task DeleteCategoryAsync(Guid id, Guid actorId, CancellationToken ct = default)
    {
        var cat = await _db.ServiceCategories.FirstOrDefaultAsync(c => c.Id == id && c.ArchivedAt == null, ct)
            ?? throw new AppException("not_found", "Category was not found.", 404);
        if (cat.IsSystem)
            throw new ValidationAppException("System categories cannot be deleted. Disable instead.");
        cat.ArchivedAt = DateTimeOffset.UtcNow;
        cat.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(actorId, "catalogue.category.delete", "ServiceCategory", id.ToString(), ct: ct);
    }

    public async Task ReorderCategoriesAsync(ReorderCatalogueRequest request, Guid actorId, CancellationToken ct = default)
    {
        var ids = request.OrderedIds ?? Array.Empty<Guid>();
        var cats = await _db.ServiceCategories.Where(c => ids.Contains(c.Id) && c.ArchivedAt == null).ToListAsync(ct);
        for (var i = 0; i < ids.Count; i++)
        {
            var cat = cats.FirstOrDefault(c => c.Id == ids[i]);
            if (cat is null) continue;
            cat.SortOrder = (i + 1) * 10;
            cat.UpdatedAt = DateTimeOffset.UtcNow;
        }
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(actorId, "catalogue.category.reorder", "ServiceCategory", null, ct: ct);
    }

    public async Task<IReadOnlyList<CatalogueServiceDto>> SearchServicesAsync(
        string? q, string? category, string? deviceType, string? brand, string? model, bool activeOnly = true, CancellationToken ct = default)
    {
        var query = _db.ServicePricings.AsNoTracking()
            .Include(s => s.ServiceCategory)
            .Where(s => s.ArchivedAt == null);
        if (activeOnly) query = query.Where(s => s.IsActive);
        if (!string.IsNullOrWhiteSpace(category))
        {
            var key = category.Trim().ToLowerInvariant();
            query = query.Where(s =>
                (s.ServiceCategory != null && s.ServiceCategory.Key == key) ||
                (s.Category != null && s.Category.ToLower() == key) ||
                (s.Category != null && s.Category.ToLower().Contains(key)));
        }
        if (!string.IsNullOrWhiteSpace(deviceType))
        {
            var dt = deviceType.Trim().ToLowerInvariant();
            query = query.Where(s => s.DeviceType != null && s.DeviceType.ToLower() == dt);
        }
        if (!string.IsNullOrWhiteSpace(brand))
        {
            var b = brand.Trim().ToLowerInvariant();
            query = query.Where(s =>
                s.CompatibleBrandsJson == null ||
                s.CompatibleBrandsJson == "[]" ||
                s.CompatibleBrandsJson.ToLower().Contains(b));
        }
        if (!string.IsNullOrWhiteSpace(model))
        {
            var m = model.Trim().ToLowerInvariant();
            query = query.Where(s =>
                s.CompatibleModelsJson == null ||
                s.CompatibleModelsJson == "[]" ||
                s.CompatibleModelsJson.ToLower().Contains(m));
        }

        var rows = await query.OrderBy(s => s.SortOrder).ThenBy(s => s.Name).Take(500).ToListAsync(ct);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var terms = Tokenize(q);
            rows = rows
                .Select(s => (Svc: s, Score: Score(s, terms)))
                .Where(x => x.Score > 0)
                .OrderByDescending(x => x.Score)
                .ThenBy(x => x.Svc.SortOrder)
                .ThenBy(x => x.Svc.Name)
                .Select(x => x.Svc)
                .Take(100)
                .ToList();
        }
        return rows.Select(MapService).ToList();
    }

    public async Task<CatalogueServiceDto> UpsertServiceAsync(UpsertCatalogueServiceRequest request, Guid actorId, CancellationToken ct = default)
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

        Guid? categoryId = request.CategoryId;
        if (categoryId is null && !string.IsNullOrWhiteSpace(request.CategoryKey))
        {
            var key = request.CategoryKey.Trim().ToLowerInvariant();
            categoryId = await _db.ServiceCategories.Where(c => c.Key == key && c.ArchivedAt == null)
                .Select(c => (Guid?)c.Id).FirstOrDefaultAsync(ct);
        }

        svc.Name = request.Name.Trim();
        svc.Category = string.IsNullOrWhiteSpace(request.Category) ? null : request.Category.Trim();
        svc.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        svc.DefaultLabourFee = PricingCalculator.RoundMoney(request.DefaultLabourFee);
        svc.DefaultPartMarkupPercent = request.DefaultPartMarkupPercent is null
            ? null : PricingCalculator.RoundMoney(request.DefaultPartMarkupPercent.Value);
        svc.IsActive = request.IsActive;
        svc.SortOrder = request.SortOrder;
        svc.Code = string.IsNullOrWhiteSpace(request.Code) ? AutoCode(svc.Name, svc.Category) : request.Code.Trim().ToUpperInvariant();
        svc.Subcategory = NullIfBlank(request.Subcategory);
        svc.DeviceType = NullIfBlank(request.DeviceType);
        svc.CompatibleBrandsJson = SerializeList(request.CompatibleBrands);
        svc.CompatibleModelsJson = SerializeList(request.CompatibleModels);
        svc.ServiceFee = PricingCalculator.RoundMoney(request.ServiceFee);
        svc.EstimatedMinutes = request.EstimatedMinutes;
        svc.MinCharge = request.MinCharge is null ? null : PricingCalculator.RoundMoney(request.MinCharge.Value);
        svc.DiagnosticFee = request.DiagnosticFee is null ? null : PricingCalculator.RoundMoney(request.DiagnosticFee.Value);
        svc.PartsRequired = request.PartsRequired;
        svc.SerialRequired = request.SerialRequired;
        svc.WarrantyDays = request.WarrantyDays;
        svc.TechNotes = NullIfBlank(request.TechNotes);
        svc.CustomerDescription = NullIfBlank(request.CustomerDescription);
        svc.CategoryId = categoryId;
        if (svc.Category is null && categoryId is Guid cid)
        {
            var catName = await _db.ServiceCategories.Where(c => c.Id == cid).Select(c => c.Name).FirstOrDefaultAsync(ct);
            svc.Category = catName;
        }
        svc.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _db.Entry(svc).Reference(s => s.ServiceCategory).LoadAsync(ct);
        await _audit.WriteAsync(actorId, "catalogue.service.upsert", "ServicePricing", svc.Id.ToString(), ct: ct);
        return MapService(svc);
    }

    public async Task DeleteServiceAsync(Guid id, Guid actorId, CancellationToken ct = default)
    {
        var svc = await _db.ServicePricings.FirstOrDefaultAsync(s => s.Id == id && s.ArchivedAt == null, ct)
            ?? throw new AppException("not_found", "Service was not found.", 404);
        if (svc.IsSystem)
        {
            svc.IsActive = false;
        }
        else
        {
            svc.ArchivedAt = DateTimeOffset.UtcNow;
        }
        svc.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(actorId, "catalogue.service.delete", "ServicePricing", id.ToString(), ct: ct);
    }

    public async Task<IReadOnlyList<CatalogueServiceDto>> ListFavouritesAsync(Guid userId, CancellationToken ct = default)
    {
        var ids = await _db.ServiceFavourites.AsNoTracking()
            .Where(f => f.UserId == userId)
            .OrderByDescending(f => f.CreatedAt)
            .Select(f => f.ServicePricingId)
            .ToListAsync(ct);
        if (ids.Count == 0) return Array.Empty<CatalogueServiceDto>();
        var map = await _db.ServicePricings.AsNoTracking().Include(s => s.ServiceCategory)
            .Where(s => ids.Contains(s.Id) && s.ArchivedAt == null && s.IsActive)
            .ToDictionaryAsync(s => s.Id, ct);
        return ids.Where(map.ContainsKey).Select(i => MapService(map[i])).ToList();
    }

    public async Task FavouriteAsync(Guid userId, Guid serviceId, CancellationToken ct = default)
    {
        _ = await _db.ServicePricings.FirstOrDefaultAsync(s => s.Id == serviceId && s.ArchivedAt == null, ct)
            ?? throw new AppException("not_found", "Service was not found.", 404);
        if (await _db.ServiceFavourites.AnyAsync(f => f.UserId == userId && f.ServicePricingId == serviceId, ct))
            return;
        _db.ServiceFavourites.Add(new ServiceFavourite { UserId = userId, ServicePricingId = serviceId });
        await _db.SaveChangesAsync(ct);
    }

    public async Task UnfavouriteAsync(Guid userId, Guid serviceId, CancellationToken ct = default)
    {
        var row = await _db.ServiceFavourites.FirstOrDefaultAsync(f => f.UserId == userId && f.ServicePricingId == serviceId, ct);
        if (row is null) return;
        _db.ServiceFavourites.Remove(row);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<CatalogueServiceDto>> ListRecentAsync(Guid userId, int take = 12, CancellationToken ct = default)
    {
        take = Math.Clamp(take, 1, 50);
        var ids = await _db.ServiceRecentSelections.AsNoTracking()
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.SelectedAt)
            .Select(r => r.ServicePricingId)
            .Distinct()
            .Take(take)
            .ToListAsync(ct);
        if (ids.Count == 0) return Array.Empty<CatalogueServiceDto>();
        var map = await _db.ServicePricings.AsNoTracking().Include(s => s.ServiceCategory)
            .Where(s => ids.Contains(s.Id) && s.ArchivedAt == null && s.IsActive)
            .ToDictionaryAsync(s => s.Id, ct);
        return ids.Where(map.ContainsKey).Select(i => MapService(map[i])).ToList();
    }

    public async Task RecordRecentAsync(Guid userId, Guid serviceId, CancellationToken ct = default)
    {
        _ = await _db.ServicePricings.FirstOrDefaultAsync(s => s.Id == serviceId && s.ArchivedAt == null, ct)
            ?? throw new AppException("not_found", "Service was not found.", 404);
        var existing = await _db.ServiceRecentSelections
            .FirstOrDefaultAsync(r => r.UserId == userId && r.ServicePricingId == serviceId, ct);
        if (existing is null)
            _db.ServiceRecentSelections.Add(new ServiceRecentSelection { UserId = userId, ServicePricingId = serviceId });
        else
            existing.SelectedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        // Cap history per user
        var old = await _db.ServiceRecentSelections.Where(r => r.UserId == userId)
            .OrderByDescending(r => r.SelectedAt).Skip(40).ToListAsync(ct);
        if (old.Count > 0)
        {
            _db.ServiceRecentSelections.RemoveRange(old);
            await _db.SaveChangesAsync(ct);
        }
    }

    public async Task RecordRecentManyAsync(Guid userId, IEnumerable<Guid> serviceIds, CancellationToken ct = default)
    {
        foreach (var id in serviceIds.Distinct())
            await RecordRecentAsync(userId, id, ct);
    }

    public async Task<IReadOnlyList<CatalogueBundleDto>> ListBundlesAsync(bool expand = false, CancellationToken ct = default)
    {
        var bundles = await _db.ServiceBundles.AsNoTracking()
            .Include(b => b.Items)
            .Where(b => b.ArchivedAt == null)
            .OrderBy(b => b.SortOrder).ThenBy(b => b.Name)
            .ToListAsync(ct);
        Dictionary<Guid, ServicePricing>? svcMap = null;
        if (expand)
        {
            var ids = bundles.SelectMany(b => b.Items.Select(i => i.ServicePricingId)).Distinct().ToList();
            svcMap = await _db.ServicePricings.AsNoTracking().Include(s => s.ServiceCategory)
                .Where(s => ids.Contains(s.Id)).ToDictionaryAsync(s => s.Id, ct);
        }
        return bundles.Select(b =>
        {
            var ordered = b.Items.OrderBy(i => i.SortOrder).Select(i => i.ServicePricingId).ToList();
            IReadOnlyList<CatalogueServiceDto>? services = null;
            if (svcMap is not null)
                services = ordered.Where(svcMap.ContainsKey).Select(id => MapService(svcMap[id])).ToList();
            return new CatalogueBundleDto(b.Id, b.Code, b.Name, b.Description, b.BundlePrice, b.IsActive, b.SortOrder, b.IsSystem, ordered, services);
        }).ToList();
    }

    public async Task<CatalogueBundleDto> UpsertBundleAsync(UpsertCatalogueBundleRequest request, Guid actorId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name))
            throw new ValidationAppException("Bundle code and name are required.");
        ServiceBundle bundle;
        if (request.Id is Guid id)
        {
            bundle = await _db.ServiceBundles.Include(b => b.Items)
                .FirstOrDefaultAsync(b => b.Id == id && b.ArchivedAt == null, ct)
                ?? throw new AppException("not_found", "Bundle was not found.", 404);
            _db.ServiceBundleItems.RemoveRange(bundle.Items);
            bundle.Items.Clear();
        }
        else
        {
            bundle = new ServiceBundle();
            _db.ServiceBundles.Add(bundle);
        }
        bundle.Code = request.Code.Trim().ToUpperInvariant();
        bundle.Name = request.Name.Trim();
        bundle.Description = NullIfBlank(request.Description);
        bundle.BundlePrice = request.BundlePrice is null ? null : PricingCalculator.RoundMoney(request.BundlePrice.Value);
        bundle.IsActive = request.IsActive;
        bundle.SortOrder = request.SortOrder;
        bundle.UpdatedAt = DateTimeOffset.UtcNow;
        var order = 0;
        foreach (var sid in request.ServicePricingIds ?? Array.Empty<Guid>())
        {
            order += 10;
            bundle.Items.Add(new ServiceBundleItem { BundleId = bundle.Id, ServicePricingId = sid, SortOrder = order });
        }
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(actorId, "catalogue.bundle.upsert", "ServiceBundle", bundle.Id.ToString(), ct: ct);
        return (await ListBundlesAsync(true, ct)).First(b => b.Id == bundle.Id);
    }

    public async Task DeleteBundleAsync(Guid id, Guid actorId, CancellationToken ct = default)
    {
        var bundle = await _db.ServiceBundles.FirstOrDefaultAsync(b => b.Id == id && b.ArchivedAt == null, ct)
            ?? throw new AppException("not_found", "Bundle was not found.", 404);
        bundle.ArchivedAt = DateTimeOffset.UtcNow;
        bundle.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(actorId, "catalogue.bundle.delete", "ServiceBundle", id.ToString(), ct: ct);
    }

    public async Task<IReadOnlyList<DeviceBrandDto>> ListBrandsAsync(string? deviceType = null, CancellationToken ct = default)
    {
        var q = _db.DeviceBrands.AsNoTracking().Where(b => b.ArchivedAt == null);
        if (!string.IsNullOrWhiteSpace(deviceType))
        {
            var dt = deviceType.Trim();
            q = q.Where(b => b.DeviceTypes.Contains(dt));
        }
        var rows = await q.OrderBy(b => b.SortOrder).ThenBy(b => b.Name).ToListAsync(ct);
        return rows.Select(b => new DeviceBrandDto(b.Id, b.Name,
            b.DeviceTypes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            b.SortOrder)).ToList();
    }

    public async Task<IReadOnlyList<DeviceModelDto>> ListModelsAsync(Guid? brandId = null, string? deviceType = null, string? brandName = null, CancellationToken ct = default)
    {
        var q = _db.DeviceModels.AsNoTracking().Include(m => m.Brand).Where(m => m.ArchivedAt == null);
        if (brandId is Guid bid) q = q.Where(m => m.BrandId == bid);
        if (!string.IsNullOrWhiteSpace(brandName))
        {
            var bn = brandName.Trim().ToLowerInvariant();
            q = q.Where(m => m.Brand.Name.ToLower() == bn);
        }
        if (!string.IsNullOrWhiteSpace(deviceType))
        {
            var dt = deviceType.Trim().ToLowerInvariant();
            q = q.Where(m => m.DeviceType != null && m.DeviceType.ToLower() == dt);
        }
        var rows = await q.OrderBy(m => m.SortOrder).ThenBy(m => m.Name).ToListAsync(ct);
        return rows.Select(m => new DeviceModelDto(m.Id, m.BrandId, m.Brand.Name, m.Name, m.DeviceType, m.SortOrder)).ToList();
    }

    private async Task<CatalogueImportResultDto> ImportSeedAsync(SeedDocument seed, bool force, Guid? actorId, CancellationToken ct)
    {
        var catUpsert = 0;
        var brandUpsert = 0;
        var modelUpsert = 0;
        var svcUpsert = 0;
        var bundleUpsert = 0;

        var catByKey = await _db.ServiceCategories.Where(c => c.ArchivedAt == null)
            .ToDictionaryAsync(c => c.Key, StringComparer.OrdinalIgnoreCase, ct);

        foreach (var c in seed.Categories.OrderBy(x => x.SortOrder))
        {
            if (!catByKey.TryGetValue(c.Key, out var cat))
            {
                cat = new ServiceCategory { Key = c.Key, IsSystem = true };
                _db.ServiceCategories.Add(cat);
                catByKey[c.Key] = cat;
            }
            else if (!force && !cat.IsSystem)
                continue;
            cat.Name = c.Name;
            cat.DeviceType = c.DeviceType;
            cat.SortOrder = c.SortOrder;
            cat.IsSystem = true;
            cat.UpdatedAt = DateTimeOffset.UtcNow;
            cat.ArchivedAt = null;
            catUpsert++;
        }
        await _db.SaveChangesAsync(ct);

        // Resolve parents after keys exist
        foreach (var c in seed.Categories.Where(x => !string.IsNullOrWhiteSpace(x.ParentKey)))
        {
            if (!catByKey.TryGetValue(c.Key, out var cat)) continue;
            if (catByKey.TryGetValue(c.ParentKey!, out var parent))
                cat.ParentId = parent.Id;
        }
        await _db.SaveChangesAsync(ct);

        var brandByName = await _db.DeviceBrands.Where(b => b.ArchivedAt == null)
            .ToDictionaryAsync(b => b.Name, StringComparer.OrdinalIgnoreCase, ct);
        foreach (var b in seed.Brands)
        {
            if (!brandByName.TryGetValue(b.Name, out var brand))
            {
                brand = new DeviceBrand { Name = b.Name, IsSystem = true };
                _db.DeviceBrands.Add(brand);
                brandByName[b.Name] = brand;
            }
            brand.DeviceTypes = string.Join(',', b.DeviceTypes ?? Array.Empty<string>());
            brand.SortOrder = b.SortOrder;
            brand.IsSystem = true;
            brand.UpdatedAt = DateTimeOffset.UtcNow;
            brandUpsert++;
        }
        await _db.SaveChangesAsync(ct);

        var models = await _db.DeviceModels.Include(m => m.Brand).Where(m => m.ArchivedAt == null).ToListAsync(ct);
        foreach (var m in seed.Models)
        {
            if (!brandByName.TryGetValue(m.Brand, out var brand)) continue;
            var existing = models.FirstOrDefault(x => x.BrandId == brand.Id &&
                string.Equals(x.Name, m.Name, StringComparison.OrdinalIgnoreCase));
            if (existing is null)
            {
                existing = new DeviceModel { BrandId = brand.Id, Name = m.Name, IsSystem = true };
                _db.DeviceModels.Add(existing);
                models.Add(existing);
            }
            existing.DeviceType = m.DeviceType;
            existing.SortOrder = m.SortOrder;
            existing.IsSystem = true;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
            modelUpsert++;
        }
        await _db.SaveChangesAsync(ct);

        var byCode = await _db.ServicePricings.Where(s => s.Code != null && s.ArchivedAt == null)
            .ToDictionaryAsync(s => s.Code!, StringComparer.OrdinalIgnoreCase, ct);

        foreach (var s in seed.Services)
        {
            if (!byCode.TryGetValue(s.Code, out var svc))
            {
                catByKey.TryGetValue(s.CategoryKey, out var existingCat);
                var catName = existingCat?.Name;
                // Prefer match on existing thin catalogue row by name+category (preserve shop data/FKs)
                svc = await _db.ServicePricings.FirstOrDefaultAsync(x =>
                    x.ArchivedAt == null && x.Code == null &&
                    x.Name == s.Name &&
                    (x.Category == null || x.Category == s.CategoryKey || (catName != null && x.Category == catName)), ct);
                if (svc is null)
                {
                    svc = new ServicePricing { IsSystem = true };
                    _db.ServicePricings.Add(svc);
                }
                byCode[s.Code] = svc;
            }
            else if (!force && !svc.IsSystem)
            {
                // Shop-owned row with same code — leave pricing alone, keep identity
                continue;
            }

            catByKey.TryGetValue(s.CategoryKey, out var cat);
            svc.Code = s.Code;
            svc.Name = s.Name;
            svc.Category = cat?.Name ?? s.CategoryKey;
            svc.CategoryId = cat?.Id;
            svc.Subcategory = s.Subcategory;
            svc.DeviceType = s.DeviceType;
            svc.Description = s.Description;
            svc.CustomerDescription = s.CustomerDescription;
            svc.TechNotes = s.TechNotes;
            svc.DefaultLabourFee = s.DefaultLabourFee;
            svc.ServiceFee = s.ServiceFee;
            svc.EstimatedMinutes = s.EstimatedMinutes;
            svc.MinCharge = s.MinCharge;
            svc.DiagnosticFee = s.DiagnosticFee;
            svc.PartsRequired = s.PartsRequired;
            svc.SerialRequired = s.SerialRequired;
            svc.WarrantyDays = s.WarrantyDays;
            svc.DefaultPartMarkupPercent = s.DefaultPartMarkupPercent;
            svc.CompatibleBrandsJson = SerializeList(s.CompatibleBrands);
            svc.CompatibleModelsJson = SerializeList(s.CompatibleModels);
            svc.IsActive = s.IsActive;
            svc.SortOrder = s.SortOrder;
            svc.IsSystem = true;
            svc.UpdatedAt = DateTimeOffset.UtcNow;
            svc.ArchivedAt = null;
            svcUpsert++;
        }
        await _db.SaveChangesAsync(ct);

        var bundles = await _db.ServiceBundles.Include(b => b.Items).Where(b => b.ArchivedAt == null).ToListAsync(ct);
        foreach (var b in seed.Bundles)
        {
            var bundle = bundles.FirstOrDefault(x => string.Equals(x.Code, b.Code, StringComparison.OrdinalIgnoreCase));
            if (bundle is null)
            {
                bundle = new ServiceBundle { Code = b.Code, IsSystem = true };
                _db.ServiceBundles.Add(bundle);
                bundles.Add(bundle);
                await _db.SaveChangesAsync(ct);
            }
            else if (bundle.Items.Count > 0)
            {
                var stale = bundle.Items.ToList();
                _db.ServiceBundleItems.RemoveRange(stale);
                await _db.SaveChangesAsync(ct);
                bundle.Items.Clear();
            }
            bundle.Name = b.Name;
            bundle.Description = b.Description;
            bundle.BundlePrice = b.BundlePrice;
            bundle.IsActive = true;
            bundle.SortOrder = b.SortOrder;
            bundle.IsSystem = true;
            bundle.UpdatedAt = DateTimeOffset.UtcNow;
            var order = 0;
            foreach (var code in b.ServiceCodes ?? Array.Empty<string>())
            {
                if (!byCode.TryGetValue(code, out var svc)) continue;
                order += 10;
                _db.ServiceBundleItems.Add(new ServiceBundleItem
                {
                    BundleId = bundle.Id,
                    ServicePricingId = svc.Id,
                    SortOrder = order
                });
            }
            bundleUpsert++;
        }
        await _db.SaveChangesAsync(ct);

        await DbSeed.SetSettingAsync(_db, SettingKeys.ServiceCatalogueVersion, seed.Version, actorId, ct);
        await _db.SaveChangesAsync(ct);
        if (actorId is Guid aid)
            await _audit.WriteAsync(aid, "catalogue.import", "ServiceCatalogue", seed.Version, ct: ct);

        return new CatalogueImportResultDto(seed.Version, catUpsert, brandUpsert, modelUpsert, svcUpsert, bundleUpsert, false);
    }

    private static SeedDocument LoadSeedDocument()
    {
        var asm = typeof(ServiceCatalogueService).Assembly;
        var resource = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("service-catalogue.v1.json", StringComparison.OrdinalIgnoreCase));
        string json;
        if (resource is not null)
        {
            using var stream = asm.GetManifestResourceStream(resource)
                ?? throw new InvalidOperationException("Embedded catalogue seed missing.");
            using var reader = new StreamReader(stream);
            json = reader.ReadToEnd();
        }
        else
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Data", "service-catalogue.v1.json");
            if (!File.Exists(path))
            {
                // Dev fallback: walk up from assembly to find repo Data folder
                var dir = new DirectoryInfo(AppContext.BaseDirectory);
                while (dir is not null)
                {
                    var candidate = Path.Combine(dir.FullName, "Data", "service-catalogue.v1.json");
                    if (File.Exists(candidate)) { path = candidate; break; }
                    candidate = Path.Combine(dir.FullName, "services", "shared", "WorkshopOS.Infrastructure", "Data", "service-catalogue.v1.json");
                    if (File.Exists(candidate)) { path = candidate; break; }
                    dir = dir.Parent;
                }
            }
            json = File.ReadAllText(path);
        }
        return JsonSerializer.Deserialize<SeedDocument>(json, JsonOpts)
            ?? throw new InvalidOperationException("Invalid catalogue seed JSON.");
    }

    internal static CatalogueServiceDto MapService(ServicePricing s) =>
        new(
            s.Id, s.Name, s.Category, s.Description, s.DefaultLabourFee, s.DefaultPartMarkupPercent,
            s.IsActive, s.SortOrder, s.Code, s.Subcategory, s.DeviceType,
            DeserializeList(s.CompatibleBrandsJson), DeserializeList(s.CompatibleModelsJson),
            s.ServiceFee, s.EstimatedMinutes, s.MinCharge, s.DiagnosticFee, s.PartsRequired, s.SerialRequired,
            s.WarrantyDays, s.TechNotes, s.CustomerDescription, s.IsSystem, s.CategoryId,
            s.ServiceCategory?.Key, s.ServiceCategory?.Name);

    internal static ServicePricingDto MapThin(ServicePricing s) =>
        new(s.Id, s.Name, s.Category, s.Description, s.DefaultLabourFee, s.DefaultPartMarkupPercent,
            s.IsActive, s.SortOrder, s.Code, s.Subcategory, s.DeviceType, s.ServiceFee, s.EstimatedMinutes,
            s.MinCharge, s.DiagnosticFee, s.PartsRequired, s.SerialRequired, s.WarrantyDays, s.TechNotes,
            s.CustomerDescription, s.IsSystem, s.CategoryId,
            DeserializeList(s.CompatibleBrandsJson), DeserializeList(s.CompatibleModelsJson));

    private static IReadOnlyList<string> DeserializeList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Array.Empty<string>();
        try { return JsonSerializer.Deserialize<string[]>(json, JsonOpts) ?? Array.Empty<string>(); }
        catch { return Array.Empty<string>(); }
    }

    private static string? SerializeList(IReadOnlyList<string>? items)
    {
        if (items is null || items.Count == 0) return "[]";
        return JsonSerializer.Serialize(items.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToArray(), JsonOpts);
    }

    private static string? NullIfBlank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static string AutoCode(string name, string? category)
    {
        var prefix = string.IsNullOrWhiteSpace(category) ? "SVC" : new string(category.Where(char.IsLetterOrDigit).Take(6).ToArray()).ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(prefix)) prefix = "SVC";
        var rest = new string(name.ToUpperInvariant().Where(c => char.IsLetterOrDigit(c) || c == ' ').ToArray())
            .Trim().Replace(' ', '-');
        if (rest.Length > 32) rest = rest[..32].Trim('-');
        return $"{prefix}-{rest}";
    }

    private static List<string> Tokenize(string q) =>
        q.Trim().ToLowerInvariant().Split(new[] { ' ', ',', ';', '/', '-' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    private static int Score(ServicePricing s, List<string> terms)
    {
        if (terms.Count == 0) return 1;
        var hay = $"{s.Name} {s.Code} {s.Category} {s.Subcategory} {s.Description} {s.CustomerDescription} {s.DeviceType}".ToLowerInvariant();
        var score = 0;
        foreach (var t in terms)
        {
            if (string.Equals(s.Code, t, StringComparison.OrdinalIgnoreCase)) score += 100;
            else if (s.Code != null && s.Code.Contains(t, StringComparison.OrdinalIgnoreCase)) score += 40;
            if (s.Name.Contains(t, StringComparison.OrdinalIgnoreCase)) score += 30;
            else if (FuzzyContains(hay, t)) score += 10;
            else return 0;
        }
        return score;
    }

    private static bool FuzzyContains(string hay, string term)
    {
        if (hay.Contains(term, StringComparison.OrdinalIgnoreCase)) return true;
        // light fuzzy: allow 1-char typo for terms length >= 4
        if (term.Length < 4) return false;
        var parts = hay.Split(new[] { ' ', '-', '/', ',' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var p in parts)
        {
            if (Math.Abs(p.Length - term.Length) > 1) continue;
            if (Levenshtein(p, term) <= 1) return true;
        }
        return false;
    }

    private static int Levenshtein(string a, string b)
    {
        var n = a.Length; var m = b.Length;
        var d = new int[n + 1, m + 1];
        for (var i = 0; i <= n; i++) d[i, 0] = i;
        for (var j = 0; j <= m; j++) d[0, j] = j;
        for (var i = 1; i <= n; i++)
        for (var j = 1; j <= m; j++)
        {
            var cost = a[i - 1] == b[j - 1] ? 0 : 1;
            d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
        }
        return d[n, m];
    }

    private sealed class SeedDocument
    {
        public string Version { get; set; } = "v1";
        public List<SeedCategory> Categories { get; set; } = new();
        public List<SeedBrand> Brands { get; set; } = new();
        public List<SeedModel> Models { get; set; } = new();
        public List<SeedService> Services { get; set; } = new();
        public List<SeedBundle> Bundles { get; set; } = new();
    }
    private sealed class SeedCategory
    {
        public string Key { get; set; } = "";
        public string Name { get; set; } = "";
        public string? DeviceType { get; set; }
        public string? ParentKey { get; set; }
        public int SortOrder { get; set; }
    }
    private sealed class SeedBrand
    {
        public string Name { get; set; } = "";
        public string[]? DeviceTypes { get; set; }
        public int SortOrder { get; set; }
    }
    private sealed class SeedModel
    {
        public string Brand { get; set; } = "";
        public string Name { get; set; } = "";
        public string? DeviceType { get; set; }
        public int SortOrder { get; set; }
    }
    private sealed class SeedService
    {
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public string CategoryKey { get; set; } = "";
        public string? Subcategory { get; set; }
        public string? DeviceType { get; set; }
        public string[]? CompatibleBrands { get; set; }
        public string[]? CompatibleModels { get; set; }
        public decimal DefaultLabourFee { get; set; }
        public decimal ServiceFee { get; set; }
        public int? EstimatedMinutes { get; set; }
        public decimal? MinCharge { get; set; }
        public decimal? DiagnosticFee { get; set; }
        public bool PartsRequired { get; set; }
        public bool SerialRequired { get; set; }
        public int? WarrantyDays { get; set; }
        public string? TechNotes { get; set; }
        public string? CustomerDescription { get; set; }
        public decimal? DefaultPartMarkupPercent { get; set; }
        public bool IsActive { get; set; } = true;
        public int SortOrder { get; set; }
        public string? Description { get; set; }
    }
    private sealed class SeedBundle
    {
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public decimal? BundlePrice { get; set; }
        public string[]? ServiceCodes { get; set; }
        public int SortOrder { get; set; }
    }
}
