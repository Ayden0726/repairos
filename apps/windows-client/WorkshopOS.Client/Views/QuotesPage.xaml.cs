using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WorkshopOS.Client.Services;
using WorkshopOS.Contracts.Operations;

namespace WorkshopOS.Client.Views;

public sealed partial class QuotesPage : Page
{
    public QuotesListViewModel ViewModel { get; }

    public QuotesPage()
    {
        ViewModel = new QuotesListViewModel(App.Services.GetRequiredService<ApiClient>());
        InitializeComponent();
        DataContext = ViewModel;
        Loaded += async (_, _) => await ViewModel.RefreshCommand.ExecuteAsync(null);
    }

    private void NewQuote_Click(object sender, RoutedEventArgs e) =>
        Frame.Navigate(typeof(QuoteBuilderPage), new QuoteBuilderArgs());

    private void Quotes_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is QuoteListItemDto item)
            Frame.Navigate(typeof(QuoteBuilderPage), new QuoteBuilderArgs(QuoteId: item.Id));
    }
}
