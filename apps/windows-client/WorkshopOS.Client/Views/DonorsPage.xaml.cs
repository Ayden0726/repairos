using Microsoft.UI.Xaml.Controls;
using WorkshopOS.Client.Services;

namespace WorkshopOS.Client.Views;

public sealed partial class DonorsPage : Page
{
    public DonorsViewModel ViewModel { get; } = new(App.Services.GetRequiredService<ApiClient>());

    public DonorsPage()
    {
        InitializeComponent();
        DataContext = ViewModel;
        Loaded += async (_, _) => await ViewModel.RefreshCommand.ExecuteAsync(null);
    }
}
