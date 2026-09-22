using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WorkshopOS.Domain.Entities;
using WorkshopOS.Domain.Enums;
using WorkshopOS.Domain.Security;

namespace WorkshopOS.Infrastructure.Persistence;

public sealed class WorkshopDbContext : DbContext
{
    public WorkshopDbContext(DbContextOptions<WorkshopDbContext> options) : base(options) { }

    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<AppRole> Roles => Set<AppRole>();
    public DbSet<PermissionDefinition> Permissions => Set<PermissionDefinition>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<BusinessSetting> Settings => Set<BusinessSetting>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppUser>(e =>
        {
            e.ToTable("users");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.NormalizedEmail).IsUnique();
            e.Property(x => x.Email).HasMaxLength(320).IsRequired();
            e.Property(x => x.DisplayName).HasMaxLength(200).IsRequired();
            e.Property(x => x.PasswordHash).IsRequired();
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            e.HasOne(x => x.Role).WithMany(r => r.Users).HasForeignKey(x => x.RoleId);
            e.HasOne(x => x.Location).WithMany(l => l.Users).HasForeignKey(x => x.LocationId);
        });

        modelBuilder.Entity<AppRole>(e =>
        {
            e.ToTable("roles");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Key).IsUnique();
            e.Property(x => x.Key).HasMaxLength(64).IsRequired();
            e.Property(x => x.Name).HasMaxLength(120).IsRequired();
        });

        modelBuilder.Entity<PermissionDefinition>(e =>
        {
            e.ToTable("permissions");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Key).IsUnique();
            e.Property(x => x.Key).HasMaxLength(128).IsRequired();
        });

        modelBuilder.Entity<RolePermission>(e =>
        {
            e.ToTable("role_permissions");
            e.HasKey(x => new { x.RoleId, x.PermissionKey });
            e.Property(x => x.PermissionKey).HasMaxLength(128);
            e.HasOne(x => x.Role).WithMany(r => r.Permissions).HasForeignKey(x => x.RoleId);
        });

        modelBuilder.Entity<RefreshToken>(e =>
        {
            e.ToTable("refresh_tokens");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.TokenHash).IsUnique();
            e.HasOne(x => x.User).WithMany(u => u.RefreshTokens).HasForeignKey(x => x.UserId);
        });

        modelBuilder.Entity<Location>(e =>
        {
            e.ToTable("locations");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
        });

        modelBuilder.Entity<BusinessSetting>(e =>
        {
            e.ToTable("settings");
            e.HasKey(x => x.Key);
            e.Property(x => x.Key).HasMaxLength(128);
            e.Property(x => x.JsonValue).HasColumnType("jsonb");
        });

        modelBuilder.Entity<AuditEvent>(e =>
        {
            e.ToTable("audit_events");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.EntityType, x.EntityId, x.CreatedAt });
            e.HasIndex(x => x.CreatedAt);
        });
    }
}

public static class SettingKeys
{
    public const string SetupCompleted = "setup.completed";
    public const string BusinessProfile = "business.profile";
    public const string Gst = "gst";
    public const string FinanceDefaults = "finance.defaults";
    public const string ModuleVisibility = "modules.visibility";
}

public static class DbSeed
{
    public static async Task EnsureFoundationAsync(WorkshopDbContext db, CancellationToken ct = default)
    {
        foreach (var (key, group, label) in PermissionKeys.Catalogue)
        {
            if (!await db.Permissions.AnyAsync(p => p.Key == key, ct))
            {
                db.Permissions.Add(new PermissionDefinition { Key = key, Group = group, Label = label });
            }
        }

        var roles = new (string Key, string Name, string Description)[]
        {
            ("owner", "Owner", "Founding owner with full access"),
            ("administrator", "Administrator", "Full administrative access"),
            ("manager", "Manager", "Operations and limited administration"),
            ("technician", "Technician", "Workshop and assigned jobs"),
            ("front_desk", "Front Desk", "Intake, customers and payments"),
            ("sales", "Sales", "Quotes and used technology sales"),
            ("read_only", "Read Only", "View-only access")
        };

        foreach (var (key, name, description) in roles)
        {
            var role = await db.Roles.Include(r => r.Permissions).FirstOrDefaultAsync(r => r.Key == key, ct);
            if (role is null)
            {
                role = new AppRole { Key = key, Name = name, Description = description, IsSystem = true };
                db.Roles.Add(role);
                await db.SaveChangesAsync(ct);
            }

            var desired = PermissionKeys.DefaultRolePermissions[key];
            db.RolePermissions.RemoveRange(role.Permissions);
            foreach (var perm in desired)
            {
                db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionKey = perm });
            }
        }

        await db.SaveChangesAsync(ct);
    }

    public static async Task<T> GetSettingAsync<T>(WorkshopDbContext db, string key, T fallback, CancellationToken ct = default)
    {
        var row = await db.Settings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == key, ct);
        if (row is null) return fallback;
        return JsonSerializer.Deserialize<T>(row.JsonValue) ?? fallback;
    }

    public static async Task SetSettingAsync(WorkshopDbContext db, string key, object value, Guid? userId, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(value);
        var row = await db.Settings.FirstOrDefaultAsync(s => s.Key == key, ct);
        if (row is null)
        {
            db.Settings.Add(new BusinessSetting { Key = key, JsonValue = json, UpdatedById = userId, UpdatedAt = DateTimeOffset.UtcNow });
        }
        else
        {
            row.JsonValue = json;
            row.UpdatedById = userId;
            row.UpdatedAt = DateTimeOffset.UtcNow;
        }
        await db.SaveChangesAsync(ct);
    }
}
