using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WorkshopOS.Client.Services;
using WorkshopOS.Contracts.Finance;

namespace WorkshopOS.Client.Views;

public sealed partial class ExpensesPage : Page
{
    private readonly ApiClient _api = App.Services.GetRequiredService<ApiClient>();
    private Guid? _editingId;
    private IReadOnlyList<ExpenseCategoryDto> _categories = Array.Empty<ExpenseCategoryDto>();
    private string? _pendingEdits;

    public ExpensesPage()
    {
        InitializeComponent();
        Loaded += async (_, _) => await ReloadAsync();
        AmountBox.ValueChanged += async (_, _) => await UpdateGstPreviewAsync();
        GstCombo.SelectionChanged += async (_, _) => await UpdateGstPreviewAsync();
        PurchaseDatePicker.Date = DateTimeOffset.Now;
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await ReloadAsync();
    private async void Search_Click(object sender, RoutedEventArgs e) => await LoadListAsync();

    private async Task ReloadAsync()
    {
        ListError.Text = string.Empty;
        FormError.Text = string.Empty;
        try
        {
            var dash = await _api.GetAsync<ExpenseDashboardDto>("api/expenses/dashboard");
            DashMonth.Text = $"${dash.TotalThisMonth:N2}";
            DashFy.Text = $"${dash.TotalThisFinancialYear:N2}";
            DashGst.Text = $"${dash.GstPaidEligible:N2}";
            DashUnpaid.Text = $"${dash.UnpaidTotal:N2}";
            DashOverdue.Text = $"${dash.OverdueTotal:N2}";
            DashRecurring.Text = dash.RecurringActiveCount.ToString();
            DashNote.Text = dash.AccountingBasisNote;

            _categories = await _api.GetAsync<IReadOnlyList<ExpenseCategoryDto>>("api/expenses/categories");
            CategoryCombo.ItemsSource = _categories;
            if (CategoryCombo.SelectedIndex < 0 && _categories.Count > 0)
                CategoryCombo.SelectedIndex = 0;

            await LoadListAsync();
        }
        catch (Exception ex)
        {
            ListError.Text = ex.Message;
        }
    }

    private async Task LoadListAsync()
    {
        ListStatus.Text = "Loading…";
        ListError.Text = string.Empty;
        try
        {
            var status = StatusFilter.SelectedItem as string;
            if (string.Equals(status, "All", StringComparison.OrdinalIgnoreCase)) status = null;
            var q = Uri.EscapeDataString(SearchBox.Text?.Trim() ?? "");
            var path = $"api/expenses?q={q}";
            if (!string.IsNullOrWhiteSpace(status))
                path += $"&status={Uri.EscapeDataString(status)}";
            var rows = await _api.GetAsync<IReadOnlyList<ExpenseListItemDto>>(path);
            ExpensesList.ItemsSource = rows;
            ListStatus.Text = $"{rows.Count} expense(s)";
        }
        catch (Exception ex)
        {
            ListError.Text = ex.Message;
            ListStatus.Text = string.Empty;
        }
    }

    private async void ExpensesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ExpensesList.SelectedItem is not ExpenseListItemDto item) return;
        FormError.Text = string.Empty;
        try
        {
            var detail = await _api.GetAsync<ExpenseDetailDto>($"api/expenses/{item.Id}");
            _editingId = detail.Id;
            TitleBox.Text = detail.Title;
            SupplierBox.Text = detail.SupplierName ?? "";
            AmountBox.Value = (double)detail.TotalAmount;
            PaymentMethodBox.Text = detail.PaymentMethod ?? "";
            InvoiceRefBox.Text = detail.InvoiceReference ?? "";
            NotesBox.Text = detail.Notes ?? "";
            InventoryLinkedCheck.IsChecked = detail.IsInventoryOrCogsLinked;
            PurchaseDatePicker.Date = new DateTimeOffset(detail.PurchaseDate.ToDateTime(TimeOnly.MinValue));
            SelectCombo(StatusCombo, detail.Status);
            SelectCombo(GstCombo, detail.GstTreatment);
            if (detail.CategoryId is Guid cid)
                CategoryCombo.SelectedItem = _categories.FirstOrDefault(c => c.Id == cid);
            FormStatus.Text = $"Editing {detail.Title}";
            await UpdateGstPreviewAsync();
        }
        catch (Exception ex)
        {
            FormError.Text = ex.Message;
        }
    }

    private void New_Click(object sender, RoutedEventArgs e)
    {
        _editingId = null;
        TitleBox.Text = "";
        SupplierBox.Text = "";
        AmountBox.Value = 0;
        PaymentMethodBox.Text = "";
        InvoiceRefBox.Text = "";
        NotesBox.Text = "";
        InventoryLinkedCheck.IsChecked = false;
        PurchaseDatePicker.Date = DateTimeOffset.Now;
        StatusCombo.SelectedIndex = 1;
        GstCombo.SelectedIndex = 0;
        FormStatus.Text = "New expense";
        FormError.Text = string.Empty;
        GstPreviewText.Text = string.Empty;
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        FormError.Text = string.Empty;
        _pendingEdits = CaptureForm();
        try
        {
            var purchase = PurchaseDatePicker.Date?.Date ?? DateTime.Today;
            var cat = CategoryCombo.SelectedItem as ExpenseCategoryDto;
            var request = new UpsertExpenseRequest(
                Title: TitleBox.Text?.Trim() ?? "",
                Description: null,
                CategoryId: cat?.Id,
                SupplierId: null,
                SupplierName: string.IsNullOrWhiteSpace(SupplierBox.Text) ? null : SupplierBox.Text.Trim(),
                PurchaseDate: DateOnly.FromDateTime(purchase),
                PaymentDate: null,
                InvoiceReference: string.IsNullOrWhiteSpace(InvoiceRefBox.Text) ? null : InvoiceRefBox.Text.Trim(),
                Status: StatusCombo.SelectedItem as string,
                TotalAmount: (decimal)(AmountBox.Value is double.NaN ? 0 : AmountBox.Value),
                GstTreatment: GstCombo.SelectedItem as string ?? "Inclusive",
                AmountExGst: null,
                GstAmount: null,
                PaymentMethod: string.IsNullOrWhiteSpace(PaymentMethodBox.Text) ? null : PaymentMethodBox.Text.Trim(),
                AmountPaid: null,
                RefundAmount: null,
                BusinessUsePercent: 100m,
                Notes: string.IsNullOrWhiteSpace(NotesBox.Text) ? null : NotesBox.Text.Trim(),
                RepairTicketId: null,
                InventoryItemId: null,
                PurchaseOrderId: null,
                IsInventoryOrCogsLinked: InventoryLinkedCheck.IsChecked == true);

            ExpenseDetailDto saved;
            if (_editingId is Guid id)
                saved = await _api.PutAsync<UpsertExpenseRequest, ExpenseDetailDto>($"api/expenses/{id}", request);
            else
                saved = await _api.PostAsync<UpsertExpenseRequest, ExpenseDetailDto>("api/expenses", request);

            _editingId = saved.Id;
            FormStatus.Text = "Saved on server.";
            _pendingEdits = null;
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            FormError.Text = ex.Message + (_pendingEdits is null ? "" : " Your edits are still in the form.");
            FormStatus.Text = "Save failed — changes were not discarded.";
        }
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (_editingId is not Guid id) return;
        FormError.Text = string.Empty;
        try
        {
            await _api.DeleteAsync($"api/expenses/{id}");
            FormStatus.Text = "Expense archived (soft delete).";
            New_Click(sender, e);
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            FormError.Text = ex.Message;
        }
    }

    private async void Export_Click(object sender, RoutedEventArgs e)
    {
        ListError.Text = string.Empty;
        try
        {
            var csv = await _api.GetRawAsync("api/expenses/export/csv");
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "WorkshopOS");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, $"expenses-{DateTime.Now:yyyyMMdd-HHmmss}.csv");
            await File.WriteAllTextAsync(path, csv);
            ListStatus.Text = $"Exported to {path}";
        }
        catch (Exception ex)
        {
            ListError.Text = ex.Message;
        }
    }

    private async Task UpdateGstPreviewAsync()
    {
        try
        {
            var amount = (decimal)(AmountBox.Value is double.NaN ? 0 : AmountBox.Value);
            var treatment = GstCombo.SelectedItem as string ?? "Inclusive";
            var result = await _api.PostAsync<GstCalcRequest, GstCalcResult>(
                "api/expenses/gst/preview", new GstCalcRequest(amount, treatment));
            GstPreviewText.Text =
                $"Ex GST ${result.AmountExGst:N2} · GST ${result.GstAmount:N2} · Total ${result.TotalAmount:N2} ({result.Treatment})";
        }
        catch
        {
            GstPreviewText.Text = string.Empty;
        }
    }

    private string CaptureForm() =>
        $"{TitleBox.Text}|{SupplierBox.Text}|{AmountBox.Value}|{GstCombo.SelectedItem}|{StatusCombo.SelectedItem}|{NotesBox.Text}";

    private static void SelectCombo(ComboBox box, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        foreach (var item in box.Items.OfType<string>())
        {
            if (string.Equals(item, value, StringComparison.OrdinalIgnoreCase))
            {
                box.SelectedItem = item;
                return;
            }
        }
    }
}
