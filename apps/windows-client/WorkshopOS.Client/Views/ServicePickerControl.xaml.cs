using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using WorkshopOS.Client.Services;
using WorkshopOS.Contracts.Operations;

namespace WorkshopOS.Client.Views;

public sealed partial class ServicePickerControl : UserControl
{
    private readonly ApiClient _api;
    private bool _loaded;
    private CancellationTokenSource? _searchCts;

    public ObservableCollection<CatalogueCategoryDto> Categories { get; } = new();
    public ObservableCollection<DeviceBrandDto> Brands { get; } = new();
    public ObservableCollection<DeviceModelDto> Models { get; } = new();
    public ObservableCollection<CatalogueServiceDto> Services { get; } = new();
    public ObservableCollection<CatalogueServiceDto> Favourites { get; } = new();
    public ObservableCollection<CatalogueServiceDto> Recent { get; } = new();
    public ObservableCollection<CatalogueServiceDto> SelectedServices { get; } = new();
    public ObservableCollection<string> DeviceTypes { get; } = new();

    public event EventHandler? SelectionChanged;

    public string? SelectedBrandName => BrandBox.SelectedItem is DeviceBrandDto b ? b.Name : BrandBox.Text;
    public string? SelectedModelName => ModelBox.SelectedItem is DeviceModelDto m ? m.Name : ModelBox.Text;
    public string? SelectedDeviceType => DeviceTypeBox.SelectedItem as string;
    public string? SelectedCategoryKey => CategoryBox.SelectedItem is CatalogueCategoryDto c ? c.Key : null;

    public string Notes
    {
        get => NotesBox.Text ?? string.Empty;
        set => NotesBox.Text = value ?? string.Empty;
    }

    public string PartsNotes
    {
        get => PartsBox.Text ?? string.Empty;
        set => PartsBox.Text = value ?? string.Empty;
    }

    public ServicePickerControl()
    {
        _api = App.Services.GetRequiredService<ApiClient>();
        InitializeComponent();
        Root.DataContext = this;
        Loaded += async (_, _) =>
        {
            if (_loaded) return;
            _loaded = true;
            await RefreshAsync();
        };
    }

    public IReadOnlyList<Guid> SelectedServiceIds => SelectedServices.Select(s => s.Id).ToList();

    public async Task RefreshAsync()
    {
        try
        {
            StatusText.Text = "Loading catalogue…";
            ErrorText.Text = string.Empty;
            var cats = await _api.GetAsync<CatalogueCategoryDto[]>("api/catalogue/categories");
            Categories.Clear();
            foreach (var c in cats) Categories.Add(c);
            CategoryBox.ItemsSource = Categories;

            DeviceTypes.Clear();
            foreach (var dt in cats.Select(c => c.DeviceType).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase))
                DeviceTypes.Add(dt!);
            if (DeviceTypes.Count == 0)
            {
                foreach (var dt in new[] { "Phone", "Tablet", "Laptop", "PC" }) DeviceTypes.Add(dt);
            }
            DeviceTypeBox.ItemsSource = DeviceTypes;

            await LoadFavouritesAndRecentAsync();
            await SearchServicesAsync();
            StatusText.Text = $"{cats.Length} categories · {Services.Count} services shown";
        }
        catch (Exception ex)
        {
            ErrorText.Text = ex.Message;
            StatusText.Text = string.Empty;
        }
    }

    private async Task LoadFavouritesAndRecentAsync()
    {
        try
        {
            var fav = await _api.GetAsync<CatalogueServiceDto[]>("api/catalogue/favourites");
            Favourites.Clear();
            foreach (var s in fav) Favourites.Add(s);
            FavList.ItemsSource = Favourites;

            var recent = await _api.GetAsync<CatalogueServiceDto[]>("api/catalogue/recent?take=12");
            Recent.Clear();
            foreach (var s in recent) Recent.Add(s);
            RecentList.ItemsSource = Recent;
        }
        catch { /* optional */ }
    }

    private async void CategoryBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CategoryBox.SelectedItem is CatalogueCategoryDto cat && !string.IsNullOrWhiteSpace(cat.DeviceType))
        {
            DeviceTypeBox.SelectedItem = DeviceTypes.FirstOrDefault(d =>
                string.Equals(d, cat.DeviceType, StringComparison.OrdinalIgnoreCase));
            await LoadBrandsAsync(cat.DeviceType);
        }
        await SearchServicesAsync();
    }

    private async void DeviceTypeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        await LoadBrandsAsync(SelectedDeviceType);
        await SearchServicesAsync();
    }

    private async Task LoadBrandsAsync(string? deviceType)
    {
        try
        {
            var url = string.IsNullOrWhiteSpace(deviceType)
                ? "api/catalogue/brands"
                : $"api/catalogue/brands?deviceType={Uri.EscapeDataString(deviceType)}";
            var brands = await _api.GetAsync<DeviceBrandDto[]>(url);
            Brands.Clear();
            foreach (var b in brands) Brands.Add(b);
            BrandBox.ItemsSource = Brands;
            Models.Clear();
            ModelBox.ItemsSource = Models;
        }
        catch (Exception ex) { ErrorText.Text = ex.Message; }
    }

    private async void BrandBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (BrandBox.SelectedItem is not DeviceBrandDto brand) return;
        try
        {
            var dt = SelectedDeviceType;
            var url = $"api/catalogue/models?brandId={brand.Id}" +
                      (string.IsNullOrWhiteSpace(dt) ? "" : $"&deviceType={Uri.EscapeDataString(dt)}");
            var models = await _api.GetAsync<DeviceModelDto[]>(url);
            Models.Clear();
            foreach (var m in models) Models.Add(m);
            ModelBox.ItemsSource = Models;
        }
        catch (Exception ex) { ErrorText.Text = ex.Message; }
        await SearchServicesAsync();
    }

    private async void ModelBox_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        await SearchServicesAsync();

    private async void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;
        try
        {
            await Task.Delay(220, token);
            await SearchServicesAsync();
        }
        catch (TaskCanceledException) { }
    }

    private async void SearchBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            await SearchServicesAsync();
            e.Handled = true;
        }
    }

    private async Task SearchServicesAsync()
    {
        try
        {
            var q = SearchBox.Text?.Trim();
            var parts = new List<string> { "activeOnly=true" };
            if (!string.IsNullOrWhiteSpace(q)) parts.Add("q=" + Uri.EscapeDataString(q));
            if (!string.IsNullOrWhiteSpace(SelectedCategoryKey)) parts.Add("category=" + Uri.EscapeDataString(SelectedCategoryKey));
            if (!string.IsNullOrWhiteSpace(SelectedDeviceType)) parts.Add("deviceType=" + Uri.EscapeDataString(SelectedDeviceType));
            if (!string.IsNullOrWhiteSpace(SelectedBrandName)) parts.Add("brand=" + Uri.EscapeDataString(SelectedBrandName));
            if (!string.IsNullOrWhiteSpace(SelectedModelName)) parts.Add("model=" + Uri.EscapeDataString(SelectedModelName));
            var list = await _api.GetAsync<CatalogueServiceDto[]>("api/catalogue/services?" + string.Join('&', parts));
            Services.Clear();
            foreach (var s in list) Services.Add(s);
            ServicesList.ItemsSource = Services;
            StatusText.Text = $"{list.Length} service(s)";
        }
        catch (Exception ex)
        {
            ErrorText.Text = ex.Message;
        }
    }

    private async void ServicesList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is CatalogueServiceDto svc)
            await ToggleSelectAsync(svc);
    }

    private async void Quick_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is CatalogueServiceDto svc)
            await ToggleSelectAsync(svc);
    }

    private async Task ToggleSelectAsync(CatalogueServiceDto svc)
    {
        var existing = SelectedServices.FirstOrDefault(s => s.Id == svc.Id);
        if (existing is not null)
            SelectedServices.Remove(existing);
        else
        {
            SelectedServices.Add(svc);
            try { await _api.PostAsync($"api/catalogue/recent/{svc.Id}"); }
            catch { /* fire and forget */ }
        }
        SelectedList.ItemsSource = null;
        SelectedList.ItemsSource = SelectedServices;
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RemoveSelected_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: CatalogueServiceDto svc })
        {
            SelectedServices.Remove(svc);
            SelectedList.ItemsSource = null;
            SelectedList.ItemsSource = SelectedServices;
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private async void Favourite_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: CatalogueServiceDto svc }) return;
        try
        {
            await _api.PostAsync($"api/catalogue/favourites/{svc.Id}");
            await LoadFavouritesAndRecentAsync();
        }
        catch (Exception ex) { ErrorText.Text = ex.Message; }
    }

    private async void CustomSave_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var name = (CustomNameBox.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                ErrorText.Text = "Custom service name is required.";
                return;
            }
            var labour = CustomLabourBox.Value;
            var fee = double.IsNaN(labour) ? 50m : (decimal)labour;
            var mins = CustomMinutesBox.Value;
            int? estimated = double.IsNaN(mins) ? null : (int)mins;
            var created = await _api.PostAsync<UpsertCatalogueServiceRequest, CatalogueServiceDto>(
                "api/catalogue/services",
                new UpsertCatalogueServiceRequest(
                    null, name, SelectedCategoryKey, CustomDescBox.Text, fee, null, true, 0,
                    null, null, SelectedDeviceType, null, null, 0m, estimated,
                    null, null, false, false, null, Notes, CustomDescBox.Text, null, SelectedCategoryKey));
            SelectedServices.Add(created);
            SelectedList.ItemsSource = null;
            SelectedList.ItemsSource = SelectedServices;
            CustomNameBox.Text = string.Empty;
            CustomDescBox.Text = string.Empty;
            SelectionChanged?.Invoke(this, EventArgs.Empty);
            await SearchServicesAsync();
        }
        catch (Exception ex) { ErrorText.Text = ex.Message; }
    }

    private void ClearFilters_Click(object sender, RoutedEventArgs e)
    {
        SearchBox.Text = string.Empty;
        CategoryBox.SelectedItem = null;
        DeviceTypeBox.SelectedItem = null;
        BrandBox.SelectedItem = null;
        ModelBox.SelectedItem = null;
        _ = SearchServicesAsync();
    }
}
