using WorkshopOS.Contracts.Auth;
using WorkshopOS.Contracts.Common;
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
    Task<PagedResult<RepairListItemDto>> ListAsync(string? q, string? statusKey, Guid? assignedToId, bool? overdueOnly, int page, int pageSize, CancellationToken ct = default);
    Task<RepairDetailDto> GetAsync(Guid id, bool canViewCredentials, CancellationToken ct = default);
    Task<RepairDetailDto> CreateAsync(CreateRepairRequest request, Guid actorId, CancellationToken ct = default);
    Task<RepairDetailDto> ChangeStatusAsync(Guid id, Guid statusId, Guid actorId, CancellationToken ct = default);
    Task<RepairDetailDto> AssignAsync(Guid id, Guid? assignedToId, Guid actorId, CancellationToken ct = default);
    Task<RepairNoteDto> AddNoteAsync(Guid id, AddNoteRequest request, Guid actorId, bool canInternal, CancellationToken ct = default);
    Task<RepairDetailDto> UpdateDiagnosisAsync(Guid id, UpdateDiagnosisRequest request, Guid actorId, CancellationToken ct = default);
    Task<RepairLookupsDto> GetLookupsAsync(CancellationToken ct = default);
}
