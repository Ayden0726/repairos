using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WorkshopOS.Client.ViewModels;

namespace WorkshopOS.Client.Views;

public sealed partial class LoginPage : Page
{
    public LoginViewModel ViewModel { get; }

    public LoginPage()
    {
        ViewModel = App.Services.GetRequiredService<LoginViewModel>();
        InitializeComponent();
        DataContext = ViewModel;
        ViewModel.OnLoggedIn = () => Frame.Navigate(typeof(ShellPage));
    }

    private void Password_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is PasswordBox box)
            ViewModel.Password = box.Password;
    }
}
