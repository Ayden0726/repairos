using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using WorkshopOS.Client.Services;
using WorkshopOS.Contracts.Workshop;

namespace WorkshopOS.Client.Views;

public sealed partial class RepairsPage : Page
{
    public RepairsViewModel ViewModel { get; }
    private bool _ready;

    public RepairsPage()
    {
        ViewModel = new RepairsViewModel(App.Services.GetRequiredService<ApiClient>());
        InitializeComponent();
        DataContext = ViewModel;
        Loaded += async (_, _) =>
        {
            await ViewModel.RefreshCommand.ExecuteAsync(null);
            _ready = true;
        };
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is string filter)
            ViewModel.ApplyIncomingFilter(filter);
    }

    private void Repair_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is RepairListItemDto r)
            Frame.Navigate(typeof(RepairDetailPage), r.Id);
    }

    private void NewRepair_Click(object sender, RoutedEventArgs e) => Frame.Navigate(typeof(NewRepairPage));

    private async void Filter_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready) return;
        await ViewModel.RefreshCommand.ExecuteAsync(null);
    }

    private async void Overdue_Changed(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        await ViewModel.RefreshCommand.ExecuteAsync(null);
    }

    private void Query_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
            ViewModel.RefreshCommand.Execute(null);
    }
}
