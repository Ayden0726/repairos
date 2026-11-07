using Microsoft.UI.Xaml.Controls;
using WorkshopOS.Client.Services;

namespace WorkshopOS.Client.Views;

public sealed partial class RefundsPage : Page
{
    public RefundsViewModel ViewModel { get; } = new(App.Services.GetRequiredService<ApiClient>());

    public RefundsPage()
    {
        InitializeComponent();
        DataContext = ViewModel;
        Loaded += async (_, _) => await ViewModel.RefreshCommand.ExecuteAsync(null);
    }
}
