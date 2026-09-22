using Microsoft.UI.Xaml.Controls;
using WorkshopOS.Client.Services;

namespace WorkshopOS.Client.Views;

public sealed partial class DashboardPage : Page
{
    public DashboardViewModel ViewModel { get; }

    public DashboardPage()
    {
        ViewModel = new DashboardViewModel(App.Services.GetRequiredService<ApiClient>());
        InitializeComponent();
        DataContext = ViewModel;
        Loaded += async (_, _) => await ViewModel.RefreshCommand.ExecuteAsync(null);
    }

    private void Card_Click(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is DashboardCardVm card && !string.IsNullOrEmpty(card.Filter))
            Frame.Navigate(typeof(RepairsPage), card.Filter);
    }
}
