using WorkshopOS.Contracts.Auth;
using WorkshopOS.Contracts.Common;

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
