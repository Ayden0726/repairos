using Microsoft.UI.Xaml.Controls;
using WorkshopOS.Client.Services;

namespace WorkshopOS.Client.Views;

public sealed partial class FitmentPage : Page
{
    public FitmentViewModel ViewModel { get; } = new(App.Services.GetRequiredService<ApiClient>());

    public FitmentPage()
    {
        InitializeComponent();
        DataContext = ViewModel;
        Loaded += async (_, _) => await ViewModel.RefreshCommand.ExecuteAsync(null);
    }
}
