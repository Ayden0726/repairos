using System.Collections.ObjectModel;
using System.Globalization;
using WorkshopOS.Client.Services;
using WorkshopOS.Contracts.Operations;

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
/// </summary>
public sealed class ServiceCataloguePickerSession
{
    private readonly ApiClient _api;
    private CancellationTokenSource? _searchCts;

    public ObservableCollection<CatalogueCategoryDto> Categories { get; } = new();
    public ObservableCollection<CatalogueServiceRow> Services { get; } = new();
    public ObservableCollection<CatalogueServiceDto> SelectedServices { get; } = new();
    public ObservableCollection<CatalogueServiceRow> SelectedRows { get; } = new();

    public string? Status { get; private set; }
    public string? Error { get; private set; }
    public string? SelectedCategoryKey { get; private set; }

    public event EventHandler? Changed;

    public ServiceCataloguePickerSession(ApiClient api) => _api = api;

    public IReadOnlyList<Guid> SelectedServiceIds => SelectedServices.Select(s => s.Id).ToList();

    public async Task RefreshAsync()
    {
        try
        {
            Status = "Loading catalogue…";
            Error = null;
            Changed?.Invoke(this, EventArgs.Empty);

            var cats = await _api.GetAsync<CatalogueCategoryDto[]>("api/catalogue/categories");
            Categories.Clear();
            foreach (var c in cats) Categories.Add(c);

            await SearchAsync(null, null);
            Status = $"{cats.Length} categories · {Services.Count} services";
            Changed?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            Error = ex.Message;
            Status = null;
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
            var list = await _api.GetAsync<CatalogueServiceDto[]>("api/catalogue/services?" + string.Join('&', parts));
            Services.Clear();
            foreach (var s in list) Services.Add(new CatalogueServiceRow(s));
            Status = $"{list.Length} service(s)";
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

    private void RefreshSelected()
    {
        SelectedRows.Clear();
        foreach (var s in SelectedServices)
            SelectedRows.Add(new CatalogueServiceRow(s));
    }
}
