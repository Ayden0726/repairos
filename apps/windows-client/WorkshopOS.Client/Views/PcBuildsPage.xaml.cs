using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WorkshopOS.Client.Services;

namespace WorkshopOS.Client.Views;

public sealed partial class PcBuildsPage : Page
{
    public PcBuildsListViewModel ViewModel { get; }

    public PcBuildsPage()
    {
        ViewModel = new PcBuildsListViewModel(App.Services.GetRequiredService<ApiClient>());
        InitializeComponent();
        DataContext = ViewModel;
        Loaded += async (_, _) => await ViewModel.RefreshCommand.ExecuteAsync(null);
    }

    private void NewBuild_Click(object sender, RoutedEventArgs e) =>
        Frame.Navigate(typeof(PcBuildBuilderPage), new PcBuildBuilderArgs());

    private void Build_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is PcBuildRowVm row)
            Frame.Navigate(typeof(PcBuildBuilderPage), new PcBuildBuilderArgs(row.Id));
    }
}
