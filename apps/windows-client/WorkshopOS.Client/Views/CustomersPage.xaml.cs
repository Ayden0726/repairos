using Microsoft.UI.Xaml.Controls;
using WorkshopOS.Client.Services;
using WorkshopOS.Contracts.Workshop;

namespace WorkshopOS.Client.Views;

public sealed partial class CustomersPage : Page
{
    public CustomersViewModel ViewModel { get; }

    public CustomersPage()
    {
        ViewModel = new CustomersViewModel(App.Services.GetRequiredService<ApiClient>());
        InitializeComponent();
        DataContext = ViewModel;
        Loaded += async (_, _) => await ViewModel.RefreshCommand.ExecuteAsync(null);
    }

    private void Customer_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is CustomerListItemDto c)
            Frame.Navigate(typeof(CustomerDetailPage), c.Id);
    }
}
