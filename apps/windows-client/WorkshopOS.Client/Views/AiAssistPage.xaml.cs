using Microsoft.UI.Xaml.Controls;
using WorkshopOS.Client.Services;

namespace WorkshopOS.Client.Views;

public sealed partial class AiAssistPage : Page
{
    public AiAssistViewModel ViewModel { get; } = new(App.Services.GetRequiredService<ApiClient>());

    public AiAssistPage()
    {
        InitializeComponent();
        DataContext = ViewModel;
    }
}
