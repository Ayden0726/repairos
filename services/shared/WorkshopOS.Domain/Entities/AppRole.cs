namespace WorkshopOS.Domain.Entities;

public class AppRole
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsSystem { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public ICollection<RolePermission> Permissions { get; set; } = new List<RolePermission>();
    public ICollection<AppUser> Users { get; set; } = new List<AppUser>();
}
