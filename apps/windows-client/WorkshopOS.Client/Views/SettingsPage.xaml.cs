using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using WorkshopOS.Client.Services;
using WorkshopOS.Client.ViewModels;
using WorkshopOS.Contracts.Auth;
using WorkshopOS.Contracts.Common;
using WorkshopOS.Contracts.Operations;

namespace WorkshopOS.Client.Views;

public sealed partial class SettingsPage : Page
{
    public UsersViewModel Users { get; }
    public BackupsViewModel Backups { get; }
    private bool _themeComboReady;
    private bool _usersLoaded;
    private bool _backupsLoaded;
    private bool _pricingLoaded;
    private bool _servicesLoaded;
    private bool _taxLoaded;
    private string? _initialSection;
    private PricingSettingsDto? _pricing;
    private BusinessProfileDto? _business;

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
            _business = profile;
            SummaryText.Text =
                $"{profile.Name}\n{profile.Phone} · {profile.Email}\nTax {(profile.GstRegistered ? "enabled" : "off")} at {profile.GstRate:P0} · {profile.Currency} · {(profile.GstInclusive ? "inclusive" : "exclusive")}";
        }
        catch (Exception ex)
        {
            SummaryText.Text = ex.Message;
        }

        if (string.Equals(_initialSection, "pricing", StringComparison.OrdinalIgnoreCase))
            SettingsPivot.SelectedIndex = 1;
        else if (string.Equals(_initialSection, "services", StringComparison.OrdinalIgnoreCase))
            SettingsPivot.SelectedIndex = 2;
        else if (string.Equals(_initialSection, "tax", StringComparison.OrdinalIgnoreCase))
            SettingsPivot.SelectedIndex = 3;
        else if (string.Equals(_initialSection, "users", StringComparison.OrdinalIgnoreCase))
            SettingsPivot.SelectedIndex = 4;
        else if (string.Equals(_initialSection, "backups", StringComparison.OrdinalIgnoreCase))
            SettingsPivot.SelectedIndex = 5;
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
        if (SettingsPivot.SelectedIndex == 1)
        {
            _pricingLoaded = true;
            await LoadPricingAsync();
        }
        else if (SettingsPivot.SelectedIndex == 2)
        {
            _servicesLoaded = true;
            await LoadServicesAsync();
        }
        else if (SettingsPivot.SelectedIndex == 3)
        {
            _taxLoaded = true;
            await LoadTaxAsync();
        }
        else if (SettingsPivot.SelectedIndex == 4 && !_usersLoaded)
        {
            _usersLoaded = true;
            await RefreshUsersUiAsync();
        }
        else if (SettingsPivot.SelectedIndex == 5 && !_backupsLoaded)
        {
            _backupsLoaded = true;
            await RefreshBackupsUiAsync();
        }
    }

    private async Task LoadPricingAsync()
    {
        PricingErrorText.Text = string.Empty;
        PricingStatusText.Text = "Loading pricing settings…";
        try
        {
            var api = App.Services.GetRequiredService<ApiClient>();
            _pricing = await api.GetAsync<PricingSettingsDto>("api/pricing/settings");
            DefaultLabourBox.Value = (double)_pricing.Labour.DefaultLabourFee;
            MinLabourBox.Value = (double)_pricing.Labour.MinimumLabourFee;
            DifficultyPricingCheck.IsChecked = _pricing.Labour.DifficultyPricingEnabled;
            SelectComboString(MarkupMethodCombo, _pricing.Parts.MarkupMethod);
            DefaultMarkupBox.Value = (double)_pricing.Parts.DefaultMarkupPercent;
            FixedMarkupBox.Value = (double)_pricing.Parts.FixedMarkupAmount;
            MinPartProfitBox.Value = (double)_pricing.Parts.MinimumPartProfit;
            MinMarginBox.Value = (double)_pricing.Profitability.MinimumGrossMarginPercent;
            WarnMarginBox.Value = (double)_pricing.Profitability.WarnBelowMarginPercent;
            ApprovalRequiredCheck.IsChecked = _pricing.Profitability.ManagerApprovalRequired;
            SelectComboString(RoundingCombo, _pricing.Rounding.Method);
            MaxTechDiscountBox.Value = (double)_pricing.Discounts.MaxTechDiscountPercent;
            ValidityDaysBox.Value = _pricing.Quote.DefaultValidityDays;
            AutoExpireCheck.IsChecked = _pricing.Quote.AutoExpire;
            var tiers = await api.GetAsync<MarkupTierDto[]>("api/pricing/tiers");
            TiersList.ItemsSource = tiers.Select(t => $"{t.MinCost:0.##}–{t.MaxCost?.ToString("0.##") ?? "∞"} @ {t.MarkupPercent:0.##}%").ToList();
            PricingStatusText.Text =
                $"Loaded from server — markup {_pricing.Parts.DefaultMarkupPercent:0.##}% · labour ${_pricing.Labour.DefaultLabourFee:0.##} · {_pricing.Parts.MarkupMethod} · round {_pricing.Rounding.Method}.";
        }
        catch (Exception ex)
        {
            PricingStatusText.Text = string.Empty;
            PricingErrorText.Text = ex.Message;
        }
    }

    private static void SelectComboString(ComboBox combo, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            combo.SelectedIndex = -1;
            return;
        }
        foreach (var item in combo.Items)
        {
            var text = item as string ?? (item as ComboBoxItem)?.Content as string;
            if (text is not null && text.Equals(value, StringComparison.OrdinalIgnoreCase))
            {
                combo.SelectedItem = item;
                return;
            }
        }
        combo.SelectedItem = value;
    }

    private static decimal ReadMoney(NumberBox box, decimal fallback)
    {
        var v = box.Value;
        if (double.IsNaN(v) || double.IsInfinity(v)) return fallback;
        return (decimal)v;
    }

    private static int ReadInt(NumberBox box, int fallback)
    {
        var v = box.Value;
        if (double.IsNaN(v) || double.IsInfinity(v)) return fallback;
        return (int)Math.Round(v, MidpointRounding.AwayFromZero);
    }

    private async void SavePricing_Click(object sender, RoutedEventArgs e)
    {
        PricingErrorText.Text = string.Empty;
        PricingStatusText.Text = "Saving pricing settings…";
        try
        {
            var api = App.Services.GetRequiredService<ApiClient>();
            // Always re-fetch so DifficultyLevels / Tax survive and we don't PUT a stale partial DTO.
            var current = await api.GetAsync<PricingSettingsDto>("api/pricing/settings");
            var updated = current with
            {
                Labour = current.Labour with
                {
                    DefaultLabourFee = ReadMoney(DefaultLabourBox, current.Labour.DefaultLabourFee),
                    MinimumLabourFee = ReadMoney(MinLabourBox, current.Labour.MinimumLabourFee),
                    DifficultyPricingEnabled = DifficultyPricingCheck.IsChecked == true
                },
                Parts = current.Parts with
                {
                    MarkupMethod = ReadComboString(MarkupMethodCombo) ?? current.Parts.MarkupMethod ?? "FlatPercent",
                    DefaultMarkupPercent = ReadMoney(DefaultMarkupBox, current.Parts.DefaultMarkupPercent),
                    FixedMarkupAmount = ReadMoney(FixedMarkupBox, current.Parts.FixedMarkupAmount),
                    MinimumPartProfit = ReadMoney(MinPartProfitBox, current.Parts.MinimumPartProfit)
                },
                Profitability = current.Profitability with
                {
                    MinimumGrossMarginPercent = ReadMoney(MinMarginBox, current.Profitability.MinimumGrossMarginPercent),
                    WarnBelowMarginPercent = ReadMoney(WarnMarginBox, current.Profitability.WarnBelowMarginPercent),
                    ManagerApprovalRequired = ApprovalRequiredCheck.IsChecked == true
                },
                Rounding = current.Rounding with
                {
                    Method = ReadComboString(RoundingCombo) ?? current.Rounding.Method ?? "End9"
                },
                Discounts = current.Discounts with
                {
                    MaxTechDiscountPercent = ReadMoney(MaxTechDiscountBox, current.Discounts.MaxTechDiscountPercent)
                },
                Quote = current.Quote with
                {
                    DefaultValidityDays = ReadInt(ValidityDaysBox, current.Quote.DefaultValidityDays > 0 ? current.Quote.DefaultValidityDays : 14),
                    AutoExpire = AutoExpireCheck.IsChecked == true
                }
            };
            _pricing = await api.PutAsync<PricingSettingsDto, PricingSettingsDto>("api/pricing/settings", updated);
            // Prove persistence: reload from GET and reflect server values in the UI.
            await LoadPricingAsync();
            PricingStatusText.Text =
                $"Saved and verified — markup {_pricing!.Parts.DefaultMarkupPercent:0.##}% · labour ${_pricing.Labour.DefaultLabourFee:0.##} · {_pricing.Parts.MarkupMethod}.";
            PricingErrorText.Text = string.Empty;
        }
        catch (Exception ex)
        {
            PricingStatusText.Text = string.Empty;
            PricingErrorText.Text = $"Save failed: {ex.Message}";
        }
    }

    private static string? ReadComboString(ComboBox combo) =>
        combo.SelectedItem as string
        ?? (combo.SelectedItem as ComboBoxItem)?.Content as string
        ?? combo.SelectedItem?.ToString();

    private async void AddTier_Click(object sender, RoutedEventArgs e)
    {
        PricingErrorText.Text = string.Empty;
        PricingStatusText.Text = "Adding markup tier…";
        try
        {
            var api = App.Services.GetRequiredService<ApiClient>();
            var maxVal = TierMaxBox.Value;
            decimal? max = !double.IsNaN(maxVal) && maxVal > 0 ? (decimal)maxVal : null;
            await api.PostAsync<UpsertMarkupTierRequest, MarkupTierDto>("api/pricing/tiers",
                new UpsertMarkupTierRequest(null, ReadMoney(TierMinBox, 0m), max, ReadMoney(TierPctBox, 0m), 0));
            await LoadPricingAsync();
            PricingStatusText.Text = "Markup tier saved.";
        }
        catch (Exception ex)
        {
            PricingStatusText.Text = string.Empty;
            PricingErrorText.Text = $"Tier save failed: {ex.Message}";
        }
    }

    private async Task LoadServicesAsync()
    {
        ServicesErrorText.Text = string.Empty;
        ServicesStatusText.Text = "Loading services…";
        try
        {
            var api = App.Services.GetRequiredService<ApiClient>();
            var list = await api.GetAsync<ServicePricingDto[]>("api/pricing/services");
            ServicesList.ItemsSource = list.Select(s => $"{s.Name} · labour {s.DefaultLabourFee:0.##} · markup {s.DefaultPartMarkupPercent?.ToString("0.##") ?? "—"}%").ToList();
            ServicesStatusText.Text = list.Length == 0 ? "No services yet." : $"{list.Length} service(s) loaded from server.";
        }
        catch (Exception ex)
        {
            ServicesStatusText.Text = string.Empty;
            ServicesErrorText.Text = ex.Message;
        }
    }

    private async void SaveService_Click(object sender, RoutedEventArgs e)
    {
        ServicesErrorText.Text = string.Empty;
        ServicesStatusText.Text = "Saving service…";
        try
        {
            var api = App.Services.GetRequiredService<ApiClient>();
            var name = (ServiceNameBox.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name))
                throw new InvalidOperationException("Service name is required.");
            var markupVal = ServiceMarkupBox.Value;
            decimal? markup = !double.IsNaN(markupVal) && markupVal > 0 ? (decimal)markupVal : null;
            var saved = await api.PostAsync<UpsertServicePricingRequest, ServicePricingDto>("api/pricing/services",
                new UpsertServicePricingRequest(null, name, ServiceCategoryBox.Text,
                    null, ReadMoney(ServiceLabourBox, 0m), markup, true, 0));
            ServiceNameBox.Text = string.Empty;
            ServiceCategoryBox.Text = string.Empty;
            ServiceLabourBox.Value = double.NaN;
            ServiceMarkupBox.Value = double.NaN;
            await LoadServicesAsync();
            ServicesStatusText.Text = $"Saved “{saved.Name}” (labour {saved.DefaultLabourFee:0.##}) and reloaded list.";
            ServicesErrorText.Text = string.Empty;
        }
        catch (Exception ex)
        {
            ServicesStatusText.Text = string.Empty;
            ServicesErrorText.Text = $"Save failed: {ex.Message}";
        }
    }

    private async Task LoadTaxAsync()
    {
        TaxErrorText.Text = string.Empty;
        TaxStatusText.Text = "Loading tax settings…";
        try
        {
            var api = App.Services.GetRequiredService<ApiClient>();
            _business = await api.GetAsync<BusinessProfileDto>("api/settings/business");
            TaxEnabledCheck.IsChecked = _business.GstRegistered;
            TaxRateBox.Value = (double)_business.GstRate;
            TaxInclusiveCheck.IsChecked = _business.GstInclusive;
            CurrencyBox.Text = _business.Currency;
            TaxStatusText.Text =
                $"Loaded from server — tax {(_business.GstRegistered ? "on" : "off")} at {_business.GstRate:P0} · {(_business.GstInclusive ? "inclusive" : "exclusive")} · {_business.Currency}.";
        }
        catch (Exception ex)
        {
            TaxStatusText.Text = string.Empty;
            TaxErrorText.Text = ex.Message;
        }
    }

    private async void SaveTax_Click(object sender, RoutedEventArgs e)
    {
        TaxErrorText.Text = string.Empty;
        TaxStatusText.Text = "Saving tax settings…";
        try
        {
            var api = App.Services.GetRequiredService<ApiClient>();
            var current = await api.GetAsync<BusinessProfileDto>("api/settings/business");
            var updated = current with
            {
                GstRegistered = TaxEnabledCheck.IsChecked == true,
                GstRate = ReadMoney(TaxRateBox, current.GstRate),
                GstInclusive = TaxInclusiveCheck.IsChecked == true,
                Currency = string.IsNullOrWhiteSpace(CurrencyBox.Text) ? current.Currency : CurrencyBox.Text.Trim()
            };
            _business = await api.PutAsync<BusinessProfileDto, BusinessProfileDto>("api/settings/business", updated);
            await LoadTaxAsync();
            // Confirm pricing mirror picked up the same tax (server UpdateBusiness syncs pricing.settings).
            try
            {
                var pricing = await api.GetAsync<PricingSettingsDto>("api/pricing/settings");
                if (pricing.Tax is { } tax &&
                    (tax.Enabled != _business!.GstRegistered || tax.Rate != _business.GstRate || tax.Inclusive != _business.GstInclusive))
                {
                    TaxStatusText.Text =
                        $"Tax saved on business profile, but pricing mirror differs (pricing tax {(tax.Enabled ? "on" : "off")} @ {tax.Rate:P0}).";
                }
                else
                {
                    TaxStatusText.Text =
                        $"Saved and verified — tax {(_business!.GstRegistered ? "on" : "off")} at {_business.GstRate:P0} · pricing mirror OK.";
                }
            }
            catch
            {
                TaxStatusText.Text =
                    $"Saved business tax {(_business!.GstRegistered ? "on" : "off")} at {_business.GstRate:P0} (could not verify pricing mirror).";
            }
            TaxErrorText.Text = string.Empty;
        }
        catch (Exception ex)
        {
            TaxStatusText.Text = string.Empty;
            TaxErrorText.Text = $"Save failed: {ex.Message}";
        }
    }

    private async void UsersRefresh_Click(object sender, RoutedEventArgs e) => await RefreshUsersUiAsync();

    private async Task RefreshUsersUiAsync()
    {
        UsersLoadingRing.IsActive = true;
        UsersErrorText.Text = string.Empty;
        RolesHintText.Text = string.Empty;
        try
        {
            await Users.LoadCommand.ExecuteAsync(null);
            UsersList.ItemsSource = Users.Users;
            NewRoleCombo.ItemsSource = Users.Roles;
            EditRoleCombo.ItemsSource = Users.Roles;
            if (Users.NewRole is not null)
                NewRoleCombo.SelectedItem = Users.Roles.FirstOrDefault(r => r.Key == Users.NewRole.Key);
            else if (NewRoleCombo.SelectedItem is null && Users.Roles.Count > 0)
                NewRoleCombo.SelectedItem = Users.Roles[0];

            RolesHintText.Text = Users.Roles.Count == 0
                ? "Role list empty — check GET /api/roles and staff.view permission."
                : string.Join(" · ", Users.Roles.Select(r => r.DisplayLabel));

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
        Users.NewRole = NewRoleCombo.SelectedItem as RoleOption;
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
        Users.EditRole = EditRoleCombo.SelectedItem as RoleOption;
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
