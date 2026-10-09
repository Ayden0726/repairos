namespace WorkshopOS.Domain.Entities;

public class ServiceCategory : SoftDeleteEntity
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? DeviceType { get; set; }
    public Guid? ParentId { get; set; }
    public ServiceCategory? Parent { get; set; }
    public ICollection<ServiceCategory> Children { get; set; } = new List<ServiceCategory>();
    public int SortOrder { get; set; }
    public bool IsSystem { get; set; }
    public string? Icon { get; set; }
    public ICollection<ServicePricing> Services { get; set; } = new List<ServicePricing>();
}

public class ServiceBundle : SoftDeleteEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal? BundlePrice { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public bool IsSystem { get; set; }
    public ICollection<ServiceBundleItem> Items { get; set; } = new List<ServiceBundleItem>();
}

public class ServiceBundleItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BundleId { get; set; }
    public ServiceBundle Bundle { get; set; } = null!;
    public Guid ServicePricingId { get; set; }
    public ServicePricing ServicePricing { get; set; } = null!;
    public int SortOrder { get; set; }
}

public class ServiceFavourite
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid ServicePricingId { get; set; }
    public ServicePricing ServicePricing { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class ServiceRecentSelection
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid ServicePricingId { get; set; }
    public ServicePricing ServicePricing { get; set; } = null!;
    public DateTimeOffset SelectedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class DeviceBrand : SoftDeleteEntity
{
    public string Name { get; set; } = string.Empty;
    /// <summary>Comma-separated device types this brand applies to (Phone,Tablet,Laptop,PC).</summary>
    public string DeviceTypes { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsSystem { get; set; }
    public ICollection<DeviceModel> Models { get; set; } = new List<DeviceModel>();
}

public class DeviceModel : SoftDeleteEntity
{
    public Guid BrandId { get; set; }
    public DeviceBrand Brand { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string? DeviceType { get; set; }
    public int SortOrder { get; set; }
    public bool IsSystem { get; set; }
}

/// <summary>Per-service line on a multi-service repair ticket (work tracking; money still lives on quotes).</summary>
public class RepairServiceLine : SoftDeleteEntity
{
    public Guid RepairTicketId { get; set; }
    public RepairTicket RepairTicket { get; set; } = null!;
    public Guid? ServicePricingId { get; set; }
    public ServicePricing? ServicePricing { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public string? Code { get; set; }
    public decimal LabourFee { get; set; }
    public decimal ServiceFee { get; set; }
    public int? EstimatedMinutes { get; set; }
    public int? WarrantyDays { get; set; }
    public string? Notes { get; set; }
    public string? PartsJson { get; set; }
    public bool IsCompleted { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public int SortOrder { get; set; }
}
