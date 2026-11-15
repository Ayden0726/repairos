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
    string? AccentColour,
    /// <summary>FullShop (default) or Home — side-hustle / beginner profile.</summary>
    string? WorkshopStyle = null,
    bool ShowBeginnerTips = true,
    /// <summary>Optional 4–8 digit owner PIN for tablet sign-in.</summary>
    string? OwnerPin = null);

public sealed record LoginRequest(string Email, string Password);

/// <summary>Owner/staff PIN sign-in for bench tablets (4–8 digits).</summary>
public sealed record PinLoginRequest(string Pin);

/// <summary>Set or clear the signed-in user's PIN. Null/empty clears it.</summary>
public sealed record SetPinRequest(string? Pin);

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
    decimal DefaultLabourRate,
    bool GstInclusive = true);
