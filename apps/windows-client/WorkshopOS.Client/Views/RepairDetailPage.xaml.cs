using Microsoft.UI.Xaml.Controls;
using WorkshopOS.Client.Services;

namespace WorkshopOS.Client.Views;

public sealed partial class RepairDetailPage : Page
{
    public RepairDetailViewModel ViewModel { get; } = new(App.Services.GetRequiredService<ApiClient>());

    public RepairDetailPage()
    {
        InitializeComponent();
        DataContext = ViewModel;
    }

    protected override async void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        if (e.Parameter is Guid id)
            await ViewModel.LoadAsync(id);
    }
}
