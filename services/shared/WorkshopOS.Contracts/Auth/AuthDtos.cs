namespace WorkshopOS.Contracts.Auth;

public sealed record SetupStatusDto(bool IsComplete, string ProductName);

public sealed record SetupRequest(
    string BusinessName,
    string? Phone,
    string? Email,
    string? Abn,
    string? AddressLine1,
    string? Suburb,
    string? State,
    string? Postcode,
    string? Website,
    bool GstRegistered,
    decimal GstRate,
    string Currency,
    decimal DefaultLabourRate,
    string OwnerName,
    string OwnerEmail,
    string OwnerPassword,
    string? AccentColour);

public sealed record LoginRequest(string Email, string Password);

public sealed record RefreshRequest(string RefreshToken);

public sealed record LogoutRequest(string? RefreshToken);

public sealed record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset AccessTokenExpiresAt,
    UserDto User);

public sealed record UserDto(
    Guid Id,
    string Email,
    string DisplayName,
    string RoleKey,
    string RoleName,
    bool IsOwner,
    IReadOnlyList<string> Permissions,
    string Theme,
    BusinessProfileDto? Business);

public sealed record BusinessProfileDto(
    string Name,
    string? Phone,
    string? Email,
    string? Abn,
    string? AddressLine1,
    string? Suburb,
    string? State,
    string? Postcode,
    string? Website,
    string? AccentColour,
    bool GstRegistered,
    decimal GstRate,
    string Currency,
    decimal DefaultLabourRate);
