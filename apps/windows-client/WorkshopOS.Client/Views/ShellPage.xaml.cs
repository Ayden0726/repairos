using Microsoft.UI.Xaml.Controls;
using WorkshopOS.Client.ViewModels;
using WorkshopOS.Contracts.Common;

namespace WorkshopOS.Client.Views;

public sealed partial class ShellPage : Page
{
    public ShellViewModel ViewModel { get; }

    public ShellPage()
    {
        ViewModel = App.Services.GetRequiredService<ShellViewModel>();
        InitializeComponent();
        DataContext = ViewModel;
        ViewModel.OpenModule = (key, phase, implemented) =>
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
                        ContentFrame.Navigate(typeof(UsersPage));
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
                        ContentFrame.Navigate(typeof(GenericListPage), new GenericListArgs("Quotes", "api/quotes"));
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
                    case "knowledge":
                        ContentFrame.Navigate(typeof(GenericListPage), new GenericListArgs("Knowledge Base", "api/knowledge"));
                        return;
                    case "notifications":
                        ContentFrame.Navigate(typeof(GenericListPage), new GenericListArgs("Notifications", "api/notifications"));
                        return;
                    case "backups":
                        ContentFrame.Navigate(typeof(BackupsPage));
                        return;
                    case "ai":
                        ContentFrame.Navigate(typeof(AiAssistPage));
                        return;
                }
            }
            ContentFrame.Navigate(typeof(PlaceholderPage), new PlaceholderArgs(key, phase));
        };
        ViewModel.LoggedOut = () => Frame.Navigate(typeof(LoginPage));
        Loaded += async (_, _) =>
        {
            await ViewModel.LoadCommand.ExecuteAsync(null);
            ContentFrame.Navigate(typeof(DashboardPage));
        };
    }

    private void OnSearchKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
            ViewModel.SearchCommand.Execute(null);
    }

    private void Module_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is ModuleDto module)
            ViewModel.NavigateModuleCommand.Execute(module);
    }

    private void Notifications_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) =>
        ContentFrame.Navigate(typeof(GenericListPage), new GenericListArgs("Notifications", "api/notifications"));
}
