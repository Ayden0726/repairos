using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WorkshopOS.Client.Services;
using WorkshopOS.Contracts.Workshop;

namespace WorkshopOS.Client.Views;

public sealed partial class RepairsPage : Page
{
    public RepairsViewModel ViewModel { get; }

    public RepairsPage()
    {
        ViewModel = new RepairsViewModel(App.Services.GetRequiredService<ApiClient>());
        InitializeComponent();
        DataContext = ViewModel;
        Loaded += async (_, _) => await ViewModel.RefreshCommand.ExecuteAsync(null);
    }

    private void Repair_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is RepairListItemDto r)
            Frame.Navigate(typeof(RepairDetailPage), r.Id);
    }

    private void NewRepair_Click(object sender, RoutedEventArgs e) => Frame.Navigate(typeof(NewRepairPage));
}
