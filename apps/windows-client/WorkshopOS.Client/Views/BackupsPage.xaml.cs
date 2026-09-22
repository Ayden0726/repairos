using Microsoft.UI.Xaml.Controls;
using WorkshopOS.Client.Services;

namespace WorkshopOS.Client.Views;

public sealed partial class BackupsPage : Page
{
    public BackupsViewModel ViewModel { get; } = new(App.Services.GetRequiredService<ApiClient>());

    public BackupsPage()
    {
        InitializeComponent();
        DataContext = ViewModel;
        Loaded += async (_, _) => await ViewModel.RefreshCommand.ExecuteAsync(null);
    }
}
