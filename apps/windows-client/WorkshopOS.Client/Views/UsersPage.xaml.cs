using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace WorkshopOS.Client.Views;

/// <summary>Legacy route — redirects into Settings → Users &amp; Roles.</summary>
public sealed partial class UsersPage : Page
{
    public UsersPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        Frame.Navigate(typeof(SettingsPage), "users");
    }
}
