using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using WorkshopOS.Client.Services;

namespace WorkshopOS.Client.Views;

public sealed partial class CalendarPage : Page
{
    public CalendarViewModel ViewModel { get; }

    public CalendarPage()
    {
        ViewModel = new CalendarViewModel(
            App.Services.GetRequiredService<ApiClient>(),
            App.Services.GetRequiredService<AuthSession>());
        InitializeComponent();
        DataContext = ViewModel;
        Loaded += async (_, _) => await ViewModel.RefreshCommand.ExecuteAsync(null);
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is string s &&
            (string.Equals(s, "new", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(s, "new-booking", StringComparison.OrdinalIgnoreCase)))
        {
            ViewModel.RequestNewBookingOnLoad();
        }
    }

    private void Day_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: CalendarDayVm day })
            ViewModel.SelectDay(day);
    }

    private void Booking_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is CalendarBookingChipVm chip)
            ViewModel.SelectBooking(chip);
    }
}
