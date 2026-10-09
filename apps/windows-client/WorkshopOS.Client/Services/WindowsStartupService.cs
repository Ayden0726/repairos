using Microsoft.Win32;

namespace WorkshopOS.Client.Services;

/// <summary>
/// Registers WorkshopOS in the current-user Run key so it launches at Windows sign-in.
/// Unpackaged clients only — uses HKCU\Software\Microsoft\Windows\CurrentVersion\Run.
/// </summary>
public static class WindowsStartupService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "WorkshopOS";

    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            return key?.GetValue(ValueName) is string s && !string.IsNullOrWhiteSpace(s);
        }
        catch
        {
            return false;
        }
    }

    public static string? ResolveExecutablePath()
    {
        try
        {
            var path = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                return path;
        }
        catch { /* ignore */ }

        try
        {
            var path = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                return path;
        }
        catch { /* ignore */ }

        return null;
    }

    /// <summary>Enable or disable start-with-Windows. Returns null on success, else an error message.</summary>
    public static string? SetEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
                ?? Registry.CurrentUser.CreateSubKey(RunKeyPath)
                ?? throw new InvalidOperationException("Could not open the Windows Run registry key.");

            if (!enabled)
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
                return null;
            }

            var exe = ResolveExecutablePath();
            if (string.IsNullOrWhiteSpace(exe))
                return "Could not resolve WorkshopOS.Client.exe path for startup.";

            key.SetValue(ValueName, $"\"{exe}\"");
            return null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    /// <summary>Re-apply the saved preference (e.g. after an update moved the exe).</summary>
    public static void SyncFromPreference(bool startWithWindows)
    {
        if (!startWithWindows)
        {
            if (IsEnabled()) SetEnabled(false);
            return;
        }

        var exe = ResolveExecutablePath();
        if (string.IsNullOrWhiteSpace(exe)) return;

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            var current = key?.GetValue(ValueName) as string;
            var expected = $"\"{exe}\"";
            if (string.Equals(current, expected, StringComparison.OrdinalIgnoreCase))
                return;
        }
        catch { /* fall through and set */ }

        SetEnabled(true);
    }
}
