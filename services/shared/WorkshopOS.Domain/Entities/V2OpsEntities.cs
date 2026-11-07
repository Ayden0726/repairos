namespace WorkshopOS.Domain.Entities;

public class DonorDevice : SoftDeleteEntity
{
    public string Number { get; set; } = string.Empty;
    public string? Brand { get; set; }
    public string? Model { get; set; }
    public string? Imei { get; set; }
    public string? Serial { get; set; }
    public string Status { get; set; } = "Open"; // Open, Harvesting, Exhausted, Scrapped
    public string? Notes { get; set; }
    public Guid? SourceUsedDeviceId { get; set; }
    public UsedDevice? SourceUsedDevice { get; set; }
    public Guid? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public ICollection<HarvestedComponent> Components { get; set; } = new List<HarvestedComponent>();
}

public class HarvestedComponent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DonorDeviceId { get; set; }
    public DonorDevice DonorDevice { get; set; } = null!;
    public string Description { get; set; } = string.Empty;
    public string? ConditionGrade { get; set; }
    public Guid? InventoryItemId { get; set; }
    public InventoryItem? InventoryItem { get; set; }
    public int Quantity { get; set; } = 1;
    public DateTimeOffset HarvestedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid? HarvestedById { get; set; }
}

public class LoanDevice : SoftDeleteEntity
{
    public string AssetTag { get; set; } = string.Empty;
    public string? Brand { get; set; }
    public string? Model { get; set; }
    public string? Imei { get; set; }
    public string? Serial { get; set; }
    public string Status { get; set; } = "Available"; // Available, OnLoan, Maintenance, Retired
    public string? Notes { get; set; }
    public ICollection<LoanAgreement> Agreements { get; set; } = new List<LoanAgreement>();
}

public class LoanAgreement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid LoanDeviceId { get; set; }
    public LoanDevice LoanDevice { get; set; } = null!;
    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public Guid? RepairTicketId { get; set; }
    public RepairTicket? RepairTicket { get; set; }
    public DateTimeOffset CheckedOutAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? DueAt { get; set; }
    public DateTimeOffset? ReturnedAt { get; set; }
    public string Status { get; set; } = "Active"; // Active, Returned, Overdue, Lost
    public string? Notes { get; set; }
    public Guid CheckedOutById { get; set; }
}

public class WarrantyClaim : SoftDeleteEntity
{
    public string Number { get; set; } = string.Empty;
    public Guid WarrantyRecordId { get; set; }
    public WarrantyRecord WarrantyRecord { get; set; } = null!;
    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public Guid? RepairTicketId { get; set; }
    public RepairTicket? RepairTicket { get; set; }
    public string Status { get; set; } = "Open"; // Open, Approved, Rejected, Completed
    public string Summary { get; set; } = string.Empty;
    public string? ResolutionNotes { get; set; }
    public DateTimeOffset OpenedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ClosedAt { get; set; }
    public Guid OpenedById { get; set; }
}

public class RefundRequest : SoftDeleteEntity
{
    public string Number { get; set; } = string.Empty;
    public Guid? PosSaleId { get; set; }
    public PosSale? PosSale { get; set; }
    public Guid? InvoiceId { get; set; }
    public Invoice? Invoice { get; set; }
    public Guid RequestedById { get; set; }
    public Guid? ApprovedById { get; set; }
    public decimal Amount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending"; // Pending, Approved, Rejected, Completed
    public string? DecisionNotes { get; set; }
    public DateTimeOffset RequestedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? DecidedAt { get; set; }
}

public class RepairAttachment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TicketId { get; set; }
    public RepairTicket Ticket { get; set; } = null!;
    public string Kind { get; set; } = "Photo"; // Photo, Document, Other
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
    public string StoragePath { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string? Caption { get; set; }
    public Guid UploadedById { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class RepairSignature
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TicketId { get; set; }
    public RepairTicket Ticket { get; set; } = null!;
    public string SignerName { get; set; } = string.Empty;
    public string Purpose { get; set; } = "Intake"; // Intake, Pickup, Waiver
    public string StoragePath { get; set; } = string.Empty;
    public DateTimeOffset SignedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid? CapturedById { get; set; }
}

public class PrinterProfile : SoftDeleteEntity
{
    public string Name { get; set; } = string.Empty;
    public string Kind { get; set; } = "PdfFallback"; // PdfFallback, Brother, Zebra, Network
    public string? Connection { get; set; }
    public string? DefaultTemplate { get; set; } // repair | used | part
    public bool IsDefault { get; set; }
}

public class PrintJob
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? PrinterProfileId { get; set; }
    public PrinterProfile? PrinterProfile { get; set; }
    public string LabelType { get; set; } = "part";
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string Status { get; set; } = "Rendered"; // Rendered, Sent, Failed
    public string? OutputPath { get; set; }
    public Guid RequestedById { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class IntegrationHealthEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ProviderKey { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid? ActorUserId { get; set; }
}
