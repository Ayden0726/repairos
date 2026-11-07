using Microsoft.UI.Xaml.Controls;
using WorkshopOS.Client.Services;

namespace WorkshopOS.Client.Views;

public sealed partial class WarrantyClaimsPage : Page
{
    public WarrantyClaimsViewModel ViewModel { get; } = new(App.Services.GetRequiredService<ApiClient>());

    public WarrantyClaimsPage()
    {
        InitializeComponent();
        DataContext = ViewModel;
        Loaded += async (_, _) => await ViewModel.RefreshCommand.ExecuteAsync(null);
    }
}
