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
    private QuoteBuilderArgs? _args;
    private bool _loaded;

    public QuoteBuilderPage()
    {
        ViewModel = new QuoteBuilderViewModel(
            App.Services.GetRequiredService<ApiClient>(),
            App.Services.GetRequiredService<AuthSession>());
        InitializeComponent();
        DataContext = ViewModel;
        Loaded += async (_, _) =>
        {
            if (_loaded) return;
            _loaded = true;
            await ViewModel.InitAsync(_args);
        };
    }

    protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        _args = e.Parameter as QuoteBuilderArgs ?? new QuoteBuilderArgs();
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
        {
            combo.ItemsSource ??= ViewModel.InventoryParts;
        }
    }

    private void ServiceCombo_GotFocus(object sender, RoutedEventArgs e)
    {
        if (sender is ComboBox combo)
        {
            combo.ItemsSource ??= ViewModel.Services;
        }
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

    private async void AddCatalogueServices_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(QuoteServicePicker.SelectedBrandName))
            ViewModel.DeviceBrand = QuoteServicePicker.SelectedBrandName!;
        if (!string.IsNullOrWhiteSpace(QuoteServicePicker.SelectedModelName))
            ViewModel.DeviceModel = QuoteServicePicker.SelectedModelName!;
        var selected = QuoteServicePicker.SelectedServices.ToList();
        if (selected.Count == 0) return;
        ViewModel.AddCatalogueServices(selected);
        await ViewModel.RecalcCommand.ExecuteAsync(null);
    }
}
