using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace WorkshopOS.Client.Views;

/// <summary>
/// Legacy splash. MainWindow no longer navigates here (1.2.4+).
/// If reached somehow, immediately leave for ServerConnect — never probe/block.
/// </summary>
public sealed partial class BootstrapPage : Page
{
    private bool _navigated;

    public BootstrapPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e) =>
        GoConnect("Enter a pairing code from http://<server>:5088/connect, or paste the server URL.");

    private void EnterServer_Click(object sender, RoutedEventArgs e) =>
        GoConnect("Enter the pairing code from http://<server>:5088/connect, or paste the server URL.");

    private void GoConnect(string? message)
    {
        if (_navigated) return;
        _navigated = true;
        try
        {
            Frame.Navigate(typeof(ServerConnectPage), message);
        }
        catch
        {
            /* frame may already be navigating */
        }
    }
}
