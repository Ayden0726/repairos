using Microsoft.UI.Xaml.Controls;
using WorkshopOS.Client.Services;

namespace WorkshopOS.Client.Views;

public sealed partial class InventoryPage : Page
{
    public InventoryViewModel ViewModel { get; } = new(App.Services.GetRequiredService<ApiClient>());

    public InventoryPage()
    {
        InitializeComponent();
        DataContext = ViewModel;
        Loaded += async (_, _) => await ViewModel.RefreshCommand.ExecuteAsync(null);
    }
}
