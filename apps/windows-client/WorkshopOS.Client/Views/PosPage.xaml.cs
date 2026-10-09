using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WorkshopOS.Client.Services;
using WorkshopOS.Contracts.Operations;

namespace WorkshopOS.Client.Views;

public sealed partial class PosPage : Page
{
    public PosViewModel ViewModel { get; } = new(App.Services.GetRequiredService<ApiClient>());

    public PosPage()
    {
        InitializeComponent();
        DataContext = ViewModel;
        Loaded += async (_, _) => await ViewModel.InitializeAsync();
    }

    private void Product_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is InventoryListItemDto item)
            ViewModel.AddProduct(item);
    }

    private void RemoveCart_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: PosCartLine line })
            ViewModel.RemoveLine(line);
    }
}
