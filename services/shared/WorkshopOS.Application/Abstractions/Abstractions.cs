using WorkshopOS.Contracts.Auth;
using WorkshopOS.Contracts.Common;
using WorkshopOS.Contracts.Operations;
using WorkshopOS.Contracts.Workshop;

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
}

public interface ISettingsService
{
    Task<BusinessProfileDto> GetBusinessAsync(CancellationToken ct = default);
    Task<BusinessProfileDto> UpdateBusinessAsync(BusinessProfileDto profile, Guid actorId, CancellationToken ct = default);
    Task<IReadOnlyList<ModuleDto>> GetModulesAsync(CancellationToken ct = default);
}

public interface IRoleService
{
    Task<IReadOnlyList<RoleDto>> ListRolesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<PermissionDto>> ListPermissionsAsync(CancellationToken ct = default);
}

public interface IStaffService
{
    Task<IReadOnlyList<StaffUserDto>> ListAsync(CancellationToken ct = default);
    Task<StaffUserDto> CreateAsync(CreateStaffUserRequest request, Guid actorId, CancellationToken ct = default);
    Task<StaffUserDto> UpdateAsync(Guid id, UpdateStaffUserRequest request, Guid actorId, CancellationToken ct = default);
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

public interface IInvoiceService
{
    Task<IReadOnlyList<InvoiceListItemDto>> ListAsync(CancellationToken ct = default);
    Task<InvoiceDetailDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<InvoiceDetailDto> CreateFromRepairAsync(Guid repairTicketId, Guid actorId, CancellationToken ct = default);
    Task<InvoiceDetailDto> RecordPaymentAsync(Guid id, RecordPaymentRequest request, Guid actorId, CancellationToken ct = default);
}

public interface IInventoryService
{
    Task<IReadOnlyList<InventoryListItemDto>> ListAsync(CancellationToken ct = default);
    Task<InventoryListItemDto> UpsertAsync(UpsertInventoryRequest request, Guid actorId, CancellationToken ct = default);
    Task<InventoryListItemDto> AdjustAsync(Guid id, AdjustStockRequest request, Guid actorId, CancellationToken ct = default);
    Task ReserveAsync(ReserveStockRequest request, Guid actorId, CancellationToken ct = default);
    Task ConsumeReservationAsync(Guid reservationId, Guid actorId, CancellationToken ct = default);
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
    Task<BookingDto> CreateAsync(CreateBookingRequest request, Guid actorId, CancellationToken ct = default);
}

public interface IKnowledgeService
{
    Task<IReadOnlyList<KnowledgeDto>> ListAsync(string? q, CancellationToken ct = default);
    Task<KnowledgeDto> UpsertAsync(UpsertKnowledgeRequest request, Guid actorId, CancellationToken ct = default);
}

public interface IPcBuildService
{
    Task<IReadOnlyList<PcBuildListItemDto>> ListAsync(CancellationToken ct = default);
    Task<PcBuildListItemDto> CreateAsync(CreatePcBuildRequest request, Guid actorId, CancellationToken ct = default);
}

public interface IUsedTechService
{
    Task<IReadOnlyList<UsedDeviceDto>> ListAsync(CancellationToken ct = default);
    Task<UsedDeviceDto> CreateAsync(CreateUsedDeviceRequest request, Guid actorId, CancellationToken ct = default);
    Task<UsedDeviceDto> UpdateStatusAsync(Guid id, UpdateUsedStatusRequest request, Guid actorId, CancellationToken ct = default);
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
