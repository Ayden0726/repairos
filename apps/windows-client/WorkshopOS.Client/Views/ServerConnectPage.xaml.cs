using Microsoft.UI.Xaml.Controls;
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
}
