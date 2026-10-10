using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using WorkshopOS.Client.Services;
using WorkshopOS.Contracts.Operations;

namespace WorkshopOS.Client.Views;

public sealed partial class NewRepairPage : Page
{
    public NewRepairViewModel ViewModel { get; }
    private readonly ServiceCataloguePickerSession _picker;
    private Guid? _navCustomerId;
    private bool _loaded;
    private bool _suppressDeviceTypeEvent;
    private bool _suppressBrandEvent;

    public NewRepairPage()
    {
        ViewModel = new NewRepairViewModel(App.Services.GetRequiredService<ApiClient>());
        _picker = new ServiceCataloguePickerSession(App.Services.GetRequiredService<ApiClient>());
        InitializeComponent();
        DataContext = ViewModel;
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

        _suppressBrandEvent = true;
        try
        {
            var brandText = BrandBox.Text;
            var modelText = ModelBox.Text;
            BrandBox.ItemsSource = null;
            BrandBox.ItemsSource = _picker.BrandNames;
            ModelBox.ItemsSource = null;
            ModelBox.ItemsSource = _picker.ModelNames;
            if (!string.IsNullOrWhiteSpace(brandText)) BrandBox.Text = brandText;
            else if (!string.IsNullOrWhiteSpace(ViewModel.Brand)) BrandBox.Text = ViewModel.Brand;
            if (!string.IsNullOrWhiteSpace(modelText)) ModelBox.Text = modelText;
            else if (!string.IsNullOrWhiteSpace(ViewModel.Model)) ModelBox.Text = ViewModel.Model;
        }
        finally { _suppressBrandEvent = false; }
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
        if (ViewModel.CreateCommand.CanExecute(null))
            await ViewModel.CreateCommand.ExecuteAsync(null);
    }
}
