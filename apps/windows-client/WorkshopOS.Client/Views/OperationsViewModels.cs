using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WorkshopOS.Client.Services;
using WorkshopOS.Contracts.Operations;

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
            Cards.Add(new("Revenue 30d", d.Cards.Revenue30Days.ToString("C"), null));
            Cards.Add(new("Gross Profit 30d", d.Cards.GrossProfit30Days.ToString("C"), null));
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

public partial class GenericListViewModel : ObservableObject
{
    private readonly ApiClient _api;
    private string _path = "";
    [ObservableProperty] private string _title = "";
    public ObservableCollection<string> Lines { get; } = new();
    [ObservableProperty] private string? _error;

    public GenericListViewModel(ApiClient api) => _api = api;

    public void Configure(string title, string path)
    {
        Title = title;
        _path = path;
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        Error = null;
        try
        {
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
                var extra = el.TryGetProperty("total", out var tot) ? tot.GetRawText() :
                    el.TryGetProperty("available", out var av) ? $"avail {av.GetInt32()}" : "";
                Lines.Add($"{number}  {status}  {extra}".Trim());
            }
            if (Lines.Count == 0) Lines.Add("No records yet — create them from the API or upcoming detail forms.");
        }
        catch (Exception ex) { Error = ex.Message; }
    }
}

public partial class InventoryViewModel : ObservableObject
{
    private readonly ApiClient _api;
    public ObservableCollection<InventoryListItemDto> Items { get; } = new();
    [ObservableProperty] private string _sku = string.Empty;
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string? _error;
    public InventoryViewModel(ApiClient api) => _api = api;

    [RelayCommand]
    private async Task RefreshAsync()
    {
        try
        {
            var list = await _api.GetAsync<IReadOnlyList<InventoryListItemDto>>("api/inventory");
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
            await _api.PostAsync("api/inventory", new UpsertInventoryRequest(null, Sku, null, Name, "Parts", 0, 0, 0, 1, 5, null, null));
            Sku = Name = string.Empty;
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
