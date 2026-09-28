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

    public NewRepairPage()
    {
        ViewModel = new NewRepairViewModel(App.Services.GetRequiredService<ApiClient>());
        _picker = new ServiceCataloguePickerSession(App.Services.GetRequiredService<ApiClient>());
        InitializeComponent();
        DataContext = ViewModel;
        _picker.Changed += (_, _) => SyncPickerUi();
        Loaded += async (_, _) =>
        {
            if (_loaded) return;
            _loaded = true;
            await ViewModel.InitAsync(_navCustomerId);
            await _picker.RefreshAsync();
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
        SvcServicesBox.ItemsSource = null;
        SvcServicesBox.ItemsSource = _picker.Services;
        SvcSelectedBox.ItemsSource = null;
        SvcSelectedBox.ItemsSource = _picker.SelectedRows;
        SvcStatusText.Text = _picker.Status ?? string.Empty;
        SvcErrorText.Text = _picker.Error ?? string.Empty;
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
        if (_picker.SelectedServices.Count > 0 && string.IsNullOrWhiteSpace(ViewModel.Issue))
            ViewModel.Issue = string.Join(", ", _picker.SelectedServices.Select(s => s.Name));
        ViewModel.SelectedServiceIds = _picker.SelectedServiceIds.ToList();
        ViewModel.ServiceNotes = SvcNotesBox.Text;
        ViewModel.ServicePartsNotes = SvcPartsBox.Text;
        if (ViewModel.CreateCommand.CanExecute(null))
            await ViewModel.CreateCommand.ExecuteAsync(null);
    }
}
