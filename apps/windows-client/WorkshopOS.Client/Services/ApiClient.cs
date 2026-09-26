using System.Text.Json;
using System.Text.Json.Serialization;

namespace WorkshopOS.Client.Services;

public interface IAppSettingsStore
{
    string? ServerUrl { get; set; }
    string? AccessToken { get; set; }
    string? RefreshToken { get; set; }
    /// <summary>App appearance: System, Light, or Dark. Survives ClearConnection.</summary>
    string Theme { get; set; }
    bool HasServerUrl { get; }
    void ClearTokens();
    /// <summary>Clears connection persistence (JSON tokens/URL, WinRT LocalSettings, PasswordVault). Keeps Theme.</summary>
    void ClearConnection();
}

/// <summary>
/// Persists client connection state under %LOCALAPPDATA%\WorkshopOS\client-settings.json
/// so users can reset without digging into WinRT LocalSettings.
/// </summary>
public sealed class AppSettingsStore : IAppSettingsStore
{
    private static readonly object Gate = new();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly string[] CredentialResources =
    [
        "WorkshopOS",
        "WorkshopOS.Client",
        "WorkshopOS.AccessToken",
        "WorkshopOS.RefreshToken",
        "WorkshopOS.ServerUrl"
    ];

    private sealed class SettingsDto
    {
        public string? ServerUrl { get; set; }
        public string? AccessToken { get; set; }
        public string? RefreshToken { get; set; }
        public string Theme { get; set; } = "System";
    }

    public static string SettingsDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WorkshopOS");

    public static string SettingsFilePath => Path.Combine(SettingsDirectory, "client-settings.json");

    private SettingsDto _data;

    public AppSettingsStore()
    {
        // Never resurrect a dead URL from WinRT LocalSettings into JSON.
        // Stale LocalSettings were the 1.2.0→1.2.1 hang vector after "clearing" the folder.
        ClearWinRtLocalSettings();
        ClearPasswordVault();
        _data = Load();
    }

    public string? ServerUrl
    {
        get { lock (Gate) return _data.ServerUrl; }
        set { lock (Gate) { _data.ServerUrl = value; Save(); } }
    }

    public string? AccessToken
    {
        get { lock (Gate) return _data.AccessToken; }
        set { lock (Gate) { _data.AccessToken = value; Save(); } }
    }

    public string? RefreshToken
    {
        get { lock (Gate) return _data.RefreshToken; }
        set { lock (Gate) { _data.RefreshToken = value; Save(); } }
    }

    public string Theme
    {
        get
        {
            lock (Gate)
            {
                var t = _data.Theme;
                return string.IsNullOrWhiteSpace(t) ? "System" : t;
            }
        }
        set
        {
            lock (Gate)
            {
                var v = string.IsNullOrWhiteSpace(value) ? "System" : value.Trim();
                if (v is not ("System" or "Light" or "Dark")) v = "System";
                _data.Theme = v;
                Save();
            }
        }
    }

    public bool HasServerUrl
    {
        get { lock (Gate) return !string.IsNullOrWhiteSpace(_data.ServerUrl); }
    }

    public void ClearTokens()
    {
        lock (Gate)
        {
            _data.AccessToken = null;
            _data.RefreshToken = null;
            Save();
        }
        ClearPasswordVault();
    }

    public void ClearConnection()
    {
        lock (Gate)
        {
            var keepTheme = string.IsNullOrWhiteSpace(_data.Theme) ? "System" : _data.Theme;
            _data = new SettingsDto { Theme = keepTheme };
            try
            {
                if (File.Exists(SettingsFilePath))
                    File.Delete(SettingsFilePath);
                if (Directory.Exists(SettingsDirectory))
                {
                    foreach (var f in Directory.EnumerateFiles(SettingsDirectory, "*", SearchOption.AllDirectories))
                    {
                        try { File.Delete(f); } catch { /* best-effort */ }
                    }
                }
                Save();
            }
            catch
            {
                try { Save(); } catch { /* ignore */ }
            }
        }
        ClearWinRtLocalSettings();
        ClearPasswordVault();
        ClearWinRtApplicationData();
    }

    private static SettingsDto Load()
    {
        try
        {
            if (!File.Exists(SettingsFilePath)) return new SettingsDto();
            var json = File.ReadAllText(SettingsFilePath);
            return JsonSerializer.Deserialize<SettingsDto>(json, JsonOptions) ?? new SettingsDto();
        }
        catch
        {
            return new SettingsDto();
        }
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(SettingsDirectory);
            var json = JsonSerializer.Serialize(_data, JsonOptions);
            var tmp = SettingsFilePath + ".tmp";
            File.WriteAllText(tmp, json);
            File.Copy(tmp, SettingsFilePath, overwrite: true);
            File.Delete(tmp);
        }
        catch
        {
            /* best-effort persistence */
        }
    }

    private static void ClearWinRtLocalSettings()
    {
        try
        {
            var local = Windows.Storage.ApplicationData.Current.LocalSettings.Values;
            local.Remove(nameof(ServerUrl));
            local.Remove(nameof(AccessToken));
            local.Remove(nameof(RefreshToken));
            // Sweep any leftover connection-related keys from older builds.
            var toRemove = new List<string>();
            foreach (var entry in local)
            {
                if (entry.Key is not string s) continue;
                if (s.Contains("WorkshopOS", StringComparison.OrdinalIgnoreCase) ||
                    s.Contains("ServerUrl", StringComparison.OrdinalIgnoreCase) ||
                    s.Contains("AccessToken", StringComparison.OrdinalIgnoreCase) ||
                    s.Contains("RefreshToken", StringComparison.OrdinalIgnoreCase))
                    toRemove.Add(s);
            }
            foreach (var key in toRemove)
                local.Remove(key);
        }
        catch
        {
            /* unpackaged / no WinRT identity — ignore */
        }
    }

    private static void ClearWinRtApplicationData()
    {
        try
        {
            Windows.Storage.ApplicationData.Current.LocalSettings.Values.Clear();
        }
        catch
        {
            /* ignore */
        }
    }

    private static void ClearPasswordVault()
    {
        try
        {
            var vault = new Windows.Security.Credentials.PasswordVault();
            foreach (var resource in CredentialResources)
            {
                try
                {
                    foreach (var cred in vault.FindAllByResource(resource))
                    {
                        try { vault.Remove(cred); } catch { /* ignore */ }
                    }
                }
                catch
                {
                    /* FindAllByResource throws if none */
                }
            }
        }
        catch
        {
            /* PasswordVault unavailable — ignore */
        }
    }
}

public sealed class AuthSession
{
    public WorkshopOS.Contracts.Auth.UserDto? User { get; set; }
    public bool IsAuthenticated => User is not null;
}

public sealed class ApiClient
{
    private readonly IAppSettingsStore _settings;
    private readonly AuthSession _session;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>Default per-request budget for API calls (avoids multi-minute hangs).</summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(15);

    /// <summary>Short budget used during bootstrap so the connect UI appears quickly.</summary>
    public static readonly TimeSpan BootstrapTimeout = TimeSpan.FromSeconds(4);

    public ApiClient(IAppSettingsStore settings, AuthSession session)
    {
        _settings = settings;
        _session = session;
    }

    public bool HasServer => !string.IsNullOrWhiteSpace(_settings.ServerUrl);

    private HttpClient CreateClient(TimeSpan? timeout = null)
    {
        if (string.IsNullOrWhiteSpace(_settings.ServerUrl))
            throw new InvalidOperationException("Server URL is not configured.");
        var http = new HttpClient
        {
            BaseAddress = new Uri(_settings.ServerUrl.TrimEnd('/') + "/"),
            Timeout = timeout ?? DefaultTimeout
        };
        http.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        if (!string.IsNullOrWhiteSpace(_settings.AccessToken))
            http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _settings.AccessToken);
        return http;
    }

    public async Task<T> GetAsync<T>(string path, CancellationToken ct = default, TimeSpan? timeout = null)
    {
        using var http = CreateClient(timeout);
        using var response = await http.GetAsync(path, ct);
        await EnsureSuccess(response);
        return (await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct))!;
    }

    public async Task<string> GetRawAsync(string path, CancellationToken ct = default)
    {
        using var http = CreateClient();
        using var response = await http.GetAsync(path, ct);
        await EnsureSuccess(response);
        return await response.Content.ReadAsStringAsync(ct);
    }

    public async Task<TResponse> PostAsync<TRequest, TResponse>(string path, TRequest body, CancellationToken ct = default)
    {
        using var http = CreateClient();
        using var response = await http.PostAsJsonAsync(path, body, JsonOptions, ct);
        await EnsureSuccess(response);
        return (await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions, ct))!;
    }

    public async Task PostAsync<TRequest>(string path, TRequest body, CancellationToken ct = default)
    {
        using var http = CreateClient();
        using var response = await http.PostAsJsonAsync(path, body, JsonOptions, ct);
        await EnsureSuccess(response);
    }

    public async Task PostAsync(string path, CancellationToken ct = default)
    {
        using var http = CreateClient();
        using var response = await http.PostAsync(path, content: null, ct);
        await EnsureSuccess(response);
    }

    public async Task<TResponse> PostAsync<TResponse>(string path, CancellationToken ct = default)
    {
        using var http = CreateClient();
        using var response = await http.PostAsync(path, content: null, ct);
        await EnsureSuccess(response);
        return (await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions, ct))!;
    }

    public async Task<TResponse> PutAsync<TRequest, TResponse>(string path, TRequest body, CancellationToken ct = default)
    {
        using var http = CreateClient();
        using var response = await http.PutAsJsonAsync(path, body, JsonOptions, ct);
        await EnsureSuccess(response);
        return (await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions, ct))!;
    }

    public async Task DeleteAsync(string path, CancellationToken ct = default)
    {
        using var http = CreateClient();
        using var response = await http.DeleteAsync(path, ct);
        await EnsureSuccess(response);
    }

    public async Task<WorkshopOS.Contracts.Common.HealthDto> HealthAsync(CancellationToken ct = default, TimeSpan? timeout = null) =>
        await GetAsync<WorkshopOS.Contracts.Common.HealthDto>("api/health", ct, timeout);

    public async Task<WorkshopOS.Contracts.Auth.SetupStatusDto> SetupStatusAsync(CancellationToken ct = default, TimeSpan? timeout = null) =>
        await GetAsync<WorkshopOS.Contracts.Auth.SetupStatusDto>("api/setup/status", ct, timeout);

    public Task CompleteSetupAsync(WorkshopOS.Contracts.Auth.SetupRequest request, CancellationToken ct = default) =>
        PostAsync("api/setup", request, ct);

    public async Task<WorkshopOS.Contracts.Auth.AuthResponse> LoginAsync(string email, string password, CancellationToken ct = default)
    {
        var auth = await PostAsync<WorkshopOS.Contracts.Auth.LoginRequest, WorkshopOS.Contracts.Auth.AuthResponse>(
            "api/auth/login", new(email, password), ct);
        _settings.AccessToken = auth.AccessToken;
        _settings.RefreshToken = auth.RefreshToken;
        _session.User = auth.User;
        return auth;
    }

    public async Task LogoutAsync(CancellationToken ct = default)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(_settings.RefreshToken))
                await PostAsync("api/auth/logout", new WorkshopOS.Contracts.Auth.LogoutRequest(_settings.RefreshToken), ct);
        }
        catch { /* still clear local */ }
        _settings.ClearTokens();
        _session.User = null;
    }

    private static async Task EnsureSuccess(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;
        var body = await response.Content.ReadAsStringAsync();
        string message = body;
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("title", out var title))
                message = title.GetString() ?? body;
        }
        catch { /* raw */ }
        throw new InvalidOperationException(message);
    }
}
