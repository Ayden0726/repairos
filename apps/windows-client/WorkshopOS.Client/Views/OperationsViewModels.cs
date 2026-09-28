using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WorkshopOS.Client.Services;
using WorkshopOS.Contracts.Common;
using WorkshopOS.Contracts.Operations;
using WorkshopOS.Contracts.Workshop;

namespace WorkshopOS.Client.Views;

public partial class DashboardViewModel : ObservableObject
{
    private readonly ApiClient _api;
    private readonly AuthSession _session;
    public ObservableCollection<DashboardCardVm> Cards { get; } = new();
    public ObservableCollection<PipelineStageDto> Pipeline { get; } = new();
    public ObservableCollection<UrgentJobDto> Urgent { get; } = new();
    public ObservableCollection<TechnicianWorkloadDto> Workload { get; } = new();
    public ObservableCollection<LowStockDto> LowStock { get; } = new();
    public ObservableCollection<ActivityDto> Activity { get; } = new();
    public ObservableCollection<DashboardBookingVm> UpcomingBookings { get; } = new();
    public ObservableCollection<MyTicketVm> MyOpenTickets { get; } = new();
    [ObservableProperty] private string? _error;
    [ObservableProperty] private string? _calendarStatus;
    [ObservableProperty] private int _unassigned;
    [ObservableProperty] private string _greeting = "Welcome";
    [ObservableProperty] private string _myTicketsStatus = "Loading your open tickets…";
    [ObservableProperty] private bool _hasMyTickets;
    [ObservableProperty] private bool _showMyTicketsEmpty = true;

    public DashboardViewModel(ApiClient api, AuthSession session)
    {
        _api = api;
        _session = session;
        Greeting = BuildGreeting(session.User?.DisplayName);
    }

    private static string BuildGreeting(string? displayName)
    {
        var hour = DateTime.Now.Hour;
        var part = hour switch
        {
            >= 5 and < 12 => "Good morning",
            >= 12 and < 17 => "Good afternoon",
            _ => "Good evening"
        };
        var name = string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim();
        return name is null ? part : $"{part}, {name}";
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        Error = null;
        CalendarStatus = null;
        Greeting = BuildGreeting(_session.User?.DisplayName);
        try
        {
            var d = await _api.GetAsync<DashboardDto>("api/dashboard");
            Cards.Clear();
            Cards.Add(new("Open Jobs", d.Cards.OpenJobs.ToString(), "open"));
            Cards.Add(new("Due Today", d.Cards.DueToday.ToString(), "dueToday"));
            Cards.Add(new("Awaiting Approval", d.Cards.AwaitingApproval.ToString(), "awaiting_approval"));
            Cards.Add(new("Waiting for Parts", d.Cards.WaitingForParts.ToString(), "waiting_parts"));
            Cards.Add(new("Ready for Pickup", d.Cards.ReadyForPickup.ToString(), "ready_pickup"));
            Cards.Add(new("Overdue", d.Cards.Overdue.ToString(), "overdue"));
            Cards.Add(new("Revenue 30d", MoneyDisplay.Format(d.Cards.Revenue30Days), null));
            Cards.Add(new("Gross Profit 30d", MoneyDisplay.Format(d.Cards.GrossProfit30Days), null));
            Pipeline.Clear(); foreach (var p in d.Pipeline) Pipeline.Add(p);
            Urgent.Clear(); foreach (var u in d.UrgentJobs) Urgent.Add(u);
            Workload.Clear(); foreach (var w in d.Workload) Workload.Add(w);
            LowStock.Clear(); foreach (var l in d.LowStock) LowStock.Add(l);
            Activity.Clear(); foreach (var a in d.RecentActivity) Activity.Add(a);
            Unassigned = d.UnassignedJobs;
        }
        catch (Exception ex) { Error = ex.Message; }

        await LoadMyOpenTicketsAsync();

        try
        {
            var from = DateTimeOffset.Now.Date;
            var to = from.AddDays(7);
            var bookings = await _api.GetAsync<IReadOnlyList<BookingDto>>(
                $"api/bookings?from={Uri.EscapeDataString(from.ToString("o"))}&to={Uri.EscapeDataString(to.ToString("o"))}");
            UpcomingBookings.Clear();
            foreach (var b in bookings.OrderBy(x => x.StartsAt).Take(12))
            {
                var when = b.StartsAt.ToLocalTime();
                var label = when.Date == DateTime.Today
                    ? $"Today {when:HH:mm}"
                    : when.Date == DateTime.Today.AddDays(1)
                        ? $"Tomorrow {when:HH:mm}"
                        : when.ToString("ddd d MMM HH:mm");
                UpcomingBookings.Add(new DashboardBookingVm(b.Id, label, b.Status, b.CustomerName,
                    string.IsNullOrWhiteSpace(b.StaffName) ? b.Type : $"{b.Type} · {b.StaffName}", b.Notes));
            }
            CalendarStatus = UpcomingBookings.Count == 0
                ? "No bookings in the next 7 days."
                : $"{UpcomingBookings.Count} upcoming (next 7 days)";
        }
        catch (Exception ex)
        {
            UpcomingBookings.Clear();
            CalendarStatus = $"Calendar unavailable: {ex.Message}";
        }
    }

    private async Task LoadMyOpenTicketsAsync()
    {
        MyOpenTickets.Clear();
        HasMyTickets = false;
        ShowMyTicketsEmpty = true;

        var userId = _session.User?.Id;
        if (userId is null)
        {
            MyTicketsStatus = "Sign in to see tickets assigned to you.";
            return;
        }

        try
        {
            var page = await _api.GetAsync<WorkshopOS.Contracts.Workshop.PagedResult<WorkshopOS.Contracts.Workshop.RepairListItemDto>>(
                $"api/repairs?assignedToId={userId}&pageSize=100");
            var open = page.Items
                .Where(t => !string.Equals(t.StatusKey, "completed", StringComparison.OrdinalIgnoreCase)
                            && !string.Equals(t.StatusKey, "cancelled", StringComparison.OrdinalIgnoreCase))
                .OrderBy(t => t.DueAt ?? DateTimeOffset.MaxValue)
                .ThenByDescending(t => t.CreatedAt)
                .Take(12)
                .ToList();

            foreach (var t in open)
            {
                var due = t.DueAt is DateTimeOffset d
                    ? (d.ToLocalTime().Date == DateTime.Today
                        ? $"Due today {d.ToLocalTime():HH:mm}"
                        : $"Due {d.ToLocalTime():ddd d MMM}")
                    : "No due date";
                if (t.IsOverdue) due = "Overdue · " + due;
                MyOpenTickets.Add(new MyTicketVm(t.Id, t.TicketNumber, t.CustomerName, t.StatusName, due, t.IsOverdue));
            }

            HasMyTickets = MyOpenTickets.Count > 0;
            ShowMyTicketsEmpty = !HasMyTickets;
            MyTicketsStatus = HasMyTickets
                ? $"{MyOpenTickets.Count} open ticket(s) assigned to you"
                : "No open tickets assigned to you.";
        }
        catch (Exception ex)
        {
            MyTicketsStatus = $"Could not load your tickets: {ex.Message}";
            ShowMyTicketsEmpty = true;
        }
    }
}

public sealed record DashboardCardVm(string Title, string Value, string? Filter);
public sealed record DashboardBookingVm(Guid Id, string WhenLabel, string Status, string CustomerName, string TypeLine, string? Notes);
public sealed record MyTicketVm(Guid Id, string TicketNumber, string CustomerName, string Status, string DueLabel, bool IsOverdue);

public partial class InventoryViewModel : ObservableObject
{
    private readonly ApiClient _api;
    public ObservableCollection<InventoryListItemDto> Items { get; } = new();
    public ObservableCollection<string> ComponentFilterOptions { get; } = new(
        ["All", "CPU", "Motherboard", "RAM", "GPU", "Storage", "PSU", "Case", "Cooler", "OS", "Peripheral", "Other"]);
    public ObservableCollection<string> ComponentTypeOptions { get; } = new(
        ["CPU", "Motherboard", "RAM", "GPU", "Storage", "PSU", "Case", "Cooler", "OS", "Peripheral", "Other"]);

    [ObservableProperty] private string _sku = string.Empty;
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _category = "Parts";
    [ObservableProperty] private string _componentType = "Other";
    [ObservableProperty] private string _componentFilter = "All";
    [ObservableProperty] private string _cost = "0";
    [ObservableProperty] private string _sell = "0";
    [ObservableProperty] private string _qtyOnHand = "1";
    [ObservableProperty] private string? _error;
    public InventoryViewModel(ApiClient api) => _api = api;

    partial void OnComponentFilterChanged(string value) => _ = RefreshAsync();

    [RelayCommand]
    private async Task RefreshAsync()
    {
        try
        {
            var path = "api/inventory";
            if (!string.IsNullOrWhiteSpace(ComponentFilter) && ComponentFilter != "All")
                path += $"?componentType={Uri.EscapeDataString(ComponentFilter)}";
            var list = await _api.GetAsync<IReadOnlyList<InventoryListItemDto>>(path);
            Items.Clear();
            foreach (var i in list) Items.Add(i);
        }
        catch (Exception ex) { Error = ex.Message; }
    }

    [RelayCommand]
    private async Task CreateAsync()
    {
        try
        {
            _ = decimal.TryParse(Cost, out var cost);
            _ = decimal.TryParse(Sell, out var sell);
            _ = int.TryParse(QtyOnHand, out var qty);
            if (qty < 0) qty = 0;
            await _api.PostAsync("api/inventory", new UpsertInventoryRequest(
                null, Sku, null, Name,
                string.IsNullOrWhiteSpace(Category) ? "Parts" : Category.Trim(),
                cost, sell, qty, 1, 5, null, null, ComponentType));
            Sku = Name = string.Empty;
            Cost = Sell = "0";
            QtyOnHand = "1";
            await RefreshAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
    }
}

public partial class ReportsViewModel : ObservableObject
{
    private readonly ApiClient _api;
    [ObservableProperty] private string _summary = "Loading…";
    [ObservableProperty] private string? _error;
    public ReportsViewModel(ApiClient api) => _api = api;

    [RelayCommand]
    private async Task RefreshAsync()
    {
        try
        {
            var r = await _api.GetAsync<ReportSummaryDto>("api/reports/summary");
            Summary = $"From {r.From:d} to {r.To:d}\nRevenue {r.Revenue:C}\nCOGS {r.CostOfGoods:C}\nGross profit {r.GrossProfit:C}\nOpened {r.RepairsOpened} · Completed {r.RepairsCompleted}";
        }
        catch (Exception ex) { Error = ex.Message; }
    }
}

public partial class BackupsViewModel : ObservableObject
{
    private readonly ApiClient _api;
    public ObservableCollection<string> Lines { get; } = new();
    [ObservableProperty] private string? _error;
    [ObservableProperty] private string? _status;
    public BackupsViewModel(ApiClient api) => _api = api;

    [RelayCommand]
    private async Task RefreshAsync()
    {
        Error = null;
        try
        {
            var list = await _api.GetAsync<IReadOnlyList<BackupDto>>("api/backups");
            Lines.Clear();
            foreach (var b in list)
                Lines.Add($"{b.StartedAt:u}  {b.Type}  {b.Status}  {b.Path ?? "(in progress)"}");
            if (Lines.Count == 0) Lines.Add("No backups yet. Click Create backup.");
            try
            {
                var health = await _api.GetAsync<SystemHealthDetailDto>("api/health/detail");
                Status = $"System {health.Status} · DB {(health.Database ? "ok" : "down")} · backups {health.BackupCount ?? 0} · last {health.LastBackupAt?.ToString("u") ?? "never"}";
            }
            catch { /* optional */ }
        }
        catch (Exception ex) { Error = ex.Message; }
    }

    [RelayCommand]
    private async Task CreateAsync()
    {
        Error = null;
        try
        {
            await _api.PostAsync<BackupDto>("api/backups");
            await RefreshAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
    }
}

public partial class AiAssistViewModel : ObservableObject
{
    private readonly ApiClient _api;
    [ObservableProperty] private string _prompt = string.Empty;
    [ObservableProperty] private string _result = "Enter a diagnosis or customer question. Output is advisory — confirm before applying.";
    [ObservableProperty] private string? _error;
    public AiAssistViewModel(ApiClient api) => _api = api;

    [RelayCommand]
    private async Task AssistAsync()
    {
        Error = null;
        try
        {
            var response = await _api.PostAsync<AiAssistRequest, AiAssistResponse>(
                "api/ai/assist", new AiAssistRequest(Prompt, null));
            Result = $"[{response.Provider}] {(response.Enabled ? "on" : "offline")}\n{response.Output}\n\n{response.Disclaimer}";
        }
        catch (Exception ex) { Error = ex.Message; }
    }
}

public sealed class UsedDeviceRowVm
{
    public UsedDeviceRowVm(UsedDeviceDto dto, bool showMargin)
    {
        Dto = dto;
        ShowMargin = showMargin;
        DeviceLabel = string.IsNullOrWhiteSpace(dto.Brand) && string.IsNullOrWhiteSpace(dto.Model)
            ? dto.Summary
            : string.Join(" ", new[] { dto.Brand, dto.Model, dto.StorageCapacity }.Where(s => !string.IsNullOrWhiteSpace(s)));
        SpecsLine = string.Join(" · ", new[] { dto.Category, dto.Colour, dto.Serial }.Where(s => !string.IsNullOrWhiteSpace(s)));
        CostLine = $"Buy ${dto.PurchasePrice:0.00} + parts ${dto.PartsCost:0.00} = ${dto.TotalCost:0.00}";
        SellLine = dto.ActualSalePrice is decimal sold
            ? $"Sold ${sold:0.00}"
            : $"Ask ${dto.AskingPrice:0.00}";
        MarginLine = showMargin && dto.Margin is decimal m ? $"Margin ${m:0.00}" : "";
    }

    public UsedDeviceDto Dto { get; }
    public Guid Id => Dto.Id;
    public string DeviceLabel { get; }
    public string SpecsLine { get; }
    public string Status => Dto.Status;
    public string ConditionGrade => Dto.ConditionGrade;
    public string CostLine { get; }
    public string SellLine { get; }
    public string MarginLine { get; }
    public bool ShowMargin { get; }
    public bool HasMargin => ShowMargin && !string.IsNullOrEmpty(MarginLine);
}

public sealed class UsedPickItem
{
    public UsedPickItem(Guid id, string label)
    {
        Id = id;
        Label = label;
    }

    public Guid Id { get; }
    public string Label { get; }
}

public partial class UsedTechViewModel : ObservableObject
{
    private readonly ApiClient _api;
    private readonly AuthSession _session;
    private Guid? _editingId;

    public ObservableCollection<UsedDeviceRowVm> Items { get; } = new();
    public ObservableCollection<UsedPickItem> Customers { get; } = new();
    public ObservableCollection<UsedPickItem> Repairs { get; } = new();
    public ObservableCollection<string> StatusOptions { get; } = new(["All", "In stock", "Reserved", "Listed", "Sold"]);
    public ObservableCollection<string> FormStatusOptions { get; } = new(["In stock", "Reserved", "Listed", "Sold"]);
    public ObservableCollection<string> ConditionOptions { get; } = new(["New refurbished", "Excellent", "Good", "Fair"]);

    [ObservableProperty] private string _query = string.Empty;
    [ObservableProperty] private string _statusFilter = "All";
    [ObservableProperty] private string? _error;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _canViewPricing;
    [ObservableProperty] private bool _canManage;
    [ObservableProperty] private string _formTitle = "Add refurbished device";
    [ObservableProperty] private string _saveButtonLabel = "Add device";

    [ObservableProperty] private string _brand = string.Empty;
    [ObservableProperty] private string _model = string.Empty;
    [ObservableProperty] private string _category = string.Empty;
    [ObservableProperty] private string _serial = string.Empty;
    [ObservableProperty] private string _imei = string.Empty;
    [ObservableProperty] private string _colour = string.Empty;
    [ObservableProperty] private string _storage = string.Empty;
    [ObservableProperty] private string _specs = string.Empty;
    [ObservableProperty] private string _condition = "Good";
    [ObservableProperty] private string _status = "In stock";
    [ObservableProperty] private string _purchasePrice = "0";
    [ObservableProperty] private string _partsCost = "0";
    [ObservableProperty] private string _askingPrice = "0";
    [ObservableProperty] private string _actualSalePrice = string.Empty;
    [ObservableProperty] private string _faults = string.Empty;
    [ObservableProperty] private string _notes = string.Empty;
    [ObservableProperty] private string _source = string.Empty;
    [ObservableProperty] private UsedPickItem? _selectedCustomer;
    [ObservableProperty] private UsedPickItem? _selectedRepair;
    [ObservableProperty] private string _totalCostPreview = "$0.00";
    [ObservableProperty] private string _marginPreview = "";

    public UsedTechViewModel(ApiClient api, AuthSession session)
    {
        _api = api;
        _session = session;
        UpdatePermissions();
        RecalcPreview();
    }

    private void UpdatePermissions()
    {
        var perms = _session.User?.Permissions ?? Array.Empty<string>();
        var owner = _session.User?.IsOwner == true;
        CanViewPricing = owner || perms.Contains("pricing.view") || perms.Contains("pricing.view_profit")
            || perms.Contains("pricing.view_cost");
        CanManage = owner || perms.Contains("used.manage") || perms.Contains("inventory.manage");
    }

    partial void OnPurchasePriceChanged(string value) => RecalcPreview();
    partial void OnPartsCostChanged(string value) => RecalcPreview();
    partial void OnAskingPriceChanged(string value) => RecalcPreview();
    partial void OnActualSalePriceChanged(string value) => RecalcPreview();

    private void RecalcPreview()
    {
        _ = decimal.TryParse(PurchasePrice, out var buy);
        _ = decimal.TryParse(PartsCost, out var parts);
        _ = decimal.TryParse(AskingPrice, out var ask);
        var total = buy + parts;
        TotalCostPreview = $"${total:0.00}";
        if (!CanViewPricing)
        {
            MarginPreview = "";
            return;
        }
        decimal? sold = decimal.TryParse(ActualSalePrice, out var s) ? s : null;
        var margin = (sold ?? ask) - total;
        MarginPreview = $"${margin:0.00}";
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        Error = null;
        IsBusy = true;
        UpdatePermissions();
        try
        {
            await LoadLookupsAsync();
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(Query))
                parts.Add($"q={Uri.EscapeDataString(Query.Trim())}");
            if (!string.IsNullOrWhiteSpace(StatusFilter) && StatusFilter != "All")
                parts.Add($"status={Uri.EscapeDataString(StatusFilter)}");
            var path = "api/used-tech" + (parts.Count > 0 ? "?" + string.Join("&", parts) : "");
            var list = await _api.GetAsync<IReadOnlyList<UsedDeviceDto>>(path);
            Items.Clear();
            foreach (var d in list)
                Items.Add(new UsedDeviceRowVm(d, CanViewPricing));
            StatusMessage = Items.Count == 0
                ? "No refurbished devices yet. Fill the form and add one."
                : $"{Items.Count} device(s)";
        }
        catch (Exception ex)
        {
            Error = Friendly(ex.Message);
            Items.Clear();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void NewDevice()
    {
        _editingId = null;
        FormTitle = "Add refurbished device";
        SaveButtonLabel = "Add device";
        Brand = Model = Category = Serial = Imei = Colour = Storage = Specs = string.Empty;
        Condition = "Good";
        Status = "In stock";
        PurchasePrice = "0";
        PartsCost = "0";
        AskingPrice = "0";
        ActualSalePrice = Faults = Notes = Source = string.Empty;
        SelectedCustomer = null;
        SelectedRepair = null;
        Error = null;
        RecalcPreview();
    }

    public async Task LoadDeviceAsync(Guid id)
    {
        Error = null;
        IsBusy = true;
        try
        {
            var d = await _api.GetAsync<UsedDeviceDetailDto>($"api/used-tech/{id}");
            _editingId = d.Id;
            FormTitle = "Edit refurbished device";
            SaveButtonLabel = "Save changes";
            Brand = d.Brand ?? "";
            Model = d.Model ?? "";
            Category = d.Category ?? "";
            Serial = d.Serial ?? "";
            Imei = d.Imei ?? "";
            Colour = d.Colour ?? "";
            Storage = d.StorageCapacity ?? "";
            Specs = d.Specs ?? "";
            Condition = string.IsNullOrWhiteSpace(d.ConditionGrade) ? "Good" : d.ConditionGrade;
            Status = string.IsNullOrWhiteSpace(d.Status) ? "In stock" : d.Status;
            PurchasePrice = d.PurchasePrice.ToString("0.##");
            PartsCost = d.PartsCost.ToString("0.##");
            AskingPrice = d.AskingPrice.ToString("0.##");
            ActualSalePrice = d.ActualSalePrice?.ToString("0.##") ?? "";
            Faults = d.Faults ?? "";
            Notes = d.Notes ?? "";
            Source = d.Source ?? "";
            SelectedCustomer = d.CustomerId is Guid cid
                ? Customers.FirstOrDefault(c => c.Id == cid) ?? new UsedPickItem(cid, d.CustomerName ?? cid.ToString()[..8])
                : null;
            SelectedRepair = d.RepairTicketId is Guid tid
                ? Repairs.FirstOrDefault(r => r.Id == tid) ?? new UsedPickItem(tid, d.RepairTicketNumber ?? tid.ToString()[..8])
                : null;
            RecalcPreview();
        }
        catch (Exception ex)
        {
            Error = Friendly(ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (!CanManage)
        {
            Error = "You do not have permission to manage refurbished devices.";
            return;
        }
        Error = null;
        IsBusy = true;
        try
        {
            if (string.IsNullOrWhiteSpace(Brand) && string.IsNullOrWhiteSpace(Model))
            {
                Error = "Brand or model is required.";
                return;
            }
            if (!decimal.TryParse(PurchasePrice, out var purchase)) purchase = 0m;
            if (!decimal.TryParse(PartsCost, out var parts)) parts = 0m;
            if (!decimal.TryParse(AskingPrice, out var ask)) ask = 0m;
            decimal? sold = decimal.TryParse(ActualSalePrice, out var s) ? s : null;

            Guid? customerId = SelectedCustomer is { Id: var cid } && cid != Guid.Empty ? cid : null;
            Guid? repairId = SelectedRepair is { Id: var rid } && rid != Guid.Empty ? rid : null;

            if (_editingId is Guid id)
            {
                await _api.PutAsync<UpdateUsedDeviceRequest, UsedDeviceDetailDto>(
                    $"api/used-tech/{id}",
                    new UpdateUsedDeviceRequest(
                        null, NullIfEmpty(Brand), NullIfEmpty(Model), NullIfEmpty(Category),
                        NullIfEmpty(Serial), NullIfEmpty(Imei), NullIfEmpty(Colour), NullIfEmpty(Storage), NullIfEmpty(Specs),
                        string.IsNullOrWhiteSpace(Condition) ? "Good" : Condition.Trim(),
                        string.IsNullOrWhiteSpace(Status) ? "In stock" : Status.Trim(),
                        purchase, ask, parts, sold,
                        NullIfEmpty(Faults), NullIfEmpty(Notes), NullIfEmpty(Source),
                        null, customerId, repairId));
                StatusMessage = "Device updated.";
            }
            else
            {
                await _api.PostAsync<CreateUsedDeviceRequest, UsedDeviceDetailDto>(
                    "api/used-tech",
                    new CreateUsedDeviceRequest(
                        null, NullIfEmpty(Brand), NullIfEmpty(Model), NullIfEmpty(Category),
                        NullIfEmpty(Serial), NullIfEmpty(Imei), NullIfEmpty(Colour), NullIfEmpty(Storage), NullIfEmpty(Specs),
                        string.IsNullOrWhiteSpace(Condition) ? "Good" : Condition.Trim(),
                        string.IsNullOrWhiteSpace(Status) ? "In stock" : Status.Trim(),
                        purchase, ask, parts,
                        NullIfEmpty(Faults), NullIfEmpty(Notes), NullIfEmpty(Source),
                        null, customerId, repairId));
                StatusMessage = "Device added.";
            }

            NewDevice();
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            Error = Friendly(ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadLookupsAsync()
    {
        try
        {
            var page = await _api.GetAsync<PagedResult<CustomerListItemDto>>("api/customers?page=1&pageSize=80");
            Customers.Clear();
            Customers.Add(new UsedPickItem(Guid.Empty, "(none)"));
            foreach (var c in page.Items)
                Customers.Add(new UsedPickItem(c.Id, $"{c.DisplayName} · {c.Phone ?? c.Email ?? c.Id.ToString()[..8]}"));
        }
        catch { /* optional */ }

        try
        {
            var page = await _api.GetAsync<PagedResult<RepairListItemDto>>("api/repairs?page=1&pageSize=80");
            Repairs.Clear();
            Repairs.Add(new UsedPickItem(Guid.Empty, "(none)"));
            foreach (var r in page.Items)
                Repairs.Add(new UsedPickItem(r.Id, $"{r.TicketNumber} · {r.CustomerName}"));
        }
        catch { /* optional */ }
    }

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string Friendly(string message)
    {
        if (message.Contains("404", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("Not Found", StringComparison.OrdinalIgnoreCase))
            return "Server outdated — update/restart WorkshopOS server (git pull + docker compose up -d --build), then reconnect. " + message;
        return message;
    }
}
