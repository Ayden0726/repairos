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
    bool IncludeInternalFinancials,
    /// <summary>Σ landed part costs before job markup.</summary>
    decimal PartsCostTotal = 0m,
    /// <summary>Parts sell after job markup applied once to Σ costs.</summary>
    decimal PartsSellTotal = 0m,
    /// <summary>Job-level markup % applied to PartsCostTotal.</summary>
    decimal MarkupPercent = 0m,
    /// <summary>Job-level markup $ amount.</summary>
    decimal MarkupAmount = 0m,
    /// <summary>Single labour/service fee for the whole job.</summary>
    decimal LabourFee = 0m,
    decimal AdditionalTotal = 0m,
    string? DeviceImei = null,
    string? DeviceColour = null,
    string? DeviceStorage = null,
    string? DeviceModelNumber = null);

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
    /// <summary>Ignored — markup is job-level. Kept for wire compat.</summary>
    decimal? MarkupPercent,
    /// <summary>Ignored — markup is job-level. Kept for wire compat.</summary>
    decimal? MarkupAmount,
    decimal? PartSell,
    /// <summary>Ignored — labour is job-level. Kept for wire compat.</summary>
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
    IReadOnlyList<LineInputDto>? SimpleLines = null,
    /// <summary>Job labour fee. Null → pricing settings default.</summary>
    decimal? LabourFee = null,
    /// <summary>Job markup % on Σ landed part costs. Null → settings default.</summary>
    decimal? MarkupPercent = null,
    /// <summary>Job fixed markup $ (instead of %). Null → use MarkupPercent / settings.</summary>
    decimal? MarkupAmount = null,
    string? DifficultyLevelKey = null,
    Guid? ServicePricingId = null,
    decimal? DiscountPercent = null,
    decimal? DiscountAmount = null,
    string? DeviceImei = null,
    string? DeviceColour = null,
    string? DeviceStorage = null,
    string? DeviceModelNumber = null);

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
    string? ReviseReason,
    decimal? LabourFee = null,
    decimal? MarkupPercent = null,
    decimal? MarkupAmount = null,
    string? DifficultyLevelKey = null,
    Guid? ServicePricingId = null,
    decimal? DiscountPercent = null,
    decimal? DiscountAmount = null,
    string? DeviceImei = null,
    string? DeviceColour = null,
    string? DeviceStorage = null,
    string? DeviceModelNumber = null);

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
    int Minimum, decimal SellPrice, bool IsLow, decimal Cost = 0m, string? SupplierName = null,
    string ComponentType = "Other", int Quarantined = 0, string? LocationBin = null, string? Barcode = null);
public sealed record UpsertInventoryRequest(
    Guid? Id, string Sku, string? Barcode, string Name, string Category, decimal Cost, decimal SellPrice,
    int QuantityOnHand, int MinimumStock, int ReorderQuantity, string? LocationBin, Guid? SupplierId,
    string? ComponentType = null);
public sealed record AdjustStockRequest(int QuantityDelta, string Reason);
/// <summary>Reserve stock for a repair ticket or a PC build (exactly one of TicketId / PcBuildId).</summary>
public sealed record ReserveStockRequest(Guid ItemId, int Quantity, Guid? TicketId = null, Guid? PcBuildId = null);
public sealed record TransferBinRequest(string? LocationBin);
public sealed record QuarantineStockRequest(int Quantity, string Reason);
public sealed record InventoryReservationDto(
    Guid Id, Guid ItemId, string Sku, string Name, int Quantity, string Status,
    Guid? TicketId, Guid? PcBuildId, DateTimeOffset CreatedAt);

public sealed record SupplierDto(Guid Id, string Name, string? Phone, string? Email);
public sealed record UpsertSupplierRequest(Guid? Id, string Name, string? Contact, string? Phone, string? Email, string? Website, string? AccountNumber, string? Notes);
public sealed record PurchaseOrderListItemDto(Guid Id, string Number, string SupplierName, string Status, decimal Total, DateTimeOffset? ExpectedAt);
public sealed record CreatePurchaseOrderRequest(Guid SupplierId, DateTimeOffset? ExpectedAt, decimal Shipping, IReadOnlyList<PoLineInputDto> Lines);
public sealed record PoLineInputDto(Guid? ItemId, string Description, int Quantity, decimal UnitCost);
public sealed record ReceivePoLineRequest(Guid LineId, int Quantity);

public sealed record NotificationDto(Guid Id, string Title, string Body, string Severity, string? Route, bool IsRead, DateTimeOffset CreatedAt);
public sealed record BookingDto(Guid Id, Guid CustomerId, string CustomerName, Guid? StaffId, string? StaffName, string Type, string Status, DateTimeOffset StartsAt, DateTimeOffset EndsAt, string? Notes);
public sealed record CreateBookingRequest(Guid CustomerId, Guid? StaffId, string Type, DateTimeOffset StartsAt, DateTimeOffset EndsAt, string? Notes);
public sealed record UpdateBookingRequest(Guid CustomerId, Guid? StaffId, string Type, string Status, DateTimeOffset StartsAt, DateTimeOffset EndsAt, string? Notes);
public sealed record KnowledgeDto(Guid Id, string Title, string Category, string Body, string? Tags, DateTimeOffset CreatedAt);
public sealed record UpsertKnowledgeRequest(Guid? Id, string Title, string Category, string Body, string? Tags);

public sealed record PcBuildListItemDto(
    Guid Id, string Number, string? Name, string? CustomerName, string Status,
    decimal CostTotal, decimal SellTotal, decimal Margin, int PartCount = 0);

public sealed record PcBuildPartDto(
    Guid Id, string Category, string Name, Guid? InventoryItemId, string? InventorySku,
    int Quantity, decimal Cost, decimal SellPrice, Guid? ReservationId);

public sealed record PcBuildDetailDto(
    Guid Id, string Number, string? Name, Guid? CustomerId, string? CustomerName,
    string Status, string? UseCase, decimal? Budget, string? Notes,
    decimal CostTotal, decimal SellTotal, decimal Margin,
    IReadOnlyList<PcBuildPartDto> Parts);

public sealed record CreatePcBuildRequest(
    Guid? CustomerId, string? Name, string? UseCase, decimal? Budget, string? Notes,
    IReadOnlyList<PcPartInputDto>? Parts, string? Status = null);

public sealed record UpdatePcBuildRequest(
    Guid? CustomerId, string? Name, string? UseCase, decimal? Budget, string? Notes,
    string? Status, IReadOnlyList<PcPartInputDto> Parts);

public sealed record UpdatePcBuildStatusRequest(string Status);

public sealed record PcPartInputDto(
    string Category, string Name, Guid? InventoryItemId, decimal Cost, decimal SellPrice, int Quantity = 1);

public sealed record UsedDeviceDto(
    Guid Id,
    string Summary,
    string? Brand,
    string? Model,
    string? Category,
    string Status,
    string ConditionGrade,
    decimal PurchasePrice,
    decimal PartsCost,
    decimal TotalCost,
    decimal AskingPrice,
    decimal? ActualSalePrice,
    decimal? Margin,
    string? Serial,
    string? Colour,
    string? StorageCapacity,
    string? StockId = null,
    string? LocationBin = null,
    bool IsPartsOnly = false);

public sealed record UsedDeviceDetailDto(
    Guid Id,
    string Summary,
    string? Brand,
    string? Model,
    string? Category,
    string? Serial,
    string? Imei,
    string? Colour,
    string? StorageCapacity,
    string? Specs,
    string Status,
    string ConditionGrade,
    decimal PurchasePrice,
    decimal PartsCost,
    decimal TotalCost,
    decimal AskingPrice,
    decimal? ActualSalePrice,
    decimal? Margin,
    string? Faults,
    string? Notes,
    string? Source,
    Guid? SellerCustomerId,
    string? SellerCustomerName,
    Guid? CustomerId,
    string? CustomerName,
    Guid? RepairTicketId,
    string? RepairTicketNumber,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string? StockId = null,
    string? Sku = null,
    string? LocationBin = null,
    Guid? AssignedTechnicianId = null,
    Guid? AcquisitionLeadId = null,
    bool IsPartsOnly = false,
    string? FunctionalGrade = null,
    int? BatteryHealthPercent = null,
    UsedDeviceSecurityDto? Security = null,
    UsedDeviceCostBreakdownDto? Costs = null);

public sealed record CreateUsedDeviceRequest(
    string? Summary,
    string? Brand,
    string? Model,
    string? Category,
    string? Serial,
    string? Imei,
    string? Colour,
    string? StorageCapacity,
    string? Specs,
    string ConditionGrade,
    string? Status,
    decimal PurchasePrice,
    decimal AskingPrice,
    decimal PartsCost,
    string? Faults,
    string? Notes,
    string? Source,
    Guid? SellerCustomerId,
    Guid? CustomerId,
    Guid? RepairTicketId,
    int? BatteryHealthPercent = null,
    string? FunctionalGrade = null,
    bool IsPartsOnly = false);

public sealed record UpdateUsedDeviceRequest(
    string? Summary,
    string? Brand,
    string? Model,
    string? Category,
    string? Serial,
    string? Imei,
    string? Colour,
    string? StorageCapacity,
    string? Specs,
    string ConditionGrade,
    string Status,
    decimal PurchasePrice,
    decimal AskingPrice,
    decimal PartsCost,
    decimal? ActualSalePrice,
    string? Faults,
    string? Notes,
    string? Source,
    Guid? SellerCustomerId,
    Guid? CustomerId,
    Guid? RepairTicketId,
    int? BatteryHealthPercent = null,
    string? FunctionalGrade = null,
    bool IsPartsOnly = false);

public sealed record PurchaseOrderLineDto(
    Guid Id,
    Guid? ItemId,
    string Description,
    int QuantityOrdered,
    int QuantityReceived,
    decimal UnitCost);

public sealed record PurchaseOrderDetailDto(
    Guid Id,
    string Number,
    Guid SupplierId,
    string SupplierName,
    string Status,
    decimal Total,
    decimal Shipping,
    DateTimeOffset? ExpectedAt,
    DateTimeOffset? ReceivedAt,
    IReadOnlyList<PurchaseOrderLineDto> Lines);

public sealed record StocktakeListItemDto(
    Guid Id,
    string Number,
    string Status,
    int LineCount,
    int CountedLines,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    string? Notes);

public sealed record StocktakeLineDto(
    Guid Id,
    Guid InventoryItemId,
    string Sku,
    string Name,
    string? LocationBin,
    int ExpectedQty,
    int? CountedQty,
    int? Variance);

public sealed record StocktakeDetailDto(
    Guid Id,
    string Number,
    string Status,
    string? Notes,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    IReadOnlyList<StocktakeLineDto> Lines);

public sealed record CreateStocktakeRequest(string? Notes = null);
public sealed record SetStocktakeCountRequest(int CountedQty);

public sealed record UpdateUsedStatusRequest(string Status, decimal? ActualSalePrice);

public sealed record QaItemDto(Guid Id, string Item, string Result, int SortOrder);
public sealed record SetQaResultRequest(string Result);

public sealed record ReportSummaryDto(
    DateTimeOffset From,
    DateTimeOffset To,
    decimal Revenue,
    decimal CostOfGoods,
    decimal GrossProfit,
    int RepairsCompleted,
    int RepairsOpened,
    IReadOnlyList<NamedCountDto> ByStatus,
    IReadOnlyList<NamedMoneyDto> PaymentsByMethod,
    /// <summary>False when no stock-consume costs were recorded — GrossProfit then equals Revenue (COGS treated as unknown 0, not invented).</summary>
    bool CogsKnown = true,
    string? CogsNote = null);
public sealed record NamedCountDto(string Name, int Count);
public sealed record NamedMoneyDto(string Name, decimal Amount);

public sealed record AiAssistRequest(string Prompt, Guid? TicketId);
public sealed record AiAssistResponse(bool Enabled, string Provider, string Output, string Disclaimer);

public sealed record BackupDto(
    Guid Id,
    string Type,
    string Status,
    string? Path,
    DateTimeOffset StartedAt,
    DateTimeOffset? FinishedAt,
    string? Method = null,
    long? SizeBytes = null,
    bool Verified = false,
    string? Sha256 = null,
    string? Detail = null);
public sealed record SystemHealthDetailDto(
    string Status,
    bool Database,
    bool WorkerHeartbeat,
    string ApiVersion,
    string ClientMinVersion,
    DateTimeOffset? LastBackupAt,
    long? BackupCount,
    DateTimeOffset? LastVerifiedBackupAt = null);

public sealed record AuditEventDto(
    Guid Id,
    DateTimeOffset CreatedAt,
    Guid? ActorUserId,
    string? ActorName,
    string Action,
    string? EntityType,
    string? EntityId,
    string? OldValueJson,
    string? NewValueJson,
    string? Ip);

public sealed record AuditSearchResponse(IReadOnlyList<AuditEventDto> Items, int Total);

// --- POS ---

public sealed record PosDiscountPresetDto(
    string Key,
    string Name,
    decimal Percent,
    bool IsActive = true);

public sealed record PosSettingsDto(
    bool Enabled,
    string Provider,
    string? MerchantId,
    string? TerminalId,
    string? ProviderLocationId,
    bool ApiKeyConfigured,
    string? ConnectionNotes,
    IReadOnlyList<PosDiscountPresetDto> DiscountPresets);

public sealed record UpdatePosSettingsRequest(
    bool Enabled,
    string Provider,
    string? MerchantId,
    string? TerminalId,
    string? ProviderLocationId,
    string? ApiKey,
    bool ClearApiKey,
    string? ConnectionNotes,
    IReadOnlyList<PosDiscountPresetDto>? DiscountPresets = null);

public sealed record UpdateModuleVisibilityRequest(IReadOnlyDictionary<string, bool> Visibility);

public sealed record PosProviderInfoDto(
    string Key,
    string Name,
    string Region,
    string ConnectionType,
    string Summary,
    IReadOnlyList<string> SetupSteps,
    string DocsUrl);

public sealed record PosSaleLineInputDto(
    Guid? InventoryItemId,
    string? Description,
    decimal Quantity,
    decimal? UnitPrice,
    Guid? UsedDeviceId = null);

/// <summary>
/// Retail cart and/or repair payoff. When RepairTicketId is set, Lines may be empty
/// (invoice is created/loaded from the ticket). AmountToPay + IsDeposit control deposit vs full pay.
/// </summary>
public sealed record CreatePosSaleRequest(
    Guid? CustomerId,
    string PaymentMethod,
    string? ProviderReference,
    string? Notes,
    IReadOnlyList<PosSaleLineInputDto> Lines,
    Guid? RepairTicketId = null,
    decimal? AmountToPay = null,
    bool IsDeposit = false,
    string? DiscountPresetKey = null,
    decimal? DiscountPercent = null,
    string? DiscountLabel = null);

public sealed record PosSaleLineDto(
    Guid Id,
    Guid? InventoryItemId,
    string Description,
    string? Sku,
    decimal Quantity,
    decimal UnitPrice,
    decimal LineTotal,
    Guid? UsedDeviceId = null);

public sealed record PosSaleListItemDto(
    Guid Id,
    string Number,
    string CustomerName,
    string Status,
    string PaymentMethod,
    string PaymentProvider,
    decimal Total,
    DateTimeOffset CreatedAt,
    Guid InvoiceId,
    Guid? RepairTicketId,
    bool IsDeposit);

public sealed record PosSaleDetailDto(
    Guid Id,
    string Number,
    Guid InvoiceId,
    string InvoiceNumber,
    Guid CustomerId,
    string CustomerName,
    string Status,
    string PaymentMethod,
    string PaymentProvider,
    string? ProviderReference,
    string ProviderStatus,
    decimal Subtotal,
    decimal GstAmount,
    decimal Total,
    decimal AmountPaid,
    string? Notes,
    DateTimeOffset CreatedAt,
    IReadOnlyList<PosSaleLineDto> Lines,
    Guid? RepairTicketId,
    string? RepairTicketNumber,
    string? DiscountLabel,
    decimal DiscountPercent,
    decimal DiscountAmount,
    bool IsDeposit,
    decimal InvoiceBalance);

/// <summary>Open repair with remaining balance for POS import.</summary>
public sealed record PosPayableRepairDto(
    Guid RepairTicketId,
    string TicketNumber,
    string CustomerName,
    string? DeviceLabel,
    string StatusName,
    Guid? InvoiceId,
    string? InvoiceNumber,
    decimal InvoiceTotal,
    decimal AmountPaid,
    decimal Balance,
    decimal? EstimatedPrice);

// --- V2 remaining ops ---

public sealed record DonorDeviceDto(
    Guid Id, string Number, string? Brand, string? Model, string? Imei, string Status,
    int ComponentCount, Guid? SourceUsedDeviceId, DateTimeOffset CreatedAt);

public sealed record HarvestedComponentDto(
    Guid Id, string Description, string? ConditionGrade, int Quantity,
    Guid? InventoryItemId, string? InventorySku, DateTimeOffset HarvestedAt);

public sealed record DonorDeviceDetailDto(
    Guid Id, string Number, string? Brand, string? Model, string? Imei, string? Serial,
    string Status, string? Notes, Guid? SourceUsedDeviceId, Guid? CustomerId,
    IReadOnlyList<HarvestedComponentDto> Components, DateTimeOffset CreatedAt);

public sealed record CreateDonorDeviceRequest(
    string? Brand, string? Model, string? Imei, string? Serial, string? Notes,
    Guid? SourceUsedDeviceId = null, Guid? CustomerId = null);

public sealed record HarvestComponentRequest(
    string Description, string? ConditionGrade, int Quantity = 1,
    string? NewSku = null, decimal? Cost = null, decimal? SellPrice = null,
    string? LocationBin = null, Guid? ExistingInventoryItemId = null);

public sealed record LoanDeviceDto(
    Guid Id, string AssetTag, string? Brand, string? Model, string? Imei, string Status, string? Notes);

public sealed record LoanAgreementDto(
    Guid Id, Guid LoanDeviceId, string AssetTag, Guid CustomerId, string CustomerName,
    Guid? RepairTicketId, string? TicketNumber, DateTimeOffset CheckedOutAt, DateTimeOffset? DueAt,
    DateTimeOffset? ReturnedAt, string Status, string? Notes);

public sealed record UpsertLoanDeviceRequest(
    Guid? Id, string AssetTag, string? Brand, string? Model, string? Imei, string? Serial, string? Notes);

public sealed record CheckoutLoanRequest(Guid LoanDeviceId, Guid CustomerId, Guid? RepairTicketId, DateTimeOffset? DueAt, string? Notes);
public sealed record ReturnLoanRequest(string? Notes);

public sealed record WarrantyClaimDto(
    Guid Id, string Number, Guid WarrantyRecordId, Guid CustomerId, string CustomerName,
    Guid? RepairTicketId, string? TicketNumber, string Status, string Summary,
    string? ResolutionNotes, DateTimeOffset OpenedAt, DateTimeOffset? ClosedAt,
    DateTimeOffset WarrantyExpiresAt);

public sealed record CreateWarrantyClaimRequest(
    Guid WarrantyRecordId, string Summary, Guid? RepairTicketId = null);

public sealed record UpdateWarrantyClaimRequest(string Status, string? ResolutionNotes);

public sealed record StaffWarrantyDto(
    Guid Id, Guid CustomerId, string CustomerName, string CoverageSummary, int WarrantyDays,
    DateTimeOffset StartsAt, DateTimeOffset ExpiresAt, string Status, Guid? RepairTicketId, string? TicketNumber);

public sealed record RefundRequestDto(
    Guid Id, string Number, Guid? PosSaleId, string? PosSaleNumber, Guid? InvoiceId,
    string? InvoiceNumber, decimal Amount, string Reason, string Status,
    string? DecisionNotes, DateTimeOffset RequestedAt, DateTimeOffset? DecidedAt,
    string? RequestedByName);

public sealed record CreateRefundRequestDto(Guid? PosSaleId, Guid? InvoiceId, decimal Amount, string Reason);
public sealed record DecideRefundRequest(bool Approve, string? DecisionNotes);

public sealed record RepairAttachmentDto(
    Guid Id, string Kind, string FileName, string ContentType, long SizeBytes,
    string? Caption, DateTimeOffset CreatedAt);

public sealed record UploadRepairAttachmentRequest(
    string FileName, string ContentType, string Base64Content, string? Caption = null, string Kind = "Photo");

public sealed record RepairSignatureDto(
    Guid Id, string SignerName, string Purpose, DateTimeOffset SignedAt);

public sealed record CaptureRepairSignatureRequest(
    string SignerName, string Purpose, string Base64Png);

public sealed record PrinterProfileDto(
    Guid Id, string Name, string Kind, string? Connection, string? DefaultTemplate, bool IsDefault);

public sealed record UpsertPrinterProfileRequest(
    Guid? Id, string Name, string Kind, string? Connection, string? DefaultTemplate, bool IsDefault);

public sealed record BatchLabelRequest(string LabelType, IReadOnlyList<Guid> Ids, bool IncludePrice = false);
public sealed record BatchLabelResult(string CombinedHtml, int Count);

public sealed record IntegrationHealthEventDto(
    Guid Id, string ProviderKey, bool Success, string Message, DateTimeOffset CreatedAt);

public sealed record BackupRestoreTestDto(
    Guid BackupId, bool Verified, bool FileExists, long? SizeBytes, string? Sha256,
    string Detail, DateTimeOffset TestedAt);

// --- Platform (fitment / offline / multi-site) — board-level repair intentionally excluded ---

public sealed record PartFitmentDto(
    Guid Id, Guid? InventoryItemId, string? InventorySku, string? PartSku, string PartName,
    string Brand, string Model, string? DeviceType, string? Notes, bool IsVerified);

public sealed record UpsertPartFitmentRequest(
    Guid? Id, Guid? InventoryItemId, string? PartSku, string PartName,
    string Brand, string Model, string? DeviceType, string? Notes, bool IsVerified = true);

public sealed record SyncMutationDto(
    Guid ClientMutationId, string Method, string Path, string? BodyJson);

public sealed record SyncPushRequest(IReadOnlyList<SyncMutationDto> Mutations);

public sealed record SyncMutationResultDto(
    Guid ClientMutationId, string Status, string? Error, Guid? ServerEntryId);

public sealed record SyncPushResponse(IReadOnlyList<SyncMutationResultDto> Results);

public sealed record SyncOutboxListItemDto(
    Guid Id, Guid ClientMutationId, string Method, string Path, string Status,
    string? Error, DateTimeOffset CreatedAt, DateTimeOffset? AppliedAt);

public sealed record WorkshopLocationDto(
    Guid Id, string Name, string? Phone, string? Email, string? AddressLine1,
    string? Suburb, string? State, string? Postcode, bool IsDefault);

public sealed record UpsertWorkshopLocationRequest(
    Guid? Id, string Name, string? Phone, string? Email, string? AddressLine1,
    string? Suburb, string? State, string? Postcode, bool IsDefault);

public sealed record InventoryBinDto(
    Guid Id, Guid? LocationId, string? LocationName, Guid? ParentBinId, string Code, string? Name, string? Notes);

public sealed record UpsertInventoryBinRequest(
    Guid? Id, Guid? LocationId, Guid? ParentBinId, string Code, string? Name, string? Notes);
