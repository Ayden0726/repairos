using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace WorkshopOS.Client.Views;

public sealed record PlaceholderArgs(string Key, int Phase, string? Message = null);

public sealed partial class PlaceholderPage : Page
{
    public PlaceholderPage() => InitializeComponent();

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        if (e.Parameter is PlaceholderArgs args)
        {
            if (!string.IsNullOrWhiteSpace(args.Message))
            {
                TitleText.Text = "Access restricted";
                BodyText.Text = args.Message;
                return;
            }

            TitleText.Text = "Not yet implemented";
            BodyText.Text = $"The “{args.Key}” module arrives in Phase {args.Phase}. No data is written from this screen.";
        }
    }
}
