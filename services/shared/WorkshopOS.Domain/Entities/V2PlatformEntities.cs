namespace WorkshopOS.Domain.Entities;

/// <summary>Parts-to-device fitment rule (compatibility graph edge).</summary>
public class PartFitment : SoftDeleteEntity
{
    public Guid? InventoryItemId { get; set; }
    public InventoryItem? InventoryItem { get; set; }
    /// <summary>SKU when not linked to a stock row (catalogue-only fitment).</summary>
    public string? PartSku { get; set; }
    public string PartName { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string? DeviceType { get; set; }
    public string? Notes { get; set; }
    public bool IsVerified { get; set; } = true;
}

/// <summary>Client offline mutation awaiting server apply / ack.</summary>
public class SyncOutboxEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ClientMutationId { get; set; }
    public Guid UserId { get; set; }
    public string Method { get; set; } = "POST";
    public string Path { get; set; } = string.Empty;
    public string? BodyJson { get; set; }
    public string Status { get; set; } = "Pending"; // Pending, Applied, Rejected, Duplicate
    public string? Error { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? AppliedAt { get; set; }
}

/// <summary>Named stock bin / shelf under a workshop site (optional parent for hierarchy).</summary>
public class InventoryBin : SoftDeleteEntity
{
    public Guid? LocationId { get; set; }
    public Location? Location { get; set; }
    public Guid? ParentBinId { get; set; }
    public InventoryBin? ParentBin { get; set; }
    public ICollection<InventoryBin> Children { get; set; } = new List<InventoryBin>();
    public string Code { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Notes { get; set; }
}
