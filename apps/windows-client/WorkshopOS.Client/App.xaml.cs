using Microsoft.UI.Xaml;
using Microsoft.Extensions.DependencyInjection;
using WorkshopOS.Client.Services;
using WorkshopOS.Client.ViewModels;
using WorkshopOS.Client.Views;

namespace WorkshopOS.Client;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;
    private Window? _window;

    public App()
    {
        InitializeComponent();
        var sc = new ServiceCollection();
        sc.AddSingleton<IAppSettingsStore, AppSettingsStore>();
        sc.AddSingleton<AuthSession>();
        sc.AddSingleton<ApiClient>();
        sc.AddTransient<ServerConnectViewModel>();
        sc.AddTransient<SetupViewModel>();
        sc.AddTransient<LoginViewModel>();
        sc.AddTransient<ShellViewModel>();
        sc.AddTransient<UsersViewModel>();
        sc.AddTransient<ServerConnectPage>();
        sc.AddTransient<SetupPage>();
        sc.AddTransient<LoginPage>();
        sc.AddTransient<ShellPage>();
        sc.AddTransient<PlaceholderPage>();
        sc.AddTransient<SettingsPage>();
        sc.AddTransient<UsersPage>();
        Services = sc.BuildServiceProvider();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        ThemeService.Apply(_window, Services.GetRequiredService<IAppSettingsStore>().Theme);
        _window.Activate();
    }

    public static Window? MainWindowInstance => ((App)Current)._window;
}
