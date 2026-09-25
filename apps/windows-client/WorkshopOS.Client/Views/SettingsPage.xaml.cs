using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using WorkshopOS.Client.Services;
using WorkshopOS.Client.ViewModels;
using WorkshopOS.Contracts.Auth;
using WorkshopOS.Contracts.Common;

namespace WorkshopOS.Client.Views;

public sealed partial class SettingsPage : Page
{
    public UsersViewModel Users { get; }
    public BackupsViewModel Backups { get; }
    private bool _themeComboReady;
    private bool _usersLoaded;
    private bool _backupsLoaded;
    private string? _initialSection;

    public SettingsPage()
    {
        Users = App.Services.GetRequiredService<UsersViewModel>();
        Backups = new BackupsViewModel(App.Services.GetRequiredService<ApiClient>());
        InitializeComponent();
        Loaded += SettingsPage_Loaded;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _initialSection = e.Parameter as string;
    }

    private async void SettingsPage_Loaded(object sender, RoutedEventArgs e)
    {
        var settings = App.Services.GetRequiredService<IAppSettingsStore>();
        SelectThemeCombo(settings.Theme);
        _themeComboReady = true;
        ThemeStatusText.Text = $"Current: {settings.Theme}";

        ServerUrlText.Text = string.IsNullOrWhiteSpace(settings.ServerUrl)
            ? "No server saved."
            : $"Connected server: {settings.ServerUrl}";

        try
        {
            var api = App.Services.GetRequiredService<ApiClient>();
            var profile = await api.GetAsync<BusinessProfileDto>("api/settings/business");
            SummaryText.Text =
                $"{profile.Name}\n{profile.Phone} · {profile.Email}\nGST {(profile.GstRegistered ? "registered" : "not registered")} at {profile.GstRate:P0} · {profile.Currency}";
        }
        catch (Exception ex)
        {
            SummaryText.Text = ex.Message;
        }

        if (string.Equals(_initialSection, "users", StringComparison.OrdinalIgnoreCase))
            SettingsPivot.SelectedIndex = 1;
        else if (string.Equals(_initialSection, "backups", StringComparison.OrdinalIgnoreCase))
            SettingsPivot.SelectedIndex = 2;
    }

    private void SelectThemeCombo(string theme)
    {
        foreach (var item in ThemeCombo.Items.OfType<ComboBoxItem>())
        {
            if (string.Equals(item.Tag as string, theme, StringComparison.OrdinalIgnoreCase))
            {
                ThemeCombo.SelectedItem = item;
                return;
            }
        }
        ThemeCombo.SelectedIndex = 0;
    }

    private void ThemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_themeComboReady) return;
        if (ThemeCombo.SelectedItem is not ComboBoxItem item || item.Tag is not string tag) return;
        var settings = App.Services.GetRequiredService<IAppSettingsStore>();
        settings.Theme = tag;
        ThemeService.ApplyFromStore(settings);
        ThemeStatusText.Text = $"Applied: {settings.Theme}";
    }

    private async void SettingsPivot_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SettingsPivot.SelectedIndex == 1 && !_usersLoaded)
        {
            _usersLoaded = true;
            await RefreshUsersUiAsync();
        }
        else if (SettingsPivot.SelectedIndex == 2 && !_backupsLoaded)
        {
            _backupsLoaded = true;
            await RefreshBackupsUiAsync();
        }
    }

    private async void UsersRefresh_Click(object sender, RoutedEventArgs e) => await RefreshUsersUiAsync();

    private async Task RefreshUsersUiAsync()
    {
        UsersLoadingRing.IsActive = true;
        UsersErrorText.Text = string.Empty;
        try
        {
            await Users.LoadCommand.ExecuteAsync(null);
            UsersList.ItemsSource = Users.Users;
            NewRoleCombo.ItemsSource = Users.Roles;
            EditRoleCombo.ItemsSource = Users.Roles;
            if (NewRoleCombo.SelectedItem is null && Users.Roles.Count > 0)
                NewRoleCombo.SelectedItem = Users.Roles[0];
            UsersStatusText.Text = Users.Status ?? (Users.Users.Count == 0 ? "No staff yet." : $"{Users.Users.Count} account(s)");
            if (!string.IsNullOrWhiteSpace(Users.Error))
                UsersErrorText.Text = Users.Error;
        }
        finally
        {
            UsersLoadingRing.IsActive = false;
        }
    }

    private void UsersList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        Users.SelectedUser = UsersList.SelectedItem as StaffUserDto;
        if (Users.SelectedUser is null)
        {
            EditUserPanel.Visibility = Visibility.Collapsed;
            return;
        }

        EditUserPanel.Visibility = Visibility.Visible;
        EditRoleCombo.SelectedItem = Users.Roles.FirstOrDefault(r => r.Key == Users.SelectedUser.RoleKey);
        EditStatusCombo.SelectedItem = Users.SelectedUser.Status;
    }

    private async void CreateUser_Click(object sender, RoutedEventArgs e)
    {
        Users.NewDisplayName = NewDisplayNameBox.Text ?? string.Empty;
        Users.NewEmail = NewEmailBox.Text ?? string.Empty;
        Users.NewPassword = NewPasswordBox.Password ?? string.Empty;
        Users.NewPhone = NewPhoneBox.Text ?? string.Empty;
        Users.NewRole = NewRoleCombo.SelectedItem as RoleDto;
        CreateUserButton.IsEnabled = false;
        try
        {
            await Users.CreateCommand.ExecuteAsync(null);
            if (string.IsNullOrWhiteSpace(Users.Error))
            {
                NewDisplayNameBox.Text = string.Empty;
                NewEmailBox.Text = string.Empty;
                NewPasswordBox.Password = string.Empty;
                NewPhoneBox.Text = string.Empty;
            }
            await RefreshUsersUiAsync();
            UsersStatusText.Text = Users.Status ?? UsersStatusText.Text;
            if (!string.IsNullOrWhiteSpace(Users.Error))
                UsersErrorText.Text = Users.Error;
        }
        finally
        {
            CreateUserButton.IsEnabled = true;
        }
    }

    private async void SaveUser_Click(object sender, RoutedEventArgs e)
    {
        if (Users.SelectedUser is null) return;
        Users.EditRole = EditRoleCombo.SelectedItem as RoleDto;
        Users.EditStatus = EditStatusCombo.SelectedItem as string ?? "Active";
        await Users.SaveSelectedCommand.ExecuteAsync(null);
        await RefreshUsersUiAsync();
        UsersStatusText.Text = Users.Status ?? UsersStatusText.Text;
        if (!string.IsNullOrWhiteSpace(Users.Error))
            UsersErrorText.Text = Users.Error;
    }

    private async void BackupsRefresh_Click(object sender, RoutedEventArgs e) => await RefreshBackupsUiAsync();

    private async void BackupsCreate_Click(object sender, RoutedEventArgs e)
    {
        await Backups.CreateCommand.ExecuteAsync(null);
        await RefreshBackupsUiAsync();
    }

    private async Task RefreshBackupsUiAsync()
    {
        BackupsErrorText.Text = string.Empty;
        await Backups.RefreshCommand.ExecuteAsync(null);
        BackupsList.ItemsSource = Backups.Lines;
        BackupsStatusText.Text = Backups.Status ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(Backups.Error))
            BackupsErrorText.Text = Backups.Error;
    }

    private void ChangeServer_Click(object sender, RoutedEventArgs e)
    {
        App.Services.GetRequiredService<IAppSettingsStore>().ClearConnection();
        if (App.MainWindowInstance is MainWindow window)
        {
            window.AppRootFrame.Navigate(typeof(ServerConnectPage), "Enter a new server URL or pairing code.");
            return;
        }

        Frame.Navigate(typeof(ServerConnectPage), "Enter a new server URL or pairing code.");
    }
}
