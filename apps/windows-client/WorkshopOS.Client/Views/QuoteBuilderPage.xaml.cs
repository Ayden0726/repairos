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

    public QuoteBuilderPage()
    {
        ViewModel = new QuoteBuilderViewModel(
            App.Services.GetRequiredService<ApiClient>(),
            App.Services.GetRequiredService<AuthSession>());
        _picker = new ServiceCataloguePickerSession(App.Services.GetRequiredService<ApiClient>());
        InitializeComponent();
        DataContext = ViewModel;
        _picker.Changed += (_, _) => SyncPickerUi();
        Loaded += async (_, _) =>
        {
            if (_loaded) return;
            _loaded = true;
            await ViewModel.InitAsync(_args);
            await _picker.RefreshAsync();
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
                combo.ItemsSource = ViewModel.InventoryParts;
                combo.Tag = line;
            }
        }
    }

    private void PartCombo_GotFocus(object sender, RoutedEventArgs e)
    {
        if (sender is ComboBox combo)
            combo.ItemsSource ??= ViewModel.InventoryParts;
    }

    private void ServiceCombo_GotFocus(object sender, RoutedEventArgs e)
    {
        if (sender is ComboBox combo)
            combo.ItemsSource ??= ViewModel.Services;
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
            combo.ItemsSource ??= ViewModel.Services;
            if (combo is { SelectedItem: ServicePricingDto svc, Tag: QuoteLineDraft line })
                ViewModel.ApplyService(line, svc);
        }
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
        var selected = _picker.SelectedServices.ToList();
        if (selected.Count == 0) return;
        ViewModel.AddCatalogueServices(selected);
        await ViewModel.RecalcCommand.ExecuteAsync(null);
    }
}
