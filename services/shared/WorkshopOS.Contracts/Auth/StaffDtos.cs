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
    DateTimeOffset CreatedAt,
    bool MustResetPassword = false);

/// <summary>
/// Create staff. Leave <see cref="Password"/> null/blank to auto-generate a temporary password
/// (returned once in <see cref="CreateStaffUserResponse.TemporaryPassword"/>). New accounts always
/// require a password change on first login.
/// </summary>
public sealed record CreateStaffUserRequest(
    string Email,
    string DisplayName,
    string? Password,
    string RoleKey,
    string? Phone);

public sealed record CreateStaffUserResponse(
    StaffUserDto User,
    string TemporaryPassword);

public sealed record UpdateStaffUserRequest(
    string? DisplayName,
    string? RoleKey,
    string? Status,
    string? Phone);

/// <summary>Admin/owner sets a new password for a staff user (no current password required). Staff must change it at next login.</summary>
public sealed record ResetStaffPasswordRequest(string? NewPassword = null);

/// <summary>Logged-in user changes their own password.</summary>
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
