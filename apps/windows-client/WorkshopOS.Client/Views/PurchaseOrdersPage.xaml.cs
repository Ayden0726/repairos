using Microsoft.UI.Xaml.Controls;
using WorkshopOS.Client.Services;

namespace WorkshopOS.Client.Views;

public sealed partial class PurchaseOrdersPage : Page
{
    public PurchaseOrdersViewModel ViewModel { get; } = new(App.Services.GetRequiredService<ApiClient>());

    public PurchaseOrdersPage()
    {
        InitializeComponent();
        DataContext = ViewModel;
        Loaded += async (_, _) => await ViewModel.RefreshCommand.ExecuteAsync(null);
    }

    private async void Line_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is PoLineRowVm row)
            await ViewModel.ReceiveRemainingCommand.ExecuteAsync(row);
    }
}
