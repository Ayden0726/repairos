namespace WorkshopOS.Domain.Entities;

/// <summary>Cached TAC (Type Allocation Code) model-level identification to reduce API cost.</summary>
public class ImeiTacCache
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Tac { get; set; } = string.Empty;
    public string Provider { get; set; } = "imei.info";
    public string? Brand { get; set; }
    public string? Manufacturer { get; set; }
    public string? Model { get; set; }
    public string? ModelNumber { get; set; }
    public string? DeviceCategory { get; set; }
    public string? Display { get; set; }
    public string? Processor { get; set; }
    public string? Chipset { get; set; }
    public string? BatteryCapacity { get; set; }
    public string? Network { get; set; }
    public string? StorageHint { get; set; }
    public string? RamHint { get; set; }
    /// <summary>JSON object of additional model-level fields from the provider.</summary>
    public string? SpecsJson { get; set; }
    public string VerificationStatus { get; set; } = "ModelLevel";
    public DateTimeOffset LookedUpAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAt { get; set; }
    public int HitCount { get; set; }
}

/// <summary>Audit of IMEI lookups. Stores a hash of the IMEI — never the raw identifier.</summary>
public class ImeiLookupLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ImeiHash { get; set; } = string.Empty;
    public string? Tac { get; set; }
    public string Provider { get; set; } = "imei.info";
    public int? ServiceId { get; set; }
    public bool Advanced { get; set; }
    public bool FromCache { get; set; }
    public bool Success { get; set; }
    public string? ErrorCode { get; set; }
    public string VerificationStatus { get; set; } = "Unverified";
    public decimal? Cost { get; set; }
    public Guid? ActorUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
