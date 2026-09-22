namespace WorkshopOS.Domain.Entities;

public class Quote : SoftDeleteEntity
{
    public string Number { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public Guid? RepairTicketId { get; set; }
    public RepairTicket? RepairTicket { get; set; }
    public string Status { get; set; } = "Draft"; // Draft, Sent, Approved, Declined, Expired
    public string? Issue { get; set; }
    public string? CustomerNotes { get; set; }
    public string? InternalNotes { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public Guid CreatedById { get; set; }
    public decimal Subtotal { get; set; }
    public decimal GstAmount { get; set; }
    public decimal Total { get; set; }
    public ICollection<QuoteLine> Lines { get; set; } = new List<QuoteLine>();
}

public class QuoteLine
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid QuoteId { get; set; }
    public Quote Quote { get; set; } = null!;
    public string Type { get; set; } = "SERVICE";
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public int SortOrder { get; set; }
}

public class Invoice : SoftDeleteEntity
{
    public string Number { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public Guid? RepairTicketId { get; set; }
    public RepairTicket? RepairTicket { get; set; }
    public Guid? QuoteId { get; set; }
    public string Status { get; set; } = "Draft"; // Draft, Unpaid, PartiallyPaid, Paid, Refunded, Cancelled
    public Guid CreatedById { get; set; }
    public decimal Subtotal { get; set; }
    public decimal GstAmount { get; set; }
    public decimal Total { get; set; }
    public decimal AmountPaid { get; set; }
    public DateTimeOffset? IssuedAt { get; set; }
    public DateTimeOffset? DueAt { get; set; }
    public ICollection<InvoiceLine> Lines { get; set; } = new List<InvoiceLine>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}

public class InvoiceLine
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InvoiceId { get; set; }
    public Invoice Invoice { get; set; } = null!;
    public string Type { get; set; } = "SERVICE";
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public int SortOrder { get; set; }
}

public class Payment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InvoiceId { get; set; }
    public Invoice Invoice { get; set; } = null!;
    public string Method { get; set; } = "Cash"; // Cash, Eftpos, BankTransfer, Other
    public decimal Amount { get; set; }
    public string? Reference { get; set; }
    public Guid RecordedById { get; set; }
    public DateTimeOffset PaidAt { get; set; } = DateTimeOffset.UtcNow;
    public bool IsDeposit { get; set; }
}

public class InventoryItem : SoftDeleteEntity
{
    public string Sku { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Category { get; set; } = "Parts";
    public string? Manufacturer { get; set; }
    public Guid? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
    public decimal Cost { get; set; }
    public decimal SellPrice { get; set; }
    public int QuantityOnHand { get; set; }
    public int QuantityReserved { get; set; }
    public int MinimumStock { get; set; }
    public int ReorderQuantity { get; set; }
    public string? LocationBin { get; set; }
    public int Available => Math.Max(0, QuantityOnHand - QuantityReserved);
}

public class InventoryTransaction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ItemId { get; set; }
    public InventoryItem Item { get; set; } = null!;
    public string Type { get; set; } = "Adjustment";
    public int QuantityDelta { get; set; }
    public string Reason { get; set; } = string.Empty;
    public Guid? TicketId { get; set; }
    public Guid? ActorUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class InventoryReservation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ItemId { get; set; }
    public InventoryItem Item { get; set; } = null!;
    public Guid TicketId { get; set; }
    public RepairTicket Ticket { get; set; } = null!;
    public int Quantity { get; set; }
    public string Status { get; set; } = "Reserved"; // Reserved, Consumed, Released
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class Supplier : SoftDeleteEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Contact { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }
    public string? AccountNumber { get; set; }
    public string? Notes { get; set; }
}

public class PurchaseOrder : SoftDeleteEntity
{
    public string Number { get; set; } = string.Empty;
    public Guid SupplierId { get; set; }
    public Supplier Supplier { get; set; } = null!;
    public string Status { get; set; } = "Draft"; // Draft, Ordered, PartiallyReceived, Received, Cancelled
    public DateTimeOffset? OrderedAt { get; set; }
    public DateTimeOffset? ExpectedAt { get; set; }
    public DateTimeOffset? ReceivedAt { get; set; }
    public Guid CreatedById { get; set; }
    public decimal Shipping { get; set; }
    public decimal GstAmount { get; set; }
    public decimal Total { get; set; }
    public ICollection<PurchaseOrderLine> Lines { get; set; } = new List<PurchaseOrderLine>();
}

public class PurchaseOrderLine
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PurchaseOrderId { get; set; }
    public PurchaseOrder PurchaseOrder { get; set; } = null!;
    public Guid? ItemId { get; set; }
    public InventoryItem? Item { get; set; }
    public string Description { get; set; } = string.Empty;
    public int QuantityOrdered { get; set; }
    public int QuantityReceived { get; set; }
    public decimal UnitCost { get; set; }
}

public class AppNotification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Severity { get; set; } = "Info"; // Info, Warning, Critical, Success
    public string? Route { get; set; }
    public bool IsRead { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class Booking
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public Guid? StaffId { get; set; }
    public AppUser? Staff { get; set; }
    public string Type { get; set; } = "Intake"; // Intake, Diagnostic, Pickup, Onsite, Consult
    public string Status { get; set; } = "Confirmed";
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class KnowledgeArticle : SoftDeleteEntity
{
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = "General";
    public string Body { get; set; } = string.Empty;
    public string? Tags { get; set; }
    public Guid AuthorId { get; set; }
}

public class PcBuild : SoftDeleteEntity
{
    public string Number { get; set; } = string.Empty;
    public Guid? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public Guid? AssignedToId { get; set; }
    public string Status { get; set; } = "Quoted";
    public string? UseCase { get; set; }
    public decimal? Budget { get; set; }
    public decimal CostTotal { get; set; }
    public decimal SellTotal { get; set; }
    public string? Notes { get; set; }
    public ICollection<PcBuildPart> Parts { get; set; } = new List<PcBuildPart>();
}

public class PcBuildPart
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PcBuildId { get; set; }
    public PcBuild PcBuild { get; set; } = null!;
    public string Category { get; set; } = "Other";
    public string Name { get; set; } = string.Empty;
    public Guid? InventoryItemId { get; set; }
    public decimal Cost { get; set; }
    public decimal SellPrice { get; set; }
}

public class UsedDevice : SoftDeleteEntity
{
    public string Summary { get; set; } = string.Empty;
    public string? Serial { get; set; }
    public string? Imei { get; set; }
    public string ConditionGrade { get; set; } = "B";
    public string Status { get; set; } = "Purchased";
    public decimal PurchasePrice { get; set; }
    public decimal ExpectedResale { get; set; }
    public decimal ExpectedRepairCost { get; set; }
    public decimal? ActualSalePrice { get; set; }
    public string? Faults { get; set; }
    public Guid? SellerCustomerId { get; set; }
}

public class QaChecklist
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TicketId { get; set; }
    public RepairTicket Ticket { get; set; } = null!;
    public string Item { get; set; } = string.Empty;
    public string Result { get; set; } = "NotTested"; // Pass, Fail, NotApplicable, NotTested
    public int SortOrder { get; set; }
}

public class BackupRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Type { get; set; } = "Manual";
    public string Status { get; set; } = "Completed";
    public string? Path { get; set; }
    public string? Detail { get; set; }
    public Guid? StartedById { get; set; }
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? FinishedAt { get; set; }
}
