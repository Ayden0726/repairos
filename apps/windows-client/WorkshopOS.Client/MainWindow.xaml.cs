using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace WorkshopOS.Client;

public sealed partial class MainWindow : Window
{
    public Frame AppRootFrame => RootFrame;

    public MainWindow()
    {
        InitializeComponent();
        Title = "WorkshopOS";
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        // ALWAYS open ServerConnect (pairing / URL) first.
        // Never start on Bootstrap / "Connecting to server…" — that splash hung users
        // whenever a stale ServerUrl existed. Connect UI works offline; user connects when ready.
        RootFrame.Navigate(typeof(Views.ServerConnectPage));
    }
}
