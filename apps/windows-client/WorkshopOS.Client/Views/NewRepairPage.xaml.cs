using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using WorkshopOS.Client.Services;

namespace WorkshopOS.Client.Views;

public sealed partial class NewRepairPage : Page
{
    public NewRepairViewModel ViewModel { get; }
    private Guid? _navCustomerId;
    private bool _loaded;

    public NewRepairPage()
    {
        ViewModel = new NewRepairViewModel(App.Services.GetRequiredService<ApiClient>());
        InitializeComponent();
        DataContext = ViewModel;
        Loaded += async (_, _) =>
        {
            if (_loaded) return;
            _loaded = true;
            await ViewModel.InitAsync(_navCustomerId);
        };
        ViewModel.Created = id => Frame.Navigate(typeof(RepairDetailPage), id);
    }

    protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        if (e.Parameter is Guid customerId)
            _navCustomerId = customerId;
    }

    private void CustomerSearch_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter && ViewModel.SearchCustomersCommand.CanExecute(null))
        {
            ViewModel.SearchCustomersCommand.Execute(null);
            e.Handled = true;
        }
    }

    private async void CreateTicket_Click(object sender, RoutedEventArgs e)
    {
        // Sync device fields from picker when set
        if (!string.IsNullOrWhiteSpace(ServicePicker.SelectedBrandName))
            ViewModel.Brand = ServicePicker.SelectedBrandName!;
        if (!string.IsNullOrWhiteSpace(ServicePicker.SelectedModelName))
            ViewModel.Model = ServicePicker.SelectedModelName!;
        if (ServicePicker.SelectedServices.Count > 0 && string.IsNullOrWhiteSpace(ViewModel.Issue))
            ViewModel.Issue = string.Join(", ", ServicePicker.SelectedServices.Select(s => s.Name));
        ViewModel.SelectedServiceIds = ServicePicker.SelectedServiceIds.ToList();
        ViewModel.ServiceNotes = ServicePicker.Notes;
        ViewModel.ServicePartsNotes = ServicePicker.PartsNotes;
        if (ViewModel.CreateCommand.CanExecute(null))
            await ViewModel.CreateCommand.ExecuteAsync(null);
    }
}
