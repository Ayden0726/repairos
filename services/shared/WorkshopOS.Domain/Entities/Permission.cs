namespace WorkshopOS.Domain.Entities;

public class PermissionDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Key { get; set; } = string.Empty;
    public string Group { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}

public class RolePermission
{
    public Guid RoleId { get; set; }
    public AppRole Role { get; set; } = null!;
    public string PermissionKey { get; set; } = string.Empty;
}
