namespace WorkshopOS.Domain.Entities;

public class BusinessSetting
{
    public string Key { get; set; } = string.Empty;
    public string JsonValue { get; set; } = "{}";
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid? UpdatedById { get; set; }
}
