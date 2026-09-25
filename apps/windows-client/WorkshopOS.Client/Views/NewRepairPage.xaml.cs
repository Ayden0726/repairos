using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using WorkshopOS.Client.Services;

namespace WorkshopOS.Client.Views;

public sealed partial class NewRepairPage : Page
{
    public NewRepairViewModel ViewModel { get; }
    private Guid? _navCustomerId;
    private bool _loaded;

    public NewRepairPage()
    {
        ViewModel = new NewRepairViewModel(App.Services.GetRequiredService<ApiClient>());
        InitializeComponent();
        DataContext = ViewModel;
        Loaded += async (_, _) =>
        {
            if (_loaded) return;
            _loaded = true;
            await ViewModel.InitAsync(_navCustomerId);
        };
        ViewModel.Created = id => Frame.Navigate(typeof(RepairDetailPage), id);
    }

    protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        if (e.Parameter is Guid customerId)
            _navCustomerId = customerId;
    }

    private void CustomerSearch_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter && ViewModel.SearchCustomersCommand.CanExecute(null))
        {
            ViewModel.SearchCustomersCommand.Execute(null);
            e.Handled = true;
        }
    }
}
