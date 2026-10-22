namespace WorkshopOS.Contracts.Operations;

/// <summary>Known dashboard widget identifiers.</summary>
public static class DashboardWidgetIds
{
    public const string MyTickets = "myTickets";
    public const string SummaryCards = "summaryCards";
    public const string Calendar = "calendar";
    public const string Pipeline = "pipeline";
    public const string Workload = "workload";
    public const string LowStock = "lowStock";
    public const string Activity = "activity";

    public static readonly IReadOnlyList<string> All =
    [
        MyTickets, SummaryCards, Calendar, Pipeline, Workload, LowStock, Activity
    ];

    public static string DisplayName(string id) => id switch
    {
        MyTickets => "My open tickets",
        SummaryCards => "Summary cards",
        Calendar => "Calendar",
        Pipeline => "Repair pipeline",
        Workload => "Technician workload",
        LowStock => "Low stock",
        Activity => "Recent activity",
        _ => id
    };
}

public sealed record DashboardWidgetPreferenceDto(
    string Id,
    string Title,
    bool Visible,
    int Order);

public sealed record DashboardLayoutDto(
    IReadOnlyList<DashboardWidgetPreferenceDto> Widgets);

public sealed record UpdateDashboardLayoutRequest(
    IReadOnlyList<DashboardWidgetPreferenceDto> Widgets);
