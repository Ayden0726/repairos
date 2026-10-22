namespace WorkshopOS.Domain.Entities;

/// <summary>
/// Outbound buying opportunity (Facebook Marketplace, eBay, Gumtree, trade-in, wholesale, walk-in, etc.).
/// Distinct from inbound <see cref="SellDeviceRequest"/> (Sell Your Tech).
/// </summary>
public class AcquisitionLead : SoftDeleteEntity
{
    public string Reference { get; set; } = string.Empty;
    /// <summary>New Lead | Under Evaluation | Offer Prepared | Offer Sent | Negotiating | Accepted | Purchased | Awaiting Arrival | Received | Inspection | Rejected | Cancelled | Seller Unresponsive | Sold Elsewhere | Returned to Seller</summary>
    public string Status { get; set; } = "New Lead";
    public string Brand { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string? ModelNumber { get; set; }
    public string? Category { get; set; } = "Phone";
    public string? StorageCapacity { get; set; }
    public string? Colour { get; set; }
    public string? Imei { get; set; }
    public string? Serial { get; set; }
    public string? Carrier { get; set; }
    public string? SellerName { get; set; }
    public string? SellerContact { get; set; }
    public string? SellerEmail { get; set; }
    public string? SellerPhone { get; set; }
    /// <summary>Individual | Business | Wholesale | Unknown</summary>
    public string SellerType { get; set; } = "Individual";
    /// <summary>Facebook Marketplace | eBay | Gumtree | Trade-In | Walk-In | Wholesale | Buy/Sell Group | Other Online | Other</summary>
    public string AcquisitionSource { get; set; } = "Other";
    public string? ListingUrl { get; set; }
    public decimal? AskingPrice { get; set; }
    public decimal? ProposedOffer { get; set; }
    public decimal? AcceptedPurchasePrice { get; set; }
    public string? DeviceCondition { get; set; }
    public string? KnownFaults { get; set; }
    public string? SuspectedFaults { get; set; }
    /// <summary>Unknown | Cleared | Locked | Suspected Locked | Not Applicable</summary>
    public string ActivationLockStatus { get; set; } = "Unknown";
    public string? PhotoUrlsJson { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset? PurchaseDate { get; set; }
    public string? PaymentMethod { get; set; }
    public string? ShippingOrCollection { get; set; }
    public Guid? AssignedStaffId { get; set; }
    public DateTimeOffset? FollowUpAt { get; set; }
    public Guid? UsedDeviceId { get; set; }
    public UsedDevice? UsedDevice { get; set; }
    public Guid? SellerCustomerId { get; set; }

    public ICollection<AcquisitionOffer> Offers { get; set; } = new List<AcquisitionOffer>();
    public ICollection<AcquisitionEvent> Events { get; set; } = new List<AcquisitionEvent>();
}

public class AcquisitionOffer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AcquisitionLeadId { get; set; }
    public AcquisitionLead AcquisitionLead { get; set; } = null!;
    public decimal Amount { get; set; }
    /// <summary>Draft | Prepared | Sent | Accepted | Declined | Superseded</summary>
    public string Status { get; set; } = "Draft";
    public string? Message { get; set; }
    public string? Notes { get; set; }
    public Guid? CreatedById { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? SentAt { get; set; }
    public DateTimeOffset? RespondedAt { get; set; }

    // Snapshot of buy calculator at offer time (AUD)
    public decimal? EstimatedResalePrice { get; set; }
    public decimal? EstimatedPartsCost { get; set; }
    public decimal? LabourAllowance { get; set; }
    public decimal? EstimatedProfit { get; set; }
    public decimal? EstimatedMarginPercent { get; set; }
    public decimal? AbsoluteMaxPurchasePrice { get; set; }
    public string? BuyRecommendation { get; set; }
}

public class AcquisitionEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AcquisitionLeadId { get; set; }
    public AcquisitionLead AcquisitionLead { get; set; } = null!;
    public Guid? ActorUserId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
