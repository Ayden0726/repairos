using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WorkshopOS.Client.Services;
using WorkshopOS.Contracts.Auth;
using WorkshopOS.Contracts.Common;
using WorkshopOS.Contracts.Operations;
using WorkshopOS.Contracts.Workshop;

namespace WorkshopOS.Client.Views;

public sealed record QuoteBuilderArgs(Guid? QuoteId = null, Guid? RepairTicketId = null, Guid? CustomerId = null);

/// <summary>Combo row for existing customers — shows name, phone, email, and id.</summary>
public sealed class QuoteCustomerOption
{
    public QuoteCustomerOption(CustomerListItemDto customer) => Customer = customer;

    public CustomerListItemDto Customer { get; }
    public Guid Id => Customer.Id;
    public string DisplayName => Customer.DisplayName;
    public string? Phone => Customer.Phone;
    public string? Email => Customer.Email;
    public string ComboLabel
    {
        get
        {
            var phone = string.IsNullOrWhiteSpace(Customer.Phone) ? "—" : Customer.Phone.Trim();
            var email = string.IsNullOrWhiteSpace(Customer.Email) ? "—" : Customer.Email.Trim();
            return $"{Customer.DisplayName}  ·  {phone}  ·  {email}  ·  {Customer.Id}";
        }
    }
}

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
    [ObservableProperty] private double _additionalAmount;
    [ObservableProperty] private double _discountAmount;
    [ObservableProperty] private double _landedCost;
    [ObservableProperty] private double _lineCost;
    [ObservableProperty] private double _partSell;
    [ObservableProperty] private double _lineTotal;

    /// <summary>Shared with QuoteBuilderViewModel — business currency symbol, default $.</summary>
    public static string CurrencySymbol { get; set; } = "$";

    public string LandedCostText => MoneyDisplay.FormatWithSymbol(LandedCost, CurrencySymbol);
    public string LineCostText => MoneyDisplay.FormatWithSymbol(LineCost, CurrencySymbol);

    partial void OnLandedCostChanged(double value) => OnPropertyChanged(nameof(LandedCostText));
    partial void OnLineCostChanged(double value) => OnPropertyChanged(nameof(LineCostText));

    public void RefreshMoneyText()
    {
        OnPropertyChanged(nameof(LandedCostText));
        OnPropertyChanged(nameof(LineCostText));
    }
}

public partial class QuoteBuilderViewModel : ObservableObject
{
    private readonly ApiClient _api;
    private readonly AuthSession _session;
    private Guid? _preferCustomerId;
    private PricingSettingsDto? _pricing;
    private bool _applyingJobDefaults;

    public ObservableCollection<QuoteLineDraft> Lines { get; } = new();
    public ObservableCollection<QuoteCustomerOption> Customers { get; } = new();
    public ObservableCollection<InventoryListItemDto> InventoryParts { get; } = new();
    public ObservableCollection<ServicePricingDto> Services { get; } = new();

    [ObservableProperty] private Guid? _quoteId;
    [ObservableProperty] private Guid? _repairTicketId;
    [ObservableProperty] private string? _quoteNumber;
    [ObservableProperty] private string _status = "Draft";
    [ObservableProperty] private bool _useExistingCustomer = true;
    [ObservableProperty] private string _customerQuery = string.Empty;
    [ObservableProperty] private bool _isLoadingCustomers;
    [ObservableProperty] private string? _customersStatus;
    [ObservableProperty] private QuoteCustomerOption? _selectedCustomer;
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

    // Job-level labour + markup (NumberBox binds double).
    [ObservableProperty] private double _jobLabourFee = 50d;
    [ObservableProperty] private double _jobMarkupPercent = 20d;
    [ObservableProperty] private bool _labourOverridden;
    [ObservableProperty] private bool _markupOverridden;

    // Summary totals stay decimal for API math; XAML binds to *Text helpers (string).
    [ObservableProperty] private decimal _partsCostTotal;
    [ObservableProperty] private decimal _markupAmount;
    [ObservableProperty] private decimal _partsSellTotal;
    [ObservableProperty] private decimal _partsSubtotal;
    [ObservableProperty] private decimal _labourSubtotal;
    [ObservableProperty] private decimal _discountTotal;
    [ObservableProperty] private decimal _additionalTotal;
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
    [ObservableProperty] private bool _canChangeMarkup;
    [ObservableProperty] private bool _canOverrideLabour;
    [ObservableProperty] private string _pricingDefaultsSummary = "Loading pricing settings…";
    [ObservableProperty] private int _defaultValidityDays = 14;
    [ObservableProperty] private string _currencySymbol = "$";

    private string Money(decimal amount) => MoneyDisplay.FormatWithSymbol(amount, CurrencySymbol);

    public string PartsCostTotalText => Money(PartsCostTotal);
    public string MarkupAmountText => Money(MarkupAmount);
    public string PartsSellTotalText => Money(PartsSellTotal);
    public string PartsSubtotalText => Money(PartsSellTotal);
    public string LabourSubtotalText => Money(LabourSubtotal);
    public string DiscountTotalText => Money(DiscountTotal);
    public string AdditionalTotalText => Money(AdditionalTotal);
    public string SubtotalText => Money(Subtotal);
    public string GstAmountText => Money(GstAmount);
    public string TotalText => Money(Total);
    public string CostTotalText => Money(CostTotal);
    public string ProfitTotalText => Money(ProfitTotal);
    public string MarginPercentText => MarginPercent.ToString("0.0");
    public string MarginWarningText => MarginWarning ?? string.Empty;
    public string JobMarkupPercentText => JobMarkupPercent.ToString("0.##");

    public bool ShowNewCustomerFields => !UseExistingCustomer;
    public bool HasCustomers => Customers.Count > 0;
    public bool HasCustomersStatus => !string.IsNullOrWhiteSpace(CustomersStatus);
    public bool IsCustomersIdle => !IsLoadingCustomers;

    public QuoteBuilderViewModel(ApiClient api, AuthSession session)
    {
        _api = api;
        _session = session;
        UpdatePermissionFlags();
    }

    partial void OnUseExistingCustomerChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowNewCustomerFields));
        Error = null;
    }

    partial void OnIsLoadingCustomersChanged(bool value) => OnPropertyChanged(nameof(IsCustomersIdle));
    partial void OnCustomersStatusChanged(string? value) => OnPropertyChanged(nameof(HasCustomersStatus));

    partial void OnJobLabourFeeChanged(double value)
    {
        if (!_applyingJobDefaults) LabourOverridden = true;
    }

    partial void OnJobMarkupPercentChanged(double value)
    {
        if (!_applyingJobDefaults) MarkupOverridden = true;
        OnPropertyChanged(nameof(JobMarkupPercentText));
    }

    private void UpdatePermissionFlags()
    {
        var perms = _session.User?.Permissions ?? Array.Empty<string>();
        var owner = _session.User?.IsOwner == true;
        CanViewCost = owner || perms.Contains("pricing.view_cost") || perms.Contains("pricing.view");
        CanViewProfit = owner || perms.Contains("pricing.view_profit") || perms.Contains("pricing.view");
        CanChangeMarkup = owner || perms.Contains("pricing.change_markup") || perms.Contains("pricing.edit")
            || perms.Contains("pricing.edit_settings") || perms.Contains("quotes.manage");
        CanOverrideLabour = owner || perms.Contains("pricing.override_labour") || perms.Contains("pricing.edit_settings")
            || perms.Contains("quotes.manage");
    }

    public async Task InitAsync(QuoteBuilderArgs? args)
    {
        UpdatePermissionFlags();
        Error = null;
        CustomersStatus = null;
        _preferCustomerId = args?.CustomerId;
        try
        {
            await LoadPricingSettingsAsync();

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

            Lines.Clear();
            SeedJobDefaultsFromSettings();
            Lines.Add(new QuoteLineDraft());

            if (args?.RepairTicketId is Guid rid)
            {
                RepairTicketId = rid;
                var repair = await _api.GetAsync<RepairDetailDto>($"api/repairs/{rid}");
                _preferCustomerId = repair.CustomerId;
                UseExistingCustomer = true;
                DeviceBrand = ExtractBrand(repair.DeviceLabel);
                DeviceModel = ExtractModel(repair.DeviceLabel);
                Issue = repair.ReportedIssue ?? string.Empty;
            }
            else if (args?.CustomerId is Guid)
            {
                UseExistingCustomer = true;
            }

            await LoadCustomersAsync();
            await RecalcAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
    }

    private async Task LoadPricingSettingsAsync()
    {
        try
        {
            await LoadCurrencySymbolAsync();
            _pricing = await _api.GetAsync<PricingSettingsDto>("api/pricing/settings");
            DefaultValidityDays = _pricing.Quote.DefaultValidityDays > 0
                ? _pricing.Quote.DefaultValidityDays
                : 14;
            var tax = _pricing.Tax;
            var taxText = tax is null || !tax.Enabled
                ? "tax off"
                : $"{(tax.Inclusive ? "inc" : "ex")} GST {(tax.Rate * 100m):0.##}%";
            PricingDefaultsSummary =
                $"Job labour + markup on Σ part costs · {_pricing.Parts.MarkupMethod} {_pricing.Parts.DefaultMarkupPercent:0.##}% · " +
                $"labour {Money(_pricing.Labour.DefaultLabourFee)} · round {_pricing.Rounding.Method} · " +
                $"min margin {_pricing.Profitability.MinimumGrossMarginPercent:0.##}% · {taxText}";
        }
        catch (Exception ex)
        {
            _pricing = null;
            PricingDefaultsSummary = $"Could not load pricing settings: {ex.Message}";
        }
    }

    private async Task LoadCurrencySymbolAsync()
    {
        try
        {
            var business = await _api.GetAsync<BusinessProfileDto>("api/settings/business");
            CurrencySymbol = MoneyDisplay.SymbolFromCurrencyCode(business?.Currency);
        }
        catch
        {
            CurrencySymbol = "$";
        }

        QuoteLineDraft.CurrencySymbol = CurrencySymbol;
        foreach (var line in Lines)
            line.RefreshMoneyText();
        NotifySummaryText();
    }

    private void SeedJobDefaultsFromSettings()
    {
        _applyingJobDefaults = true;
        JobMarkupPercent = (double)(_pricing?.Parts.DefaultMarkupPercent ?? 20m);
        JobLabourFee = (double)(_pricing?.Labour.DefaultLabourFee ?? 50m);
        MarkupOverridden = false;
        LabourOverridden = false;
        _applyingJobDefaults = false;
    }

    private void SetJobLabourFromSettings(double value)
    {
        _applyingJobDefaults = true;
        JobLabourFee = value;
        LabourOverridden = false;
        _applyingJobDefaults = false;
    }

    private void SetJobMarkupFromSettings(double value)
    {
        _applyingJobDefaults = true;
        JobMarkupPercent = value;
        MarkupOverridden = false;
        _applyingJobDefaults = false;
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
        _preferCustomerId = q.CustomerId;
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
            var draft = new QuoteLineDraft
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
                AdditionalAmount = (double)l.AdditionalAmount,
                DiscountAmount = (double)l.DiscountAmount,
                LandedCost = (double)l.LandedCost,
                LineCost = (double)(l.LandedCost * l.Quantity),
                PartSell = (double)l.PartSell,
                LineTotal = (double)l.LineTotal
            };
            Lines.Add(draft);
        }

        _applyingJobDefaults = true;
        JobLabourFee = (double)(q.LabourFee != 0m ? q.LabourFee : q.LabourSubtotal);
        JobMarkupPercent = (double)(q.MarkupPercent != 0m
            ? q.MarkupPercent
            : (_pricing?.Parts.DefaultMarkupPercent ?? 20m));
        LabourOverridden = false;
        MarkupOverridden = false;
        _applyingJobDefaults = false;

        ApplyPreviewTotals(
            q.PartsCostTotal != 0m ? q.PartsCostTotal : q.CostTotal,
            q.MarkupAmount,
            q.PartsSellTotal != 0m ? q.PartsSellTotal : q.PartsSubtotal,
            q.LabourFee != 0m ? q.LabourFee : q.LabourSubtotal,
            q.DiscountTotal, q.AdditionalTotal,
            q.Subtotal, q.GstAmount, q.Total,
            q.CostTotal, q.ProfitTotal, q.MarginPercent, q.RequiresApproval, null);
        await LoadCustomersAsync();
    }

    [RelayCommand]
    private async Task SearchCustomersAsync() => await LoadCustomersAsync();

    private async Task LoadCustomersAsync()
    {
        IsLoadingCustomers = true;
        CustomersStatus = null;
        Error = null;
        try
        {
            var q = Uri.EscapeDataString(CustomerQuery?.Trim() ?? string.Empty);
            var url = string.IsNullOrEmpty(q)
                ? "api/customers?pageSize=200"
                : $"api/customers?q={q}&pageSize=100";
            var page = await _api.GetAsync<PagedResult<CustomerListItemDto>>(url);
            Customers.Clear();
            foreach (var c in page.Items) Customers.Add(new QuoteCustomerOption(c));
            OnPropertyChanged(nameof(HasCustomers));

            if (_preferCustomerId is Guid prefer)
            {
                SelectedCustomer = Customers.FirstOrDefault(c => c.Id == prefer) ?? SelectedCustomer;
                _preferCustomerId = null;
            }
            else if (SelectedCustomer is not null)
            {
                SelectedCustomer = Customers.FirstOrDefault(c => c.Id == SelectedCustomer.Id);
            }

            if (Customers.Count == 0)
            {
                CustomersStatus = string.IsNullOrWhiteSpace(CustomerQuery)
                    ? "No customers yet. Uncheck Existing customer to create one here."
                    : "No customers match that search.";
                SelectedCustomer = null;
            }
            else
            {
                CustomersStatus = $"{Customers.Count} customer{(Customers.Count == 1 ? "" : "s")}";
                SelectedCustomer ??= Customers[0];
            }
        }
        catch (Exception ex)
        {
            Customers.Clear();
            OnPropertyChanged(nameof(HasCustomers));
            SelectedCustomer = null;
            CustomersStatus = null;
            Error = $"Could not load customers: {ex.Message}";
        }
        finally
        {
            IsLoadingCustomers = false;
        }
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
        if (!LabourOverridden)
            SetJobLabourFromSettings((double)service.DefaultLabourFee);
        if (service.DefaultPartMarkupPercent is decimal m && !MarkupOverridden)
            SetJobMarkupFromSettings((double)m);
        if (string.IsNullOrWhiteSpace(line.Description)) line.Description = service.Name;
        _ = RecalcAsync();
    }

    [RelayCommand]
    private async Task RecalcAsync()
    {
        try
        {
            var inputs = Lines.Select(ToInput).ToList();
            var labour = (decimal)JobLabourFee;
            var markup = (decimal)JobMarkupPercent;

            var preview = await _api.PostAsync<PricingPreviewRequest, PricingPreviewResponse>(
                "api/pricing/preview",
                new PricingPreviewRequest(inputs, null, null, null, null, labour, markup, null, null, null));
            for (var i = 0; i < Math.Min(Lines.Count, preview.Lines.Count); i++)
            {
                var src = preview.Lines[i];
                var dst = Lines[i];
                dst.LandedCost = (double)src.LandedCost;
                dst.LineCost = (double)src.LineCost;
                dst.PartSell = (double)src.PartSell;
                dst.LineTotal = (double)src.LineTotal;
            }

            _applyingJobDefaults = true;
            JobLabourFee = (double)preview.LabourFee;
            JobMarkupPercent = (double)preview.MarkupPercent;
            _applyingJobDefaults = false;

            ApplyPreviewTotals(
                preview.PartsCostTotal, preview.MarkupAmount, preview.PartsSellTotal,
                preview.LabourFee, preview.DiscountTotal, preview.AdditionalTotal,
                preview.Subtotal, preview.GstAmount, preview.Total, preview.CostTotal,
                preview.ProfitTotal, preview.MarginPercent, preview.RequiresApproval, preview.Warning);
            Error = null;
        }
        catch (Exception ex) { Error = ex.Message; }
    }

    private void ApplyPreviewTotals(
        decimal partsCost, decimal markupAmt, decimal partsSell, decimal labour,
        decimal discount, decimal additional,
        decimal sub, decimal gst, decimal total,
        decimal cost, decimal profit, decimal margin, bool requiresApproval, string? warning)
    {
        PartsCostTotal = partsCost;
        MarkupAmount = markupAmt;
        PartsSellTotal = partsSell;
        PartsSubtotal = partsSell;
        LabourSubtotal = labour;
        DiscountTotal = discount;
        AdditionalTotal = additional;
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
        OnPropertyChanged(nameof(PartsCostTotalText));
        OnPropertyChanged(nameof(MarkupAmountText));
        OnPropertyChanged(nameof(PartsSellTotalText));
        OnPropertyChanged(nameof(PartsSubtotalText));
        OnPropertyChanged(nameof(LabourSubtotalText));
        OnPropertyChanged(nameof(DiscountTotalText));
        OnPropertyChanged(nameof(AdditionalTotalText));
        OnPropertyChanged(nameof(SubtotalText));
        OnPropertyChanged(nameof(GstAmountText));
        OnPropertyChanged(nameof(TotalText));
        OnPropertyChanged(nameof(CostTotalText));
        OnPropertyChanged(nameof(ProfitTotalText));
        OnPropertyChanged(nameof(MarginPercentText));
        OnPropertyChanged(nameof(MarginWarningText));
        OnPropertyChanged(nameof(JobMarkupPercentText));
    }

    private QuoteLineCalcInput ToInput(QuoteLineDraft l) => new(
        l.Type, string.IsNullOrWhiteSpace(l.Description) ? (l.PartName ?? l.ServiceName ?? "Line") : l.Description,
        l.ServiceName, l.PartName, l.SupplierName,
        string.IsNullOrWhiteSpace(l.Sku) ? null : l.Sku,
        l.InventoryItemId, l.ServicePricingId,
        l.DifficultyLevelKey,
        (decimal)(l.Quantity <= 0 ? 1 : l.Quantity),
        (decimal)l.PartCost, (decimal)l.ShippingCost, (decimal)l.OtherCost,
        null, null, null, null,
        (decimal)l.AdditionalAmount, (decimal)l.DiscountAmount, null);

    private async Task<Guid> EnsureCustomerAsync()
    {
        if (UseExistingCustomer)
        {
            if (SelectedCustomer is null) throw new InvalidOperationException(
                HasCustomers ? "Select a customer." : "No customer selected. Search again or create a new customer.");
            return SelectedCustomer.Id;
        }
        if (string.IsNullOrWhiteSpace(NewFirstName) && string.IsNullOrWhiteSpace(NewLastName)
            && string.IsNullOrWhiteSpace(NewPhone) && string.IsNullOrWhiteSpace(NewEmail))
            throw new InvalidOperationException("Enter a name, phone, or email for the new customer.");
        var created = await _api.PostAsync<UpsertCustomerRequest, CustomerDetailDto>("api/customers",
            new UpsertCustomerRequest(null, CustomerType.Individual,
                string.IsNullOrWhiteSpace(NewFirstName) ? null : NewFirstName.Trim(),
                string.IsNullOrWhiteSpace(NewLastName) ? null : NewLastName.Trim(),
                null,
                string.IsNullOrWhiteSpace(NewPhone) ? null : NewPhone.Trim(),
                string.IsNullOrWhiteSpace(NewEmail) ? null : NewEmail.Trim(),
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
            null, null, null, null,
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
            if (!string.IsNullOrWhiteSpace(Error)) return;
            var customerId = await EnsureCustomerAsync();
            var lines = ToLineInputs();
            var labour = (decimal)JobLabourFee;
            var markup = (decimal)JobMarkupPercent;
            if (QuoteId is Guid id)
            {
                var updated = await _api.PutAsync<UpdateQuoteRequest, QuoteDetailDto>($"api/quotes/{id}",
                    new UpdateQuoteRequest(Issue, CustomerNotes, InternalNotes, DeviceBrand, DeviceModel, DeviceSerial,
                        null, null, lines, "Save", labour, markup));
                await LoadQuoteAsync(updated.Id);
                StatusMessage = "Quote saved.";
            }
            else
            {
                var created = await _api.PostAsync<CreateQuoteRequest, QuoteDetailDto>("api/quotes",
                    new CreateQuoteRequest(customerId, RepairTicketId, Issue, CustomerNotes, InternalNotes,
                        DeviceBrand, DeviceModel, DeviceSerial, null, DefaultValidityDays, lines,
                        null, labour, markup));
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
