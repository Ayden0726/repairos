using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace WorkshopOS.Client.Services;

public static class ThemeService
{
    // Solid Workshop palette — never rely on Application.Current ThemeDictionary
    // "Default" lookup (that was light-on-light when RequestedTheme was Dark).
    private static readonly Color DarkPage = Color.FromArgb(255, 0x0F, 0x14, 0x19);
    private static readonly Color LightPage = Color.FromArgb(255, 0xF3, 0xF5, 0xF7);
    private static readonly Color DarkChrome = Color.FromArgb(255, 0x0B, 0x10, 0x15);
    private static readonly Color LightChrome = Color.FromArgb(255, 0xE8, 0xEE, 0xF1);

    public static ElementTheme ToElementTheme(string? preference) =>
        preference switch
        {
            "Light" => ElementTheme.Light,
            "Dark" => ElementTheme.Dark,
            "System" => ElementTheme.Default,
            _ => ElementTheme.Dark
        };

    public static string FromElementTheme(ElementTheme theme) =>
        theme switch
        {
            ElementTheme.Light => "Light",
            ElementTheme.Dark => "Dark",
            _ => "System"
        };

    public static bool IsDarkPreference(string? preference) =>
        preference switch
        {
            "Light" => false,
            "Dark" => true,
            "System" =>
                Application.Current?.RequestedTheme == ApplicationTheme.Dark,
            _ => true
        };

    public static void Apply(Window? window, string? preference)
    {
        if (window is null) return;
        var theme = ToElementTheme(preference);
        var dark = IsDarkPreference(preference);

        if (window.Content is FrameworkElement root)
        {
            root.RequestedTheme = theme;
            ApplySolidBackground(root, dark);
        }

        if (window.Content is Panel panel)
        {
            foreach (var child in panel.Children.OfType<FrameworkElement>())
                ApplySolidBackground(child, dark);
        }
    }

    public static void ApplyFromStore(IAppSettingsStore settings) =>
        Apply(App.MainWindowInstance, settings.Theme);

    public static void SetPreference(string theme)
    {
        var settings = App.Services.GetRequiredService<IAppSettingsStore>();
        settings.Theme = theme;
        ApplyFromStore(settings);
    }

    /// <summary>Compact Light/Dark switch used on pre-shell pages and shell chrome.</summary>
    public static void SetLightOrDark(bool dark) =>
        SetPreference(dark ? "Dark" : "Light");

    public static void ApplySolidBackground(FrameworkElement element) =>
        ApplySolidBackground(element, IsDarkPreference(CurrentTheme()));

    public static void ApplySolidBackground(FrameworkElement element, bool dark)
    {
        if (element is null) return;
        try
        {
            var brush = new SolidColorBrush(dark ? DarkPage : LightPage);
            if (element is Control control)
                control.Background = brush;
            else if (element is Panel panel)
                panel.Background = brush;
            else if (element is Border border)
                border.Background = brush;
        }
        catch
        {
            /* early init */
        }
    }

    public static Brush PageBrush(bool dark) =>
        new SolidColorBrush(dark ? DarkPage : LightPage);

    public static Brush ChromeBrush(bool dark) =>
        new SolidColorBrush(dark ? DarkChrome : LightChrome);

    private static string CurrentTheme()
    {
        try
        {
            return App.Services.GetRequiredService<IAppSettingsStore>().Theme;
        }
        catch
        {
            return "Dark";
        }
    }
}
