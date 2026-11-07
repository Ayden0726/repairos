using Microsoft.UI.Xaml.Controls;
using WorkshopOS.Client.Services;

namespace WorkshopOS.Client.Views;

public sealed partial class AuditLogPage : Page
{
    public AuditLogViewModel ViewModel { get; }

    public AuditLogPage()
    {
        ViewModel = new AuditLogViewModel(App.Services.GetRequiredService<ApiClient>());
        InitializeComponent();
        DataContext = ViewModel;
        Loaded += async (_, _) => await ViewModel.RefreshCommand.ExecuteAsync(null);
    }
}
