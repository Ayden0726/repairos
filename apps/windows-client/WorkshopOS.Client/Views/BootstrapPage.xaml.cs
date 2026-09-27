using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WorkshopOS.Client.Services;

namespace WorkshopOS.Client.Views;

/// <summary>
/// Legacy splash. MainWindow no longer navigates here (1.2.4+).
/// If reached somehow, immediately leave for ServerConnect — never probe/block.
/// </summary>
public sealed partial class BootstrapPage : Page
{
    private bool _navigated;

    public BootstrapPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        SyncThemeToggle();
        GoConnect("Enter a pairing code from http://<server>:5088/connect, or paste the server URL.");
    }

    private void EnterServer_Click(object sender, RoutedEventArgs e) =>
        GoConnect("Enter the pairing code from http://<server>:5088/connect, or paste the server URL.");

    private void GoConnect(string? message)
    {
        if (_navigated) return;
        _navigated = true;
        try
        {
            Frame.Navigate(typeof(ServerConnectPage), message);
        }
        catch
        {
            /* frame may already be navigating */
        }
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
