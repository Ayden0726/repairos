using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using WorkshopOS.Client.Services;
using WorkshopOS.Contracts.Operations;

namespace WorkshopOS.Client.Views;

public sealed partial class QuoteBuilderPage : Page
{
    public QuoteBuilderViewModel ViewModel { get; }
    private readonly ServiceCataloguePickerSession _picker;
    private QuoteBuilderArgs? _args;
    private bool _loaded;
    private readonly HashSet<ComboBox> _partSearchHooked = new();
    private readonly HashSet<ComboBox> _serviceSearchHooked = new();
    private bool _suppressPartFilter;
    private bool _suppressServiceFilter;
    private bool _suppressBrandEvent;

    public QuoteBuilderPage()
    {
        ViewModel = new QuoteBuilderViewModel(
            App.Services.GetRequiredService<ApiClient>(),
            App.Services.GetRequiredService<AuthSession>());
        _picker = new ServiceCataloguePickerSession(App.Services.GetRequiredService<ApiClient>());
        InitializeComponent();
        DataContext = ViewModel;
        _picker.Changed += (_, _) => SyncPickerUi();
        ViewModel.DeviceTypeChanged = async dt => await ApplyDeviceTypeAsync(dt);
        ViewModel.DeviceBrandModelChanged = () =>
        {
            if (!string.IsNullOrWhiteSpace(ViewModel.DeviceBrand))
                QuoteBrandBox.Text = ViewModel.DeviceBrand;
            if (!string.IsNullOrWhiteSpace(ViewModel.DeviceModel))
                QuoteModelBox.Text = ViewModel.DeviceModel;
        };
        Loaded += async (_, _) =>
        {
            if (_loaded) return;
            _loaded = true;
            await ViewModel.InitAsync(_args);
            await _picker.RefreshAsync();
            var catalogueType = string.Equals(ViewModel.SelectedDeviceType, "Other", StringComparison.OrdinalIgnoreCase)
                ? null
                : ViewModel.SelectedDeviceType;
            await _picker.SetDeviceTypeAsync(catalogueType);
            if (!string.IsNullOrWhiteSpace(ViewModel.DeviceBrand))
                await _picker.SetBrandAsync(ViewModel.DeviceBrand);
            SyncPickerUi();
        };
    }

    protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        _args = e.Parameter as QuoteBuilderArgs ?? new QuoteBuilderArgs();
    }

    private void SyncPickerUi()
    {
        QuoteSvcCategoryBox.ItemsSource = null;
        QuoteSvcCategoryBox.ItemsSource = _picker.Categories;
        QuoteSvcServicesBox.ItemsSource = null;
        QuoteSvcServicesBox.ItemsSource = _picker.Services;
        QuoteSvcSelectedBox.ItemsSource = null;
        QuoteSvcSelectedBox.ItemsSource = _picker.SelectedRows;
        QuoteSvcStatusText.Text = _picker.Status ?? string.Empty;
        QuoteSvcErrorText.Text = _picker.Error ?? string.Empty;
        QuoteSvcFilterHint.Text = _picker.DeviceTypeFilter is null
            ? "Showing all services — pick a device type to narrow the catalogue."
            : $"Showing {_picker.DeviceTypeFilter} services only.";

        _suppressBrandEvent = true;
        try
        {
            var brandText = QuoteBrandBox.Text;
            var modelText = QuoteModelBox.Text;
            QuoteBrandBox.ItemsSource = null;
            QuoteBrandBox.ItemsSource = _picker.BrandNames;
            QuoteModelBox.ItemsSource = null;
            QuoteModelBox.ItemsSource = _picker.ModelNames;
            if (!string.IsNullOrWhiteSpace(brandText)) QuoteBrandBox.Text = brandText;
            else if (!string.IsNullOrWhiteSpace(ViewModel.DeviceBrand)) QuoteBrandBox.Text = ViewModel.DeviceBrand;
            if (!string.IsNullOrWhiteSpace(modelText)) QuoteModelBox.Text = modelText;
            else if (!string.IsNullOrWhiteSpace(ViewModel.DeviceModel)) QuoteModelBox.Text = ViewModel.DeviceModel;
        }
        finally { _suppressBrandEvent = false; }
    }

    private async Task ApplyDeviceTypeAsync(string? deviceTypeLabel)
    {
        var catalogueType = string.Equals(deviceTypeLabel, "Other", StringComparison.OrdinalIgnoreCase)
            ? null
            : deviceTypeLabel;
        await _picker.SetDeviceTypeAsync(catalogueType);
        SyncPickerUi();
    }

    private async void QuoteDeviceType_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loaded) return;
        if (sender is ComboBox { SelectedItem: string dt })
            await ApplyDeviceTypeAsync(dt);
    }

    private async void QuoteBrandBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressBrandEvent || !_loaded) return;
        var brand = QuoteBrandBox.SelectedItem as string ?? QuoteBrandBox.Text;
        if (!string.IsNullOrWhiteSpace(brand))
            ViewModel.DeviceBrand = brand;
        await _picker.SetBrandAsync(brand);
        SyncPickerUi();
    }

    private async void QuoteBrandBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;
        var brand = QuoteBrandBox.Text;
        if (!string.IsNullOrWhiteSpace(brand))
            ViewModel.DeviceBrand = brand.Trim();
        await _picker.SetBrandAsync(brand);
        QuoteModelBox.ItemsSource = null;
        QuoteModelBox.ItemsSource = _picker.ModelNames;
    }

    private void QuoteModelBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(QuoteModelBox.Text))
            ViewModel.DeviceModel = QuoteModelBox.Text.Trim();
    }

    private void CustomerSearch_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter && ViewModel.SearchCustomersCommand.CanExecute(null))
        {
            ViewModel.SearchCustomersCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void Back_Click(object sender, RoutedEventArgs e) =>
        Frame.Navigate(typeof(QuotesPage));

    private void RemoveLine_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is QuoteLineDraft line)
            ViewModel.RemoveLineCommand.Execute(line);
    }

    private async void LineField_LostFocus(object sender, RoutedEventArgs e) =>
        await ViewModel.RecalcCommand.ExecuteAsync(null);

    private void LoadParts_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: QuoteLineDraft line }) return;
        if (sender is FrameworkElement fe)
        {
            var parent = fe.Parent as Panel;
            var combo = parent?.Children.OfType<ComboBox>().FirstOrDefault(c =>
                string.Equals(c.Header as string, "Inventory part", StringComparison.Ordinal));
            if (combo is not null)
            {
                _suppressPartFilter = true;
                try
                {
                    combo.ItemsSource = ViewModel.InventoryParts;
                    combo.Tag = line;
                }
                finally { _suppressPartFilter = false; }
                HookPartSearch(combo);
            }
        }
    }

    private void PartCombo_GotFocus(object sender, RoutedEventArgs e)
    {
        if (sender is not ComboBox combo) return;
        if (combo.ItemsSource is null)
        {
            _suppressPartFilter = true;
            try { combo.ItemsSource = ViewModel.InventoryParts; }
            finally { _suppressPartFilter = false; }
        }
        HookPartSearch(combo);
    }

    private void ServiceCombo_GotFocus(object sender, RoutedEventArgs e)
    {
        if (sender is not ComboBox combo) return;
        if (combo.ItemsSource is null)
        {
            _suppressServiceFilter = true;
            try { combo.ItemsSource = ViewModel.Services; }
            finally { _suppressServiceFilter = false; }
        }
        HookServiceSearch(combo);
    }

    private void HookPartSearch(ComboBox combo)
    {
        if (!_partSearchHooked.Add(combo)) return;
        combo.RegisterPropertyChangedCallback(ComboBox.TextProperty, (_, _) =>
        {
            if (_suppressPartFilter) return;
            FilterPartItems(combo);
        });
    }

    private void HookServiceSearch(ComboBox combo)
    {
        if (!_serviceSearchHooked.Add(combo)) return;
        combo.RegisterPropertyChangedCallback(ComboBox.TextProperty, (_, _) =>
        {
            if (_suppressServiceFilter) return;
            FilterServiceItems(combo);
        });
    }

    private void FilterPartItems(ComboBox combo)
    {
        var q = (combo.Text ?? string.Empty).Trim();
        if (combo.SelectedItem is InventoryListItemDto selected &&
            string.Equals(selected.Name, combo.Text, StringComparison.Ordinal))
            return;

        IEnumerable<InventoryListItemDto> src = ViewModel.InventoryParts;
        if (!string.IsNullOrEmpty(q))
        {
            src = src.Where(p =>
                p.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                p.Sku.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrEmpty(p.Category) && p.Category.Contains(q, StringComparison.OrdinalIgnoreCase)));
        }

        var list = src.ToList();
        _suppressPartFilter = true;
        try
        {
            var keep = combo.SelectedItem as InventoryListItemDto;
            combo.ItemsSource = list;
            if (keep is not null && list.Contains(keep))
                combo.SelectedItem = keep;
            if (!string.IsNullOrEmpty(q) && !combo.IsDropDownOpen)
                combo.IsDropDownOpen = true;
        }
        finally { _suppressPartFilter = false; }
    }

    private void FilterServiceItems(ComboBox combo)
    {
        var q = (combo.Text ?? string.Empty).Trim();
        if (combo.SelectedItem is ServicePricingDto selected &&
            string.Equals(selected.Name, combo.Text, StringComparison.OrdinalIgnoreCase))
            return;

        IEnumerable<ServicePricingDto> src = ViewModel.Services;
        if (!string.IsNullOrEmpty(q))
        {
            src = src.Where(s =>
                s.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrEmpty(s.Code) && s.Code.Contains(q, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(s.Category) && s.Category.Contains(q, StringComparison.OrdinalIgnoreCase)));
        }

        var list = src.ToList();
        _suppressServiceFilter = true;
        try
        {
            var keep = combo.SelectedItem as ServicePricingDto;
            combo.ItemsSource = list;
            if (keep is not null && list.Contains(keep))
                combo.SelectedItem = keep;
            if (!string.IsNullOrEmpty(q) && !combo.IsDropDownOpen)
                combo.IsDropDownOpen = true;
        }
        finally { _suppressServiceFilter = false; }
    }

    private void PartCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox { SelectedItem: InventoryListItemDto item, Tag: QuoteLineDraft line })
            ViewModel.ApplyInventoryPart(line, item);
    }

    private void ServiceCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox combo)
        {
            if (combo.ItemsSource is null)
            {
                _suppressServiceFilter = true;
                try { combo.ItemsSource = ViewModel.Services; }
                finally { _suppressServiceFilter = false; }
            }
            if (combo is { SelectedItem: ServicePricingDto svc, Tag: QuoteLineDraft line })
                ViewModel.ApplyService(line, svc);
        }
    }

    private void PartCombo_TextSubmitted(ComboBox sender, ComboBoxTextSubmittedEventArgs args)
    {
        var text = args.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(text) || sender.Tag is not QuoteLineDraft line) return;

        var match = ViewModel.InventoryParts.FirstOrDefault(p =>
                string.Equals(p.Name, text, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(p.Sku, text, StringComparison.OrdinalIgnoreCase))
            ?? ViewModel.InventoryParts.FirstOrDefault(p =>
                p.Name.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                p.Sku.Contains(text, StringComparison.OrdinalIgnoreCase));

        if (match is null) return;
        _suppressPartFilter = true;
        try
        {
            sender.ItemsSource = ViewModel.InventoryParts;
            sender.SelectedItem = match;
        }
        finally { _suppressPartFilter = false; }
        ViewModel.ApplyInventoryPart(line, match);
        args.Handled = true;
    }

    private void ServiceCombo_TextSubmitted(ComboBox sender, ComboBoxTextSubmittedEventArgs args)
    {
        var text = args.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(text) || sender.Tag is not QuoteLineDraft line) return;

        var match = ViewModel.Services.FirstOrDefault(s =>
                string.Equals(s.Name, text, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrEmpty(s.Code) && string.Equals(s.Code, text, StringComparison.OrdinalIgnoreCase)))
            ?? ViewModel.Services.FirstOrDefault(s =>
                s.Name.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrEmpty(s.Code) && s.Code.Contains(text, StringComparison.OrdinalIgnoreCase)));

        if (match is null) return;
        _suppressServiceFilter = true;
        try
        {
            sender.ItemsSource = ViewModel.Services;
            sender.SelectedItem = match;
        }
        finally { _suppressServiceFilter = false; }
        ViewModel.ApplyService(line, match);
        args.Handled = true;
    }

    private async void QuoteSvcSearchBox_TextChanged(object sender, TextChangedEventArgs e) =>
        await _picker.SearchDebouncedAsync(QuoteSvcSearchBox.Text);

    private void QuoteSvcCategoryBox_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        _picker.SetCategory(QuoteSvcCategoryBox.SelectedItem as CatalogueCategoryDto);

    private async void QuoteSvcAdd_Click(object sender, RoutedEventArgs e) =>
        await _picker.AddAsync(QuoteSvcServicesBox.SelectedItem as CatalogueServiceRow);

    private void QuoteSvcRemove_Click(object sender, RoutedEventArgs e) =>
        _picker.Remove(QuoteSvcSelectedBox.SelectedItem as CatalogueServiceRow);

    private async void AddCatalogueServices_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(QuoteBrandBox.Text))
            ViewModel.DeviceBrand = QuoteBrandBox.Text.Trim();
        if (!string.IsNullOrWhiteSpace(QuoteModelBox.Text))
            ViewModel.DeviceModel = QuoteModelBox.Text.Trim();

        var selected = _picker.SelectedServices.ToList();
        if (selected.Count == 0) return;
        ViewModel.AddCatalogueServices(selected);
        await ViewModel.RecalcCommand.ExecuteAsync(null);
    }
}
