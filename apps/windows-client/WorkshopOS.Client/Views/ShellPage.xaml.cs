using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using WorkshopOS.Client.Services;
using WorkshopOS.Client.ViewModels;
using WorkshopOS.Contracts.Common;
using WorkshopOS.Contracts.Operations;

namespace WorkshopOS.Client.Views;

public sealed partial class ShellPage : Page
{
    public ShellViewModel ViewModel { get; }

    public ShellPage()
    {
        ViewModel = App.Services.GetRequiredService<ShellViewModel>();
        InitializeComponent();
        DataContext = ViewModel;
        ViewModel.OpenModule = NavigateToModule;
        ViewModel.OpenSearchRoute = NavigateSearchRoute;
        ViewModel.LoggedOut = () => Frame.Navigate(typeof(LoginPage));
        Unloaded += (_, _) => ViewModel.StopNotificationPolling();
        Loaded += async (_, _) =>
        {
            SyncThemeToggle();
            await ViewModel.LoadCommand.ExecuteAsync(null);
            BuildNavigation();
            ContentFrame.Navigate(typeof(DashboardPage));
            SelectNavKey("dashboard");
        };
    }

    private void CommandCentreAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        SearchBox.Focus(FocusState.Programmatic);
        ViewModel.OpenCommandCentreCommand.Execute(null);
    }

    private void ThemeLight_Click(object sender, RoutedEventArgs e)
    {
        ThemeService.SetLightOrDark(false);
        SyncThemeToggle();
    }

    private void ThemeDark_Click(object sender, RoutedEventArgs e)
    {
        ThemeService.SetLightOrDark(true);
        SyncThemeToggle();
    }

    private void SyncThemeToggle()
    {
        var dark = ThemeService.IsDarkPreference(App.Services.GetRequiredService<IAppSettingsStore>().Theme);
        ThemeDarkBtn.Style = dark ? (Style)Application.Current.Resources["AccentButtonStyle"] : null;
        ThemeLightBtn.Style = dark ? null : (Style)Application.Current.Resources["AccentButtonStyle"];
    }

    private void BuildNavigation()
    {
        NavView.MenuItems.Clear();
        var byKey = ViewModel.Modules.ToDictionary(m => m.Key, StringComparer.OrdinalIgnoreCase);

        foreach (var (title, keys) in ShellViewModel.NavGroups)
        {
            var visible = keys
                .Select(k => byKey.TryGetValue(k, out var m) ? m : null)
                .Where(m => m is not null && m.Visible && ViewModel.CanSeeModule(m.Key))
                .Cast<ModuleDto>()
                .ToList();

            if (visible.Count == 0 && ViewModel.IsOffline)
            {
                visible = keys
                    .Where(ViewModel.CanSeeModule)
                    .Select(k => new ModuleDto(k, title, TitleForKey(k), 1, true, true))
                    .ToList();
            }

            if (visible.Count == 0) continue;

            if (visible.Count == 1 && keys.Length == 1)
            {
                var m = visible[0];
                NavView.MenuItems.Add(new NavigationViewItem
                {
                    Content = m.Title,
                    Tag = m.Key,
                    Icon = new SymbolIcon(IconForKey(m.Key))
                });
                continue;
            }

            var parent = new NavigationViewItem
            {
                Content = title,
                SelectsOnInvoked = false,
                Icon = new SymbolIcon(IconForGroup(title))
            };
            foreach (var m in visible)
            {
                parent.MenuItems.Add(new NavigationViewItem
                {
                    Content = m.Title,
                    Tag = m.Key,
                    Icon = new SymbolIcon(IconForKey(m.Key))
                });
            }
            NavView.MenuItems.Add(parent);
        }
    }

    private static string TitleForKey(string key) => key switch
    {
        "dashboard" => "Dashboard",
        "repairs" => "Repairs",
        "quotes" => "Quotes",
        "invoices" => "Invoices",
        "expenses" => "Expenses",
        "pos" => "POS",
        "calendar" => "Bookings",
        "inventory" => "Inventory",
        "purchasing" => "Purchasing",
        "used" => "Refurbished",
        "customers" => "Customers",
        "reports" => "Reports",
        "ai" => "AI Analyst",
        "knowledge" => "Knowledge",
        "notifications" => "Notifications",
        "backups" => "Backups",
        _ => key
    };

    private static Symbol IconForGroup(string title) => title switch
    {
        "Repairs" => Symbol.Repair,
        "Bookings" => Symbol.Calendar,
        "Sales" => Symbol.Shop,
        "Finance" => Symbol.Calculator,
        "Inventory" => Symbol.Library,
        "Insights" => Symbol.View,
        "Customers" => Symbol.People,
        _ => Symbol.AllApps
    };

    private static Symbol IconForKey(string key) => key switch
    {
        "dashboard" => Symbol.Home,
        "repairs" => Symbol.Repair,
        "repairs_waiting_approval" => Symbol.Clock,
        "repairs_waiting_parts" => Symbol.Shop,
        "repairs_ready" => Symbol.Accept,
        "quotes" => Symbol.Document,
        "invoices" => Symbol.Calculator,
        "expenses" => Symbol.Calculator,
        "pos" => Symbol.Shop,
        "calendar" => Symbol.Calendar,
        "inventory" => Symbol.Library,
        "purchasing" => Symbol.Shop,
        "used" => Symbol.Tag,
        "customers" => Symbol.People,
        "reports" => Symbol.View,
        "ai" => Symbol.Comment,
        "knowledge" => Symbol.Bookmarks,
        "notifications" => Symbol.Message,
        "backups" => Symbol.Save,
        _ => Symbol.Page
    };

    private void NavigateToModule(string key, int phase, bool implemented)
    {
        if (key is "settings" or "users" or "backups")
        {
            if (!ViewModel.CanOpenSettings)
            {
                ContentFrame.Navigate(typeof(PlaceholderPage),
                    new PlaceholderArgs(key, phase, "Your role cannot open Settings."));
                return;
            }
        }
        else if (!ViewModel.CanSeeModule(key) && key is not ("repairs_waiting_approval" or "repairs_waiting_parts" or "repairs_ready"))
        {
            ContentFrame.Navigate(typeof(PlaceholderPage),
                new PlaceholderArgs(key, phase, "Your role cannot open this module."));
            return;
        }
        else if (key.StartsWith("repairs_", StringComparison.OrdinalIgnoreCase) && !ViewModel.CanSeeModule("repairs"))
        {
            ContentFrame.Navigate(typeof(PlaceholderPage),
                new PlaceholderArgs(key, phase, "Your role cannot open Repairs."));
            return;
        }

        if (!implemented)
        {
            ContentFrame.Navigate(typeof(PlaceholderPage), new PlaceholderArgs(key, phase));
            return;
        }

        switch (key)
        {
            case "dashboard":
                ContentFrame.Navigate(typeof(DashboardPage));
                return;
            case "settings":
                ContentFrame.Navigate(typeof(SettingsPage));
                return;
            case "users":
                ContentFrame.Navigate(typeof(SettingsPage), "users");
                return;
            case "backups":
                ContentFrame.Navigate(typeof(SettingsPage), "backups");
                return;
            case "customers":
                ContentFrame.Navigate(typeof(CustomersPage));
                return;
            case "repairs":
                ContentFrame.Navigate(typeof(RepairsPage));
                return;
            case "repairs_waiting_approval":
                ContentFrame.Navigate(typeof(RepairsPage), "awaiting_approval");
                return;
            case "repairs_waiting_parts":
                ContentFrame.Navigate(typeof(RepairsPage), "waiting_parts");
                return;
            case "repairs_ready":
                ContentFrame.Navigate(typeof(RepairsPage), "ready_pickup");
                return;
            case "inventory":
                ContentFrame.Navigate(typeof(InventoryPage));
                return;
            case "reports":
                ContentFrame.Navigate(typeof(ReportsPage));
                return;
            case "quotes":
                ContentFrame.Navigate(typeof(QuotesPage));
                return;
            case "invoices":
                ContentFrame.Navigate(typeof(GenericListPage), new GenericListArgs("Invoices", "api/invoices"));
                return;
            case "expenses":
                ContentFrame.Navigate(typeof(ExpensesPage));
                return;
            case "pos":
                ContentFrame.Navigate(typeof(PosPage));
                return;
            case "purchasing":
                ContentFrame.Navigate(typeof(GenericListPage), new GenericListArgs("Purchase Orders", "api/purchase-orders"));
                return;
            case "calendar":
                ContentFrame.Navigate(typeof(CalendarPage));
                return;
            case "used":
                ContentFrame.Navigate(typeof(UsedTechPage));
                return;
            case "notifications":
                ContentFrame.Navigate(typeof(GenericListPage), new GenericListArgs("Notifications", "api/notifications"));
                return;
            case "ai":
                ContentFrame.Navigate(typeof(AiAssistPage));
                return;
            case "knowledge":
                ContentFrame.Navigate(typeof(GenericListPage), new GenericListArgs("Knowledge Base", "api/knowledge"));
                return;
        }

        ContentFrame.Navigate(typeof(PlaceholderPage), new PlaceholderArgs(key, phase));
    }

    private void NavigateSearchRoute(string route)
    {
        if (string.IsNullOrWhiteSpace(route)) return;
        var path = route.Trim();

        if (path.StartsWith("/repairs/new", StringComparison.OrdinalIgnoreCase))
        {
            ViewModel.CurrentPageTitle = "New repair";
            ContentFrame.Navigate(typeof(NewRepairPage));
            SelectNavKey("repairs");
            return;
        }
        if (path.StartsWith("/repairs/", StringComparison.OrdinalIgnoreCase) &&
            Guid.TryParse(path["/repairs/".Length..].Trim('/'), out var repairId))
        {
            ContentFrame.Navigate(typeof(RepairDetailPage), repairId);
            SelectNavKey("repairs");
            return;
        }
        if (path.StartsWith("/customers/new", StringComparison.OrdinalIgnoreCase))
        {
            ContentFrame.Navigate(typeof(CustomersPage));
            SelectNavKey("customers");
            return;
        }
        if (path.StartsWith("/customers/", StringComparison.OrdinalIgnoreCase) &&
            Guid.TryParse(path["/customers/".Length..].Trim('/'), out var customerId))
        {
            ContentFrame.Navigate(typeof(CustomerDetailPage), customerId);
            SelectNavKey("customers");
            return;
        }
        if (path.StartsWith("/quotes", StringComparison.OrdinalIgnoreCase))
        {
            ContentFrame.Navigate(typeof(QuotesPage));
            SelectNavKey("quotes");
            return;
        }
        if (path.StartsWith("/invoices", StringComparison.OrdinalIgnoreCase))
        {
            ContentFrame.Navigate(typeof(GenericListPage), new GenericListArgs("Invoices", "api/invoices"));
            SelectNavKey("invoices");
            return;
        }
        if (path.StartsWith("/expenses", StringComparison.OrdinalIgnoreCase))
        {
            ContentFrame.Navigate(typeof(ExpensesPage));
            SelectNavKey("expenses");
            return;
        }
        if (path.StartsWith("/pos", StringComparison.OrdinalIgnoreCase))
        {
            ContentFrame.Navigate(typeof(PosPage));
            SelectNavKey("pos");
            return;
        }
        if (path.StartsWith("/inventory", StringComparison.OrdinalIgnoreCase))
        {
            ContentFrame.Navigate(typeof(InventoryPage));
            SelectNavKey("inventory");
            return;
        }
        if (path.StartsWith("/calendar", StringComparison.OrdinalIgnoreCase))
        {
            ContentFrame.Navigate(typeof(CalendarPage));
            SelectNavKey("calendar");
            return;
        }
        if (path.StartsWith("/used", StringComparison.OrdinalIgnoreCase))
        {
            ContentFrame.Navigate(typeof(UsedTechPage));
            SelectNavKey("used");
            return;
        }
        if (path.StartsWith("/purchasing", StringComparison.OrdinalIgnoreCase))
        {
            ContentFrame.Navigate(typeof(GenericListPage), new GenericListArgs("Purchase Orders", "api/purchase-orders"));
            SelectNavKey("purchasing");
            return;
        }
        if (path.StartsWith("/settings", StringComparison.OrdinalIgnoreCase))
        {
            ContentFrame.Navigate(typeof(SettingsPage), path.Contains("users", StringComparison.OrdinalIgnoreCase) ? "users" : null);
            return;
        }

        ViewModel.NavigateByKey("dashboard");
    }

    private void NavView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (args.IsSettingsInvoked)
        {
            ViewModel.CurrentPageTitle = "Settings";
            ContentFrame.Navigate(typeof(SettingsPage));
            return;
        }

        if (args.InvokedItemContainer is NavigationViewItem item && item.Tag is string key)
            ViewModel.NavigateByKey(key);
    }

    private void SelectNavKey(string key)
    {
        foreach (var obj in NavView.MenuItems)
        {
            if (obj is NavigationViewItem item)
            {
                if (item.Tag as string == key)
                {
                    NavView.SelectedItem = item;
                    return;
                }
                foreach (var child in item.MenuItems.OfType<NavigationViewItem>())
                {
                    if (child.Tag as string == key)
                    {
                        NavView.SelectedItem = child;
                        return;
                    }
                }
            }
        }
    }

    private void OnSearchKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            if (ViewModel.SelectedSearchHit is not null)
                ViewModel.ActivateSearchHitCommand.Execute(ViewModel.SelectedSearchHit);
            else if (ViewModel.SearchHits.Count > 0)
                ViewModel.ActivateSearchHitCommand.Execute(ViewModel.SearchHits[0]);
            else
                ViewModel.SearchCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == VirtualKey.Escape)
        {
            ViewModel.CloseCommandCentreCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == VirtualKey.Down && ViewModel.SearchHits.Count > 0)
        {
            var idx = ViewModel.SelectedSearchHit is null ? -1 : ViewModel.SearchHits.IndexOf(ViewModel.SelectedSearchHit);
            idx = Math.Min(ViewModel.SearchHits.Count - 1, idx + 1);
            ViewModel.SelectedSearchHit = ViewModel.SearchHits[idx];
            e.Handled = true;
        }
        else if (e.Key == VirtualKey.Up && ViewModel.SearchHits.Count > 0)
        {
            var idx = ViewModel.SelectedSearchHit is null ? 0 : ViewModel.SearchHits.IndexOf(ViewModel.SelectedSearchHit);
            idx = Math.Max(0, idx - 1);
            ViewModel.SelectedSearchHit = ViewModel.SearchHits[idx];
            e.Handled = true;
        }
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (ViewModel.IsCommandCentreOpen)
            ViewModel.SearchCommand.Execute(null);
    }

    private void SearchHit_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is SearchHitRow hit)
            ViewModel.ActivateSearchHitCommand.Execute(hit);
    }

    private void CommandCentreBackdrop_Tapped(object sender, TappedRoutedEventArgs e)
    {
        ViewModel.CloseCommandCentreCommand.Execute(null);
    }

    private void CommandCentrePanel_Tapped(object sender, TappedRoutedEventArgs e) => e.Handled = true;

    private async void Notifications_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.RefreshNotificationsAsync(showBannerForNew: false);
    }

    private void OpenAllNotifications_Click(object sender, RoutedEventArgs e)
    {
        NotificationsFlyout.Hide();
        ViewModel.CurrentPageTitle = "Notifications";
        ContentFrame.Navigate(typeof(GenericListPage), new GenericListArgs("Notifications", "api/notifications"));
    }

    private async void NotificationItem_Click(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not NotificationDto note) return;
        NotificationsFlyout.Hide();
        if (!note.IsRead)
            await ViewModel.MarkNotificationReadAsync(note.Id);
        ViewModel.CurrentPageTitle = "Notifications";
        ContentFrame.Navigate(typeof(GenericListPage), new GenericListArgs("Notifications", "api/notifications"));
    }

    private void NewTicket_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.CurrentPageTitle = "New repair";
        ContentFrame.Navigate(typeof(NewRepairPage));
        SelectNavKey("repairs");
    }
}
