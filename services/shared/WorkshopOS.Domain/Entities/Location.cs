namespace WorkshopOS.Domain.Entities;

public class Location : SoftDeleteEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? AddressLine1 { get; set; }
    public string? Suburb { get; set; }
    public string? State { get; set; }
    public string? Postcode { get; set; }
    public bool IsDefault { get; set; }
    public ICollection<AppUser> Users { get; set; } = new List<AppUser>();
}
