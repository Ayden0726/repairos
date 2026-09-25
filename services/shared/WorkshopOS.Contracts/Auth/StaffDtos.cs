namespace WorkshopOS.Contracts.Auth;

public sealed record StaffUserDto(
    Guid Id,
    string Email,
    string DisplayName,
    string? Phone,
    string RoleKey,
    string RoleName,
    string Status,
    bool IsOwner,
    DateTimeOffset? LastLoginAt,
    DateTimeOffset CreatedAt);

public sealed record CreateStaffUserRequest(
    string Email,
    string DisplayName,
    string Password,
    string RoleKey,
    string? Phone);

public sealed record UpdateStaffUserRequest(
    string? DisplayName,
    string? RoleKey,
    string? Status,
    string? Phone);
