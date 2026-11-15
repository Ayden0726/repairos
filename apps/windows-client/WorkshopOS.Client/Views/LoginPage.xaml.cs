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
    private bool _pinMode = true;

    public LoginPage()
    {
        ViewModel = App.Services.GetRequiredService<LoginViewModel>();
        InitializeComponent();
        DataContext = ViewModel;
        ViewModel.OnLoggedIn = () => Frame.Navigate(typeof(ShellPage));
        Loaded += (_, _) =>
        {
            SyncThemeToggle();
            ApplyModeUi();
            RefreshPinDots();
        };
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

    private void ModePin_Click(object sender, RoutedEventArgs e)
    {
        _pinMode = true;
        ViewModel.UsePinMode();
        ApplyModeUi();
    }

    private void ModePassword_Click(object sender, RoutedEventArgs e)
    {
        _pinMode = false;
        ViewModel.UsePasswordMode();
        ApplyModeUi();
        if (ViewModel.HasPrefillEmail)
            PasswordBox.Focus(FocusState.Programmatic);
        else
            EmailBox.Focus(FocusState.Programmatic);
    }

    private void ApplyModeUi()
    {
        PinPanel.Visibility = _pinMode ? Visibility.Visible : Visibility.Collapsed;
        PasswordPanel.Visibility = _pinMode ? Visibility.Collapsed : Visibility.Visible;
        var accent = (Style)Application.Current.Resources["AccentButtonStyle"];
        PinModeBtn.Style = _pinMode ? accent : null;
        PasswordModeBtn.Style = _pinMode ? null : accent;
    }

    private void PinDigit_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string digit })
        {
            ViewModel.AppendPinDigit(digit);
            RefreshPinDots();
        }
    }

    private void PinClear_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.ClearPin();
        RefreshPinDots();
    }

    private async void PinGo_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.LoginWithPinCommand.ExecuteAsync(null);
        RefreshPinDots();
    }

    private void RefreshPinDots()
    {
        var n = ViewModel.Pin.Length;
        PinDots.Text = n == 0 ? "····" : new string('•', Math.Max(4, n));
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
