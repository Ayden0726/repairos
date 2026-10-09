namespace WorkshopOS.Domain.Entities;

/// <summary>Customer portal login linked to a CRM <see cref="Customer"/>.</summary>
public class CustomerPortalAccount : SoftDeleteEntity
{
    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public string Email { get; set; } = string.Empty;
    public string NormalizedEmail { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public bool EmailVerified { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }
    public bool MustResetPassword { get; set; }
}

/// <summary>Dedicated WordPress / website API credential (never WordPress admin tokens).</summary>
public class WebsiteApiKey : SoftDeleteEntity
{
    public string Name { get; set; } = string.Empty;
    public string KeyPrefix { get; set; } = string.Empty;
    public string KeyHash { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset? LastUsedAt { get; set; }
    public string? AllowedOrigins { get; set; }
    public Guid CreatedById { get; set; }
}

public class SellDeviceRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Reference { get; set; } = string.Empty;
    public Guid? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public string Status { get; set; } = "New";
    public string Category { get; set; } = "Phone";
    public string Brand { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string? Storage { get; set; }
    public string? Ram { get; set; }
    public string? Cpu { get; set; }
    public string? Gpu { get; set; }
    public string? Colour { get; set; }
    public string? SerialOrImei { get; set; }
    public string? BatteryHealth { get; set; }
    public string? CarrierStatus { get; set; }
    public bool? PowersOn { get; set; }
    public bool? Charges { get; set; }
    public string? ScreenCondition { get; set; }
    public string? BodyCondition { get; set; }
    public bool? CameraOk { get; set; }
    public bool? SpeakerMicOk { get; set; }
    public bool? ButtonsOk { get; set; }
    public bool? BiometricsOk { get; set; }
    public bool? LiquidDamage { get; set; }
    public string? KnownFaults { get; set; }
    public bool? AccountLocked { get; set; }
    public bool? FindMyEnabled { get; set; }
    public bool? GoogleSamsungRemoved { get; set; }
    public string? CustomerNotes { get; set; }
    public string ContactName { get; set; } = string.Empty;
    public string? ContactPhone { get; set; }
    public string? ContactEmail { get; set; }
    public string? Suburb { get; set; }
    public string PreferredContact { get; set; } = "Phone";
    public string? PhotoUrlsJson { get; set; }
    public string? Source { get; set; } = "website";
    public string? InternalNotes { get; set; }
    public SellDeviceOffer? Offer { get; set; }
    public Guid? UsedDeviceId { get; set; }
    public UsedDevice? UsedDevice { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public ICollection<SellDeviceEvent> Events { get; set; } = new List<SellDeviceEvent>();
}

public class SellDeviceOffer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RequestId { get; set; }
    public SellDeviceRequest Request { get; set; } = null!;
    public decimal EstimatedResalePrice { get; set; }
    public decimal PurchaseCost { get; set; }
    public decimal EstimatedPartsCost { get; set; }
    public decimal LabourAllowance { get; set; }
    public decimal SellingPlatformFees { get; set; }
    public decimal ShippingPacking { get; set; }
    public decimal RiskAllowance { get; set; }
    public decimal DesiredMinProfit { get; set; }
    public decimal DesiredMinMarginPercent { get; set; }
    public decimal RecommendedOfferMin { get; set; }
    public decimal RecommendedOffer { get; set; }
    public decimal RecommendedOfferMax { get; set; }
    public decimal AbsoluteMaxPurchasePrice { get; set; }
    public decimal EstimatedTotalInvestment { get; set; }
    public decimal EstimatedResaleProfit { get; set; }
    public decimal EstimatedMarginPercent { get; set; }
    public decimal? CustomerFacingOffer { get; set; }
    public string Status { get; set; } = "Draft";
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? SentAt { get; set; }
    public DateTimeOffset? RespondedAt { get; set; }
}

public class SellDeviceEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RequestId { get; set; }
    public SellDeviceRequest Request { get; set; } = null!;
    public Guid? ActorUserId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class ConversationThread
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Subject { get; set; } = string.Empty;
    public Guid? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public Guid? RepairTicketId { get; set; }
    public RepairTicket? RepairTicket { get; set; }
    public Guid? SellDeviceRequestId { get; set; }
    public SellDeviceRequest? SellDeviceRequest { get; set; }
    public Guid? QuoteId { get; set; }
    public string Status { get; set; } = "Open";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public ICollection<ConversationMessage> Messages { get; set; } = new List<ConversationMessage>();
}

public class ConversationMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ThreadId { get; set; }
    public ConversationThread Thread { get; set; } = null!;
    /// <summary>CustomerVisible | Internal | System | Sms | Email</summary>
    public string Visibility { get; set; } = "CustomerVisible";
    public string Channel { get; set; } = "Portal";
    public string Body { get; set; } = string.Empty;
    public Guid? AuthorUserId { get; set; }
    public Guid? AuthorCustomerId { get; set; }
    public string? AuthorName { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class WarrantyRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public Guid? RepairTicketId { get; set; }
    public RepairTicket? RepairTicket { get; set; }
    public Guid? UsedDeviceId { get; set; }
    public UsedDevice? UsedDevice { get; set; }
    public Guid? DeviceId { get; set; }
    public Device? Device { get; set; }
    public string CoverageSummary { get; set; } = string.Empty;
    public int WarrantyDays { get; set; }
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public string Status { get; set; } = "Active";
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class NotificationTemplate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Channel { get; set; } = "Email"; // Email | Sms | Both
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class NotificationOutbox
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TemplateKey { get; set; } = string.Empty;
    public string Channel { get; set; } = "Email";
    public string ToAddress { get; set; } = string.Empty;
    public string? Subject { get; set; }
    public string Body { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending"; // Pending | Sent | Failed | Skipped
    public string? Error { get; set; }
    public Guid? CustomerId { get; set; }
    public Guid? RepairTicketId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? SentAt { get; set; }
}

public class QuoteApprovalRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid QuoteId { get; set; }
    public Quote Quote { get; set; } = null!;
    public Guid? RepairTicketId { get; set; }
    public Guid CustomerId { get; set; }
    public int QuoteRevisionNumber { get; set; }
    public string Action { get; set; } = string.Empty; // Approved | Declined
    public string? Comment { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string AccessMethod { get; set; } = "PublicLink";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class CustomPcRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Reference { get; set; } = string.Empty;
    public Guid? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public string Status { get; set; } = "New";
    public decimal? Budget { get; set; }
    public string? Usage { get; set; }
    public string? PreferredGamesSoftware { get; set; }
    public string? MonitorResolution { get; set; }
    public string? PerformanceTarget { get; set; }
    public bool? NeedsWifiBluetooth { get; set; }
    public string? RgbPreferences { get; set; }
    public string? CaseSize { get; set; }
    public string? Colour { get; set; }
    public string? StorageRequirements { get; set; }
    public string? Upgradeability { get; set; }
    public string ContactName { get; set; } = string.Empty;
    public string? ContactPhone { get; set; }
    public string? ContactEmail { get; set; }
    public string? CustomerNotes { get; set; }
    public Guid? PcBuildId { get; set; }
    public PcBuild? PcBuild { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class RefurbDiagnosticChecklist
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Category { get; set; } = "Phone";
    public string Name { get; set; } = string.Empty;
    public string ItemsJson { get; set; } = "[]";
    public bool IsActive { get; set; } = true;
}

public class RefurbDiagnosticResult
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UsedDeviceId { get; set; }
    public UsedDevice UsedDevice { get; set; } = null!;
    public Guid? ChecklistId { get; set; }
    public string ResultsJson { get; set; } = "{}";
    public string? OverallGrade { get; set; }
    public Guid? CompletedById { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
