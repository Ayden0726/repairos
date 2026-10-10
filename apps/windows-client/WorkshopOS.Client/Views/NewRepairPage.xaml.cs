using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using WorkshopOS.Client.Services;
using WorkshopOS.Contracts.Operations;
using WorkshopOS.Contracts.Workshop;

namespace WorkshopOS.Client.Views;

public sealed class TicketPartPick
{
    public required Guid InventoryItemId { get; init; }
    public required string Sku { get; init; }
    public required string Name { get; init; }
    public required decimal SellPrice { get; init; }
    public int Quantity { get; set; } = 1;
    public string Display => Quantity > 1
        ? $"{Sku} · {Name} ×{Quantity} · ${SellPrice * Quantity:0.00}"
        : $"{Sku} · {Name} · ${SellPrice:0.00}";
}

public sealed partial class NewRepairPage : Page
{
    public NewRepairViewModel ViewModel { get; }
    private readonly ServiceCataloguePickerSession _picker;
    private readonly ApiClient _api;
    private readonly ObservableCollection<TicketPartPick> _selectedParts = new();
    private IReadOnlyList<InventoryListItemDto> _inventory = Array.Empty<InventoryListItemDto>();
    private Guid? _navCustomerId;
    private bool _loaded;
    private bool _suppressDeviceTypeEvent;
    private bool _suppressBrandEvent;

    public NewRepairPage()
    {
        _api = App.Services.GetRequiredService<ApiClient>();
        ViewModel = new NewRepairViewModel(_api);
        _picker = new ServiceCataloguePickerSession(_api);
        InitializeComponent();
        DataContext = ViewModel;
        InvSelectedList.ItemsSource = _selectedParts;
        _picker.Changed += (_, _) => SyncPickerUi();
        ViewModel.DeviceTypeChanged = async dt => await ApplyDeviceTypeAsync(dt);
        Loaded += async (_, _) =>
        {
            if (_loaded) return;
            _loaded = true;
            await ViewModel.InitAsync(_navCustomerId);
            var catalogueType = string.Equals(ViewModel.SelectedDeviceType, "Other", StringComparison.OrdinalIgnoreCase)
                ? null
                : ViewModel.SelectedDeviceType;
            await _picker.RefreshAsync();
            await _picker.SetDeviceTypeAsync(catalogueType);
            SyncPickerUi();
            await LoadInventoryAsync();
        };
        ViewModel.Created = id => Frame.Navigate(typeof(RepairDetailPage), id);
    }

    protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        if (e.Parameter is Guid customerId)
            _navCustomerId = customerId;
    }

    private void SyncPickerUi()
    {
        SvcCategoryBox.ItemsSource = null;
        SvcCategoryBox.ItemsSource = _picker.Categories;
        if (_picker.SelectedCategoryKey is string key)
        {
            var match = _picker.Categories.FirstOrDefault(c =>
                string.Equals(c.Key, key, StringComparison.OrdinalIgnoreCase));
            if (match is not null) SvcCategoryBox.SelectedItem = match;
        }
        else
        {
            SvcCategoryBox.SelectedItem = null;
        }
        SvcServicesBox.ItemsSource = null;
        SvcServicesBox.ItemsSource = _picker.Services;
        SvcSelectedBox.ItemsSource = null;
        SvcSelectedBox.ItemsSource = _picker.SelectedRows;
        SvcStatusText.Text = _picker.Status ?? string.Empty;
        SvcErrorText.Text = _picker.Error ?? string.Empty;
        SvcFilterHint.Text = _picker.DeviceTypeFilter is null
            ? "Showing all services — pick Phone, Tablet, Laptop, or PC to narrow the list."
            : $"Showing {_picker.DeviceTypeFilter} services only — brands, models, and categories match this device.";

        DeviceSearchResultsBox.ItemsSource = null;
        DeviceSearchResultsBox.ItemsSource = _picker.ModelSearchResults;

        _suppressBrandEvent = true;
        try
        {
            var brandText = BrandBox.Text;
            var modelText = ModelBox.Text;
            BrandBox.ItemsSource = null;
            BrandBox.ItemsSource = _picker.BrandNames;
            ModelBox.ItemsSource = null;
            ModelBox.ItemsSource = _picker.ModelNames;
            if (!string.IsNullOrWhiteSpace(_picker.SelectedBrandName))
                BrandBox.Text = _picker.SelectedBrandName;
            else if (!string.IsNullOrWhiteSpace(brandText)) BrandBox.Text = brandText;
            else if (!string.IsNullOrWhiteSpace(ViewModel.Brand)) BrandBox.Text = ViewModel.Brand;
            if (!string.IsNullOrWhiteSpace(_picker.SelectedModelName))
                ModelBox.Text = _picker.SelectedModelName;
            else if (!string.IsNullOrWhiteSpace(modelText)) ModelBox.Text = modelText;
            else if (!string.IsNullOrWhiteSpace(ViewModel.Model)) ModelBox.Text = ViewModel.Model;
        }
        finally { _suppressBrandEvent = false; }
    }

    private async void DeviceSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_loaded) return;
        await _picker.SearchModelsDebouncedAsync(DeviceSearchBox.Text);
        DeviceSearchResultsBox.ItemsSource = null;
        DeviceSearchResultsBox.ItemsSource = _picker.ModelSearchResults;
    }

    private async void DeviceSearchResultsBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loaded) return;
        if (DeviceSearchResultsBox.SelectedItem is not DeviceModelPickRow row) return;
        await _picker.ApplyModelPickAsync(row);
        ViewModel.Brand = row.BrandName;
        ViewModel.Model = row.Name;
        SyncPickerUi();
    }

    private async Task ApplyDeviceTypeAsync(string? deviceTypeLabel)
    {
        // "Other" → no catalogue filter (show everything)
        var catalogueType = string.Equals(deviceTypeLabel, "Other", StringComparison.OrdinalIgnoreCase)
            ? null
            : deviceTypeLabel;
        await _picker.SetDeviceTypeAsync(catalogueType);
        if (_picker.Categories.Count == 0 && _picker.BrandNames.Count == 0)
            await _picker.RefreshAsync();
        SyncPickerUi();
    }

    private async void DeviceType_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressDeviceTypeEvent || !_loaded) return;
        if (sender is ComboBox { SelectedItem: string dt })
            await ApplyDeviceTypeAsync(dt);
    }

    private async void BrandBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressBrandEvent || !_loaded) return;
        var brand = BrandBox.SelectedItem as string ?? BrandBox.Text;
        if (!string.IsNullOrWhiteSpace(brand))
            ViewModel.Brand = brand;
        await _picker.SetBrandAsync(brand);
        SyncPickerUi();
    }

    private async void BrandBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;
        var brand = BrandBox.Text;
        if (!string.IsNullOrWhiteSpace(brand))
            ViewModel.Brand = brand.Trim();
        await _picker.SetBrandAsync(brand);
        ModelBox.ItemsSource = null;
        ModelBox.ItemsSource = _picker.ModelNames;
    }

    private void ModelBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(ModelBox.Text))
            ViewModel.Model = ModelBox.Text.Trim();
    }

    private void CustomerSearch_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter && ViewModel.SearchCustomersCommand.CanExecute(null))
        {
            ViewModel.SearchCustomersCommand.Execute(null);
            e.Handled = true;
        }
    }

    private async void SvcSearchBox_TextChanged(object sender, TextChangedEventArgs e) =>
        await _picker.SearchDebouncedAsync(SvcSearchBox.Text);

    private void SvcCategoryBox_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        _picker.SetCategory(SvcCategoryBox.SelectedItem as CatalogueCategoryDto);

    private async void SvcAdd_Click(object sender, RoutedEventArgs e) =>
        await _picker.AddAsync(SvcServicesBox.SelectedItem as CatalogueServiceRow);

    private void SvcRemove_Click(object sender, RoutedEventArgs e) =>
        _picker.Remove(SvcSelectedBox.SelectedItem as CatalogueServiceRow);

    private async Task LoadInventoryAsync()
    {
        try
        {
            _inventory = await _api.GetAsync<IReadOnlyList<InventoryListItemDto>>("api/inventory?availableOnly=true")
                         ?? Array.Empty<InventoryListItemDto>();
            ApplyInventoryFilter(InvPartBox.Text);
            InvPartsSummary.Text = $"{_inventory.Count} stock item(s) available.";
        }
        catch (Exception ex)
        {
            InvPartsSummary.Text = "Could not load inventory: " + ex.Message;
        }
    }

    private void ApplyInventoryFilter(string? query)
    {
        var q = query?.Trim() ?? string.Empty;
        IEnumerable<InventoryListItemDto> filtered = _inventory;
        if (q.Length > 0)
        {
            filtered = _inventory.Where(p =>
                p.Sku.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                p.Name.Contains(q, StringComparison.OrdinalIgnoreCase));
        }
        InvPartBox.ItemsSource = filtered.Take(80).ToList();
    }

    private async void InvLoad_Click(object sender, RoutedEventArgs e) => await LoadInventoryAsync();

    private void InvAdd_Click(object sender, RoutedEventArgs e)
    {
        InventoryListItemDto? item = InvPartBox.SelectedItem as InventoryListItemDto;
        if (item is null && !string.IsNullOrWhiteSpace(InvPartBox.Text))
        {
            var q = InvPartBox.Text.Trim();
            item = _inventory.FirstOrDefault(p =>
                p.Sku.Equals(q, StringComparison.OrdinalIgnoreCase) ||
                p.Name.Equals(q, StringComparison.OrdinalIgnoreCase) ||
                p.Name.Contains(q, StringComparison.OrdinalIgnoreCase));
        }
        if (item is null)
        {
            InvPartsSummary.Text = "Select a part from inventory first.";
            return;
        }

        var existing = _selectedParts.FirstOrDefault(p => p.InventoryItemId == item.Id);
        if (existing is not null)
        {
            existing.Quantity += 1;
            // refresh display binding
            var idx = _selectedParts.IndexOf(existing);
            _selectedParts.RemoveAt(idx);
            _selectedParts.Insert(idx, new TicketPartPick
            {
                InventoryItemId = existing.InventoryItemId,
                Sku = existing.Sku,
                Name = existing.Name,
                SellPrice = existing.SellPrice,
                Quantity = existing.Quantity
            });
        }
        else
        {
            _selectedParts.Add(new TicketPartPick
            {
                InventoryItemId = item.Id,
                Sku = item.Sku,
                Name = item.Name,
                SellPrice = item.SellPrice,
                Quantity = 1
            });
        }
        SyncPartsSummary();
    }

    private void InvRemove_Click(object sender, RoutedEventArgs e)
    {
        if (InvSelectedList.SelectedItem is TicketPartPick pick)
            _selectedParts.Remove(pick);
        SyncPartsSummary();
    }

    private void SyncPartsSummary()
    {
        if (_selectedParts.Count == 0)
        {
            InvPartsSummary.Text = $"{_inventory.Count} stock item(s) available.";
            return;
        }
        var total = _selectedParts.Sum(p => p.SellPrice * p.Quantity);
        InvPartsSummary.Text = $"{_selectedParts.Count} part line(s) · parts ${total:0.00} (service fee charged once on the ticket).";
    }

    private async void CreateTicket_Click(object sender, RoutedEventArgs e)
    {
        // Prefer ComboBox text (editable) over SelectedItem
        if (!string.IsNullOrWhiteSpace(BrandBox.Text))
            ViewModel.Brand = BrandBox.Text.Trim();
        if (!string.IsNullOrWhiteSpace(ModelBox.Text))
            ViewModel.Model = ModelBox.Text.Trim();

        if (_picker.SelectedServices.Count > 0 && string.IsNullOrWhiteSpace(ViewModel.Issue))
            ViewModel.Issue = string.Join(", ", _picker.SelectedServices.Select(s => s.Name));
        ViewModel.SelectedServiceIds = _picker.SelectedServiceIds.ToList();
        ViewModel.ServiceNotes = SvcNotesBox.Text;
        ViewModel.ServicePartsNotes = SvcPartsBox.Text;
        ViewModel.SelectedParts = _selectedParts
            .Select(p => new RepairPartSelectionDto(p.InventoryItemId, p.Quantity))
            .ToList();
        if (ViewModel.CreateCommand.CanExecute(null))
            await ViewModel.CreateCommand.ExecuteAsync(null);
    }
}
