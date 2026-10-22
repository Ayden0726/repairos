namespace WorkshopOS.Domain.Entities;

/// <summary>Per-user UI preferences (dashboard layout, etc.).</summary>
public class UserUiPreference
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;
    /// <summary>Preference key, e.g. dashboard.layout</summary>
    public string Key { get; set; } = string.Empty;
    public string JsonValue { get; set; } = "{}";
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
