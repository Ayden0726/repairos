using System.Diagnostics;
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
    private bool _connectionHealthLoaded;
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

        RestartCommandsBox.Text =
            "cd ~/workshopos\n" +
            "git pull origin main\n" +
            "./scripts/restart-workshopos.sh --update\n" +
            "# or:\n" +
            "docker compose -f docker/docker-compose.yml up -d --build";

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
        else if (string.Equals(_initialSection, "connection", StringComparison.OrdinalIgnoreCase))
            SettingsPivot.SelectedIndex = 6;
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
        else if (SettingsPivot.SelectedIndex == 6 && !_connectionHealthLoaded)
        {
            _connectionHealthLoaded = true;
            await CheckHealthAsync();
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
            PricingErrorText.Text = FormatPricingError(ex.Message);
        }
    }

    private static string FormatPricingError(string message)
    {
        if (message.Contains("404", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("Not Found", StringComparison.OrdinalIgnoreCase))
            return "Server outdated — update/restart WorkshopOS server so /api/pricing/* exists, then reconnect. " + message;
        return message;
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

    private Guid? _editingServiceId;
    private List<CatalogueServiceDto> _catalogueServicesCache = new();

    private async Task LoadServicesAsync()
    {
        ServicesErrorText.Text = string.Empty;
        ServicesStatusText.Text = "Loading catalogue…";
        try
        {
            var api = App.Services.GetRequiredService<ApiClient>();
            var cats = await api.GetAsync<CatalogueCategoryDto[]>("api/catalogue/categories");
            CatalogueCategoriesList.ItemsSource = cats
                .Select(c => $"{c.SortOrder}. {c.Name} ({c.Key}) · {c.ServiceCount} services · {c.DeviceType}")
                .ToList();
            _catalogueServicesCache = (await api.GetAsync<CatalogueServiceDto[]>("api/catalogue/services?activeOnly=false")).ToList();
            ApplyCatalogueFilter();
            var bundles = await api.GetAsync<CatalogueBundleDto[]>("api/catalogue/bundles?expand=true");
            CatalogueBundlesList.ItemsSource = bundles
                .Select(b => $"{b.Name} ({b.Code}) · ${b.BundlePrice?.ToString("0.##") ?? "—"} · {b.ServicePricingIds.Count} services")
                .ToList();
            ServicesStatusText.Text = $"{cats.Length} categories · {_catalogueServicesCache.Count} services · {bundles.Length} bundles";
        }
        catch (Exception ex)
        {
            ServicesStatusText.Text = string.Empty;
            ServicesErrorText.Text = FormatPricingError(ex.Message);
        }
    }

    private void ApplyCatalogueFilter()
    {
        var q = (CatalogueSearchBox.Text ?? string.Empty).Trim();
        IEnumerable<CatalogueServiceDto> rows = _catalogueServicesCache;
        if (!string.IsNullOrWhiteSpace(q))
        {
            rows = rows.Where(s =>
                (s.Name?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (s.Code?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (s.Subcategory?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false));
        }
        CatalogueServicesList.ItemsSource = rows
            .OrderBy(s => s.SortOrder).ThenBy(s => s.Name)
            .Select(s => new CatalogueServiceListItem(s,
                $"{(s.IsActive ? "" : "[off] ")}{s.Code} · {s.Name} · ${s.DefaultLabourFee:0.##} · {s.EstimatedMinutes}m · warranty {s.WarrantyDays ?? 0}d"))
            .ToList();
    }

    private void CatalogueSearch_TextChanged(object sender, TextChangedEventArgs e) => ApplyCatalogueFilter();

    private async void ServicesRefresh_Click(object sender, RoutedEventArgs e) => await LoadServicesAsync();

    private async void ServicesImport_Click(object sender, RoutedEventArgs e)
    {
        ServicesErrorText.Text = string.Empty;
        ServicesStatusText.Text = "Importing seed catalogue…";
        try
        {
            var api = App.Services.GetRequiredService<ApiClient>();
            var result = await api.PostAsync<CatalogueImportResultDto>("api/catalogue/import?force=true");
            ServicesStatusText.Text = result.AlreadyCurrent
                ? $"Catalogue already at {result.Version}."
                : $"Imported {result.Version}: {result.ServicesUpserted} services, {result.CategoriesUpserted} categories.";
            await LoadServicesAsync();
        }
        catch (Exception ex)
        {
            ServicesStatusText.Text = string.Empty;
            ServicesErrorText.Text = $"Import failed: {ex.Message}";
        }
    }

    private void CatalogueServicesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CatalogueServicesList.SelectedItem is not CatalogueServiceListItem item) return;
        var s = item.Service;
        _editingServiceId = s.Id;
        ServiceNameBox.Text = s.Name;
        ServiceCodeBox.Text = s.Code ?? string.Empty;
        ServiceCategoryBox.Text = s.Category ?? s.CategoryName ?? string.Empty;
        ServiceSubcategoryBox.Text = s.Subcategory ?? string.Empty;
        ServiceLabourBox.Value = (double)s.DefaultLabourFee;
        ServiceFeeBox.Value = (double)s.ServiceFee;
        ServiceMinutesBox.Value = s.EstimatedMinutes ?? double.NaN;
        ServiceWarrantyBox.Value = s.WarrantyDays ?? double.NaN;
        ServiceCustomerDescBox.Text = s.CustomerDescription ?? string.Empty;
        ServiceTechNotesBox.Text = s.TechNotes ?? string.Empty;
        ServiceMarkupBox.Value = s.DefaultPartMarkupPercent is decimal m ? (double)m : double.NaN;
        ServiceActiveCheck.IsChecked = s.IsActive;
        ServicePartsRequiredCheck.IsChecked = s.PartsRequired;
        ServiceSerialRequiredCheck.IsChecked = s.SerialRequired;
    }

    private void ClearServiceForm_Click(object sender, RoutedEventArgs e)
    {
        _editingServiceId = null;
        CatalogueServicesList.SelectedItem = null;
        ServiceNameBox.Text = string.Empty;
        ServiceCodeBox.Text = string.Empty;
        ServiceCategoryBox.Text = string.Empty;
        ServiceSubcategoryBox.Text = string.Empty;
        ServiceLabourBox.Value = 50;
        ServiceFeeBox.Value = 0;
        ServiceMinutesBox.Value = 40;
        ServiceWarrantyBox.Value = 90;
        ServiceCustomerDescBox.Text = string.Empty;
        ServiceTechNotesBox.Text = string.Empty;
        ServiceMarkupBox.Value = double.NaN;
        ServiceActiveCheck.IsChecked = true;
        ServicePartsRequiredCheck.IsChecked = false;
        ServiceSerialRequiredCheck.IsChecked = false;
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
            int? minutes = !double.IsNaN(ServiceMinutesBox.Value) ? (int)ServiceMinutesBox.Value : null;
            int? warranty = !double.IsNaN(ServiceWarrantyBox.Value) ? (int)ServiceWarrantyBox.Value : null;
            var saved = await api.PostAsync<UpsertCatalogueServiceRequest, CatalogueServiceDto>(
                "api/catalogue/services",
                new UpsertCatalogueServiceRequest(
                    _editingServiceId, name, ServiceCategoryBox.Text, ServiceCustomerDescBox.Text,
                    ReadMoney(ServiceLabourBox, 0m), markup, ServiceActiveCheck.IsChecked != false, 0,
                    string.IsNullOrWhiteSpace(ServiceCodeBox.Text) ? null : ServiceCodeBox.Text.Trim(),
                    ServiceSubcategoryBox.Text, null, null, null,
                    ReadMoney(ServiceFeeBox, 0m), minutes, null, null,
                    ServicePartsRequiredCheck.IsChecked == true,
                    ServiceSerialRequiredCheck.IsChecked == true,
                    warranty, ServiceTechNotesBox.Text, ServiceCustomerDescBox.Text));
            ServicesStatusText.Text = $"Saved “{saved.Name}” ({saved.Code}).";
            await LoadServicesAsync();
        }
        catch (Exception ex)
        {
            ServicesStatusText.Text = string.Empty;
            ServicesErrorText.Text = $"Save failed: {ex.Message}";
        }
    }

    private async void DeleteService_Click(object sender, RoutedEventArgs e)
    {
        if (_editingServiceId is not Guid id)
        {
            ServicesErrorText.Text = "Select a service first.";
            return;
        }
        try
        {
            var api = App.Services.GetRequiredService<ApiClient>();
            await api.DeleteAsync($"api/catalogue/services/{id}");
            ClearServiceForm_Click(sender, e);
            await LoadServicesAsync();
            ServicesStatusText.Text = "Service disabled/deleted.";
        }
        catch (Exception ex)
        {
            ServicesErrorText.Text = $"Delete failed: {ex.Message}";
        }
    }

    private sealed record CatalogueServiceListItem(CatalogueServiceDto Service, string Label)
    {
        public override string ToString() => Label;
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
                ? "Role list empty — update/restart server (GET /api/roles) and confirm staff.view permission."
                : string.Join(" · ", Users.Roles.Select(r => r.DisplayLabel));

            RolesList.ItemsSource = Users.AllRoles;
            PermissionsList.ItemsSource = Users.PermissionToggles;
            RolesStatusText.Text = Users.RolesStatus ?? string.Empty;
            RolesManageErrorText.Text = Users.RolesError ?? string.Empty;

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

    private async void ResetUserPassword_Click(object sender, RoutedEventArgs e)
    {
        if (Users.SelectedUser is null) return;
        UsersErrorText.Text = string.Empty;

        var newBox = new PasswordBox { Header = "New password (min 10, upper/lower/digit)", Width = 320 };
        var confirmBox = new PasswordBox { Header = "Confirm new password", Width = 320 };
        var panel = new StackPanel { Spacing = 12 };
        panel.Children.Add(new TextBlock
        {
            Text = $"Set a new password for {Users.SelectedUser.DisplayName} ({Users.SelectedUser.Email}).",
            TextWrapping = TextWrapping.Wrap
        });
        panel.Children.Add(newBox);
        panel.Children.Add(confirmBox);

        var dialog = new ContentDialog
        {
            Title = "Reset password",
            Content = panel,
            PrimaryButtonText = "Reset",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        var pwd = newBox.Password ?? string.Empty;
        var confirm = confirmBox.Password ?? string.Empty;
        if (!string.Equals(pwd, confirm, StringComparison.Ordinal))
        {
            UsersErrorText.Text = "New password and confirmation do not match.";
            return;
        }

        await Users.ResetPasswordCommand.ExecuteAsync(pwd);
        UsersStatusText.Text = Users.Status ?? UsersStatusText.Text;
        if (!string.IsNullOrWhiteSpace(Users.Error))
            UsersErrorText.Text = Users.Error;
    }

    private async void ChangeOwnPassword_Click(object sender, RoutedEventArgs e)
    {
        ChangePasswordErrorText.Text = string.Empty;
        ChangePasswordStatusText.Text = string.Empty;

        var currentBox = new PasswordBox { Header = "Current password", Width = 320 };
        var newBox = new PasswordBox { Header = "New password (min 10, upper/lower/digit)", Width = 320 };
        var confirmBox = new PasswordBox { Header = "Confirm new password", Width = 320 };
        var panel = new StackPanel { Spacing = 12 };
        panel.Children.Add(currentBox);
        panel.Children.Add(newBox);
        panel.Children.Add(confirmBox);

        var dialog = new ContentDialog
        {
            Title = "Change password",
            Content = panel,
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        var current = currentBox.Password ?? string.Empty;
        var pwd = newBox.Password ?? string.Empty;
        var confirm = confirmBox.Password ?? string.Empty;
        if (!string.Equals(pwd, confirm, StringComparison.Ordinal))
        {
            ChangePasswordErrorText.Text = "New password and confirmation do not match.";
            return;
        }

        await Users.ChangeOwnPasswordAsync(current, pwd);
        if (!string.IsNullOrWhiteSpace(Users.Error))
            ChangePasswordErrorText.Text = Users.Error;
        else
            ChangePasswordStatusText.Text = Users.Status ?? "Password changed.";
    }

    private void RolesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        Users.SelectedRole = RolesList.SelectedItem as RoleOption;
        if (Users.SelectedRole is null) return;
        RoleNameBox.Text = Users.RoleName;
        RoleKeyBox.Text = Users.RoleKey;
        RoleKeyBox.IsEnabled = false;
        RoleDescriptionBox.Text = Users.RoleDescription;
    }

    private void NewRole_Click(object sender, RoutedEventArgs e)
    {
        RolesList.SelectedItem = null;
        Users.NewRoleDraftCommand.Execute(null);
        RoleNameBox.Text = string.Empty;
        RoleKeyBox.Text = string.Empty;
        RoleKeyBox.IsEnabled = true;
        RoleDescriptionBox.Text = string.Empty;
        RolesStatusText.Text = Users.RolesStatus ?? string.Empty;
        RolesManageErrorText.Text = string.Empty;
    }

    private async void SaveRole_Click(object sender, RoutedEventArgs e)
    {
        Users.RoleName = RoleNameBox.Text ?? string.Empty;
        Users.RoleKey = RoleKeyBox.Text ?? string.Empty;
        Users.RoleDescription = RoleDescriptionBox.Text ?? string.Empty;
        await Users.SaveRoleCommand.ExecuteAsync(null);
        await RefreshUsersUiAsync();
        RolesStatusText.Text = Users.RolesStatus ?? RolesStatusText.Text;
        if (!string.IsNullOrWhiteSpace(Users.RolesError))
            RolesManageErrorText.Text = Users.RolesError;
    }

    private async void CheckHealth_Click(object sender, RoutedEventArgs e) => await CheckHealthAsync();

    private async Task CheckHealthAsync()
    {
        HealthErrorText.Text = string.Empty;
        HealthStrengthText.Text = "Checking…";
        HealthDetailText.Text = string.Empty;
        try
        {
            var api = App.Services.GetRequiredService<ApiClient>();
            var sw = Stopwatch.StartNew();
            var health = await api.HealthAsync(timeout: TimeSpan.FromSeconds(8));
            sw.Stop();
            var ms = sw.ElapsedMilliseconds;

            // Strength: lower latency + healthy DB = stronger signal (0–100).
            var strength = 100;
            if (!health.Database) strength -= 40;
            if (!string.Equals(health.Status, "Healthy", StringComparison.OrdinalIgnoreCase)) strength -= 20;
            if (ms > 150) strength -= 10;
            if (ms > 400) strength -= 15;
            if (ms > 1000) strength -= 20;
            if (ms > 2500) strength -= 15;
            strength = Math.Clamp(strength, 5, 100);
            HealthStrengthBar.Value = strength;

            var label = strength >= 80 ? "Strong" : strength >= 55 ? "OK" : strength >= 30 ? "Weak" : "Poor";
            HealthStrengthText.Text = $"API strength: {label} ({strength}/100) · {ms} ms";

            SystemInfoDto? info = null;
            try { info = await api.GetAsync<SystemInfoDto>("api/system/info"); } catch { /* older servers */ }

            var version = info?.ApiVersion ?? health.ApiVersion;
            var minClient = info?.ClientMinVersion ?? health.ClientMinVersion ?? "?";
            HealthDetailText.Text =
                $"Status {health.Status} · API {version} · DB {(health.Database ? "ok" : "down")} · " +
                $"client min {minClient} · server UTC {health.ServerTimeUtc:u}" +
                (info is null ? "\n(system/info unavailable — server may need update)" : "") +
                (info?.RestartAllowed == true ? "\nHTTP restart enabled (ALLOW_PROCESS_RESTART)." : "\nHTTP restart disabled — use the copyable commands below.");

            if (info?.UpdateCommands is { Count: > 0 })
            {
                RestartCommandsBox.Text = string.Join("\n",
                    info.RestartCommands.Concat(info.UpdateCommands).Distinct());
            }
        }
        catch (Exception ex)
        {
            HealthStrengthBar.Value = 0;
            HealthStrengthText.Text = "API strength: unreachable";
            HealthErrorText.Text = FormatPricingError(ex.Message);
        }
    }

    private async void RestartServer_Click(object sender, RoutedEventArgs e)
    {
        HealthErrorText.Text = string.Empty;
        try
        {
            var api = App.Services.GetRequiredService<ApiClient>();
            var result = await api.PostAsync<RestartResultDto>("api/system/restart");
            HealthDetailText.Text = result.Message;
            if (result.Commands is { Count: > 0 })
                RestartCommandsBox.Text = string.Join("\n", result.Commands);
            if (result.Restarted)
            {
                await Task.Delay(2500);
                await CheckHealthAsync();
            }
        }
        catch (Exception ex)
        {
            HealthErrorText.Text = FormatPricingError(ex.Message);
            RestartCommandsBox.Text =
                "cd ~/workshopos\n" +
                "git pull origin main\n" +
                "./scripts/restart-workshopos.sh --update\n" +
                "docker compose -f docker/docker-compose.yml up -d --build";
        }
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
