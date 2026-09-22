using Microsoft.UI.Xaml.Controls;
using WorkshopOS.Client.Services;

namespace WorkshopOS.Client.Views;

public sealed partial class BootstrapPage : Page
{
    public BootstrapPage()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            var settings = App.Services.GetRequiredService<IAppSettingsStore>();
            var api = App.Services.GetRequiredService<ApiClient>();
            if (string.IsNullOrWhiteSpace(settings.ServerUrl))
            {
                Frame.Navigate(typeof(ServerConnectPage));
                return;
            }
            try
            {
                var health = await api.HealthAsync();
                if (!health.Database)
                {
                    Frame.Navigate(typeof(ServerConnectPage));
                    return;
                }
                var setup = await api.SetupStatusAsync();
                Frame.Navigate(setup.IsComplete ? typeof(LoginPage) : typeof(SetupPage));
            }
            catch
            {
                Frame.Navigate(typeof(ServerConnectPage));
            }
        };
    }
}
