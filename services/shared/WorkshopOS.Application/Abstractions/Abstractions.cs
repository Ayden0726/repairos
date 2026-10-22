using WorkshopOS.Contracts.Auth;
using WorkshopOS.Contracts.Common;
using WorkshopOS.Contracts.Finance;
using WorkshopOS.Contracts.Operations;
using WorkshopOS.Contracts.Website;
using WorkshopOS.Contracts.Workshop;
using WorkshopOS.Domain.Entities;

namespace WorkshopOS.Application.Abstractions;

public interface ISetupService
{
    Task<SetupStatusDto> GetStatusAsync(CancellationToken ct = default);
    Task CompleteAsync(SetupRequest request, string? ip, CancellationToken ct = default);
}

public interface IAuthService
{
    Task<AuthResponse> LoginAsync(LoginRequest request, string? ip, string? userAgent, CancellationToken ct = default);
    Task<AuthResponse> RefreshAsync(string refreshToken, string? ip, CancellationToken ct = default);
    Task LogoutAsync(Guid userId, string? refreshToken, CancellationToken ct = default);
    Task<UserDto> GetMeAsync(Guid userId, CancellationToken ct = default);
    Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken ct = default);
}

public interface ISettingsService
{
    Task<BusinessProfileDto> GetBusinessAsync(CancellationToken ct = default);
    Task<BusinessProfileDto> UpdateBusinessAsync(BusinessProfileDto profile, Guid actorId, CancellationToken ct = default);
    Task<IReadOnlyList<ModuleDto>> GetModulesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ModuleDto>> UpdateModulesAsync(UpdateModuleVisibilityRequest request, Guid actorId, CancellationToken ct = default);
    Task<PosSettingsDto> GetPosSettingsAsync(CancellationToken ct = default);
    Task<PosSettingsDto> UpdatePosSettingsAsync(UpdatePosSettingsRequest request, Guid actorId, CancellationToken ct = default);
}

public interface IPosService
{
    Task<IReadOnlyList<PosProviderInfoDto>> ListProvidersAsync(CancellationToken ct = default);
    Task<IReadOnlyList<PosSaleListItemDto>> ListSalesAsync(DateTimeOffset? from = null, DateTimeOffset? to = null, CancellationToken ct = default);
    Task<PosSaleDetailDto> GetSaleAsync(Guid id, CancellationToken ct = default);
    Task<PosSaleDetailDto> CreateSaleAsync(CreatePosSaleRequest request, Guid actorId, CancellationToken ct = default);
    Task<PosSaleDetailDto> VoidSaleAsync(Guid id, Guid actorId, CancellationToken ct = default);
    Task<IReadOnlyList<PosPayableRepairDto>> ListPayableRepairsAsync(string? q = null, CancellationToken ct = default);
}

public interface IRoleService
{
    Task<IReadOnlyList<RoleDto>> ListRolesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<PermissionDto>> ListPermissionsAsync(CancellationToken ct = default);
    Task<RoleDto> CreateAsync(CreateRoleRequest request, Guid actorId, CancellationToken ct = default);
    Task<RoleDto> UpdateAsync(Guid id, UpdateRoleRequest request, Guid actorId, CancellationToken ct = default);
}

public interface IStaffService
{
    Task<IReadOnlyList<StaffUserDto>> ListAsync(CancellationToken ct = default);
    Task<StaffUserDto> CreateAsync(CreateStaffUserRequest request, Guid actorId, CancellationToken ct = default);
    Task<StaffUserDto> UpdateAsync(Guid id, UpdateStaffUserRequest request, Guid actorId, CancellationToken ct = default);
    Task ResetPasswordAsync(Guid id, ResetStaffPasswordRequest request, Guid actorId, CancellationToken ct = default);
}

public interface ISearchService
{
    Task<SearchResponse> SearchAsync(string query, CancellationToken ct = default);
}

public interface IAuditService
{
    Task WriteAsync(Guid? actorUserId, string action, string? entityType = null, string? entityId = null, object? oldValue = null, object? newValue = null, string? ip = null, string? userAgent = null, CancellationToken ct = default);
}

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

public interface ICustomerService
{
    Task<PagedResult<CustomerListItemDto>> ListAsync(string? q, int page, int pageSize, CancellationToken ct = default);
    Task<CustomerDetailDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<CustomerDetailDto> UpsertAsync(UpsertCustomerRequest request, Guid actorId, CancellationToken ct = default);
}

public interface IDeviceService
{
    Task<IReadOnlyList<DeviceListItemDto>> ListForCustomerAsync(Guid customerId, CancellationToken ct = default);
    Task<DeviceListItemDto> UpsertAsync(UpsertDeviceRequest request, Guid actorId, CancellationToken ct = default);
}

public interface IRepairService
{
    Task<PagedResult<RepairListItemDto>> ListAsync(string? q, string? statusKey, string? priorityKey, Guid? assignedToId, bool? overdueOnly, int page, int pageSize, CancellationToken ct = default);
    Task<RepairDetailDto> GetAsync(Guid id, bool canViewCredentials, CancellationToken ct = default);
    Task<RepairDetailDto> CreateAsync(CreateRepairRequest request, Guid actorId, CancellationToken ct = default);
    Task<RepairDetailDto> ChangeStatusAsync(Guid id, Guid statusId, Guid actorId, CancellationToken ct = default);
    Task<RepairDetailDto> ChangePriorityAsync(Guid id, Guid priorityId, Guid actorId, CancellationToken ct = default);
    Task<RepairDetailDto> AssignAsync(Guid id, Guid? assignedToId, Guid actorId, CancellationToken ct = default);
    Task<RepairNoteDto> AddNoteAsync(Guid id, AddNoteRequest request, Guid actorId, bool canInternal, CancellationToken ct = default);
    Task<RepairDetailDto> UpdateDiagnosisAsync(Guid id, UpdateDiagnosisRequest request, Guid actorId, CancellationToken ct = default);
    Task<RepairDetailDto> CompleteServiceLineAsync(Guid repairId, Guid lineId, bool isCompleted, Guid actorId, CancellationToken ct = default);
    Task<RepairLookupsDto> GetLookupsAsync(CancellationToken ct = default);
    Task<string> BuildPrintHtmlAsync(Guid id, CancellationToken ct = default);
}

public interface IDashboardService
{
    Task<DashboardDto> GetAsync(CancellationToken ct = default);
}

public interface IQuoteService
{
    Task<IReadOnlyList<QuoteListItemDto>> ListAsync(string? q = null, string? status = null, Guid? customerId = null, Guid? repairTicketId = null, CancellationToken ct = default);
    Task<QuoteDetailDto> GetAsync(Guid id, bool includeInternalFinancials, CancellationToken ct = default);
    Task<QuoteDetailDto> CreateAsync(CreateQuoteRequest request, Guid actorId, IReadOnlySet<string> permissions, CancellationToken ct = default);
    Task<QuoteDetailDto> UpdateAsync(Guid id, UpdateQuoteRequest request, Guid actorId, IReadOnlySet<string> permissions, CancellationToken ct = default);
    Task<QuoteDetailDto> SetStatusAsync(Guid id, string status, Guid actorId, IReadOnlySet<string> permissions, CancellationToken ct = default);
    Task<QuoteDetailDto> ReviseAsync(Guid id, UpdateQuoteRequest request, Guid actorId, IReadOnlySet<string> permissions, CancellationToken ct = default);
    Task<QuoteDetailDto> ConvertToRepairAsync(Guid id, ConvertQuoteToRepairRequest request, Guid actorId, CancellationToken ct = default);
    Task<string> BuildCustomerPrintHtmlAsync(Guid id, CancellationToken ct = default);
}

public interface IPricingSettingsService
{
    Task<PricingSettingsDto> GetAsync(CancellationToken ct = default);
    Task<PricingSettingsDto> UpdateAsync(PricingSettingsDto settings, Guid actorId, CancellationToken ct = default);
    Task<IReadOnlyList<MarkupTierDto>> ListTiersAsync(CancellationToken ct = default);
    Task<MarkupTierDto> UpsertTierAsync(UpsertMarkupTierRequest request, Guid actorId, CancellationToken ct = default);
    Task DeleteTierAsync(Guid id, Guid actorId, CancellationToken ct = default);
    Task<IReadOnlyList<ServicePricingDto>> ListServicesAsync(CancellationToken ct = default);
    Task<ServicePricingDto> UpsertServiceAsync(UpsertServicePricingRequest request, Guid actorId, CancellationToken ct = default);
    Task DeleteServiceAsync(Guid id, Guid actorId, CancellationToken ct = default);
    Task<PricingPreviewResponse> PreviewAsync(PricingPreviewRequest request, CancellationToken ct = default);
}

public interface IServiceCatalogueService
{
    Task EnsureSeededAsync(CancellationToken ct = default);
    Task<CatalogueImportResultDto> ImportAsync(bool force, Guid? actorId, CancellationToken ct = default);
    Task<IReadOnlyList<CatalogueCategoryDto>> ListCategoriesAsync(CancellationToken ct = default);
    Task<CatalogueCategoryDto> UpsertCategoryAsync(UpsertCatalogueCategoryRequest request, Guid actorId, CancellationToken ct = default);
    Task DeleteCategoryAsync(Guid id, Guid actorId, CancellationToken ct = default);
    Task ReorderCategoriesAsync(ReorderCatalogueRequest request, Guid actorId, CancellationToken ct = default);
    Task<IReadOnlyList<CatalogueServiceDto>> SearchServicesAsync(string? q, string? category, string? deviceType, string? brand, string? model, bool activeOnly = true, CancellationToken ct = default);
    Task<CatalogueServiceDto> UpsertServiceAsync(UpsertCatalogueServiceRequest request, Guid actorId, CancellationToken ct = default);
    Task DeleteServiceAsync(Guid id, Guid actorId, CancellationToken ct = default);
    Task<IReadOnlyList<CatalogueServiceDto>> ListFavouritesAsync(Guid userId, CancellationToken ct = default);
    Task FavouriteAsync(Guid userId, Guid serviceId, CancellationToken ct = default);
    Task UnfavouriteAsync(Guid userId, Guid serviceId, CancellationToken ct = default);
    Task<IReadOnlyList<CatalogueServiceDto>> ListRecentAsync(Guid userId, int take = 12, CancellationToken ct = default);
    Task RecordRecentAsync(Guid userId, Guid serviceId, CancellationToken ct = default);
    Task RecordRecentManyAsync(Guid userId, IEnumerable<Guid> serviceIds, CancellationToken ct = default);
    Task<IReadOnlyList<CatalogueBundleDto>> ListBundlesAsync(bool expand = false, CancellationToken ct = default);
    Task<CatalogueBundleDto> UpsertBundleAsync(UpsertCatalogueBundleRequest request, Guid actorId, CancellationToken ct = default);
    Task DeleteBundleAsync(Guid id, Guid actorId, CancellationToken ct = default);
    Task<IReadOnlyList<DeviceBrandDto>> ListBrandsAsync(string? deviceType = null, CancellationToken ct = default);
    Task<IReadOnlyList<DeviceModelDto>> ListModelsAsync(Guid? brandId = null, string? deviceType = null, string? brandName = null, CancellationToken ct = default);
}

public interface IInvoiceService
{
    Task<IReadOnlyList<InvoiceListItemDto>> ListAsync(CancellationToken ct = default);
    Task<InvoiceDetailDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<InvoiceDetailDto> CreateFromRepairAsync(Guid repairTicketId, Guid actorId, CancellationToken ct = default);
    Task<InvoiceDetailDto> RecordPaymentAsync(Guid id, RecordPaymentRequest request, Guid actorId, CancellationToken ct = default);
}

public interface IInventoryService
{
    Task<IReadOnlyList<InventoryListItemDto>> ListAsync(string? componentType = null, bool availableOnly = false, CancellationToken ct = default);
    Task<InventoryListItemDto> UpsertAsync(UpsertInventoryRequest request, Guid actorId, CancellationToken ct = default);
    Task<InventoryListItemDto> AdjustAsync(Guid id, AdjustStockRequest request, Guid actorId, CancellationToken ct = default);
    Task ReserveAsync(ReserveStockRequest request, Guid actorId, CancellationToken ct = default);
    Task ConsumeReservationAsync(Guid reservationId, Guid actorId, CancellationToken ct = default);
    Task ReleaseReservationAsync(Guid reservationId, Guid actorId, CancellationToken ct = default);
}

public interface IPurchasingService
{
    Task<IReadOnlyList<SupplierDto>> ListSuppliersAsync(CancellationToken ct = default);
    Task<SupplierDto> UpsertSupplierAsync(UpsertSupplierRequest request, Guid actorId, CancellationToken ct = default);
    Task<IReadOnlyList<PurchaseOrderListItemDto>> ListPurchaseOrdersAsync(CancellationToken ct = default);
    Task<PurchaseOrderListItemDto> CreatePurchaseOrderAsync(CreatePurchaseOrderRequest request, Guid actorId, CancellationToken ct = default);
    Task<PurchaseOrderListItemDto> ReceiveLineAsync(Guid poId, ReceivePoLineRequest request, Guid actorId, CancellationToken ct = default);
}

public interface INotificationService
{
    Task<IReadOnlyList<NotificationDto>> ListForUserAsync(Guid userId, CancellationToken ct = default);
    Task MarkReadAsync(Guid id, Guid userId, CancellationToken ct = default);
    Task NotifyAsync(Guid userId, string title, string body, string severity = "Info", string? route = null, CancellationToken ct = default);
}

public interface IBookingService
{
    Task<IReadOnlyList<BookingDto>> ListAsync(DateTimeOffset? from, DateTimeOffset? to, CancellationToken ct = default);
    Task<BookingDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<BookingDto> CreateAsync(CreateBookingRequest request, Guid actorId, CancellationToken ct = default);
    Task<BookingDto> UpdateAsync(Guid id, UpdateBookingRequest request, Guid actorId, CancellationToken ct = default);
    Task<BookingDto> CancelAsync(Guid id, Guid actorId, CancellationToken ct = default);
}

public interface IKnowledgeService
{
    Task<IReadOnlyList<KnowledgeDto>> ListAsync(string? q, CancellationToken ct = default);
    Task<KnowledgeDto> UpsertAsync(UpsertKnowledgeRequest request, Guid actorId, CancellationToken ct = default);
}

public interface IPcBuildService
{
    Task<IReadOnlyList<PcBuildListItemDto>> ListAsync(CancellationToken ct = default);
    Task<PcBuildDetailDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<PcBuildDetailDto> CreateAsync(CreatePcBuildRequest request, Guid actorId, CancellationToken ct = default);
    Task<PcBuildDetailDto> UpdateAsync(Guid id, UpdatePcBuildRequest request, Guid actorId, CancellationToken ct = default);
    Task<PcBuildDetailDto> UpdateStatusAsync(Guid id, UpdatePcBuildStatusRequest request, Guid actorId, CancellationToken ct = default);
    Task DeleteAsync(Guid id, Guid actorId, CancellationToken ct = default);
}

public interface IUsedTechService
{
    Task<IReadOnlyList<UsedDeviceDto>> ListAsync(string? q = null, string? status = null, CancellationToken ct = default);
    Task<UsedDeviceDetailDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<UsedDeviceDetailDto> CreateAsync(CreateUsedDeviceRequest request, Guid actorId, CancellationToken ct = default);
    Task<UsedDeviceDetailDto> UpdateAsync(Guid id, UpdateUsedDeviceRequest request, Guid actorId, CancellationToken ct = default);
    Task<UsedDeviceDetailDto> UpdateStatusAsync(Guid id, UpdateUsedStatusRequest request, Guid actorId, CancellationToken ct = default);
}

public interface IRefurbDashboardService
{
    Task<RefurbDashboardDto> GetAsync(DateTimeOffset? from, DateTimeOffset? to, CancellationToken ct = default);
}

public interface IRefurbBuyCalculatorService
{
    Task<RefurbBuyCalcResult> CalculateAsync(RefurbBuyCalcRequest request, CancellationToken ct = default);
}

public interface IAcquisitionService
{
    Task<IReadOnlyList<AcquisitionLeadListItemDto>> ListAsync(string? q = null, string? status = null, CancellationToken ct = default);
    Task<AcquisitionLeadDetailDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<AcquisitionLeadDetailDto> CreateAsync(CreateAcquisitionLeadRequest request, Guid actorId, CancellationToken ct = default);
    Task<AcquisitionLeadDetailDto> UpdateAsync(Guid id, UpdateAcquisitionLeadRequest request, Guid actorId, CancellationToken ct = default);
    Task<AcquisitionLeadDetailDto> UpdateStatusAsync(Guid id, UpdateAcquisitionStatusRequest request, Guid actorId, CancellationToken ct = default);
    Task<AcquisitionOfferDto> AddOfferAsync(Guid id, CreateAcquisitionOfferRequest request, Guid actorId, CancellationToken ct = default);
    Task<UsedDeviceDetailDto> ConvertToInventoryAsync(Guid id, ConvertAcquisitionRequest request, Guid actorId, CancellationToken ct = default);
}

public interface IQaService
{
    Task<IReadOnlyList<QaItemDto>> ListForTicketAsync(Guid ticketId, CancellationToken ct = default);
    Task EnsureDefaultChecklistAsync(Guid ticketId, CancellationToken ct = default);
    Task<QaItemDto> SetResultAsync(Guid ticketId, Guid itemId, string result, Guid actorId, CancellationToken ct = default);
}

public interface IReportService
{
    Task<ReportSummaryDto> SummaryAsync(DateTimeOffset? from, DateTimeOffset? to, CancellationToken ct = default);
}

public interface IAiService
{
    Task<AiAssistResponse> AssistAsync(AiAssistRequest request, Guid actorId, CancellationToken ct = default);
}

public interface IBackupService
{
    Task<IReadOnlyList<BackupDto>> ListAsync(CancellationToken ct = default);
    Task<BackupDto> CreateAsync(Guid actorId, CancellationToken ct = default);
    Task<SystemHealthDetailDto> HealthDetailAsync(CancellationToken ct = default);
}

public interface IIntegrationCredentialService
{
    IReadOnlyList<IntegrationProviderInfoDto> ListProviders();
    Task EnsureMigratedFromLegacySettingsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<IntegrationCredentialDto>> ListAsync(CancellationToken ct = default);
    Task<IntegrationCredentialDto> UpsertAsync(UpsertIntegrationCredentialRequest request, Guid actorId, CancellationToken ct = default);
    Task<TestIntegrationCredentialResponse> TestAsync(string providerKey, Guid actorId, CancellationToken ct = default);
    /// <summary>Server-side only. Returns decrypted secret map or null. Never expose via API.</summary>
    Task<Dictionary<string, string>?> GetSecretsMapAsync(string providerKey, CancellationToken ct = default);
    Task<string?> GetPublicConfigAsync(string providerKey, CancellationToken ct = default);
}

public interface IExpenseService
{
    Task EnsureDefaultsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ExpenseCategoryDto>> ListCategoriesAsync(CancellationToken ct = default);
    Task<ExpenseCategoryDto> UpsertCategoryAsync(UpsertExpenseCategoryRequest request, Guid actorId, CancellationToken ct = default);
    Task<GstCalcResult> PreviewGstAsync(GstCalcRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<ExpenseListItemDto>> ListAsync(string? q, string? status, Guid? categoryId, DateOnly? from, DateOnly? to, CancellationToken ct = default);
    Task<ExpenseDetailDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<ExpenseDetailDto> UpsertAsync(Guid? id, UpsertExpenseRequest request, Guid actorId, CancellationToken ct = default);
    Task SoftDeleteAsync(Guid id, Guid actorId, CancellationToken ct = default);
    Task<ExpenseDashboardDto> DashboardAsync(DateOnly? from, DateOnly? to, CancellationToken ct = default);
    Task<string> ExportCsvAsync(DateOnly? from, DateOnly? to, Guid? categoryId, CancellationToken ct = default);
    Task<IReadOnlyList<RecurringExpenseDto>> ListRecurringAsync(CancellationToken ct = default);
    Task<RecurringExpenseDto> UpsertRecurringAsync(Guid? id, UpsertRecurringExpenseRequest request, Guid actorId, CancellationToken ct = default);
    Task<int> ProcessDueRecurringAsync(CancellationToken ct = default);
    Task<ExpenseAttachmentDto> AddAttachmentAsync(Guid expenseId, string fileName, string contentType, Stream content, Guid actorId, CancellationToken ct = default);
    Task<(Stream Stream, string ContentType, string FileName)> OpenAttachmentAsync(Guid expenseId, Guid attachmentId, CancellationToken ct = default);
}

// —— Website / WordPress / customer portal ——

public interface IWebsitePublicService
{
    Task<PublicRepairQuoteResponse> SubmitRepairQuoteAsync(PublicRepairQuoteRequest request, string? ip, CancellationToken ct = default);
    Task<PublicRepairBookingResponse> SubmitRepairBookingAsync(PublicRepairBookingRequest request, string? ip, CancellationToken ct = default);
    Task<PublicSellDeviceResponse> SubmitSellDeviceAsync(PublicSellDeviceRequest request, string? ip, CancellationToken ct = default);
    Task<PublicCustomPcResponse> SubmitCustomPcAsync(PublicCustomPcRequest request, string? ip, CancellationToken ct = default);
    Task<PublicRepairStatusDto> GetRepairStatusAsync(string trackingCode, string accessPin, CancellationToken ct = default);
    Task<PublicRepairStatusDto> GetRepairStatusByTokenAsync(string accessToken, CancellationToken ct = default);
    Task<PublicQuoteDecisionResponse> ApproveQuoteAsync(Guid quoteId, PublicQuoteDecisionRequest request, string? ip, string? userAgent, CancellationToken ct = default);
    Task<PublicQuoteDecisionResponse> DeclineQuoteAsync(Guid quoteId, PublicQuoteDecisionRequest request, string? ip, string? userAgent, CancellationToken ct = default);
    Task<IReadOnlyList<PublicAvailabilitySlotDto>> GetBookingAvailabilityAsync(DateTimeOffset day, CancellationToken ct = default);
    Task<PublicSameDayHintDto> GetSameDayHintAsync(string? serviceCode, string? category, CancellationToken ct = default);
    Task<PublicRepairMessageResponse> PostRepairMessageAsync(string trackingCode, PublicRepairMessageRequest request, string? ip, CancellationToken ct = default);
    Task<SyncRefurbishedStatusResponse> SyncRefurbishedStatusAsync(SyncRefurbishedStatusRequest request, CancellationToken ct = default);
}

public interface ICustomerPortalService
{
    Task<CustomerPortalAuthResponse> LoginAsync(CustomerPortalLoginRequest request, CancellationToken ct = default);
    Task<CustomerPortalAuthResponse> RegisterAsync(CustomerPortalRegisterRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<CustomerRepairListItemDto>> ListRepairsAsync(Guid customerId, CancellationToken ct = default);
    Task<IReadOnlyList<CustomerQuoteListItemDto>> ListQuotesAsync(Guid customerId, CancellationToken ct = default);
    Task<IReadOnlyList<CustomerDeviceListItemDto>> ListDevicesAsync(Guid customerId, CancellationToken ct = default);
    Task<IReadOnlyList<CustomerInvoiceListItemDto>> ListInvoicesAsync(Guid customerId, CancellationToken ct = default);
    Task<IReadOnlyList<CustomerWarrantyListItemDto>> ListWarrantiesAsync(Guid customerId, CancellationToken ct = default);
    Task<IReadOnlyList<CustomerMessageListItemDto>> ListThreadsAsync(Guid customerId, CancellationToken ct = default);
    Task<IReadOnlyList<CustomerMessageDto>> ListMessagesAsync(Guid customerId, Guid threadId, CancellationToken ct = default);
    Task<CustomerMessageListItemDto> PostMessageAsync(Guid customerId, CustomerPostMessageRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<CustomerSellRequestListItemDto>> ListSellRequestsAsync(Guid customerId, CancellationToken ct = default);
}

public interface ISellTechService
{
    Task<IReadOnlyList<SellDeviceListItemDto>> ListAsync(string? q, string? status, CancellationToken ct = default);
    Task<SellDeviceDetailDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<SellDeviceDetailDto> UpdateStatusAsync(Guid id, UpdateSellDeviceStatusRequest request, Guid actorId, CancellationToken ct = default);
    Task<SellDeviceOfferDto> UpsertOfferAsync(Guid requestId, UpsertSellDeviceOfferRequest request, Guid actorId, CancellationToken ct = default);
    Task<SellDeviceOfferDto> SendOfferAsync(Guid requestId, Guid actorId, CancellationToken ct = default);
    Task<UsedDeviceDetailDto> CreateRefurbFromPurchaseAsync(Guid requestId, Guid actorId, CancellationToken ct = default);
    Task<OfferCalculatorDefaultsDto> GetOfferDefaultsAsync(CancellationToken ct = default);
    Task<OfferCalculatorDefaultsDto> SaveOfferDefaultsAsync(OfferCalculatorDefaultsDto defaults, Guid actorId, CancellationToken ct = default);
}

public interface IMessagingService
{
    Task<IReadOnlyList<StaffThreadDto>> ListThreadsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<StaffMessageDto>> ListMessagesAsync(Guid threadId, bool includeInternal, CancellationToken ct = default);
    Task<StaffMessageDto> PostStaffMessageAsync(Guid threadId, StaffPostMessageRequest request, Guid actorId, CancellationToken ct = default);
    Task<ConversationThread> EnsureRepairThreadAsync(Guid ticketId, CancellationToken ct = default);
}

public interface IWarrantyService
{
    Task<IReadOnlyList<CustomerWarrantyListItemDto>> ListForCustomerAsync(Guid customerId, CancellationToken ct = default);
    Task<WarrantyRecord> CreateForRepairAsync(Guid ticketId, int warrantyDays, string summary, Guid actorId, CancellationToken ct = default);
}

public interface INotificationTemplateService
{
    Task<IReadOnlyList<NotificationTemplateDto>> ListAsync(CancellationToken ct = default);
    Task<NotificationTemplateDto> UpsertAsync(string key, UpsertNotificationTemplateRequest request, CancellationToken ct = default);
    Task QueueAsync(string templateKey, string to, Guid? customerId, Guid? ticketId, IReadOnlyDictionary<string, string> tokens, CancellationToken ct = default);
}

public interface IWebsiteIntegrationService
{
    Task<IReadOnlyList<WebsiteApiKeyDto>> ListApiKeysAsync(CancellationToken ct = default);
    Task<CreateWebsiteApiKeyResponse> CreateApiKeyAsync(CreateWebsiteApiKeyRequest request, Guid actorId, CancellationToken ct = default);
    Task RevokeApiKeyAsync(Guid id, CancellationToken ct = default);
    Task<bool> ValidateApiKeyAsync(string apiKey, CancellationToken ct = default);
    Task<WooCommerceSettingsDto> GetWooSettingsAsync(CancellationToken ct = default);
    Task<WooCommerceSettingsDto> SaveWooSettingsAsync(UpdateWooCommerceSettingsRequest request, Guid actorId, CancellationToken ct = default);
    Task<PublishRefurbishedResponse> PublishRefurbishedAsync(PublishRefurbishedRequest request, Guid actorId, CancellationToken ct = default);
    Task<WebsiteDashboardCardsDto> GetWebsiteDashboardAsync(CancellationToken ct = default);
    Task<QrPayloadDto> BuildQrAsync(string kind, Guid id, CancellationToken ct = default);
    Task EnsureWebsiteDefaultsAsync(CancellationToken ct = default);
    Task<WebsiteWebhookSettingsDto> GetWebhookSettingsAsync(CancellationToken ct = default);
    Task<WebsiteWebhookSettingsDto> SaveWebhookSettingsAsync(UpdateWebsiteWebhookSettingsRequest request, Guid actorId, CancellationToken ct = default);
    Task EmitWebhookAsync(string eventName, object payload, CancellationToken ct = default);
    Task<BookingAvailabilitySettingsDto> GetBookingAvailabilitySettingsAsync(CancellationToken ct = default);
    Task<BookingAvailabilitySettingsDto> SaveBookingAvailabilitySettingsAsync(BookingAvailabilitySettingsDto settings, Guid actorId, CancellationToken ct = default);
}

public interface ICustomPcRequestService
{
    Task<IReadOnlyList<CustomPcRequestListItemDto>> ListAsync(string? status, CancellationToken ct = default);
    Task<CustomPcRequestDetailDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<CustomPcRequestDetailDto> UpdateStatusAsync(Guid id, UpdateCustomPcStatusRequest request, Guid actorId, CancellationToken ct = default);
    Task<CustomPcRequestDetailDto> ConvertToBuildAsync(Guid id, ConvertCustomPcToBuildRequest request, Guid actorId, CancellationToken ct = default);
}

public interface IRefurbDiagnosticService
{
    Task<IReadOnlyList<RefurbChecklistDto>> ListChecklistsAsync(CancellationToken ct = default);
    Task<RefurbChecklistDto> UpsertChecklistAsync(Guid? id, UpsertRefurbChecklistRequest request, CancellationToken ct = default);
    Task<RefurbDiagnosticResultDto> SaveResultAsync(SaveRefurbDiagnosticRequest request, Guid actorId, CancellationToken ct = default);
    Task<IReadOnlyList<RefurbDiagnosticResultDto>> ListResultsAsync(Guid usedDeviceId, CancellationToken ct = default);
}

public interface INotificationOutboxProcessor
{
    Task<int> ProcessPendingAsync(int take = 50, CancellationToken ct = default);
}
