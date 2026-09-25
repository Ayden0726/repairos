using Microsoft.UI.Xaml;

namespace WorkshopOS.Client.Services;

public static class ThemeService
{
    public static ElementTheme ToElementTheme(string? preference) =>
        preference switch
        {
            "Light" => ElementTheme.Light,
            "Dark" => ElementTheme.Dark,
            _ => ElementTheme.Default
        };

    public static string FromElementTheme(ElementTheme theme) =>
        theme switch
        {
            ElementTheme.Light => "Light",
            ElementTheme.Dark => "Dark",
            _ => "System"
        };

    public static void Apply(Window? window, string? preference)
    {
        if (window?.Content is FrameworkElement root)
            root.RequestedTheme = ToElementTheme(preference);
    }

    public static void ApplyFromStore(IAppSettingsStore settings) =>
        Apply(App.MainWindowInstance, settings.Theme);
}
