using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WorkshopOS.Client.Services;
using WorkshopOS.Contracts.Operations;
using WorkshopOS.Contracts.Workshop;

namespace WorkshopOS.Client.Views;

public sealed record PcBuildPickItem(Guid Id, string Label);

public sealed class PcBuildRowVm
{
    public Guid Id { get; init; }
    public string Number { get; init; } = "";
    public string Title { get; init; } = "";
    public string Status { get; init; } = "";
    public string CustomerLine { get; init; } = "";
    public string TotalsLine { get; init; } = "";
    public string MarginLine { get; init; } = "";
}

public partial class PcBuildSlotLine : ObservableObject
{
    public string Category { get; }
    public bool AllowMultiple { get; }

    [ObservableProperty] private InventoryListItemDto? _selectedItem;
    [ObservableProperty] private string _quantityText = "1";
    [ObservableProperty] private string _costText = "0";
    [ObservableProperty] private string _sellText = "0";
    [ObservableProperty] private string _manualName = string.Empty;
    [ObservableProperty] private ObservableCollection<InventoryListItemDto> _options = new();

    public PcBuildSlotLine(string category, bool allowMultiple)
    {
        Category = category;
        AllowMultiple = allowMultiple;
    }

    partial void OnSelectedItemChanged(InventoryListItemDto? value)
    {
        if (value is null) return;
        CostText = value.Cost.ToString("0.00");
        SellText = value.SellPrice.ToString("0.00");
        ManualName = value.Name;
    }

    public PcPartInputDto? ToInputOrNull()
    {
        if (SelectedItem is null && string.IsNullOrWhiteSpace(ManualName))
            return null;
        _ = int.TryParse(QuantityText, out var qty);
        if (qty <= 0) qty = 1;
        _ = decimal.TryParse(CostText, out var cost);
        _ = decimal.TryParse(SellText, out var sell);
        var name = string.IsNullOrWhiteSpace(ManualName)
            ? (SelectedItem?.Name ?? Category)
            : ManualName.Trim();
        return new PcPartInputDto(Category, name, SelectedItem?.Id, cost, sell, qty);
    }
}

public partial class PcBuildsListViewModel : ObservableObject
{
    private readonly ApiClient _api;

    public ObservableCollection<PcBuildRowVm> Items { get; } = new();
    [ObservableProperty] private string? _error;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private bool _isBusy;

    public PcBuildsListViewModel(ApiClient api) => _api = api;

    [RelayCommand]
    private async Task RefreshAsync()
    {
        Error = null;
        IsBusy = true;
        try
        {
            var list = await _api.GetAsync<IReadOnlyList<PcBuildListItemDto>>("api/builds");
            Items.Clear();
            foreach (var b in list)
            {
                Items.Add(new PcBuildRowVm
                {
                    Id = b.Id,
                    Number = b.Number,
                    Title = string.IsNullOrWhiteSpace(b.Name) ? b.Number : $"{b.Number} · {b.Name}",
                    Status = b.Status,
                    CustomerLine = string.IsNullOrWhiteSpace(b.CustomerName) ? "No customer" : b.CustomerName,
                    TotalsLine = $"Cost ${b.CostTotal:0.00} · Sell ${b.SellTotal:0.00} · {b.PartCount} part(s)",
                    MarginLine = $"Margin ${b.Margin:0.00}"
                });
            }
            StatusMessage = Items.Count == 0
                ? "No PC builds yet. Create one to reserve inventory parts."
                : $"{Items.Count} build(s)";
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsBusy = false; }
    }
}

public partial class PcBuildBuilderViewModel : ObservableObject
{
    private static readonly string[] PrimarySlots =
        ["CPU", "Motherboard", "RAM", "GPU", "Storage", "PSU", "Case", "Cooler", "OS", "Peripheral"];

    private readonly ApiClient _api;
    private Guid? _buildId;
    private List<InventoryListItemDto> _allInventory = new();

    public ObservableCollection<PcBuildPickItem> Customers { get; } = new();
    public ObservableCollection<string> StatusOptions { get; } = new(
        ["Quoted", "Reserved", "Building", "Completed", "Sold", "Cancelled"]);
    public ObservableCollection<PcBuildSlotLine> Slots { get; } = new();

    [ObservableProperty] private string _pageTitle = "New PC build";
    [ObservableProperty] private string _buildNumber = "";
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _useCase = string.Empty;
    [ObservableProperty] private string _budget = string.Empty;
    [ObservableProperty] private string _notes = string.Empty;
    [ObservableProperty] private string _status = "Quoted";
    [ObservableProperty] private PcBuildPickItem? _selectedCustomer;
    [ObservableProperty] private string _costTotalLabel = "$0.00";
    [ObservableProperty] private string _sellTotalLabel = "$0.00";
    [ObservableProperty] private string _marginLabel = "$0.00";
    [ObservableProperty] private string? _error;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isExisting;

    public PcBuildBuilderViewModel(ApiClient api)
    {
        _api = api;
        EnsureBaseSlots();
    }

    public async Task InitializeAsync(Guid? buildId)
    {
        _buildId = buildId;
        IsExisting = buildId.HasValue;
        PageTitle = buildId.HasValue ? "Edit PC build" : "New PC build";
        await LoadLookupsAsync();
        if (buildId is Guid id)
            await LoadBuildAsync(id);
        else
            RecalcSummary();
    }

    private void EnsureBaseSlots()
    {
        if (Slots.Count > 0) return;
        foreach (var cat in PrimarySlots)
            Slots.Add(new PcBuildSlotLine(cat, cat is "RAM" or "Storage" or "Peripheral"));
    }

    private async Task LoadLookupsAsync()
    {
        try
        {
            var customers = await _api.GetAsync<PagedResult<CustomerListItemDto>>("api/customers?pageSize=200");
            Customers.Clear();
            foreach (var c in customers.Items)
                Customers.Add(new PcBuildPickItem(c.Id, c.DisplayName));

            _allInventory = (await _api.GetAsync<IReadOnlyList<InventoryListItemDto>>("api/inventory?availableOnly=false")).ToList();
            RefreshSlotOptions();
        }
        catch (Exception ex) { Error = ex.Message; }
    }

    private void RefreshSlotOptions(Guid? keepItemId = null)
    {
        foreach (var slot in Slots)
        {
            var selectedId = slot.SelectedItem?.Id ?? keepItemId;
            var options = _allInventory
                .Where(i => string.Equals(i.ComponentType, slot.Category, StringComparison.OrdinalIgnoreCase)
                            || string.Equals(i.ComponentType, "Other", StringComparison.OrdinalIgnoreCase))
                .Where(i => i.Available > 0 || (selectedId.HasValue && i.Id == selectedId))
                .OrderBy(i => i.Sku)
                .ToList();
            slot.Options = new ObservableCollection<InventoryListItemDto>(options);
            if (selectedId is Guid sid)
                slot.SelectedItem = options.FirstOrDefault(o => o.Id == sid) ?? slot.SelectedItem;
        }
    }

    private async Task LoadBuildAsync(Guid id)
    {
        IsBusy = true;
        Error = null;
        try
        {
            var detail = await _api.GetAsync<PcBuildDetailDto>($"api/builds/{id}");
            BuildNumber = detail.Number;
            PageTitle = $"Build {detail.Number}";
            Name = detail.Name ?? "";
            UseCase = detail.UseCase ?? "";
            Budget = detail.Budget?.ToString("0.00") ?? "";
            Notes = detail.Notes ?? "";
            Status = detail.Status;
            SelectedCustomer = detail.CustomerId is Guid cid
                ? Customers.FirstOrDefault(c => c.Id == cid) ?? new PcBuildPickItem(cid, detail.CustomerName ?? "Customer")
                : null;
            if (SelectedCustomer is not null && Customers.All(c => c.Id != SelectedCustomer.Id))
                Customers.Insert(0, SelectedCustomer);

            Slots.Clear();
            foreach (var cat in PrimarySlots)
            {
                var parts = detail.Parts.Where(p => string.Equals(p.Category, cat, StringComparison.OrdinalIgnoreCase)).ToList();
                if (parts.Count == 0)
                {
                    Slots.Add(new PcBuildSlotLine(cat, cat is "RAM" or "Storage" or "Peripheral"));
                    continue;
                }
                foreach (var p in parts)
                {
                    var line = new PcBuildSlotLine(cat, cat is "RAM" or "Storage" or "Peripheral")
                    {
                        QuantityText = p.Quantity.ToString(),
                        CostText = p.Cost.ToString("0.00"),
                        SellText = p.SellPrice.ToString("0.00"),
                        ManualName = p.Name
                    };
                    Slots.Add(line);
                    if (p.InventoryItemId is Guid iid)
                    {
                        var item = _allInventory.FirstOrDefault(i => i.Id == iid);
                        if (item is not null) line.SelectedItem = item;
                    }
                }
            }
            // Extra categories (Other, etc.)
            foreach (var p in detail.Parts.Where(p => !PrimarySlots.Contains(p.Category, StringComparer.OrdinalIgnoreCase)))
            {
                var line = new PcBuildSlotLine(p.Category, true)
                {
                    QuantityText = p.Quantity.ToString(),
                    CostText = p.Cost.ToString("0.00"),
                    SellText = p.SellPrice.ToString("0.00"),
                    ManualName = p.Name
                };
                Slots.Add(line);
                if (p.InventoryItemId is Guid iid)
                {
                    var item = _allInventory.FirstOrDefault(i => i.Id == iid);
                    if (item is not null) line.SelectedItem = item;
                }
            }

            RefreshSlotOptions();
            RecalcSummary();
            StatusMessage = $"Loaded {detail.Number} · {detail.Status}";
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void AddRamLine() => AddExtraLine("RAM");

    [RelayCommand]
    private void AddStorageLine() => AddExtraLine("Storage");

    [RelayCommand]
    private void AddOtherLine() => AddExtraLine("Other");

    private void AddExtraLine(string category)
    {
        var line = new PcBuildSlotLine(category, true);
        Slots.Add(line);
        RefreshSlotOptions();
    }

    [RelayCommand]
    private void RemoveSlot(PcBuildSlotLine? line)
    {
        if (line is null) return;
        var count = Slots.Count(s => s.Category == line.Category);
        if (count <= 1 && PrimarySlots.Contains(line.Category))
        {
            line.SelectedItem = null;
            line.ManualName = "";
            line.QuantityText = "1";
            line.CostText = "0";
            line.SellText = "0";
            RecalcSummary();
            return;
        }
        Slots.Remove(line);
        RecalcSummary();
    }

    [RelayCommand]
    private void RecalcSummary()
    {
        decimal cost = 0, sell = 0;
        foreach (var slot in Slots)
        {
            var input = slot.ToInputOrNull();
            if (input is null) continue;
            cost += input.Cost * input.Quantity;
            sell += input.SellPrice * input.Quantity;
        }
        CostTotalLabel = $"${cost:0.00}";
        SellTotalLabel = $"${sell:0.00}";
        MarginLabel = $"${sell - cost:0.00}";
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        Error = null;
        IsBusy = true;
        try
        {
            RecalcSummary();
            var parts = Slots.Select(s => s.ToInputOrNull()).Where(p => p is not null).Cast<PcPartInputDto>().ToList();
            _ = decimal.TryParse(Budget, out var budget);
            if (_buildId is Guid id)
            {
                var updated = await _api.PutAsync<UpdatePcBuildRequest, PcBuildDetailDto>(
                    $"api/builds/{id}",
                    new UpdatePcBuildRequest(
                        SelectedCustomer?.Id,
                        string.IsNullOrWhiteSpace(Name) ? null : Name.Trim(),
                        string.IsNullOrWhiteSpace(UseCase) ? null : UseCase.Trim(),
                        budget <= 0 ? null : budget,
                        string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim(),
                        Status,
                        parts));
                StatusMessage = $"Saved {updated.Number}. Parts reserved where stock was selected.";
                await LoadBuildAsync(updated.Id);
            }
            else
            {
                var created = await _api.PostAsync<CreatePcBuildRequest, PcBuildDetailDto>(
                    "api/builds",
                    new CreatePcBuildRequest(
                        SelectedCustomer?.Id,
                        string.IsNullOrWhiteSpace(Name) ? null : Name.Trim(),
                        string.IsNullOrWhiteSpace(UseCase) ? null : UseCase.Trim(),
                        budget <= 0 ? null : budget,
                        string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim(),
                        parts,
                        Status));
                _buildId = created.Id;
                IsExisting = true;
                StatusMessage = $"Created {created.Number}. Selected inventory parts are reserved.";
                await LoadBuildAsync(created.Id);
            }

            _allInventory = (await _api.GetAsync<IReadOnlyList<InventoryListItemDto>>("api/inventory")).ToList();
            RefreshSlotOptions();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task SetStatusAsync(string? status)
    {
        if (_buildId is null || string.IsNullOrWhiteSpace(status)) return;
        Error = null;
        IsBusy = true;
        try
        {
            var updated = await _api.PostAsync<UpdatePcBuildStatusRequest, PcBuildDetailDto>(
                $"api/builds/{_buildId}/status",
                new UpdatePcBuildStatusRequest(status));
            Status = updated.Status;
            StatusMessage = $"Status → {updated.Status}";
            await LoadBuildAsync(updated.Id);
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task CancelBuildAsync()
    {
        if (_buildId is null) return;
        Error = null;
        IsBusy = true;
        try
        {
            await _api.DeleteAsync($"api/builds/{_buildId}");
            StatusMessage = "Build cancelled. Reservations released.";
            Status = "Cancelled";
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsBusy = false; }
    }
}
