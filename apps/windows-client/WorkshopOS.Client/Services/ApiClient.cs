using System.Text.Json;
using Windows.Storage;

namespace WorkshopOS.Client.Services;

public interface IAppSettingsStore
{
    string? ServerUrl { get; set; }
    string? AccessToken { get; set; }
    string? RefreshToken { get; set; }
    void ClearTokens();
}

public sealed class AppSettingsStore : IAppSettingsStore
{
    private static ApplicationDataContainer Local => ApplicationData.Current.LocalSettings;

    public string? ServerUrl
    {
        get => Local.Values[nameof(ServerUrl)] as string;
        set => Local.Values[nameof(ServerUrl)] = value;
    }

    public string? AccessToken
    {
        get => Local.Values[nameof(AccessToken)] as string;
        set => Local.Values[nameof(AccessToken)] = value;
    }

    public string? RefreshToken
    {
        get => Local.Values[nameof(RefreshToken)] as string;
        set => Local.Values[nameof(RefreshToken)] = value;
    }

    public void ClearTokens()
    {
        Local.Values.Remove(nameof(AccessToken));
        Local.Values.Remove(nameof(RefreshToken));
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

    public ApiClient(IAppSettingsStore settings, AuthSession session)
    {
        _settings = settings;
        _session = session;
    }

    public bool HasServer => !string.IsNullOrWhiteSpace(_settings.ServerUrl);

    private HttpClient CreateClient()
    {
        if (string.IsNullOrWhiteSpace(_settings.ServerUrl))
            throw new InvalidOperationException("Server URL is not configured.");
        var http = new HttpClient { BaseAddress = new Uri(_settings.ServerUrl.TrimEnd('/') + "/") };
        http.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        if (!string.IsNullOrWhiteSpace(_settings.AccessToken))
            http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _settings.AccessToken);
        return http;
    }

    public async Task<T> GetAsync<T>(string path, CancellationToken ct = default)
    {
        using var http = CreateClient();
        using var response = await http.GetAsync(path, ct);
        await EnsureSuccess(response);
        return (await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct))!;
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

    public async Task<WorkshopOS.Contracts.Common.HealthDto> HealthAsync(CancellationToken ct = default) =>
        await GetAsync<WorkshopOS.Contracts.Common.HealthDto>("api/health", ct);

    public async Task<WorkshopOS.Contracts.Auth.SetupStatusDto> SetupStatusAsync(CancellationToken ct = default) =>
        await GetAsync<WorkshopOS.Contracts.Auth.SetupStatusDto>("api/setup/status", ct);

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
