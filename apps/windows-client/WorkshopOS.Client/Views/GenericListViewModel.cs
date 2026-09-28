using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WorkshopOS.Client.Services;
using WorkshopOS.Contracts.Common;
using WorkshopOS.Contracts.Operations;
using WorkshopOS.Contracts.Workshop;

namespace WorkshopOS.Client.Views;

public sealed record ListPickItem(Guid Id, string Label);

public partial class GenericListViewModel : ObservableObject
{
    private readonly ApiClient _api;
    private string _path = "";
    private string _module = "";

    [ObservableProperty] private string _title = "";
    [ObservableProperty] private string? _error;
    [ObservableProperty] private string? _status;
    [ObservableProperty] private string _emptyHint = "No records yet. Use Create on the right.";
    [ObservableProperty] private string _createPanelTitle = "Create";
    [ObservableProperty] private bool _showCreatePanel = true;
    [ObservableProperty] private bool _showRepairPicker;
    [ObservableProperty] private bool _showSupplierFields;
    [ObservableProperty] private bool _showBookingFields;
    [ObservableProperty] private bool _showBuildFields;
    [ObservableProperty] private bool _showUsedFields;
    [ObservableProperty] private bool _showCustomerPicker;
    [ObservableProperty] private bool _isBusy;

    // Shared create fields
    [ObservableProperty] private ListPickItem? _selectedRepair;
    [ObservableProperty] private ListPickItem? _selectedCustomer;
    [ObservableProperty] private ListPickItem? _selectedSupplier;
    [ObservableProperty] private string _supplierName = string.Empty;
    [ObservableProperty] private string _lineDescription = "Parts order";
    [ObservableProperty] private string _lineQty = "1";
    [ObservableProperty] private string _lineCost = "0";
    [ObservableProperty] private string _bookingType = "Intake";
    [ObservableProperty] private string _bookingNotes = string.Empty;
    [ObservableProperty] private string _bookingStartLocal = string.Empty;
    [ObservableProperty] private string _bookingEndLocal = string.Empty;
    [ObservableProperty] private string _buildUseCase = "Gaming";
    [ObservableProperty] private string _buildBudget = "1500";
    [ObservableProperty] private string _buildPartName = "Build kit";
    [ObservableProperty] private string _buildPartCost = "800";
    [ObservableProperty] private string _buildPartSell = "1200";
    [ObservableProperty] private string _usedSummary = string.Empty;
    [ObservableProperty] private string _usedCondition = "B";
    [ObservableProperty] private string _usedPurchase = "0";
    [ObservableProperty] private string _usedResale = "0";

    public ObservableCollection<string> Lines { get; } = new();
    public ObservableCollection<ListPickItem> Repairs { get; } = new();
    public ObservableCollection<ListPickItem> Customers { get; } = new();
    public ObservableCollection<ListPickItem> Suppliers { get; } = new();

    public GenericListViewModel(ApiClient api) => _api = api;

    public void Configure(string title, string path)
    {
        Title = title;
        _path = path.Trim().TrimStart('/');
        _module = _path.StartsWith("api/", StringComparison.OrdinalIgnoreCase)
            ? _path["api/".Length..]
            : _path;

        ShowCreatePanel = true;
        ShowRepairPicker = false;
        ShowSupplierFields = false;
        ShowBookingFields = false;
        ShowBuildFields = false;
        ShowUsedFields = false;
        ShowCustomerPicker = false;

        switch (_module)
        {
            case "invoices":
                CreatePanelTitle = "Create invoice from repair";
                EmptyHint = "No invoices yet. Pick a repair on the right and click Create invoice.";
                ShowRepairPicker = true;
                break;
            case "purchase-orders":
                CreatePanelTitle = "Create purchase order";
                EmptyHint = "No purchase orders yet. Add a supplier (or pick one) and a line, then Create PO.";
                ShowSupplierFields = true;
                break;
            case "bookings":
                CreatePanelTitle = "Create booking";
                EmptyHint = "No bookings yet. Pick a customer, set time, then Create booking.";
                ShowBookingFields = true;
                ShowCustomerPicker = true;
                BookingStartLocal = DateTime.Now.AddHours(1).ToString("yyyy-MM-dd HH:mm");
                BookingEndLocal = DateTime.Now.AddHours(2).ToString("yyyy-MM-dd HH:mm");
                break;
            case "builds":
                CreatePanelTitle = "Create PC build";
                EmptyHint = "No PC builds yet. Enter use case / budget / part, then Create build.";
                ShowBuildFields = true;
                ShowCustomerPicker = true;
                break;
            case "used-tech":
                CreatePanelTitle = "Add used device";
                EmptyHint = "No used-tech stock yet. Open Refurbished from the Inventory menu.";
                ShowUsedFields = true;
                break;
            case "notifications":
                CreatePanelTitle = "Notifications";
                EmptyHint = "No notifications yet.";
                ShowCreatePanel = false;
                break;
            default:
                CreatePanelTitle = "Create";
                EmptyHint = "No records yet. Use Create on the right when available.";
                break;
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        Error = null;
        Status = null;
        IsBusy = true;
        try
        {
            await LoadLookupsAsync();
            using var doc = System.Text.Json.JsonDocument.Parse(await _api.GetRawAsync(_path));
            Lines.Clear();
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                var number = el.TryGetProperty("number", out var n) ? n.GetString() :
                    el.TryGetProperty("title", out var t) ? t.GetString() :
                    el.TryGetProperty("summary", out var s) ? s.GetString() :
                    el.TryGetProperty("customerName", out var c) ? c.GetString() :
                    el.TryGetProperty("name", out var nm) ? nm.GetString() : "Item";
                var status = el.TryGetProperty("status", out var st) ? st.GetString() : "";
                var when = el.TryGetProperty("startsAt", out var sa) ? sa.GetDateTimeOffset().LocalDateTime.ToString("g") : "";
                var extra = el.TryGetProperty("total", out var tot) ? tot.GetRawText() :
                    el.TryGetProperty("balance", out var bal) ? $"bal {bal.GetRawText()}" :
                    el.TryGetProperty("available", out var av) ? $"avail {av.GetInt32()}" :
                    el.TryGetProperty("type", out var ty) ? ty.GetString() : "";
                Lines.Add(string.Join("  ", new[] { number, status, when, extra }.Where(x => !string.IsNullOrWhiteSpace(x))));
            }
            Status = Lines.Count == 0 ? EmptyHint : $"{Lines.Count} record(s)";
        }
        catch (Exception ex)
        {
            Error = FriendlyOutdated(ex.Message);
            Lines.Clear();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CreateAsync()
    {
        Error = null;
        Status = null;
        IsBusy = true;
        try
        {
            switch (_module)
            {
                case "invoices":
                    if (SelectedRepair is null)
                    {
                        Error = "Select a repair ticket first.";
                        return;
                    }
                    await _api.PostAsync<CreateInvoiceFromRepairRequest, InvoiceDetailDto>(
                        "api/invoices/from-repair", new CreateInvoiceFromRepairRequest(SelectedRepair.Id));
                    Status = "Invoice created.";
                    break;

                case "purchase-orders":
                {
                    var supplierId = SelectedSupplier?.Id;
                    if (supplierId is null || supplierId == Guid.Empty)
                    {
                        if (string.IsNullOrWhiteSpace(SupplierName))
                        {
                            Error = "Enter a supplier name or pick an existing supplier.";
                            return;
                        }
                        var created = await _api.PostAsync<UpsertSupplierRequest, SupplierDto>(
                            "api/suppliers", new UpsertSupplierRequest(null, SupplierName.Trim(), null, null, null, null, null, null));
                        supplierId = created.Id;
                    }
                    if (!int.TryParse(LineQty, out var qty) || qty <= 0) qty = 1;
                    if (!decimal.TryParse(LineCost, out var cost)) cost = 0m;
                    await _api.PostAsync<CreatePurchaseOrderRequest, PurchaseOrderListItemDto>(
                        "api/purchase-orders",
                        new CreatePurchaseOrderRequest(supplierId.Value, null, 0m,
                            [new PoLineInputDto(null, string.IsNullOrWhiteSpace(LineDescription) ? "Parts" : LineDescription.Trim(), qty, cost)]));
                    Status = "Purchase order created.";
                    SupplierName = string.Empty;
                    break;
                }

                case "bookings":
                {
                    if (SelectedCustomer is null)
                    {
                        Error = "Select a customer.";
                        return;
                    }
                    if (!DateTime.TryParse(BookingStartLocal, out var startLocal) ||
                        !DateTime.TryParse(BookingEndLocal, out var endLocal))
                    {
                        Error = "Use start/end like 2026-09-27 14:00";
                        return;
                    }
                    await _api.PostAsync<CreateBookingRequest, BookingDto>(
                        "api/bookings",
                        new CreateBookingRequest(
                            SelectedCustomer.Id, null,
                            string.IsNullOrWhiteSpace(BookingType) ? "Intake" : BookingType.Trim(),
                            new DateTimeOffset(startLocal), new DateTimeOffset(endLocal),
                            string.IsNullOrWhiteSpace(BookingNotes) ? null : BookingNotes.Trim()));
                    Status = "Booking created.";
                    break;
                }

                case "builds":
                {
                    if (!decimal.TryParse(BuildBudget, out var budget)) budget = 0m;
                    if (!decimal.TryParse(BuildPartCost, out var partCost)) partCost = 0m;
                    if (!decimal.TryParse(BuildPartSell, out var partSell)) partSell = 0m;
                    await _api.PostAsync<CreatePcBuildRequest, PcBuildDetailDto>(
                        "api/builds",
                        new CreatePcBuildRequest(
                            SelectedCustomer?.Id,
                            string.IsNullOrWhiteSpace(BuildUseCase) ? null : BuildUseCase.Trim(),
                            string.IsNullOrWhiteSpace(BuildUseCase) ? null : BuildUseCase.Trim(),
                            budget <= 0 ? null : budget,
                            null,
                            [new PcPartInputDto("Other", string.IsNullOrWhiteSpace(BuildPartName) ? "Build kit" : BuildPartName.Trim(), null, partCost, partSell)]));
                    Status = "PC build created.";
                    break;
                }

                case "used-tech":
                {
                    if (string.IsNullOrWhiteSpace(UsedSummary))
                    {
                        Error = "Summary is required.";
                        return;
                    }
                    if (!decimal.TryParse(UsedPurchase, out var purchase)) purchase = 0m;
                    if (!decimal.TryParse(UsedResale, out var resale)) resale = 0m;
                    await _api.PostAsync<CreateUsedDeviceRequest, UsedDeviceDetailDto>(
                        "api/used-tech",
                        new CreateUsedDeviceRequest(
                            UsedSummary.Trim(), null, null, null,
                            null, null, null, null, null,
                            string.IsNullOrWhiteSpace(UsedCondition) ? "Good" : UsedCondition.Trim(),
                            "In stock",
                            purchase, resale, 0m, null, null, null, null, null, null));
                    UsedSummary = string.Empty;
                    Status = "Used device added.";
                    break;
                }

                default:
                    Error = "Create is not available for this list.";
                    return;
            }

            await RefreshAsync();
        }
        catch (Exception ex)
        {
            Error = FriendlyOutdated(ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadLookupsAsync()
    {
        if (ShowRepairPicker)
        {
            try
            {
                var page = await _api.GetAsync<PagedResult<RepairListItemDto>>("api/repairs?page=1&pageSize=50");
                Repairs.Clear();
                foreach (var r in page.Items)
                    Repairs.Add(new ListPickItem(r.Id, $"{r.TicketNumber} · {r.CustomerName} · {r.StatusName}"));
                if (SelectedRepair is null && Repairs.Count > 0)
                    SelectedRepair = Repairs[0];
            }
            catch { /* optional */ }
        }

        if (ShowCustomerPicker || ShowBookingFields || ShowBuildFields)
        {
            try
            {
                var page = await _api.GetAsync<PagedResult<CustomerListItemDto>>("api/customers?page=1&pageSize=50");
                Customers.Clear();
                foreach (var c in page.Items)
                    Customers.Add(new ListPickItem(c.Id, $"{c.DisplayName} · {c.Phone ?? c.Email ?? c.Id.ToString()[..8]}"));
                if (SelectedCustomer is null && Customers.Count > 0)
                    SelectedCustomer = Customers[0];
            }
            catch { /* optional */ }
        }

        if (ShowSupplierFields)
        {
            try
            {
                var list = await _api.GetAsync<IReadOnlyList<SupplierDto>>("api/suppliers");
                Suppliers.Clear();
                foreach (var s in list)
                    Suppliers.Add(new ListPickItem(s.Id, s.Name));
            }
            catch { /* optional */ }
        }
    }

    private static string FriendlyOutdated(string message)
    {
        if (message.Contains("404", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("Not Found", StringComparison.OrdinalIgnoreCase))
            return "Server outdated — update/restart WorkshopOS server (git pull + docker compose up -d --build), then reconnect. " + message;
        return message;
    }
}
