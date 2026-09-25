using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

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
        if (window is null) return;
        var theme = ToElementTheme(preference);

        if (window.Content is FrameworkElement root)
        {
            root.RequestedTheme = theme;
            ApplySolidBackground(root);
        }

        // Ensure nested frames/pages inherit a solid chrome background (avoids blurry grey).
        if (window.Content is Panel panel)
        {
            foreach (var child in panel.Children.OfType<FrameworkElement>())
                ApplySolidBackground(child);
        }
    }

    public static void ApplyFromStore(IAppSettingsStore settings) =>
        Apply(App.MainWindowInstance, settings.Theme);

    public static void ApplySolidBackground(FrameworkElement element)
    {
        if (element is null) return;
        try
        {
            if (Application.Current.Resources.TryGetValue("WorkshopPageBackgroundBrush", out var brushObj) &&
                brushObj is Brush brush)
            {
                if (element is Control control)
                    control.Background = brush;
                else if (element is Panel panel)
                    panel.Background = brush;
                else if (element is Border border)
                    border.Background = brush;
            }
        }
        catch
        {
            /* theme resource may not resolve during early init */
        }
    }
}
