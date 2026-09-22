using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WorkshopOS.Client.Services;

namespace WorkshopOS.Client.Views;

public sealed partial class CustomerDetailPage : Page
{
    public CustomerDetailViewModel ViewModel { get; } = new(App.Services.GetRequiredService<ApiClient>());

    public CustomerDetailPage()
    {
        InitializeComponent();
        DataContext = ViewModel;
    }

    protected override async void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        if (e.Parameter is Guid id)
            await ViewModel.LoadAsync(id);
    }

    private void NewRepair_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.Customer is not null)
            Frame.Navigate(typeof(NewRepairPage), ViewModel.Customer.Id);
    }
}
