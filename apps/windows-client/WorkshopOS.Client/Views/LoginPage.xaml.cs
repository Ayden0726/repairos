using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;
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
        Loaded += (_, _) => SyncThemeToggle();
    }

    private void Password_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is PasswordBox box)
            ViewModel.Password = box.Password;
    }

    private void LoginField_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter) return;
        if (ViewModel.IsBusy) return;
        if (!ViewModel.LoginCommand.CanExecute(null)) return;
        ViewModel.LoginCommand.Execute(null);
        e.Handled = true;
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

    private void ThemeLight_Click(object sender, RoutedEventArgs e)
    {
        ThemeService.SetLightOrDark(false);
        SyncThemeToggle();
    }

    private void ThemeDark_Click(object sender, RoutedEventArgs e)
    {
        ThemeService.SetLightOrDark(true);
        SyncThemeToggle();
    }

    private void SyncThemeToggle()
    {
        var dark = ThemeService.IsDarkPreference(App.Services.GetRequiredService<IAppSettingsStore>().Theme);
        ThemeDarkBtn.Style = dark ? (Style)Application.Current.Resources["AccentButtonStyle"] : null;
        ThemeLightBtn.Style = dark ? null : (Style)Application.Current.Resources["AccentButtonStyle"];
    }
}
