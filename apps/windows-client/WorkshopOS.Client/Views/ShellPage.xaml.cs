using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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
        ViewModel.LoggedOut = () => Frame.Navigate(typeof(LoginPage));
        Unloaded += (_, _) => ViewModel.StopNotificationPolling();
        Loaded += async (_, _) =>
        {
            await ViewModel.LoadCommand.ExecuteAsync(null);
            BuildNavigation();
            ContentFrame.Navigate(typeof(DashboardPage));
            SelectNavKey("dashboard");
        };
    }

    private void BuildNavigation()
    {
        NavView.MenuItems.Clear();
        var byKey = ViewModel.Modules.ToDictionary(m => m.Key, StringComparer.OrdinalIgnoreCase);

        foreach (var (title, keys) in ShellViewModel.NavGroups)
        {
            var visible = keys
                .Select(k => byKey.TryGetValue(k, out var m) ? m : null)
                .Where(m => m is not null && m.Visible)
                .Cast<ModuleDto>()
                .ToList();

            // When modules failed to load, still show the designed groups offline.
            if (visible.Count == 0 && ViewModel.IsOffline)
            {
                visible = keys.Select(k => new ModuleDto(k, title, TitleForKey(k), 1, true, true)).ToList();
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
        "repairs" => "Tickets",
        "quotes" => "Quotes",
        "invoices" => "Invoices",
        "calendar" => "Calendar",
        "builds" => "PC Builds",
        "inventory" => "Inventory",
        "purchasing" => "Purchasing",
        "used" => "Used Tech",
        "customers" => "Customers",
        "reports" => "Reports",
        "notifications" => "Notifications",
        "backups" => "Backups",
        _ => key
    };

    private static Symbol IconForGroup(string title) => title switch
    {
        "Work" => Symbol.Repair,
        "Inventory" => Symbol.Shop,
        _ => Symbol.AllApps
    };

    private static Symbol IconForKey(string key) => key switch
    {
        "dashboard" => Symbol.Home,
        "repairs" => Symbol.Repair,
        "quotes" => Symbol.Document,
        "invoices" => Symbol.Calculator,
        "calendar" => Symbol.Calendar,
        "builds" => Symbol.Setting,
        "inventory" => Symbol.Shop,
        "purchasing" => Symbol.Shop,
        "used" => Symbol.Tag,
        "customers" => Symbol.People,
        "reports" => Symbol.View,
        "notifications" => Symbol.Message,
        "backups" => Symbol.Save,
        _ => Symbol.Page
    };

    private void NavigateToModule(string key, int phase, bool implemented)
    {
        if (implemented)
        {
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
                case "purchasing":
                    ContentFrame.Navigate(typeof(GenericListPage), new GenericListArgs("Purchase Orders", "api/purchase-orders"));
                    return;
                case "calendar":
                    ContentFrame.Navigate(typeof(GenericListPage), new GenericListArgs("Calendar / Bookings", "api/bookings"));
                    return;
                case "builds":
                    ContentFrame.Navigate(typeof(GenericListPage), new GenericListArgs("PC Builds", "api/builds"));
                    return;
                case "used":
                    ContentFrame.Navigate(typeof(GenericListPage), new GenericListArgs("Used Tech", "api/used-tech"));
                    return;
                case "notifications":
                    ContentFrame.Navigate(typeof(GenericListPage), new GenericListArgs("Notifications", "api/notifications"));
                    return;
            }
        }
        ContentFrame.Navigate(typeof(PlaceholderPage), new PlaceholderArgs(key, phase));
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

    private void OnSearchKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
            ViewModel.SearchCommand.Execute(null);
    }

    private async void Notifications_Click(object sender, RoutedEventArgs e)
    {
        // Button.Flyout opens automatically; refresh list contents for the flyout.
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
        ViewModel.CurrentPageTitle = "New ticket";
        ContentFrame.Navigate(typeof(NewRepairPage));
        SelectNavKey("repairs");
    }
}
