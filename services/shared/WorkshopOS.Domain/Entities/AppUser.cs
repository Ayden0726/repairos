using WorkshopOS.Domain.Enums;

namespace WorkshopOS.Domain.Entities;

public class AppUser
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = string.Empty;
    public string NormalizedEmail { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string NormalizedUserName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public Guid RoleId { get; set; }
    public AppRole Role { get; set; } = null!;
    public Guid? LocationId { get; set; }
    public Location? Location { get; set; }
    public bool IsOwner { get; set; }
    public UserStatus Status { get; set; } = UserStatus.Active;
    public DateTimeOffset? LastLoginAt { get; set; }
    public string? LastLoginIp { get; set; }
    public bool MustResetPassword { get; set; }
    public string Theme { get; set; } = "System";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ArchivedAt { get; set; }
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
