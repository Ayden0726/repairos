namespace WorkshopOS.Contracts.Website;

// —— Public (WordPress) ——

public sealed record PublicRepairQuoteRequest(
    string ContactName,
    string? Phone,
    string? Email,
    string? Suburb,
    string DeviceCategory,
    string Brand,
    string Model,
    string FaultDescription,
    string? PreferredContact = null,
    string? Notes = null);

public sealed record PublicRepairQuoteResponse(
    Guid QuoteId,
    string QuoteNumber,
    string TrackingCode,
    string AccessPin,
    string Status,
    string Message);

public sealed record PublicRepairBookingRequest(
    string ContactName,
    string? Phone,
    string? Email,
    string DeviceCategory,
    string Brand,
    string Model,
    string FaultOrService,
    DateTimeOffset PreferredStart,
    DateTimeOffset? PreferredEnd,
    string? Notes = null);

public sealed record PublicRepairBookingResponse(
    Guid BookingId,
    Guid? RepairTicketId,
    string? TrackingCode,
    string? AccessPin,
    string Status,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    string Message);

public sealed record PublicSellDeviceRequest(
    string Category,
    string Brand,
    string Model,
    string ContactName,
    string? ContactPhone,
    string? ContactEmail,
    string? Suburb,
    string? PreferredContact,
    string? Storage = null,
    string? Ram = null,
    string? Cpu = null,
    string? Gpu = null,
    string? Colour = null,
    string? SerialOrImei = null,
    string? BatteryHealth = null,
    string? CarrierStatus = null,
    bool? PowersOn = null,
    bool? Charges = null,
    string? ScreenCondition = null,
    string? BodyCondition = null,
    bool? CameraOk = null,
    bool? SpeakerMicOk = null,
    bool? ButtonsOk = null,
    bool? BiometricsOk = null,
    bool? LiquidDamage = null,
    string? KnownFaults = null,
    bool? AccountLocked = null,
    bool? FindMyEnabled = null,
    bool? GoogleSamsungRemoved = null,
    string? CustomerNotes = null,
    IReadOnlyList<string>? PhotoUrls = null);

public sealed record PublicSellDeviceResponse(Guid Id, string Reference, string Status, string Message);

public sealed record PublicCustomPcRequest(
    string ContactName,
    string? ContactPhone,
    string? ContactEmail,
    decimal? Budget,
    string? Usage,
    string? PreferredGamesSoftware,
    string? MonitorResolution,
    string? PerformanceTarget,
    bool? NeedsWifiBluetooth,
    string? RgbPreferences,
    string? CaseSize,
    string? Colour,
    string? StorageRequirements,
    string? Upgradeability,
    string? CustomerNotes);

public sealed record PublicCustomPcResponse(Guid Id, string Reference, string Status, string Message);

public sealed record PublicRepairStatusDto(
    string TrackingCode,
    string DeviceLabel,
    string StatusKey,
    string StatusName,
    string StatusColour,
    DateTimeOffset? EstimatedCompletion,
    string? CustomerFacingNotes,
    decimal? QuoteTotal,
    decimal DepositPaid,
    decimal RemainingBalance,
    string? WarrantyStatus,
    bool CanApproveQuote,
    bool CanDeclineQuote,
    Guid? ActiveQuoteId,
    string? ActiveQuoteNumber,
    int? ActiveQuoteRevision,
    IReadOnlyList<PublicTimelineItemDto> Timeline,
    string? InvoiceNumber,
    bool MessagingEnabled);

public sealed record PublicTimelineItemDto(DateTimeOffset At, string Title, string? Detail);

public sealed record PublicQuoteDecisionRequest(string AccessPin, string? Comment = null, int? ExpectedRevisionNumber = null);
public sealed record PublicQuoteDecisionResponse(string Action, string QuoteNumber, int Revision, string Message);

public sealed record PublicAvailabilitySlotDto(DateTimeOffset Start, DateTimeOffset End, bool Available, string? Reason);
public sealed record PublicSameDayHintDto(bool SameDayPossible, string Message, int? StockAvailable);

// —— Customer portal ——

public sealed record CustomerPortalLoginRequest(string Email, string Password);
public sealed record CustomerPortalAuthResponse(
    string AccessToken,
    DateTimeOffset ExpiresAt,
    Guid CustomerId,
    string DisplayName,
    string Email);

public sealed record CustomerPortalRegisterRequest(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string? Phone);

public sealed record CustomerRepairListItemDto(
    Guid Id, string TrackingCode, string Status, string DeviceLabel, DateTimeOffset CreatedAt, DateTimeOffset? EstimatedCompletion);

public sealed record CustomerQuoteListItemDto(
    Guid Id, string Number, string Status, decimal Total, DateTimeOffset CreatedAt, string? TrackingCode);

public sealed record CustomerDeviceListItemDto(
    Guid Id, string Brand, string Model, string Category, string? SerialMasked);

public sealed record CustomerInvoiceListItemDto(
    Guid Id, string Number, string Status, decimal Total, decimal Balance, DateTimeOffset CreatedAt);

public sealed record CustomerWarrantyListItemDto(
    Guid Id, string CoverageSummary, string Status, DateTimeOffset StartsAt, DateTimeOffset ExpiresAt, string? DeviceLabel);

public sealed record CustomerMessageListItemDto(
    Guid Id, Guid ThreadId, string Subject, string Preview, DateTimeOffset UpdatedAt, bool HasUnread);

public sealed record CustomerMessageDto(
    Guid Id, string Body, string Channel, string? AuthorName, DateTimeOffset CreatedAt, bool FromCustomer);

public sealed record CustomerPostMessageRequest(Guid? ThreadId, Guid? RepairTicketId, string Subject, string Body);

public sealed record CustomerSellRequestListItemDto(
    Guid Id, string Reference, string Status, string DeviceLabel, decimal? OfferAmount, DateTimeOffset CreatedAt);

// —— Staff Sell Your Tech / offers ——

public sealed record SellDeviceListItemDto(
    Guid Id, string Reference, string Status, string Category, string Brand, string Model,
    string ContactName, string? Suburb, DateTimeOffset CreatedAt, decimal? OfferAmount);

public sealed record SellDeviceDetailDto(
    Guid Id, string Reference, string Status, string Category, string Brand, string Model,
    string? Storage, string? Ram, string? Cpu, string? Gpu, string? Colour, string? SerialOrImeiMasked,
    string? BatteryHealth, string? CarrierStatus, bool? PowersOn, bool? Charges,
    string? ScreenCondition, string? BodyCondition, bool? CameraOk, bool? SpeakerMicOk,
    bool? ButtonsOk, bool? BiometricsOk, bool? LiquidDamage, string? KnownFaults,
    bool? AccountLocked, bool? FindMyEnabled, bool? GoogleSamsungRemoved,
    string? CustomerNotes, string ContactName, string? ContactPhone, string? ContactEmail,
    string? Suburb, string PreferredContact, IReadOnlyList<string> PhotoUrls,
    string? InternalNotes, Guid? CustomerId, Guid? UsedDeviceId, SellDeviceOfferDto? Offer,
    IReadOnlyList<SellDeviceEventDto> Events);

public sealed record SellDeviceOfferDto(
    Guid Id, decimal EstimatedResalePrice, decimal PurchaseCost, decimal EstimatedPartsCost,
    decimal LabourAllowance, decimal SellingPlatformFees, decimal ShippingPacking,
    decimal RiskAllowance, decimal DesiredMinProfit, decimal DesiredMinMarginPercent,
    decimal RecommendedOfferMin, decimal RecommendedOffer, decimal RecommendedOfferMax,
    decimal AbsoluteMaxPurchasePrice, decimal EstimatedTotalInvestment,
    decimal EstimatedResaleProfit, decimal EstimatedMarginPercent,
    decimal? CustomerFacingOffer, string Status, string? Notes,
    DateTimeOffset CreatedAt, DateTimeOffset? SentAt);

public sealed record SellDeviceEventDto(DateTimeOffset At, string EventType, string Summary);

public sealed record UpsertSellDeviceOfferRequest(
    decimal EstimatedResalePrice,
    decimal? PurchaseCost,
    decimal? EstimatedPartsCost,
    decimal? LabourAllowance,
    decimal? SellingPlatformFees,
    decimal? ShippingPacking,
    decimal? RiskAllowance,
    decimal? DesiredMinProfit,
    decimal? DesiredMinMarginPercent,
    decimal? CustomerFacingOffer,
    string? Notes);

public sealed record OfferCalculatorDefaultsDto(
    decimal MinimumProfit,
    decimal MinimumMarginPercent,
    decimal RiskPercent,
    decimal SellingFeePercent,
    decimal LabourAllowance);

public sealed record UpdateSellDeviceStatusRequest(string Status, string? InternalNotes = null);

// —— Messaging (staff) ——

public sealed record StaffThreadDto(Guid Id, string Subject, string Status, string? CustomerName, string? TicketNumber, DateTimeOffset UpdatedAt);
public sealed record StaffMessageDto(Guid Id, string Visibility, string Channel, string Body, string? AuthorName, DateTimeOffset CreatedAt);
public sealed record StaffPostMessageRequest(string Body, string Visibility = "CustomerVisible", string Channel = "Portal");

// —— Warranties / notifications / integrations ——

public sealed record NotificationTemplateDto(Guid Id, string Key, string Name, string Channel, string Subject, string Body, bool IsEnabled);
public sealed record UpsertNotificationTemplateRequest(string? Name, string Channel, string Subject, string Body, bool IsEnabled);

public sealed record WebsiteApiKeyDto(Guid Id, string Name, string KeyPrefix, bool IsActive, DateTimeOffset CreatedAt, DateTimeOffset? LastUsedAt);
public sealed record CreateWebsiteApiKeyRequest(string Name, string? AllowedOrigins);
public sealed record CreateWebsiteApiKeyResponse(Guid Id, string Name, string ApiKey, string KeyPrefix);

public sealed record WooCommerceSettingsDto(
    bool Enabled,
    string? StoreUrl,
    bool HasConsumerKey,
    bool HasConsumerSecret,
    string? DefaultWarrantyText);

public sealed record UpdateWooCommerceSettingsRequest(
    bool Enabled,
    string? StoreUrl,
    string? ConsumerKey,
    string? ConsumerSecret,
    string? DefaultWarrantyText);

public sealed record PublishRefurbishedRequest(Guid UsedDeviceId, decimal? SalePriceOverride = null);
public sealed record PublishRefurbishedResponse(Guid UsedDeviceId, string StockId, string? ExternalProductId, string Message);

public sealed record QrPayloadDto(string Kind, string Id, string Label, string Payload, string DeepLink);

public sealed record WebsiteDashboardCardsDto(
    int AwaitingDiagnosis,
    int AwaitingCustomerApproval,
    int RepairsInProgress,
    int Testing,
    int ReadyForCollection,
    int NewSellRequests,
    int OffersAwaitingResponse,
    int RefurbJobs,
    int RefurbReadyForSale,
    int LowStockParts,
    int TodaysAppointments,
    int OutstandingBalanceInvoices);

public sealed record RefurbChecklistDto(Guid Id, string Category, string Name, IReadOnlyList<string> Items, bool IsActive);
public sealed record UpsertRefurbChecklistRequest(string Category, string Name, IReadOnlyList<string> Items, bool IsActive);
public sealed record SaveRefurbDiagnosticRequest(Guid UsedDeviceId, Guid? ChecklistId, IReadOnlyDictionary<string, string> Results, string? OverallGrade);
public sealed record RefurbDiagnosticResultDto(
    Guid Id, Guid UsedDeviceId, Guid? ChecklistId, IReadOnlyDictionary<string, string> Results,
    string? OverallGrade, DateTimeOffset CreatedAt);

public sealed record CustomPcRequestListItemDto(
    Guid Id, string Reference, string Status, string ContactName, decimal? Budget, string? Usage, DateTimeOffset CreatedAt);
public sealed record CustomPcRequestDetailDto(
    Guid Id, string Reference, string Status, string ContactName, string? ContactPhone, string? ContactEmail,
    decimal? Budget, string? Usage, string? PreferredGamesSoftware, string? MonitorResolution,
    string? PerformanceTarget, bool? NeedsWifiBluetooth, string? RgbPreferences, string? CaseSize,
    string? Colour, string? StorageRequirements, string? Upgradeability, string? CustomerNotes,
    Guid? CustomerId, Guid? PcBuildId, string? PcBuildNumber, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
public sealed record UpdateCustomPcStatusRequest(string Status, string? InternalNote = null);
public sealed record ConvertCustomPcToBuildRequest(string? BuildName = null);

public sealed record SyncRefurbishedStatusRequest(string StockIdOrSku, string Status, decimal? ActualSalePrice = null);
public sealed record SyncRefurbishedStatusResponse(Guid UsedDeviceId, string Status, string Message);

public sealed record WebsiteWebhookSettingsDto(bool Enabled, string? Url, bool HasSecret);
public sealed record UpdateWebsiteWebhookSettingsRequest(bool Enabled, string? Url, string? Secret);

public sealed record BookingAvailabilitySettingsDto(int OpenHour, int CloseHour, int SlotMinutes, int Capacity);
public sealed record PublicRepairMessageRequest(string AccessPin, string Body, string? Subject = null);
public sealed record PublicRepairMessageResponse(Guid ThreadId, Guid MessageId, string Message);
