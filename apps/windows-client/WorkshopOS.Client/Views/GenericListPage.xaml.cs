using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using WorkshopOS.Client.Services;

namespace WorkshopOS.Client.Views;

public sealed record GenericListArgs(string Title, string ApiPath);

public sealed partial class GenericListPage : Page
{
    public GenericListViewModel ViewModel { get; } = new(App.Services.GetRequiredService<ApiClient>());

    public GenericListPage()
    {
        InitializeComponent();
        DataContext = ViewModel;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        if (e.Parameter is GenericListArgs args)
        {
            ViewModel.Configure(args.Title, args.ApiPath);
            await ViewModel.RefreshCommand.ExecuteAsync(null);
        }
    }
}
