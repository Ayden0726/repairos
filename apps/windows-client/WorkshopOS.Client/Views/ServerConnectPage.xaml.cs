using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using WorkshopOS.Client.Services;
using WorkshopOS.Client.ViewModels;

namespace WorkshopOS.Client.Views;

public sealed partial class ServerConnectPage : Page
{
    public ServerConnectViewModel ViewModel { get; }

    public ServerConnectPage()
    {
        ViewModel = App.Services.GetRequiredService<ServerConnectViewModel>();
        InitializeComponent();
        DataContext = ViewModel;
        ViewModel.Navigate = route => Frame.Navigate(route == "setup" ? typeof(SetupPage) : typeof(LoginPage));
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is string message && !string.IsNullOrWhiteSpace(message))
            ViewModel.Error = message;
    }

    private void FoundServer_Click(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is string url)
        {
            ViewModel.ServerUrl = url;
            ViewModel.ConnectCommand.Execute(null);
        }
    }

    private void ClearSaved_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var settings = App.Services.GetRequiredService<IAppSettingsStore>();
        settings.ClearConnection();
        ViewModel.ServerUrl = "http://127.0.0.1:5088";
        ViewModel.PairingCode = string.Empty;
        ViewModel.Error = null;
        ViewModel.StatusText = "Cleared all saved server data. Enter a pairing code or URL.";
    }
}
