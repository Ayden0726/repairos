using Microsoft.UI.Xaml.Controls;
using WorkshopOS.Client.Services;

namespace WorkshopOS.Client.Views;

public sealed partial class ReportsPage : Page
{
    public ReportsViewModel ViewModel { get; } = new(App.Services.GetRequiredService<ApiClient>());

    public ReportsPage()
    {
        InitializeComponent();
        DataContext = ViewModel;
        Loaded += async (_, _) => await ViewModel.RefreshCommand.ExecuteAsync(null);
    }
}
