namespace WorkshopOS.Contracts.Common;

/// <summary>UI complexity profile — FullShop (default) or Home (side-hustle / beginner).</summary>
public static class WorkshopStyles
{
    public const string FullShop = "FullShop";
    public const string Home = "Home";

    public static bool IsHome(string? style) =>
        string.Equals(style, Home, StringComparison.OrdinalIgnoreCase);

    public static string Normalize(string? style) =>
        IsHome(style) ? Home : FullShop;
}

public sealed record WorkshopStyleSettingsDto(
    string Style = WorkshopStyles.FullShop,
    bool ShowBeginnerTips = true);

public sealed record UpdateWorkshopStyleRequest(string Style, bool ShowBeginnerTips = true);

/// <summary>Starter job ideas for home / side-hustle intake.</summary>
public sealed record JobTemplateDto(
    string Key,
    string Name,
    string DeviceType,
    string IssueHint,
    string? SuggestedPriorityKey = null,
    string? Tip = null);

/// <summary>Simplified money snapshot for Home workshop mode.</summary>
public sealed record HomeMoneySummaryDto(
    decimal PaidThisMonth,
    decimal OutstandingBalance,
    decimal PartsCostThisMonth,
    decimal EstimatedProfitThisMonth,
    int OpenJobs,
    int JobsCompletedThisMonth,
    string Currency,
    string PeriodLabel);

public static class JobTemplates
{
    public static IReadOnlyList<JobTemplateDto> All { get; } =
    [
        new("phone_screen", "Phone screen repair", "Phone",
            "Cracked or unresponsive display. Note liquid damage if any.",
            "normal", "Photograph the device before opening — handy if the customer questions damage later."),
        new("phone_battery", "Phone battery swap", "Phone",
            "Battery health low / swelling / sudden shutdowns.",
            "normal", "Warn the customer if the battery is swollen — do not puncture it."),
        new("laptop_ssd", "Laptop SSD upgrade", "Laptop",
            "Slow boot / storage full. Customer wants larger or faster drive.",
            "normal", "Ask whether they want data migrated before you wipe the old drive."),
        new("laptop_battery", "Laptop battery", "Laptop",
            "Battery not charging or lasting only a short time.",
            "normal", null),
        new("console_clean", "Console clean & paste", "Other",
            "Overheating / loud fans. Clean dust and refresh thermal paste.",
            "normal", "Keep the original screws organised by panel — consoles hate missing fasteners."),
        new("pc_build_check", "PC won't boot", "PC",
            "No POST / no display. Basic power and cable check first.",
            "high", "Confirm the monitor and cable work on another machine before deep diagnosis."),
        new("data_recovery", "Data backup / recovery", "Other",
            "Customer needs files off a failing drive or old phone.",
            "high", "Never write to the failing drive. Image first if the data matters."),
        new("general_diag", "General diagnosis", "Other",
            "Customer unsure what is wrong. Book a diagnostic fee.",
            "normal", "Agree the diagnostic fee up front before you open the device.")
    ];
}
