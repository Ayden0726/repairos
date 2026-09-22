using Microsoft.UI.Xaml.Controls;
using WorkshopOS.Client.Services;

namespace WorkshopOS.Client.Views;

public sealed partial class NewRepairPage : Page
{
    public NewRepairViewModel ViewModel { get; }

    public NewRepairPage()
    {
        ViewModel = new NewRepairViewModel(App.Services.GetRequiredService<ApiClient>());
        InitializeComponent();
        DataContext = ViewModel;
        Loaded += async (_, _) => await ViewModel.InitAsync(null);
        ViewModel.Created = id => Frame.Navigate(typeof(RepairDetailPage), id);
    }

    protected override async void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        if (e.Parameter is Guid customerId)
            await ViewModel.InitAsync(customerId);
    }
}
