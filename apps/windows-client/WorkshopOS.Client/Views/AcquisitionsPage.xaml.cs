using Microsoft.UI.Xaml.Controls;
using WorkshopOS.Client.Services;

namespace WorkshopOS.Client.Views;

public sealed partial class AcquisitionsPage : Page
{
    public AcquisitionsViewModel ViewModel { get; } = new(App.Services.GetRequiredService<ApiClient>());

    public AcquisitionsPage()
    {
        InitializeComponent();
        DataContext = ViewModel;
        Loaded += async (_, _) => await ViewModel.RefreshCommand.ExecuteAsync(null);
    }
}
