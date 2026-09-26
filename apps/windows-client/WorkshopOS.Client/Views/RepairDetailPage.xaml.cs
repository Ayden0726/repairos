using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WorkshopOS.Client.Services;
using WorkshopOS.Contracts.Workshop;

namespace WorkshopOS.Client.Views;

public sealed partial class RepairDetailPage : Page
{
    public RepairDetailViewModel ViewModel { get; } = new(App.Services.GetRequiredService<ApiClient>());

    public RepairDetailPage()
    {
        InitializeComponent();
        DataContext = ViewModel;
    }

    protected override async void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        if (e.Parameter is Guid id)
            await ViewModel.LoadAsync(id);
    }

    private void OpenCustomer_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.Repair is null) return;
        Frame.Navigate(typeof(CustomerDetailPage), ViewModel.Repair.CustomerId);
    }

    private async void Print_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.PrintAsync();
    }

    private void CreateQuote_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.Repair is null) return;
        Frame.Navigate(typeof(QuoteBuilderPage), new QuoteBuilderArgs(
            RepairTicketId: ViewModel.Repair.Id,
            CustomerId: ViewModel.Repair.CustomerId));
    }

    private async void StatusChip_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not Guid statusId) return;
        var match = ViewModel.Statuses.FirstOrDefault(s => s.Id == statusId);
        if (match is null) return;
        ViewModel.SelectedStatus = match;
        await ViewModel.SaveStatusCommand.ExecuteAsync(null);
    }
}
