using Microsoft.UI.Xaml.Controls;

namespace WorkshopOS.Client.Views;

public sealed partial class UsersPage : Page
{
    public UsersPage()
    {
        InitializeComponent();
        BodyText.Text =
            "Staff listing is available via API roles/permissions. A full Add Staff form ships next; Phase 1 keeps Users as a real navigation target without fake CRUD.";
    }
}
