using System.Collections.ObjectModel;
using System.Globalization;
using WorkshopOS.Client.Services;
using WorkshopOS.Contracts.Operations;
using WorkshopOS.Contracts.Workshop;

namespace WorkshopOS.Client.Views;

/// <summary>
/// WinUI-safe display wrapper: string fields only for DisplayMemberPath.
/// No XAML / UserControl — pages wire ComboBox/ListView in their own markup.
/// </summary>
public sealed class CatalogueServiceRow
{
    public CatalogueServiceRow(CatalogueServiceDto service) => Service = service;

    public CatalogueServiceDto Service { get; }
    public Guid Id => Service.Id;
    public string Name => Service.Name ?? string.Empty;
    public string Code => Service.Code ?? string.Empty;
    public string Subcategory => Service.Subcategory ?? string.Empty;
    public string FeeText => Service.DefaultLabourFee.ToString("0.##", CultureInfo.InvariantCulture);
    public string MinutesText => Service.EstimatedMinutes is int m ? m.ToString(CultureInfo.InvariantCulture) : "-";
    public string TitleLine => $"{Name} · ${FeeText} · ~{MinutesText} min";
}

/// <summary>
/// Shared catalogue search / selection state for New Ticket + Quote Builder.
/// Pages own the controls; this class owns API + collections (no UserControl).
/// When DeviceTypeFilter is set (Phone/Tablet/Laptop/PC), only matching categories,
/// services, brands, and models load.
/// </summary>
public sealed class ServiceCataloguePickerSession
{
    private readonly ApiClient _api;
    private CancellationTokenSource? _searchCts;
    private IReadOnlyList<CatalogueCategoryDto> _allCategories = Array.Empty<CatalogueCategoryDto>();
    private string? _selectedBrandName;

    public ObservableCollection<CatalogueCategoryDto> Categories { get; } = new();
    public ObservableCollection<CatalogueServiceRow> Services { get; } = new();
    public ObservableCollection<CatalogueServiceDto> SelectedServices { get; } = new();
    public ObservableCollection<CatalogueServiceRow> SelectedRows { get; } = new();
    public ObservableCollection<string> BrandNames { get; } = new();
    public ObservableCollection<string> ModelNames { get; } = new();
    /// <summary>WinUI-safe rows for device model search results (DisplayMemberPath = Display).</summary>
    public ObservableCollection<DeviceModelPickRow> ModelSearchResults { get; } = new();

    public string? Status { get; private set; }
    public string? Error { get; private set; }
    public string? SelectedCategoryKey { get; private set; }
    /// <summary>Catalogue device type: Phone, Tablet, Laptop, PC — or null for all.</summary>
    public string? DeviceTypeFilter { get; private set; }
    public string? SelectedBrandName => _selectedBrandName;
    public string? SelectedModelName { get; private set; }

    public event EventHandler? Changed;

    public ServiceCataloguePickerSession(ApiClient api) => _api = api;

    public IReadOnlyList<Guid> SelectedServiceIds => SelectedServices.Select(s => s.Id).ToList();

    /// <summary>Map workshop DeviceCategory → catalogue deviceType string (or null = show all).</summary>
    public static string? ToCatalogueDeviceType(DeviceCategory category) => category switch
    {
        DeviceCategory.Phone => "Phone",
        DeviceCategory.Tablet => "Tablet",
        DeviceCategory.Laptop => "Laptop",
        DeviceCategory.Mac => "Laptop",
        DeviceCategory.Desktop => "PC",
        DeviceCategory.GamingPc => "PC",
        DeviceCategory.Console => null,
        _ => null
    };

    public static DeviceCategory FromCatalogueDeviceType(string? deviceType) =>
        (deviceType ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "phone" => DeviceCategory.Phone,
            "tablet" => DeviceCategory.Tablet,
            "laptop" => DeviceCategory.Laptop,
            "pc" => DeviceCategory.Desktop,
            _ => DeviceCategory.Other
        };

    public async Task RefreshAsync()
    {
        try
        {
            Status = "Loading catalogue…";
            Error = null;
            Changed?.Invoke(this, EventArgs.Empty);

            var cats = await _api.GetAsync<CatalogueCategoryDto[]>("api/catalogue/categories");
            _allCategories = cats;
            ApplyCategoryFilter();

            await Task.WhenAll(
                SearchAsync(null, SelectedCategoryKey),
                LoadBrandsAsync(),
                LoadModelsAsync(_selectedBrandName));
            Status = DeviceTypeFilter is null
                ? $"{Categories.Count} categories · {Services.Count} services"
                : $"{DeviceTypeFilter}: {Categories.Count} categories · {Services.Count} services";
            Changed?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            Error = ex.Message;
            Status = null;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Restrict categories/services/brands/models to a catalogue device type (Phone/Tablet/Laptop/PC).</summary>
    public async Task SetDeviceTypeAsync(string? catalogueDeviceType, bool pruneSelection = true)
    {
        var next = string.IsNullOrWhiteSpace(catalogueDeviceType) ? null : catalogueDeviceType.Trim();
        var changed = !string.Equals(DeviceTypeFilter, next, StringComparison.OrdinalIgnoreCase);
        DeviceTypeFilter = next;
        if (changed)
        {
            SelectedCategoryKey = null;
            _selectedBrandName = null;
        }

        ApplyCategoryFilter();
        if (pruneSelection)
            PruneSelectedForDeviceType();

        await Task.WhenAll(
            SearchAsync(null, SelectedCategoryKey),
            LoadBrandsAsync(),
            LoadModelsAsync(_selectedBrandName));
        Status = DeviceTypeFilter is null
            ? $"All devices · {Services.Count} service(s)"
            : $"{DeviceTypeFilter} only · {Services.Count} service(s)";
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public async Task SetBrandAsync(string? brandName)
    {
        _selectedBrandName = string.IsNullOrWhiteSpace(brandName) ? null : brandName.Trim();
        SelectedModelName = null;
        await LoadModelsAsync(_selectedBrandName);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void SetModelName(string? modelName)
    {
        SelectedModelName = string.IsNullOrWhiteSpace(modelName) ? null : modelName.Trim();
    }

    /// <summary>Apply a catalogue search hit to brand + model fields.</summary>
    public async Task ApplyModelPickAsync(DeviceModelPickRow? row)
    {
        if (row is null) return;
        _selectedBrandName = row.BrandName;
        SelectedModelName = row.Name;
        if (!BrandNames.Contains(row.BrandName))
            BrandNames.Add(row.BrandName);
        await LoadModelsAsync(_selectedBrandName);
        if (!ModelNames.Contains(row.Name))
            ModelNames.Insert(0, row.Name);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public async Task SearchModelsDebouncedAsync(string? query)
    {
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;
        try
        {
            await Task.Delay(220, token);
            await SearchModelsAsync(query);
        }
        catch (TaskCanceledException) { }
    }

    public async Task SearchModelsAsync(string? query)
    {
        try
        {
            var parts = new List<string> { "take=40" };
            if (!string.IsNullOrWhiteSpace(query))
                parts.Add("q=" + Uri.EscapeDataString(query.Trim()));
            if (!string.IsNullOrWhiteSpace(DeviceTypeFilter))
                parts.Add("deviceType=" + Uri.EscapeDataString(DeviceTypeFilter));
            if (!string.IsNullOrWhiteSpace(_selectedBrandName))
                parts.Add("brand=" + Uri.EscapeDataString(_selectedBrandName));
            var list = await _api.GetAsync<DeviceModelDto[]>("api/catalogue/models/search?" + string.Join('&', parts));
            ModelSearchResults.Clear();
            foreach (var m in list)
                ModelSearchResults.Add(new DeviceModelPickRow(m));
            Status = string.IsNullOrWhiteSpace(query)
                ? $"{list.Length} popular model(s)"
                : $"{list.Length} model match(es)";
            Error = null;
            Changed?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            Error = ex.Message;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public void SetCategory(CatalogueCategoryDto? cat)
    {
        SelectedCategoryKey = cat?.Key;
        _ = SearchAsync(null, SelectedCategoryKey);
    }

    public async Task SearchDebouncedAsync(string? query)
    {
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;
        try
        {
            await Task.Delay(220, token);
            await SearchAsync(query, SelectedCategoryKey);
        }
        catch (TaskCanceledException) { }
    }

    public async Task SearchAsync(string? query, string? categoryKey)
    {
        try
        {
            var q = query?.Trim();
            var parts = new List<string> { "activeOnly=true" };
            if (!string.IsNullOrWhiteSpace(q)) parts.Add("q=" + Uri.EscapeDataString(q));
            if (!string.IsNullOrWhiteSpace(categoryKey)) parts.Add("category=" + Uri.EscapeDataString(categoryKey));
            if (!string.IsNullOrWhiteSpace(DeviceTypeFilter))
                parts.Add("deviceType=" + Uri.EscapeDataString(DeviceTypeFilter));
            var list = await _api.GetAsync<CatalogueServiceDto[]>("api/catalogue/services?" + string.Join('&', parts));
            Services.Clear();
            foreach (var s in list) Services.Add(new CatalogueServiceRow(s));
            Status = DeviceTypeFilter is null
                ? $"{list.Length} service(s)"
                : $"{DeviceTypeFilter}: {list.Length} service(s)";
            Error = null;
            Changed?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            Error = ex.Message;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public async Task AddAsync(CatalogueServiceRow? row)
    {
        if (row is null)
        {
            Error = "Select a service, then click Add.";
            Changed?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (SelectedServices.Any(s => s.Id == row.Id))
            return;

        SelectedServices.Add(row.Service);
        try { await _api.PostAsync($"api/catalogue/recent/{row.Id}"); }
        catch { /* fire and forget */ }
        RefreshSelected();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Remove(CatalogueServiceRow? row)
    {
        if (row is null && SelectedRows.Count > 0)
            row = SelectedRows[^1];
        if (row is null) return;

        var match = SelectedServices.FirstOrDefault(s => s.Id == row.Id);
        if (match is not null)
            SelectedServices.Remove(match);
        RefreshSelected();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void ApplyCategoryFilter()
    {
        Categories.Clear();
        foreach (var c in _allCategories)
        {
            if (DeviceTypeFilter is null ||
                string.Equals(c.DeviceType, DeviceTypeFilter, StringComparison.OrdinalIgnoreCase))
                Categories.Add(c);
        }

        if (SelectedCategoryKey is not null &&
            Categories.All(c => !string.Equals(c.Key, SelectedCategoryKey, StringComparison.OrdinalIgnoreCase)))
            SelectedCategoryKey = null;
    }

    private void PruneSelectedForDeviceType()
    {
        if (DeviceTypeFilter is null) return;
        var keep = SelectedServices
            .Where(s => string.IsNullOrWhiteSpace(s.DeviceType) ||
                        string.Equals(s.DeviceType, DeviceTypeFilter, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (keep.Count == SelectedServices.Count) return;
        SelectedServices.Clear();
        foreach (var s in keep) SelectedServices.Add(s);
        RefreshSelected();
    }

    private async Task LoadBrandsAsync()
    {
        try
        {
            var path = "api/catalogue/brands";
            if (!string.IsNullOrWhiteSpace(DeviceTypeFilter))
                path += "?deviceType=" + Uri.EscapeDataString(DeviceTypeFilter);
            var brands = await _api.GetAsync<DeviceBrandDto[]>(path);
            BrandNames.Clear();
            foreach (var b in brands.OrderBy(x => x.SortOrder).ThenBy(x => x.Name))
                BrandNames.Add(b.Name);
        }
        catch
        {
            BrandNames.Clear();
        }
    }

    private async Task LoadModelsAsync(string? brandName)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(brandName))
            {
                ModelNames.Clear();
                ModelNames.Add("Unknown / Other Device");
                return;
            }

            var parts = new List<string> { "brand=" + Uri.EscapeDataString(brandName) };
            if (!string.IsNullOrWhiteSpace(DeviceTypeFilter))
                parts.Add("deviceType=" + Uri.EscapeDataString(DeviceTypeFilter));
            var path = "api/catalogue/models?" + string.Join('&', parts);
            var models = await _api.GetAsync<DeviceModelDto[]>(path);
            ModelNames.Clear();
            foreach (var m in models.OrderBy(x => x.SortOrder).ThenBy(x => x.Name))
                ModelNames.Add(m.Name);
            if (ModelNames.Count == 0 ||
                !ModelNames.Any(n => n.Contains("Unknown", StringComparison.OrdinalIgnoreCase)))
                ModelNames.Add("Unknown / Other Device");
        }
        catch
        {
            ModelNames.Clear();
            ModelNames.Add("Unknown / Other Device");
        }
    }

    private void RefreshSelected()
    {
        SelectedRows.Clear();
        foreach (var s in SelectedServices)
            SelectedRows.Add(new CatalogueServiceRow(s));
    }
}

/// <summary>WinUI-safe device model search row (string DisplayMemberPath only).</summary>
public sealed class DeviceModelPickRow
{
    public DeviceModelPickRow(DeviceModelDto model)
    {
        Model = model;
        BrandName = model.BrandName ?? string.Empty;
        Name = model.Name ?? string.Empty;
        Series = model.Series ?? string.Empty;
        DeviceType = model.DeviceType ?? string.Empty;
        VerifiedText = model.IsVerified ? "Verified" : "Unverified";
        YearText = model.ReleaseYear is int y ? y.ToString(CultureInfo.InvariantCulture) : string.Empty;
        var aliasHint = model.Aliases is { Count: > 0 } ? " · " + string.Join(", ", model.Aliases.Take(3)) : string.Empty;
        Display = string.IsNullOrWhiteSpace(Series)
            ? $"{BrandName} {Name}{aliasHint}"
            : $"{BrandName} · {Name}{aliasHint}";
    }

    public DeviceModelDto Model { get; }
    public string BrandName { get; }
    public string Name { get; }
    public string Series { get; }
    public string DeviceType { get; }
    public string VerifiedText { get; }
    public string YearText { get; }
    public string Display { get; }
}
