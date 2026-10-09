using Microsoft.UI.Xaml.Controls;
using WorkshopOS.Client.Services;

namespace WorkshopOS.Client.Views;

public sealed partial class UsedTechPage : Page
{
    public UsedTechViewModel ViewModel { get; }

    public UsedTechPage()
    {
        ViewModel = new UsedTechViewModel(
            App.Services.GetRequiredService<ApiClient>(),
            App.Services.GetRequiredService<AuthSession>());
        InitializeComponent();
        DataContext = ViewModel;
        Loaded += async (_, _) => await ViewModel.RefreshCommand.ExecuteAsync(null);
    }

    private async void Device_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is UsedDeviceRowVm row)
            await ViewModel.LoadDeviceAsync(row.Id);
    }
}
