using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WorkshopOS.Client.Services;
using WorkshopOS.Contracts.Operations;
using WorkshopOS.Contracts.Workshop;

namespace WorkshopOS.Client.Views;

public sealed record QuoteBuilderArgs(Guid? QuoteId = null, Guid? RepairTicketId = null, Guid? CustomerId = null);

public partial class QuotesListViewModel : ObservableObject
{
    private readonly ApiClient _api;
    public ObservableCollection<QuoteListItemDto> Items { get; } = new();
    [ObservableProperty] private string _query = string.Empty;
    [ObservableProperty] private string _statusFilter = "All";
    [ObservableProperty] private string? _error;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private bool _isLoading;

    public QuotesListViewModel(ApiClient api) => _api = api;

    [RelayCommand]
    private async Task RefreshAsync()
    {
        Error = null;
        IsLoading = true;
        try
        {
            var status = StatusFilter is "All" or null or "" ? null : StatusFilter;
            var q = string.IsNullOrWhiteSpace(Query) ? null : Query.Trim();
            var path = "api/quotes";
            var parts = new List<string>();
            if (q is not null) parts.Add($"q={Uri.EscapeDataString(q)}");
            if (status is not null) parts.Add($"status={Uri.EscapeDataString(status)}");
            if (parts.Count > 0) path += "?" + string.Join("&", parts);
            var list = await _api.GetAsync<QuoteListItemDto[]>(path);
            Items.Clear();
            foreach (var item in list) Items.Add(item);
            StatusMessage = Items.Count == 0 ? "No quotes yet." : $"{Items.Count} quote(s)";
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }
}

public partial class QuoteLineDraft : ObservableObject
{
    [ObservableProperty] private string _type = "PART";
    [ObservableProperty] private string _description = string.Empty;
    [ObservableProperty] private string? _serviceName;
    [ObservableProperty] private string? _partName;
    [ObservableProperty] private string? _supplierName;
    // Non-null string: WinUI x:Bind TwoWay to TextBox.Text rejects string?.
    [ObservableProperty] private string _sku = string.Empty;
    [ObservableProperty] private Guid? _inventoryItemId;
    [ObservableProperty] private Guid? _servicePricingId;
    [ObservableProperty] private string? _difficultyLevelKey;
    // NumberBox.Value is double — decimal TwoWay x:Bind fails XamlCompiler (MSB3073).
    [ObservableProperty] private double _quantity = 1d;
    [ObservableProperty] private double _partCost;
    [ObservableProperty] private double _shippingCost;
    [ObservableProperty] private double _otherCost;
    [ObservableProperty] private double _markupPercent;
    [ObservableProperty] private double _labourAmount;
    [ObservableProperty] private double _additionalAmount;
    [ObservableProperty] private double _discountAmount;
    [ObservableProperty] private double _partSell;
    [ObservableProperty] private double _lineTotal;
    [ObservableProperty] private double _lineProfit;

    public string PartSellText => PartSell.ToString("0.00");
    public string LineTotalText => LineTotal.ToString("0.00");

    partial void OnPartSellChanged(double value) => OnPropertyChanged(nameof(PartSellText));
    partial void OnLineTotalChanged(double value) => OnPropertyChanged(nameof(LineTotalText));
}

public partial class QuoteBuilderViewModel : ObservableObject
{
    private readonly ApiClient _api;
    private readonly AuthSession _session;

    public ObservableCollection<QuoteLineDraft> Lines { get; } = new();
    public ObservableCollection<CustomerListItemDto> Customers { get; } = new();
    public ObservableCollection<InventoryListItemDto> InventoryParts { get; } = new();
    public ObservableCollection<ServicePricingDto> Services { get; } = new();

    [ObservableProperty] private Guid? _quoteId;
    [ObservableProperty] private Guid? _repairTicketId;
    [ObservableProperty] private string? _quoteNumber;
    [ObservableProperty] private string _status = "Draft";
    [ObservableProperty] private bool _useExistingCustomer = true;
    [ObservableProperty] private string _customerQuery = string.Empty;
    [ObservableProperty] private CustomerListItemDto? _selectedCustomer;
    [ObservableProperty] private string _newFirstName = string.Empty;
    [ObservableProperty] private string _newLastName = string.Empty;
    [ObservableProperty] private string _newPhone = string.Empty;
    [ObservableProperty] private string _newEmail = string.Empty;
    [ObservableProperty] private string _deviceBrand = string.Empty;
    [ObservableProperty] private string _deviceModel = string.Empty;
    [ObservableProperty] private string _deviceSerial = string.Empty;
    [ObservableProperty] private string _issue = string.Empty;
    [ObservableProperty] private string _customerNotes = string.Empty;
    [ObservableProperty] private string _internalNotes = string.Empty;
    // Summary totals stay decimal for API math; XAML binds to *Text helpers (string).
    [ObservableProperty] private decimal _partsSubtotal;
    [ObservableProperty] private decimal _labourSubtotal;
    [ObservableProperty] private decimal _discountTotal;
    [ObservableProperty] private decimal _subtotal;
    [ObservableProperty] private decimal _gstAmount;
    [ObservableProperty] private decimal _total;
    [ObservableProperty] private decimal _costTotal;
    [ObservableProperty] private decimal _profitTotal;
    [ObservableProperty] private decimal _marginPercent;
    [ObservableProperty] private bool _requiresApproval;
    [ObservableProperty] private string? _marginWarning;
    [ObservableProperty] private string? _error;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isFrozen;
    [ObservableProperty] private bool _canViewProfit;
    [ObservableProperty] private bool _canViewCost;

    public string PartsSubtotalText => PartsSubtotal.ToString("0.00");
    public string LabourSubtotalText => LabourSubtotal.ToString("0.00");
    public string DiscountTotalText => DiscountTotal.ToString("0.00");
    public string SubtotalText => Subtotal.ToString("0.00");
    public string GstAmountText => GstAmount.ToString("0.00");
    public string TotalText => Total.ToString("0.00");
    public string CostTotalText => CostTotal.ToString("0.00");
    public string ProfitTotalText => ProfitTotal.ToString("0.00");
    public string MarginPercentText => MarginPercent.ToString("0.0");
    public string MarginWarningText => MarginWarning ?? string.Empty;

    public bool ShowNewCustomerFields => !UseExistingCustomer;
    public bool HasCustomers => Customers.Count > 0;

    public QuoteBuilderViewModel(ApiClient api, AuthSession session)
    {
        _api = api;
        _session = session;
        Lines.Add(new QuoteLineDraft());
        UpdatePermissionFlags();
    }

    partial void OnUseExistingCustomerChanged(bool value) => OnPropertyChanged(nameof(ShowNewCustomerFields));

    private void UpdatePermissionFlags()
    {
        var perms = _session.User?.Permissions ?? Array.Empty<string>();
        var owner = _session.User?.IsOwner == true;
        CanViewCost = owner || perms.Contains("pricing.view_cost") || perms.Contains("pricing.view");
        CanViewProfit = owner || perms.Contains("pricing.view_profit") || perms.Contains("pricing.view");
    }

    public async Task InitAsync(QuoteBuilderArgs? args)
    {
        UpdatePermissionFlags();
        Error = null;
        try
        {
            var services = await _api.GetAsync<ServicePricingDto[]>("api/pricing/services");
            Services.Clear();
            foreach (var s in services.Where(x => x.IsActive)) Services.Add(s);

            var inv = await _api.GetAsync<InventoryListItemDto[]>("api/inventory");
            InventoryParts.Clear();
            foreach (var i in inv) InventoryParts.Add(i);

            if (args?.QuoteId is Guid qid)
            {
                await LoadQuoteAsync(qid);
                return;
            }

            if (args?.RepairTicketId is Guid rid)
            {
                RepairTicketId = rid;
                var repair = await _api.GetAsync<RepairDetailDto>($"api/repairs/{rid}");
                SelectedCustomer = new CustomerListItemDto(repair.CustomerId, repair.CustomerName, CustomerType.Individual, null, null, DateTimeOffset.UtcNow, null, 0, 0);
                Customers.Clear();
                Customers.Add(SelectedCustomer);
                UseExistingCustomer = true;
                DeviceBrand = ExtractBrand(repair.DeviceLabel);
                DeviceModel = ExtractModel(repair.DeviceLabel);
                Issue = repair.ReportedIssue ?? string.Empty;
            }
            else if (args?.CustomerId is Guid cid)
            {
                var detail = await _api.GetAsync<CustomerDetailDto>($"api/customers/{cid}");
                SelectedCustomer = new CustomerListItemDto(detail.Id, detail.DisplayName, detail.Type, detail.Phone, detail.Email, detail.CreatedAt, detail.LastVisitAt, detail.Devices.Count, detail.RecentRepairs.Count);
                Customers.Clear();
                Customers.Add(SelectedCustomer);
                UseExistingCustomer = true;
            }

            await RecalcAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
    }

    private static string ExtractBrand(string? label)
    {
        if (string.IsNullOrWhiteSpace(label)) return string.Empty;
        var parts = label.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 ? parts[0] : string.Empty;
    }

    private static string ExtractModel(string? label)
    {
        if (string.IsNullOrWhiteSpace(label)) return string.Empty;
        var parts = label.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 1 ? parts[1] : string.Empty;
    }

    private async Task LoadQuoteAsync(Guid id)
    {
        var q = await _api.GetAsync<QuoteDetailDto>($"api/quotes/{id}");
        QuoteId = q.Id;
        QuoteNumber = q.Number;
        Status = q.Status;
        RepairTicketId = q.RepairTicketId;
        IsFrozen = q.IsFrozen;
        SelectedCustomer = new CustomerListItemDto(q.CustomerId, q.CustomerName, CustomerType.Individual, null, null, DateTimeOffset.UtcNow, null, 0, 0);
        Customers.Clear();
        Customers.Add(SelectedCustomer);
        UseExistingCustomer = true;
        DeviceBrand = q.DeviceBrand ?? string.Empty;
        DeviceModel = q.DeviceModel ?? string.Empty;
        DeviceSerial = q.DeviceSerial ?? string.Empty;
        Issue = q.Issue ?? string.Empty;
        CustomerNotes = q.CustomerNotes ?? string.Empty;
        InternalNotes = q.InternalNotes ?? string.Empty;
        Lines.Clear();
        foreach (var l in q.Lines)
        {
            Lines.Add(new QuoteLineDraft
            {
                Type = l.Type,
                Description = l.Description,
                ServiceName = l.ServiceName,
                PartName = l.PartName,
                SupplierName = l.SupplierName,
                Sku = l.Sku ?? string.Empty,
                InventoryItemId = l.InventoryItemId,
                ServicePricingId = l.ServicePricingId,
                DifficultyLevelKey = l.DifficultyLevelKey,
                Quantity = (double)l.Quantity,
                PartCost = (double)l.PartCost,
                ShippingCost = (double)l.ShippingCost,
                OtherCost = (double)l.OtherCost,
                MarkupPercent = (double)l.MarkupPercent,
                LabourAmount = (double)l.LabourAmount,
                AdditionalAmount = (double)l.AdditionalAmount,
                DiscountAmount = (double)l.DiscountAmount,
                PartSell = (double)l.PartSell,
                LineTotal = (double)l.LineTotal,
                LineProfit = (double)l.LineProfit
            });
        }
        ApplyPreviewTotals(q.PartsSubtotal, q.LabourSubtotal, q.DiscountTotal, q.Subtotal, q.GstAmount, q.Total,
            q.CostTotal, q.ProfitTotal, q.MarginPercent, q.RequiresApproval, null);
    }

    [RelayCommand]
    private async Task SearchCustomersAsync()
    {
        try
        {
            var page = await _api.GetAsync<PagedResult<CustomerListItemDto>>(
                $"api/customers?q={Uri.EscapeDataString(CustomerQuery)}&pageSize=50");
            Customers.Clear();
            foreach (var c in page.Items) Customers.Add(c);
            OnPropertyChanged(nameof(HasCustomers));
        }
        catch (Exception ex) { Error = ex.Message; }
    }

    [RelayCommand]
    private void AddLine() => Lines.Add(new QuoteLineDraft());

    [RelayCommand]
    private void RemoveLine(QuoteLineDraft? line)
    {
        if (line is null) return;
        Lines.Remove(line);
        if (Lines.Count == 0) Lines.Add(new QuoteLineDraft());
        _ = RecalcAsync();
    }

    public void ApplyInventoryPart(QuoteLineDraft line, InventoryListItemDto item)
    {
        line.InventoryItemId = item.Id;
        line.Sku = item.Sku ?? string.Empty;
        line.PartName = item.Name;
        line.Description = string.IsNullOrWhiteSpace(line.Description) ? item.Name : line.Description;
        line.PartCost = CanViewCost ? (double)item.Cost : line.PartCost;
        line.SupplierName = item.SupplierName;
        line.Type = "PART";
        _ = RecalcAsync();
    }

    public void ApplyService(QuoteLineDraft line, ServicePricingDto service)
    {
        line.ServicePricingId = service.Id;
        line.ServiceName = service.Name;
        line.LabourAmount = (double)service.DefaultLabourFee;
        if (service.DefaultPartMarkupPercent is decimal m) line.MarkupPercent = (double)m;
        if (string.IsNullOrWhiteSpace(line.Description)) line.Description = service.Name;
        _ = RecalcAsync();
    }

    [RelayCommand]
    private async Task RecalcAsync()
    {
        try
        {
            var inputs = Lines.Select(ToInput).ToList();
            var preview = await _api.PostAsync<PricingPreviewRequest, PricingPreviewResponse>(
                "api/pricing/preview", new PricingPreviewRequest(inputs, null, null, null, null));
            for (var i = 0; i < Math.Min(Lines.Count, preview.Lines.Count); i++)
            {
                var src = preview.Lines[i];
                var dst = Lines[i];
                dst.PartSell = (double)src.PartSell;
                dst.LineTotal = (double)src.LineTotal;
                dst.LineProfit = (double)src.LineProfit;
                if (dst.MarkupPercent == 0) dst.MarkupPercent = (double)src.MarkupPercent;
                if (dst.LabourAmount == 0) dst.LabourAmount = (double)src.LabourAmount;
            }
            ApplyPreviewTotals(preview.PartsSubtotal, preview.LabourSubtotal, preview.DiscountTotal,
                preview.Subtotal, preview.GstAmount, preview.Total, preview.CostTotal, preview.ProfitTotal,
                preview.MarginPercent, preview.RequiresApproval, preview.Warning);
        }
        catch (Exception ex) { Error = ex.Message; }
    }

    private void ApplyPreviewTotals(
        decimal parts, decimal labour, decimal discount, decimal sub, decimal gst, decimal total,
        decimal cost, decimal profit, decimal margin, bool requiresApproval, string? warning)
    {
        PartsSubtotal = parts;
        LabourSubtotal = labour;
        DiscountTotal = discount;
        Subtotal = sub;
        GstAmount = gst;
        Total = total;
        CostTotal = cost;
        ProfitTotal = profit;
        MarginPercent = margin;
        RequiresApproval = requiresApproval;
        MarginWarning = warning;
        NotifySummaryText();
    }

    private void NotifySummaryText()
    {
        OnPropertyChanged(nameof(PartsSubtotalText));
        OnPropertyChanged(nameof(LabourSubtotalText));
        OnPropertyChanged(nameof(DiscountTotalText));
        OnPropertyChanged(nameof(SubtotalText));
        OnPropertyChanged(nameof(GstAmountText));
        OnPropertyChanged(nameof(TotalText));
        OnPropertyChanged(nameof(CostTotalText));
        OnPropertyChanged(nameof(ProfitTotalText));
        OnPropertyChanged(nameof(MarginPercentText));
        OnPropertyChanged(nameof(MarginWarningText));
    }

    private static QuoteLineCalcInput ToInput(QuoteLineDraft l) => new(
        l.Type, string.IsNullOrWhiteSpace(l.Description) ? (l.PartName ?? l.ServiceName ?? "Line") : l.Description,
        l.ServiceName, l.PartName, l.SupplierName,
        string.IsNullOrWhiteSpace(l.Sku) ? null : l.Sku,
        l.InventoryItemId, l.ServicePricingId,
        l.DifficultyLevelKey,
        (decimal)(l.Quantity <= 0 ? 1 : l.Quantity),
        (decimal)l.PartCost, (decimal)l.ShippingCost, (decimal)l.OtherCost,
        l.MarkupPercent > 0 ? (decimal)l.MarkupPercent : null, null, null,
        l.LabourAmount > 0 ? (decimal)l.LabourAmount : null,
        (decimal)l.AdditionalAmount, (decimal)l.DiscountAmount, null);

    private async Task<Guid> EnsureCustomerAsync()
    {
        if (UseExistingCustomer)
        {
            if (SelectedCustomer is null) throw new InvalidOperationException("Select a customer.");
            return SelectedCustomer.Id;
        }
        if (string.IsNullOrWhiteSpace(NewFirstName))
            throw new InvalidOperationException("First name is required for a new customer.");
        var created = await _api.PostAsync<UpsertCustomerRequest, CustomerDetailDto>("api/customers",
            new UpsertCustomerRequest(null, CustomerType.Individual, NewFirstName, NewLastName, null, NewPhone, NewEmail,
                null, null, null, null, null, PreferredContact.Sms, false));
        return created.Id;
    }

    private List<QuoteLineInputDto> ToLineInputs() =>
        Lines.Select(l => new QuoteLineInputDto(
            l.Type, string.IsNullOrWhiteSpace(l.Description) ? (l.PartName ?? "Line") : l.Description,
            l.ServiceName, l.PartName, l.SupplierName,
            string.IsNullOrWhiteSpace(l.Sku) ? null : l.Sku,
            l.InventoryItemId, l.ServicePricingId,
            l.DifficultyLevelKey,
            (decimal)(l.Quantity <= 0 ? 1 : l.Quantity),
            (decimal)l.PartCost, (decimal)l.ShippingCost, (decimal)l.OtherCost,
            l.MarkupPercent > 0 ? (decimal)l.MarkupPercent : null, null, null,
            l.LabourAmount > 0 ? (decimal)l.LabourAmount : null,
            (decimal)l.AdditionalAmount, (decimal)l.DiscountAmount, null)).ToList();

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsFrozen) { Error = "Accepted quotes are frozen. Use Revise to edit."; return; }
        Error = null;
        IsBusy = true;
        try
        {
            await RecalcAsync();
            var customerId = await EnsureCustomerAsync();
            var lines = ToLineInputs();
            if (QuoteId is Guid id)
            {
                var updated = await _api.PutAsync<UpdateQuoteRequest, QuoteDetailDto>($"api/quotes/{id}",
                    new UpdateQuoteRequest(Issue, CustomerNotes, InternalNotes, DeviceBrand, DeviceModel, DeviceSerial,
                        null, null, lines, "Save"));
                await LoadQuoteAsync(updated.Id);
                StatusMessage = "Quote saved.";
            }
            else
            {
                var created = await _api.PostAsync<CreateQuoteRequest, QuoteDetailDto>("api/quotes",
                    new CreateQuoteRequest(customerId, RepairTicketId, Issue, CustomerNotes, InternalNotes,
                        DeviceBrand, DeviceModel, DeviceSerial, null, 14, lines));
                await LoadQuoteAsync(created.Id);
                StatusMessage = $"Created {created.Number}";
            }
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task SendAsync()
    {
        await SaveAsync();
        if (QuoteId is null || !string.IsNullOrWhiteSpace(Error)) return;
        try
        {
            var q = await _api.PostAsync<QuoteDetailDto>($"api/quotes/{QuoteId}/send");
            Status = q.Status;
            StatusMessage = "Quote sent.";
        }
        catch (Exception ex) { Error = ex.Message; }
    }

    [RelayCommand]
    private async Task PrintAsync()
    {
        if (QuoteId is null)
        {
            await SaveAsync();
            if (QuoteId is null) return;
        }
        try
        {
            var html = await _api.GetRawAsync($"api/quotes/{QuoteId}/print");
            var path = Path.Combine(Path.GetTempPath(), $"WorkshopOS-Quote-{QuoteNumber ?? QuoteId.ToString()}.html");
            await File.WriteAllTextAsync(path, html);
            var success = await Windows.System.Launcher.LaunchUriAsync(new Uri(path));
            StatusMessage = success ? "Opened customer quote for printing." : "Saved quote HTML but could not open browser.";
        }
        catch (Exception ex) { Error = ex.Message; }
    }

    [RelayCommand]
    private async Task ConvertAsync()
    {
        if (QuoteId is null) await SaveAsync();
        if (QuoteId is null) return;
        try
        {
            var q = await _api.PostAsync<ConvertQuoteToRepairRequest, QuoteDetailDto>(
                $"api/quotes/{QuoteId}/convert-to-repair", new ConvertQuoteToRepairRequest(RepairTicketId));
            Status = q.Status;
            IsFrozen = q.IsFrozen;
            RepairTicketId = q.RepairTicketId;
            StatusMessage = q.RepairTicketId is Guid rid ? $"Converted — repair linked ({rid})." : "Converted.";
        }
        catch (Exception ex) { Error = ex.Message; }
    }
}
