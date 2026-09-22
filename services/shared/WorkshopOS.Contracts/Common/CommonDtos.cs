namespace WorkshopOS.Contracts.Common;

public sealed record HealthDto(
    string Status,
    string ApiVersion,
    bool Database,
    DateTimeOffset ServerTimeUtc);

public sealed record SearchResponse(string Query, IReadOnlyList<SearchGroupDto> Groups);

public sealed record SearchGroupDto(string Type, IReadOnlyList<SearchHitDto> Hits);

public sealed record SearchHitDto(string Id, string Title, string? Subtitle, string Route);

public sealed record RoleDto(Guid Id, string Key, string Name, string? Description, IReadOnlyList<string> Permissions);

public sealed record PermissionDto(string Key, string Group, string Label);

public sealed record ModuleDto(string Key, string Section, string Title, int Phase, bool Visible, bool Implemented);
