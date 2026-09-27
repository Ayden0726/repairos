namespace WorkshopOS.Contracts.Common;

public static class ProductVersions
{
    /// <summary>Informational API / product version shipped with this tree.</summary>
    public const string Api = "1.2.11";
    /// <summary>Minimum Windows client recommended for current API surface (pricing, roles CRUD, system info).</summary>
    public const string ClientMinimum = "1.2.11";
}

public sealed record HealthDto(
    string Status,
    string ApiVersion,
    bool Database,
    DateTimeOffset ServerTimeUtc,
    string Product = "WorkshopOS",
    string? ClientMinVersion = null,
    bool RestartAllowed = false);

public sealed record SystemInfoDto(
    string Product,
    string ApiVersion,
    string ClientMinVersion,
    bool Database,
    bool WorkerHeartbeat,
    bool RestartAllowed,
    string Status,
    DateTimeOffset ServerTimeUtc,
    IReadOnlyList<string> UpdateCommands,
    IReadOnlyList<string> RestartCommands);

public sealed record RestartResultDto(
    bool Restarted,
    string Message,
    IReadOnlyList<string> Commands);

public sealed record SearchResponse(string Query, IReadOnlyList<SearchGroupDto> Groups);

public sealed record SearchGroupDto(string Type, IReadOnlyList<SearchHitDto> Hits);

public sealed record SearchHitDto(string Id, string Title, string? Subtitle, string Route);

/// <summary>Role catalogue row. Permissions is string[] so WinUI/JSON deserialize reliably.</summary>
public sealed record RoleDto(Guid Id, string Key, string Name, string? Description, string[] Permissions, bool IsSystem = false);

public sealed record CreateRoleRequest(string Key, string Name, string? Description, string[] Permissions);

public sealed record UpdateRoleRequest(string? Name, string? Description, string[]? Permissions);

public sealed record PermissionDto(string Key, string Group, string Label);

public sealed record ModuleDto(string Key, string Section, string Title, int Phase, bool Visible, bool Implemented);

/// <summary>Public LAN discovery payload — no auth. Used by Windows clients and /connect portal.</summary>
public sealed record DiscoveryDto(
    string Product,
    string PairingCode,
    bool SetupComplete,
    string ApiVersion,
    string? BusinessName,
    IReadOnlyList<string> SuggestedUrls);
