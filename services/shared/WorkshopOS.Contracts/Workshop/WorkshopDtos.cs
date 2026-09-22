namespace WorkshopOS.Contracts.Workshop;

public sealed record CustomerListItemDto(
    Guid Id,
    string DisplayName,
    CustomerType Type,
    string? Phone,
    string? Email,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastVisitAt,
    int DeviceCount,
    int OpenRepairCount);

public sealed record CustomerDetailDto(
    Guid Id,
    CustomerType Type,
    string? FirstName,
    string? LastName,
    string DisplayName,
    string? CompanyName,
    string? Phone,
    string? Email,
    string? AddressLine1,
    string? Suburb,
    string? State,
    string? Postcode,
    string? Notes,
    PreferredContact PreferredContact,
    bool MarketingConsent,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastVisitAt,
    IReadOnlyList<DeviceListItemDto> Devices,
    IReadOnlyList<RepairListItemDto> RecentRepairs);

public sealed record UpsertCustomerRequest(
    Guid? Id,
    CustomerType Type,
    string? FirstName,
    string? LastName,
    string? CompanyName,
    string? Phone,
    string? Email,
    string? AddressLine1,
    string? Suburb,
    string? State,
    string? Postcode,
    string? Notes,
    PreferredContact PreferredContact,
    bool MarketingConsent);

public sealed record DeviceListItemDto(
    Guid Id,
    Guid CustomerId,
    string CustomerName,
    DeviceCategory Category,
    string Brand,
    string Model,
    string? Variant,
    string? Serial,
    string? Imei,
    string Label);

public sealed record UpsertDeviceRequest(
    Guid? Id,
    Guid CustomerId,
    DeviceCategory Category,
    string Brand,
    string Model,
    string? Variant,
    string? Colour,
    string? Serial,
    string? Imei,
    string? StorageCapacity,
    string? Notes);

public sealed record RepairListItemDto(
    Guid Id,
    string TicketNumber,
    string CustomerName,
    string? DeviceLabel,
    string TypeName,
    string StatusKey,
    string StatusName,
    string StatusColour,
    string PriorityName,
    string? AssignedToName,
    string ReportedIssue,
    DateTimeOffset CreatedAt,
    DateTimeOffset? DueAt,
    bool IsOverdue);

public sealed record RepairDetailDto(
    Guid Id,
    string TicketNumber,
    Guid CustomerId,
    string CustomerName,
    Guid? DeviceId,
    string? DeviceLabel,
    Guid TypeId,
    string TypeName,
    Guid StatusId,
    string StatusKey,
    string StatusName,
    string StatusColour,
    Guid PriorityId,
    string PriorityName,
    Guid? AssignedToId,
    string? AssignedToName,
    string ReportedIssue,
    string? Diagnosis,
    string? RecommendedRepair,
    string? DamageDescription,
    bool? HasExistingCracks,
    bool? HasScratches,
    bool? WaterDamageIndicators,
    bool? PowersOn,
    bool AccessoriesIncluded,
    bool ChargerIncluded,
    bool SimIncluded,
    bool CaseIncluded,
    bool HasPasscode,
    decimal? EstimatedPrice,
    decimal? DepositAmount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? DueAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    IReadOnlyList<RepairEventDto> Timeline,
    IReadOnlyList<RepairNoteDto> Notes);

public sealed record RepairEventDto(Guid Id, string EventType, string Summary, string? OldValue, string? NewValue, DateTimeOffset CreatedAt, Guid? ActorUserId);
public sealed record RepairNoteDto(Guid Id, string Body, bool IsInternal, string AuthorName, DateTimeOffset CreatedAt);

public sealed record CreateRepairRequest(
    Guid CustomerId,
    Guid? DeviceId,
    Guid? TypeId,
    Guid? PriorityId,
    Guid? AssignedToId,
    string ReportedIssue,
    string? DamageDescription,
    bool? HasExistingCracks,
    bool? HasScratches,
    bool? WaterDamageIndicators,
    bool? PowersOn,
    bool AccessoriesIncluded,
    bool ChargerIncluded,
    bool SimIncluded,
    bool CaseIncluded,
    string? Passcode,
    decimal? EstimatedPrice,
    decimal? DepositAmount,
    DateTimeOffset? DueAt,
    // inline device create when DeviceId null and device fields provided
    DeviceCategory? NewDeviceCategory,
    string? NewDeviceBrand,
    string? NewDeviceModel,
    string? NewDeviceSerial,
    string? NewDeviceImei);

public sealed record ChangeStatusRequest(Guid StatusId);
public sealed record AssignRepairRequest(Guid? AssignedToId);
public sealed record AddNoteRequest(string Body, bool IsInternal);
public sealed record UpdateDiagnosisRequest(string? Diagnosis, string? RecommendedRepair);

public sealed record RepairLookupsDto(
    IReadOnlyList<LookupDto> Statuses,
    IReadOnlyList<LookupDto> Types,
    IReadOnlyList<LookupDto> Priorities,
    IReadOnlyList<StaffLookupDto> Technicians);

public sealed record LookupDto(Guid Id, string Key, string Name, string? Extra);
public sealed record StaffLookupDto(Guid Id, string Name, string Email);

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);
