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
            if (implemented && key == "settings")
            {
                ContentFrame.Navigate(typeof(SettingsPage));
                return;
            }
            if (implemented && key == "users")
            {
                ContentFrame.Navigate(typeof(UsersPage));
                return;
            }
            ContentFrame.Navigate(typeof(PlaceholderPage), new PlaceholderArgs(key, phase));
        };
        ViewModel.LoggedOut = () => Frame.Navigate(typeof(LoginPage));
        Loaded += async (_, _) =>
        {
            await ViewModel.LoadCommand.ExecuteAsync(null);
            ContentFrame.Navigate(typeof(PlaceholderPage), new PlaceholderArgs("dashboard", 3));
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
}
