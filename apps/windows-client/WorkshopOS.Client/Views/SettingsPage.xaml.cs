using Microsoft.UI.Xaml.Controls;
using WorkshopOS.Client.Services;

namespace WorkshopOS.Client.Views;

public sealed partial class SettingsPage : Page
{
    public SettingsPage()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            try
            {
                var api = App.Services.GetRequiredService<ApiClient>();
                var profile = await api.GetAsync<WorkshopOS.Contracts.Auth.BusinessProfileDto>("api/settings/business");
                SummaryText.Text = $"{profile.Name}\n{profile.Phone} · {profile.Email}\nGST {(profile.GstRegistered ? "registered" : "not registered")} at {profile.GstRate:P0} · {profile.Currency}";
            }
            catch (Exception ex)
            {
                SummaryText.Text = ex.Message;
            }
        };
    }
}
