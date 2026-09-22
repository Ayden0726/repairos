using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WorkshopOS.Client.ViewModels;

namespace WorkshopOS.Client.Views;

public sealed partial class SetupPage : Page
{
    public SetupViewModel ViewModel { get; }

    public SetupPage()
    {
        ViewModel = App.Services.GetRequiredService<SetupViewModel>();
        InitializeComponent();
        DataContext = ViewModel;
        ViewModel.OnCompleted = () => Frame.Navigate(typeof(LoginPage));
    }

    private void OwnerPassword_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is PasswordBox box)
            ViewModel.OwnerPassword = box.Password;
    }
}
