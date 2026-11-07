using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WorkshopOS.Client.Services;

namespace WorkshopOS.Client.Views;

public sealed partial class StocktakePage : Page
{
    public StocktakeViewModel ViewModel { get; } = new(App.Services.GetRequiredService<ApiClient>());

    public StocktakePage()
    {
        InitializeComponent();
        DataContext = ViewModel;
        Loaded += async (_, _) => await ViewModel.RefreshCommand.ExecuteAsync(null);
    }

    private async void SaveCount_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: StocktakeLineRowVm row })
            await ViewModel.SaveCountCommand.ExecuteAsync(row);
    }
}
