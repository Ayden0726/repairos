using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WorkshopOS.Client.Services;
using WorkshopOS.Contracts.Common;

namespace WorkshopOS.Client.ViewModels;

public partial class ServerConnectViewModel : ObservableObject
{
    private readonly IAppSettingsStore _settings;
    private readonly ApiClient _api;

    [ObservableProperty] private string _serverUrl = "http://127.0.0.1:5088";
    [ObservableProperty] private string? _error;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _statusText;

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
            _settings.ServerUrl = ServerUrl.Trim().TrimEnd('/');
            var health = await _api.HealthAsync();
            if (!health.Database)
            {
                Error = "API reachable but database is offline.";
                return;
            }
            StatusText = $"Connected · API {health.ApiVersion}";
            var setup = await _api.SetupStatusAsync();
            Navigate?.Invoke(setup.IsComplete ? "login" : "setup");
        }
        catch (Exception ex)
        {
            Error = $"Unable to connect to WorkshopOS Server. {ex.Message}";
        }
        finally { IsBusy = false; }
    }
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
                ? $"No results yet for “{result.Query}”. Full search lands with repairs/customers (Phase 2)."
                : $"{total} result(s)";
        }
        catch (Exception ex)
        {
            SearchStatus = ex.Message;
        }
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
