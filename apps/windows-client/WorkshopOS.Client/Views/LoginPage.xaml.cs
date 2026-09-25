using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WorkshopOS.Client.Services;
using WorkshopOS.Client.ViewModels;

namespace WorkshopOS.Client.Views;

public sealed partial class LoginPage : Page
{
    public LoginViewModel ViewModel { get; }

    public LoginPage()
    {
        ViewModel = App.Services.GetRequiredService<LoginViewModel>();
        InitializeComponent();
        DataContext = ViewModel;
        ViewModel.OnLoggedIn = () => Frame.Navigate(typeof(ShellPage));
    }

    private void Password_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is PasswordBox box)
            ViewModel.Password = box.Password;
    }

    private void ChangeServer_Click(object sender, RoutedEventArgs e)
    {
        App.Services.GetRequiredService<IAppSettingsStore>().ClearConnection();
        if (App.MainWindowInstance is MainWindow window)
        {
            window.AppRootFrame.Navigate(typeof(ServerConnectPage), "Choose a different WorkshopOS server.");
            return;
        }

        Frame.Navigate(typeof(ServerConnectPage), "Choose a different WorkshopOS server.");
    }
}
