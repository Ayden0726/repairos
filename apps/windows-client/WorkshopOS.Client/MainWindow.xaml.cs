using Microsoft.UI.Xaml;

namespace WorkshopOS.Client;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Title = "WorkshopOS";
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        RootFrame.Navigate(typeof(Views.BootstrapPage));
    }
}
