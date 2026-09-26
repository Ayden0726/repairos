namespace WorkshopOS.Contracts.Operations;

public sealed record DashboardDto(
    DashboardCardsDto Cards,
    IReadOnlyList<PipelineStageDto> Pipeline,
    IReadOnlyList<UrgentJobDto> UrgentJobs,
    IReadOnlyList<TechnicianWorkloadDto> Workload,
    int UnassignedJobs,
    IReadOnlyList<LowStockDto> LowStock,
    IReadOnlyList<ActivityDto> RecentActivity,
    IReadOnlyList<TodayItemDto> TodaysWork,
    QuoteAnalyticsDto? QuoteAnalytics = null);

public sealed record QuoteAnalyticsDto(
    int QuotesSent30Days,
    int QuotesAccepted30Days,
    int QuotesDeclined30Days,
    int QuotesExpired30Days,
    decimal ConversionRatePercent,
    decimal AverageAcceptedTotal,
    decimal AverageAcceptedMarginPercent);

public sealed record DashboardCardsDto(
    int OpenJobs, int DueToday, int AwaitingApproval, int WaitingForParts,
    int ReadyForPickup, int Overdue, decimal Revenue30Days, decimal GrossProfit30Days);

public sealed record PipelineStageDto(string Key, string Name, int Count, string Colour);
public sealed record UrgentJobDto(Guid Id, string TicketNumber, string Customer, string? Device, string Status, string Priority, string? Technician, DateTimeOffset? DueAt, string OverdueLabel);
public sealed record TechnicianWorkloadDto(Guid Id, string Name, int OpenJobs, int DueToday, int Overdue, string? ActiveTicket);
public sealed record LowStockDto(Guid Id, string Sku, string Name, int Available, int Reserved, int Minimum, string? Supplier);
public sealed record ActivityDto(Guid Id, DateTimeOffset At, string Action, string Summary, string? Route);
public sealed record TodayItemDto(string Kind, string Title, DateTimeOffset When, string? Route);

public sealed record QuoteListItemDto(
    Guid Id, string Number, string CustomerName, string Status, decimal Total,
    DateTimeOffset CreatedAt, DateTimeOffset? ExpiresAt, Guid? RepairTicketId, int RevisionNumber,
    decimal? MarginPercent, bool RequiresApproval);

public sealed record QuoteLineDetailDto(
    Guid Id, string Type, string Description, string? ServiceName, string? PartName,
    string? SupplierName, string? Sku, Guid? InventoryItemId, Guid? ServicePricingId,
    string? DifficultyLevelKey, decimal Quantity,
    decimal PartCost, decimal ShippingCost, decimal OtherCost, decimal LandedCost,
    decimal MarkupPercent, decimal MarkupAmount, decimal PartSell, decimal LabourAmount,
    decimal AdditionalAmount, decimal DiscountAmount, decimal UnitPrice,
    decimal LineSubtotal, decimal LineTotal, decimal LineProfit);

public sealed record QuoteDetailDto(
    Guid Id, string Number, Guid CustomerId, string CustomerName, Guid? RepairTicketId,
    string Status, string? Issue, string? CustomerNotes, string? InternalNotes,
    string? DeviceBrand, string? DeviceModel, string? DeviceSerial, string? DeviceCategory,
    int ValidityDays, int RevisionNumber, DateTimeOffset? ExpiresAt,
    DateTimeOffset? AcceptedAt, Guid? AcceptedById, decimal? AcceptedTotal, int? AcceptedVersion,
    bool IsFrozen, decimal PartsSubtotal, decimal LabourSubtotal, decimal DiscountTotal,
    decimal CostTotal, decimal ProfitTotal, decimal MarginPercent, bool RequiresApproval,
    string? RoundingMethod, decimal PreRoundTotal,
    decimal Subtotal, decimal GstAmount, decimal Total,
    IReadOnlyList<QuoteLineDetailDto> Lines,
    IReadOnlyList<QuoteRevisionDto>? Revisions,
    bool IncludeInternalFinancials);

public sealed record QuoteRevisionDto(Guid Id, int RevisionNumber, DateTimeOffset CreatedAt, string? Reason);
public sealed record QuoteAuditDto(Guid Id, string Action, string? Detail, DateTimeOffset CreatedAt, Guid? ActorUserId);

public sealed record LineInputDto(string Type, string Description, decimal Quantity, decimal UnitPrice);
public sealed record LineDto(Guid Id, string Type, string Description, decimal Quantity, decimal UnitPrice, decimal LineTotal);

public sealed record QuoteLineInputDto(
    string Type,
    string Description,
    string? ServiceName,
    string? PartName,
    string? SupplierName,
    string? Sku,
    Guid? InventoryItemId,
    Guid? ServicePricingId,
    string? DifficultyLevelKey,
    decimal Quantity,
    decimal PartCost,
    decimal ShippingCost,
    decimal OtherCost,
    decimal? MarkupPercent,
    decimal? MarkupAmount,
    decimal? PartSell,
    decimal? LabourAmount,
    decimal AdditionalAmount,
    decimal DiscountAmount,
    decimal? UnitPrice);

public sealed record CreateQuoteRequest(
    Guid CustomerId,
    Guid? RepairTicketId,
    string? Issue,
    string? CustomerNotes,
    string? InternalNotes,
    string? DeviceBrand,
    string? DeviceModel,
    string? DeviceSerial,
    string? DeviceCategory,
    int? ValidityDays,
    IReadOnlyList<QuoteLineInputDto>? Lines,
    /// <summary>Legacy simple lines (UnitPrice × Qty). Prefer Lines.</summary>
    IReadOnlyList<LineInputDto>? SimpleLines = null);

public sealed record UpdateQuoteRequest(
    string? Issue,
    string? CustomerNotes,
    string? InternalNotes,
    string? DeviceBrand,
    string? DeviceModel,
    string? DeviceSerial,
    string? DeviceCategory,
    int? ValidityDays,
    IReadOnlyList<QuoteLineInputDto> Lines,
    string? ReviseReason);

public sealed record QuoteStatusRequest(string Status);
public sealed record QuoteSearchRequest(string? Q, string? Status, Guid? CustomerId, Guid? RepairTicketId, DateTimeOffset? From, DateTimeOffset? To);
public sealed record ConvertQuoteToRepairRequest(Guid? RepairTicketId);

public sealed record InvoiceListItemDto(Guid Id, string Number, string CustomerName, string Status, decimal Total, decimal AmountPaid, decimal Balance, DateTimeOffset CreatedAt);
public sealed record InvoiceDetailDto(Guid Id, string Number, Guid CustomerId, string CustomerName, Guid? RepairTicketId, string Status, decimal Subtotal, decimal GstAmount, decimal Total, decimal AmountPaid, decimal Balance, IReadOnlyList<LineDto> Lines, IReadOnlyList<PaymentDto> Payments);
public sealed record CreateInvoiceFromRepairRequest(Guid RepairTicketId);
public sealed record RecordPaymentRequest(string Method, decimal Amount, string? Reference, bool IsDeposit);
public sealed record PaymentDto(Guid Id, string Method, decimal Amount, string? Reference, DateTimeOffset PaidAt, bool IsDeposit);

public sealed record InventoryListItemDto(
    Guid Id, string Sku, string Name, string Category, int OnHand, int Reserved, int Available,
    int Minimum, decimal SellPrice, bool IsLow, decimal Cost = 0m, string? SupplierName = null);
public sealed record UpsertInventoryRequest(Guid? Id, string Sku, string? Barcode, string Name, string Category, decimal Cost, decimal SellPrice, int QuantityOnHand, int MinimumStock, int ReorderQuantity, string? LocationBin, Guid? SupplierId);
public sealed record AdjustStockRequest(int QuantityDelta, string Reason);
public sealed record ReserveStockRequest(Guid ItemId, Guid TicketId, int Quantity);

public sealed record SupplierDto(Guid Id, string Name, string? Phone, string? Email);
public sealed record UpsertSupplierRequest(Guid? Id, string Name, string? Contact, string? Phone, string? Email, string? Website, string? AccountNumber, string? Notes);
public sealed record PurchaseOrderListItemDto(Guid Id, string Number, string SupplierName, string Status, decimal Total, DateTimeOffset? ExpectedAt);
public sealed record CreatePurchaseOrderRequest(Guid SupplierId, DateTimeOffset? ExpectedAt, decimal Shipping, IReadOnlyList<PoLineInputDto> Lines);
public sealed record PoLineInputDto(Guid? ItemId, string Description, int Quantity, decimal UnitCost);
public sealed record ReceivePoLineRequest(Guid LineId, int Quantity);

public sealed record NotificationDto(Guid Id, string Title, string Body, string Severity, string? Route, bool IsRead, DateTimeOffset CreatedAt);
public sealed record BookingDto(Guid Id, Guid CustomerId, string CustomerName, Guid? StaffId, string? StaffName, string Type, string Status, DateTimeOffset StartsAt, DateTimeOffset EndsAt, string? Notes);
public sealed record CreateBookingRequest(Guid CustomerId, Guid? StaffId, string Type, DateTimeOffset StartsAt, DateTimeOffset EndsAt, string? Notes);
public sealed record KnowledgeDto(Guid Id, string Title, string Category, string Body, string? Tags, DateTimeOffset CreatedAt);
public sealed record UpsertKnowledgeRequest(Guid? Id, string Title, string Category, string Body, string? Tags);

public sealed record PcBuildListItemDto(Guid Id, string Number, string? CustomerName, string Status, decimal CostTotal, decimal SellTotal, decimal Margin);
public sealed record CreatePcBuildRequest(Guid? CustomerId, string? UseCase, decimal? Budget, IReadOnlyList<PcPartInputDto> Parts);
public sealed record PcPartInputDto(string Category, string Name, Guid? InventoryItemId, decimal Cost, decimal SellPrice);

public sealed record UsedDeviceDto(Guid Id, string Summary, string Status, string ConditionGrade, decimal PurchasePrice, decimal ExpectedResale, decimal? ActualSalePrice, decimal? Profit);
public sealed record CreateUsedDeviceRequest(string Summary, string? Serial, string? Imei, string ConditionGrade, decimal PurchasePrice, decimal ExpectedResale, decimal ExpectedRepairCost, string? Faults, Guid? SellerCustomerId);
public sealed record UpdateUsedStatusRequest(string Status, decimal? ActualSalePrice);

public sealed record QaItemDto(Guid Id, string Item, string Result, int SortOrder);
public sealed record SetQaResultRequest(string Result);

public sealed record ReportSummaryDto(DateTimeOffset From, DateTimeOffset To, decimal Revenue, decimal CostOfGoods, decimal GrossProfit, int RepairsCompleted, int RepairsOpened, IReadOnlyList<NamedCountDto> ByStatus, IReadOnlyList<NamedMoneyDto> PaymentsByMethod);
public sealed record NamedCountDto(string Name, int Count);
public sealed record NamedMoneyDto(string Name, decimal Amount);

public sealed record AiAssistRequest(string Prompt, Guid? TicketId);
public sealed record AiAssistResponse(bool Enabled, string Provider, string Output, string Disclaimer);

public sealed record BackupDto(Guid Id, string Type, string Status, string? Path, DateTimeOffset StartedAt, DateTimeOffset? FinishedAt);
public sealed record SystemHealthDetailDto(string Status, bool Database, bool WorkerHeartbeat, string ApiVersion, string ClientMinVersion, DateTimeOffset? LastBackupAt, long? BackupCount);
