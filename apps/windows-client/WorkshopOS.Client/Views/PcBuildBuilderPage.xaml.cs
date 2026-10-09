using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using WorkshopOS.Client.Services;

namespace WorkshopOS.Client.Views;

public sealed record PcBuildBuilderArgs(Guid? BuildId = null);

public sealed partial class PcBuildBuilderPage : Page
{
    public PcBuildBuilderViewModel ViewModel { get; }

    public PcBuildBuilderPage()
    {
        ViewModel = new PcBuildBuilderViewModel(App.Services.GetRequiredService<ApiClient>());
        InitializeComponent();
        DataContext = ViewModel;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        Guid? id = e.Parameter is PcBuildBuilderArgs args ? args.BuildId : null;
        await ViewModel.InitializeAsync(id);
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (Frame.CanGoBack) Frame.GoBack();
        else Frame.Navigate(typeof(PcBuildsPage));
    }

    private void SlotField_Changed(object sender, RoutedEventArgs e) =>
        ViewModel.RecalcSummaryCommand.Execute(null);

    private void ClearSlot_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: PcBuildSlotLine line })
            ViewModel.RemoveSlotCommand.Execute(line);
    }

    private async void Complete_Click(object sender, RoutedEventArgs e) =>
        await ViewModel.SetStatusCommand.ExecuteAsync("Completed");

    private async void Sold_Click(object sender, RoutedEventArgs e) =>
        await ViewModel.SetStatusCommand.ExecuteAsync("Sold");
}
