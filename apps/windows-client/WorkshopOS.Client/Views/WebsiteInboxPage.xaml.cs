using Microsoft.UI.Xaml.Controls;
using WorkshopOS.Client.Services;

namespace WorkshopOS.Client.Views;

public sealed partial class WebsiteInboxPage : Page
{
    public WebsiteInboxViewModel ViewModel { get; } = new(App.Services.GetRequiredService<ApiClient>());

    public WebsiteInboxPage()
    {
        InitializeComponent();
        DataContext = ViewModel;
        Loaded += async (_, _) => await ViewModel.RefreshCommand.ExecuteAsync(null);
    }
}
