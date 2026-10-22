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

/// <summary>Admin/owner sets a new password for a staff user (no current password required).</summary>
public sealed record ResetStaffPasswordRequest(string NewPassword);

/// <summary>Logged-in user changes their own password.</summary>
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
