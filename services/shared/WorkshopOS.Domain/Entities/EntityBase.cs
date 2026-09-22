namespace WorkshopOS.Domain.Entities;

public abstract class EntityBase
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public abstract class SoftDeleteEntity : EntityBase
{
    public DateTimeOffset? ArchivedAt { get; set; }
    public bool IsArchived => ArchivedAt.HasValue;
}
