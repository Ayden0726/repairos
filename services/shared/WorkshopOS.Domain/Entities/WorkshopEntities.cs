namespace WorkshopOS.Domain.Entities;

public class Customer : SoftDeleteEntity
{
    public WorkshopOS.Contracts.Workshop.CustomerType Type { get; set; } = WorkshopOS.Contracts.Workshop.CustomerType.Individual;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? CompanyName { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? AddressLine1 { get; set; }
    public string? Suburb { get; set; }
    public string? State { get; set; }
    public string? Postcode { get; set; }
    public string? Notes { get; set; }
    public WorkshopOS.Contracts.Workshop.PreferredContact PreferredContact { get; set; } = WorkshopOS.Contracts.Workshop.PreferredContact.Sms;
    public bool MarketingConsent { get; set; }
    public Guid? LocationId { get; set; }
    public Location? Location { get; set; }
    public DateTimeOffset? LastVisitAt { get; set; }
    public ICollection<Device> Devices { get; set; } = new List<Device>();
    public ICollection<RepairTicket> Repairs { get; set; } = new List<RepairTicket>();
}

public class Device : SoftDeleteEntity
{
    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public WorkshopOS.Contracts.Workshop.DeviceCategory Category { get; set; } = WorkshopOS.Contracts.Workshop.DeviceCategory.Other;
    public string Brand { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string? Variant { get; set; }
    public string? Colour { get; set; }
    public string? Serial { get; set; }
    public string? Imei { get; set; }
    public string? StorageCapacity { get; set; }
    public string? Notes { get; set; }
    public ICollection<RepairTicket> Repairs { get; set; } = new List<RepairTicket>();
}

public class RepairStatus
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Colour { get; set; } = "#64748b";
    public int SortOrder { get; set; }
    public bool IsSystem { get; set; }
    public bool IsCompleted { get; set; }
    public bool IsCancelled { get; set; }
    public bool CountsAsWaitingForParts { get; set; }
    public bool CountsAsAwaitingApproval { get; set; }
    public bool CountsAsReadyForPickup { get; set; }
    public DateTimeOffset? ArchivedAt { get; set; }
}

public class RepairType
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Prefix { get; set; } = "REP";
    public int SortOrder { get; set; }
    public bool IsSystem { get; set; }
    public DateTimeOffset? ArchivedAt { get; set; }
}

public class RepairPriority
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public int Severity { get; set; }
    public DateTimeOffset? ArchivedAt { get; set; }
}

public class DocumentSequence
{
    public string Key { get; set; } = string.Empty;
    public int NextValue { get; set; } = 1;
    public int Year { get; set; }
}

public class RepairTicket : SoftDeleteEntity
{
    public string TicketNumber { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public Guid? DeviceId { get; set; }
    public Device? Device { get; set; }
    public Guid TypeId { get; set; }
    public RepairType Type { get; set; } = null!;
    public Guid StatusId { get; set; }
    public RepairStatus Status { get; set; } = null!;
    public Guid PriorityId { get; set; }
    public RepairPriority Priority { get; set; } = null!;
    public Guid? AssignedToId { get; set; }
    public AppUser? AssignedTo { get; set; }
    public Guid CreatedById { get; set; }
    public AppUser CreatedBy { get; set; } = null!;
    public Guid? LocationId { get; set; }
    public Location? Location { get; set; }
    public string ReportedIssue { get; set; } = string.Empty;
    public string? Diagnosis { get; set; }
    public string? RecommendedRepair { get; set; }
    public string? DamageDescription { get; set; }
    public bool? HasExistingCracks { get; set; }
    public bool? HasScratches { get; set; }
    public bool? WaterDamageIndicators { get; set; }
    public bool? PowersOn { get; set; }
    public bool AccessoriesIncluded { get; set; }
    public bool ChargerIncluded { get; set; }
    public bool SimIncluded { get; set; }
    public bool CaseIncluded { get; set; }
    public string? PasscodeHint { get; set; }
    public string? PasscodeEnc { get; set; }
    public decimal? EstimatedPrice { get; set; }
    public decimal? DepositAmount { get; set; }
    public DateTimeOffset? DueAt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset? CollectedAt { get; set; }
    public ICollection<RepairEvent> Events { get; set; } = new List<RepairEvent>();
    public ICollection<RepairNote> Notes { get; set; } = new List<RepairNote>();
}

public class RepairEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TicketId { get; set; }
    public RepairTicket Ticket { get; set; } = null!;
    public Guid? ActorUserId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class RepairNote
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TicketId { get; set; }
    public RepairTicket Ticket { get; set; } = null!;
    public Guid AuthorId { get; set; }
    public AppUser Author { get; set; } = null!;
    public string Body { get; set; } = string.Empty;
    public bool IsInternal { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
