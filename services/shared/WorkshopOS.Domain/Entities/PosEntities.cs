namespace WorkshopOS.Domain.Entities;

/// <summary>Counter sale created by the POS register. Always linked to an Invoice.</summary>
public class PosSale : SoftDeleteEntity
{
    public string Number { get; set; } = string.Empty;
    public Guid InvoiceId { get; set; }
    public Invoice Invoice { get; set; } = null!;
    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public Guid CreatedById { get; set; }
    public Guid? RepairTicketId { get; set; }
    public RepairTicket? RepairTicket { get; set; }
    public string Status { get; set; } = "Completed"; // Completed, Voided
    public string PaymentMethod { get; set; } = "Cash"; // Cash, Eftpos, Card, BankTransfer, Other
    public string PaymentProvider { get; set; } = "None"; // None, ManualEftpos, Tyro, Square, StripeTerminal, Linkly
    public string? ProviderReference { get; set; }
    public string ProviderStatus { get; set; } = "Completed"; // Pending, Completed, Failed, Manual
    public decimal Subtotal { get; set; }
    public decimal GstAmount { get; set; }
    public decimal Total { get; set; }
    public decimal AmountPaid { get; set; }
    public string? DiscountLabel { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal DiscountAmount { get; set; }
    public bool IsDeposit { get; set; }
    public string? Notes { get; set; }
    public ICollection<PosSaleLine> Lines { get; set; } = new List<PosSaleLine>();
}

public class PosSaleLine
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SaleId { get; set; }
    public PosSale Sale { get; set; } = null!;
    public Guid? InventoryItemId { get; set; }
    public InventoryItem? InventoryItem { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? Sku { get; set; }
    public decimal Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
    public int SortOrder { get; set; }
}
