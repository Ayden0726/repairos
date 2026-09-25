using System.Collections.ObjectModel;
using System.Net.Http.Json;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WorkshopOS.Client.Services;
using WorkshopOS.Contracts.Common;

namespace WorkshopOS.Client.ViewModels;

public partial class ServerConnectViewModel : ObservableObject
{
    private readonly IAppSettingsStore _settings;
    private readonly ApiClient _api;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    [ObservableProperty] private string _serverUrl = "http://127.0.0.1:5088";
    [ObservableProperty] private string _pairingCode = string.Empty;
    [ObservableProperty] private string? _error;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _statusText;
    public ObservableCollection<string> FoundServers { get; } = new();

    public ServerConnectViewModel(IAppSettingsStore settings, ApiClient api)
    {
        _settings = settings;
        _api = api;
        if (!string.IsNullOrWhiteSpace(settings.ServerUrl))
            ServerUrl = settings.ServerUrl;
    }

    public Action<string>? Navigate { get; set; }

    [RelayCommand]
    private async Task ConnectAsync()
    {
        Error = null;
        IsBusy = true;
        try
        {
            if (!string.IsNullOrWhiteSpace(PairingCode) && string.IsNullOrWhiteSpace(ServerUrl))
            {
                await ConnectWithCodeCoreAsync();
                return;
            }

            if (!string.IsNullOrWhiteSpace(PairingCode))
            {
                // Prefer code match on LAN when both are filled loosely
                var match = await FindByPairingCodeAsync(NormalizeCode(PairingCode));
                if (match is not null)
                    ServerUrl = match;
            }

            await ConnectToUrlAsync(ServerUrl);
        }
        catch (Exception ex)
        {
            Error = $"Unable to connect to WorkshopOS Server. {ex.Message}";
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task ConnectWithCodeAsync()
    {
        Error = null;
        IsBusy = true;
        try
        {
            await ConnectWithCodeCoreAsync();
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task FindOnNetworkAsync()
    {
        Error = null;
        IsBusy = true;
        StatusText = "Scanning this network for WorkshopOS…";
        FoundServers.Clear();
        try
        {
            var found = await ScanLanAsync(null, TimeSpan.FromSeconds(8));
            foreach (var url in found)
                FoundServers.Add(url);

            if (found.Count == 0)
            {
                Error = "No WorkshopOS servers found on this LAN. Open http://<server-ip>:5088/connect on a browser, or enter the URL / pairing code.";
                StatusText = null;
                return;
            }

            if (!string.IsNullOrWhiteSpace(PairingCode))
            {
                var code = NormalizeCode(PairingCode);
                var match = await FindByPairingCodeAsync(code, found);
                if (match is not null)
                {
                    ServerUrl = match;
                    StatusText = $"Matched pairing code on {match}";
                    await ConnectToUrlAsync(match);
                    return;
                }
            }

            ServerUrl = found[0];
            StatusText = found.Count == 1
                ? $"Found {found[0]}"
                : $"Found {found.Count} servers — selected {found[0]}. Pick another URL if needed, then Connect.";
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
        finally { IsBusy = false; }
    }

    private async Task ConnectWithCodeCoreAsync()
    {
        var code = NormalizeCode(PairingCode);
        if (string.IsNullOrWhiteSpace(code))
            throw new InvalidOperationException("Enter the pairing code from http://<server>:5088/connect");

        StatusText = $"Looking for {code} on this network…";
        var match = await FindByPairingCodeAsync(code);
        if (match is null)
            throw new InvalidOperationException($"No server with code {code} on this LAN. Same Wi‑Fi? Or paste the server URL.");

        ServerUrl = match;
        await ConnectToUrlAsync(match);
    }

    private async Task ConnectToUrlAsync(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new InvalidOperationException("Server URL is empty.");

        _settings.ServerUrl = url.Trim().TrimEnd('/');
        ServerUrl = _settings.ServerUrl;
        var health = await _api.HealthAsync();
        if (!health.Database)
            throw new InvalidOperationException("API reachable but database is offline.");

        StatusText = $"Connected · API {health.ApiVersion}";
        var setup = await _api.SetupStatusAsync();
        Navigate?.Invoke(setup.IsComplete ? "login" : "setup");
    }

    private async Task<string?> FindByPairingCodeAsync(string code, IReadOnlyList<string>? candidates = null)
    {
        var urls = candidates ?? await ScanLanAsync(code, TimeSpan.FromSeconds(10));
        foreach (var url in urls)
        {
            var d = await TryDiscoveryAsync(url);
            if (d is not null && string.Equals(NormalizeCode(d.PairingCode), code, StringComparison.OrdinalIgnoreCase))
                return url;
        }
        return null;
    }

    private static async Task<IReadOnlyList<string>> ScanLanAsync(string? requiredCode, TimeSpan budget)
    {
        using var cts = new CancellationTokenSource(budget);
        var targets = BuildProbeUrls();
        var bag = new System.Collections.Concurrent.ConcurrentBag<string>();

        try
        {
            await Parallel.ForEachAsync(targets, new ParallelOptions
            {
                MaxDegreeOfParallelism = 48,
                CancellationToken = cts.Token
            }, async (url, token) =>
            {
                var d = await TryDiscoveryAsync(url, token);
                if (d is null) return;
                if (requiredCode is not null &&
                    !string.Equals(NormalizeCode(d.PairingCode), NormalizeCode(requiredCode), StringComparison.OrdinalIgnoreCase))
                    return;
                bag.Add(url);
            });
        }
        catch (OperationCanceledException)
        {
            /* budget elapsed — return whatever we found */
        }

        return bag.OrderBy(u => u.Contains("127.0.0.1") ? 0 : 1).ThenBy(u => u).ToList();
    }

    private static async Task<DiscoveryDto?> TryDiscoveryAsync(string baseUrl, CancellationToken ct = default)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromMilliseconds(450) };
            using var response = await http.GetAsync(baseUrl.TrimEnd('/') + "/api/discovery", ct);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<DiscoveryDto>(JsonOptions, ct);
        }
        catch
        {
            return null;
        }
    }

    private static List<string> BuildProbeUrls()
    {
        const int port = 5088;
        var urls = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            $"http://127.0.0.1:{port}",
            $"http://localhost:{port}"
        };

        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                if (ni.NetworkInterfaceType is NetworkInterfaceType.Loopback) continue;
                foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                {
                    if (ua.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                    var ip = ua.Address.GetAddressBytes();
                    if (ip[0] == 127) continue;
                    // Probe this host + common gateway + a slice of the /24 (fast enough for a shop LAN)
                    urls.Add($"http://{ua.Address}:{port}");
                    urls.Add($"http://{ip[0]}.{ip[1]}.{ip[2]}.1:{port}");
                    urls.Add($"http://{ip[0]}.{ip[1]}.{ip[2]}.254:{port}");
                    for (var host = 2; host <= 40; host++)
                        urls.Add($"http://{ip[0]}.{ip[1]}.{ip[2]}.{host}:{port}");
                    for (var host = 100; host <= 120; host++)
                        urls.Add($"http://{ip[0]}.{ip[1]}.{ip[2]}.{host}:{port}");
                }
            }
        }
        catch
        {
            /* keep localhost probes */
        }

        return urls.ToList();
    }

    private static string NormalizeCode(string? code) =>
        (code ?? "").Trim().ToUpperInvariant().Replace(" ", "");
}

public partial class SetupViewModel : ObservableObject
{
    private readonly ApiClient _api;

    [ObservableProperty] private string _businessName = string.Empty;
    [ObservableProperty] private string _phone = string.Empty;
    [ObservableProperty] private string _email = string.Empty;
    [ObservableProperty] private string _abn = string.Empty;
    [ObservableProperty] private string _address = string.Empty;
    [ObservableProperty] private string _suburb = string.Empty;
    [ObservableProperty] private string _state = "VIC";
    [ObservableProperty] private string _postcode = string.Empty;
    [ObservableProperty] private string _website = string.Empty;
    [ObservableProperty] private bool _gstRegistered = true;
    [ObservableProperty] private string _ownerName = string.Empty;
    [ObservableProperty] private string _ownerEmail = string.Empty;
    [ObservableProperty] private string _ownerPassword = string.Empty;
    [ObservableProperty] private string? _error;
    [ObservableProperty] private bool _isBusy;

    public SetupViewModel(ApiClient api) => _api = api;
    public Action? OnCompleted { get; set; }

    [RelayCommand]
    private async Task CompleteAsync()
    {
        Error = null;
        IsBusy = true;
        try
        {
            await _api.CompleteSetupAsync(new(
                BusinessName, Phone, Email, Abn, Address, Suburb, State, Postcode, Website,
                GstRegistered, 0.10m, "AUD", 110m, OwnerName, OwnerEmail, OwnerPassword, "#0F766E"));
            OnCompleted?.Invoke();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsBusy = false; }
    }
}

public partial class LoginViewModel : ObservableObject
{
    private readonly ApiClient _api;

    [ObservableProperty] private string _email = string.Empty;
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private string? _error;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _hint = "Sign in with your WorkshopOS account.";

    public LoginViewModel(ApiClient api) => _api = api;
    public Action? OnLoggedIn { get; set; }

    [RelayCommand]
    private async Task LoginAsync()
    {
        Error = null;
        IsBusy = true;
        try
        {
            await _api.LoginAsync(Email.Trim(), Password);
            OnLoggedIn?.Invoke();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsBusy = false; }
    }
}

public partial class ShellViewModel : ObservableObject
{
    private readonly ApiClient _api;
    private readonly AuthSession _session;
    private readonly IAppSettingsStore _settings;

    [ObservableProperty] private string _productName = "WorkshopOS";
    [ObservableProperty] private string _businessName = string.Empty;
    [ObservableProperty] private string _userDisplay = string.Empty;
    [ObservableProperty] private string _currentPageTitle = "Home";
    [ObservableProperty] private string _searchQuery = string.Empty;
    [ObservableProperty] private string? _searchStatus;
    [ObservableProperty] private IReadOnlyList<ModuleDto> _modules = Array.Empty<ModuleDto>();
    [ObservableProperty] private bool _isOffline;

    /// <summary>Primary destinations. AI/Knowledge/Backups/Users stay out of the sidebar.</summary>
    public static readonly (string Title, string[] Keys)[] NavGroups =
    [
        ("Dashboard", ["dashboard"]),
        ("Work", ["repairs", "quotes", "invoices", "calendar", "builds"]),
        ("Inventory", ["inventory", "purchasing", "used"]),
        ("Customers", ["customers"]),
        ("Reports", ["reports"])
    ];

    public ShellViewModel(ApiClient api, AuthSession session, IAppSettingsStore settings)
    {
        _api = api;
        _session = session;
        _settings = settings;
        if (session.User is not null)
        {
            UserDisplay = session.User.DisplayName;
            BusinessName = session.User.Business?.Name ?? string.Empty;
        }
    }

    public Action<string, int, bool>? OpenModule { get; set; }
    public Action? LoggedOut { get; set; }

    [RelayCommand]
    private async Task LoadAsync()
    {
        try
        {
            Modules = await _api.GetAsync<IReadOnlyList<ModuleDto>>("api/settings/modules");
            IsOffline = false;
        }
        catch
        {
            IsOffline = true;
        }
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchQuery))
        {
            SearchStatus = null;
            return;
        }
        try
        {
            var result = await _api.GetAsync<SearchResponse>($"api/search?q={Uri.EscapeDataString(SearchQuery)}");
            var total = result.Groups.Sum(g => g.Hits.Count);
            SearchStatus = total == 0
                ? $"No results for “{result.Query}”."
                : $"{total} result(s)";
        }
        catch (Exception ex)
        {
            SearchStatus = ex.Message;
        }
    }

    public void NavigateByKey(string key)
    {
        var module = Modules.FirstOrDefault(m => m.Key == key);
        if (module is not null)
        {
            CurrentPageTitle = module.Title;
            OpenModule?.Invoke(module.Key, module.Phase, module.Implemented);
            return;
        }

        // Offline / modules not loaded — still open known implemented destinations.
        CurrentPageTitle = key;
        OpenModule?.Invoke(key, 1, true);
    }

    [RelayCommand]
    private void NavigateModule(ModuleDto? module)
    {
        if (module is null) return;
        CurrentPageTitle = module.Title;
        OpenModule?.Invoke(module.Key, module.Phase, module.Implemented);
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        await _api.LogoutAsync();
        LoggedOut?.Invoke();
    }
}
