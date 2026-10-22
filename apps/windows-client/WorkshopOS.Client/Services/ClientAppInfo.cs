using System.Reflection;

namespace WorkshopOS.Client.Services;

/// <summary>
/// Display version for connect / settings banners. Always follows the
/// assembly InformationalVersion (set from WorkshopOS.Client.csproj / build -p:Version).
/// </summary>
public static class ClientAppInfo
{
    public static string Version { get; } = ReadVersion();

    public static string BannerText => $"Client {Version}";

    private static string ReadVersion()
    {
        var asm = typeof(ClientAppInfo).Assembly;
        var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(info))
        {
            // Strip optional "+metadata" / git hash suffixes from some SDK layouts
            var plus = info.IndexOf('+');
            return plus >= 0 ? info[..plus] : info.Trim();
        }

        var v = asm.GetName().Version;
        return v is null ? "0.0.0" : $"{v.Major}.{v.Minor}.{v.Build}";
    }
}
