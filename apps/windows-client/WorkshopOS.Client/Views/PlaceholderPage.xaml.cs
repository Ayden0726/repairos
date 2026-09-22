using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace WorkshopOS.Client.Views;

public sealed record PlaceholderArgs(string Key, int Phase);

public sealed partial class PlaceholderPage : Page
{
    public PlaceholderPage() => InitializeComponent();

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        if (e.Parameter is PlaceholderArgs args)
        {
            TitleText.Text = "Not yet implemented";
            BodyText.Text = $"The “{args.Key}” module arrives in Phase {args.Phase}. No data is written from this screen.";
        }
    }
}
