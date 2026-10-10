using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using WorkshopOS.Client.Services;
using WorkshopOS.Client.ViewModels;
using WorkshopOS.Contracts.Auth;

namespace WorkshopOS.Client.Views;

public sealed partial class LoginPage : Page
{
    public LoginViewModel ViewModel { get; }

    public LoginPage()
    {
        ViewModel = App.Services.GetRequiredService<LoginViewModel>();
        InitializeComponent();
        DataContext = ViewModel;
        ViewModel.OnLoggedIn = ContinueAfterLoginAsync;
        Loaded += (_, _) =>
        {
            SyncThemeToggle();
            if (ViewModel.HasPrefillEmail)
                PasswordBox.Focus(FocusState.Programmatic);
            else
                EmailBox.Focus(FocusState.Programmatic);
        };
    }

    private async Task ContinueAfterLoginAsync()
    {
        var session = App.Services.GetRequiredService<AuthSession>();
        if (session.User?.MustResetPassword == true)
        {
            var changed = await PromptForcedPasswordChangeAsync();
            if (!changed)
            {
                try { await App.Services.GetRequiredService<ApiClient>().LogoutAsync(); }
                catch { /* local clear still happens */ }
                ViewModel.Error = "You must set a new password before continuing.";
                return;
            }
        }
        Frame.Navigate(typeof(ShellPage));
    }

    private async Task<bool> PromptForcedPasswordChangeAsync()
    {
        var currentBox = new PasswordBox { Header = "Temporary / current password", Width = 320 };
        var newBox = new PasswordBox { Header = "New password (min 10, upper/lower/digit)", Width = 320 };
        var confirmBox = new PasswordBox { Header = "Confirm new password", Width = 320 };
        var errorText = new TextBlock
        {
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.DarkRed),
            TextWrapping = TextWrapping.Wrap
        };
        var panel = new StackPanel { Spacing = 12 };
        panel.Children.Add(new TextBlock
        {
            Text = "Your account requires a new password before you can use WorkshopOS.",
            TextWrapping = TextWrapping.Wrap
        });
        panel.Children.Add(currentBox);
        panel.Children.Add(newBox);
        panel.Children.Add(confirmBox);
        panel.Children.Add(errorText);

        var dialog = new ContentDialog
        {
            Title = "Change password",
            Content = panel,
            PrimaryButtonText = "Save & continue",
            CloseButtonText = "Sign out",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };

        while (true)
        {
            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
                return false;

            var current = currentBox.Password ?? string.Empty;
            var pwd = newBox.Password ?? string.Empty;
            var confirm = confirmBox.Password ?? string.Empty;
            if (!string.Equals(pwd, confirm, StringComparison.Ordinal))
            {
                errorText.Text = "New password and confirmation do not match.";
                continue;
            }

            try
            {
                await App.Services.GetRequiredService<ApiClient>().PutAsync(
                    "api/auth/password",
                    new ChangePasswordRequest(current, pwd));
                var session = App.Services.GetRequiredService<AuthSession>();
                if (session.User is not null)
                    session.User = session.User with { MustResetPassword = false };
                return true;
            }
            catch (Exception ex)
            {
                errorText.Text = ex.Message;
            }
        }
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
