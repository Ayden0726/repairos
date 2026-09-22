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
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<RepairStatus> RepairStatuses => Set<RepairStatus>();
    public DbSet<RepairType> RepairTypes => Set<RepairType>();
    public DbSet<RepairPriority> RepairPriorities => Set<RepairPriority>();
    public DbSet<DocumentSequence> DocumentSequences => Set<DocumentSequence>();
    public DbSet<RepairTicket> RepairTickets => Set<RepairTicket>();
    public DbSet<RepairEvent> RepairEvents => Set<RepairEvent>();
    public DbSet<RepairNote> RepairNotes => Set<RepairNote>();

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

        modelBuilder.Entity<Customer>(e =>
        {
            e.ToTable("customers");
            e.HasKey(x => x.Id);
            e.Property(x => x.DisplayName).HasMaxLength(240).IsRequired();
            e.Property(x => x.Type).HasConversion<string>().HasMaxLength(32);
            e.Property(x => x.PreferredContact).HasConversion<string>().HasMaxLength(32);
            e.HasIndex(x => x.Phone);
            e.HasIndex(x => x.Email);
            e.HasIndex(x => x.DisplayName);
            e.HasOne(x => x.Location).WithMany().HasForeignKey(x => x.LocationId);
        });

        modelBuilder.Entity<Device>(e =>
        {
            e.ToTable("devices");
            e.HasKey(x => x.Id);
            e.Property(x => x.Category).HasConversion<string>().HasMaxLength(32);
            e.Property(x => x.Brand).HasMaxLength(120).IsRequired();
            e.Property(x => x.Model).HasMaxLength(160).IsRequired();
            e.HasIndex(x => x.Serial);
            e.HasIndex(x => x.Imei);
            e.HasOne(x => x.Customer).WithMany(c => c.Devices).HasForeignKey(x => x.CustomerId);
        });

        modelBuilder.Entity<RepairStatus>(e =>
        {
            e.ToTable("repair_statuses");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Key).IsUnique();
            e.Property(x => x.Key).HasMaxLength(64).IsRequired();
            e.Property(x => x.Name).HasMaxLength(120).IsRequired();
        });

        modelBuilder.Entity<RepairType>(e =>
        {
            e.ToTable("repair_types");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Key).IsUnique();
            e.Property(x => x.Prefix).HasMaxLength(8).IsRequired();
        });

        modelBuilder.Entity<RepairPriority>(e =>
        {
            e.ToTable("repair_priorities");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Key).IsUnique();
        });

        modelBuilder.Entity<DocumentSequence>(e =>
        {
            e.ToTable("document_sequences");
            e.HasKey(x => x.Key);
            e.Property(x => x.Key).HasMaxLength(64);
        });

        modelBuilder.Entity<RepairTicket>(e =>
        {
            e.ToTable("repair_tickets");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.TicketNumber).IsUnique();
            e.Property(x => x.TicketNumber).HasMaxLength(40).IsRequired();
            e.Property(x => x.ReportedIssue).IsRequired();
            e.Property(x => x.EstimatedPrice).HasPrecision(12, 2);
            e.Property(x => x.DepositAmount).HasPrecision(12, 2);
            e.HasIndex(x => x.DueAt);
            e.HasIndex(x => x.CreatedAt);
            e.HasOne(x => x.Customer).WithMany(c => c.Repairs).HasForeignKey(x => x.CustomerId);
            e.HasOne(x => x.Device).WithMany(d => d.Repairs).HasForeignKey(x => x.DeviceId);
            e.HasOne(x => x.Type).WithMany().HasForeignKey(x => x.TypeId);
            e.HasOne(x => x.Status).WithMany().HasForeignKey(x => x.StatusId);
            e.HasOne(x => x.Priority).WithMany().HasForeignKey(x => x.PriorityId);
            e.HasOne(x => x.AssignedTo).WithMany().HasForeignKey(x => x.AssignedToId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.CreatedBy).WithMany().HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Location).WithMany().HasForeignKey(x => x.LocationId);
        });

        modelBuilder.Entity<RepairEvent>(e =>
        {
            e.ToTable("repair_events");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.TicketId, x.CreatedAt });
            e.HasOne(x => x.Ticket).WithMany(t => t.Events).HasForeignKey(x => x.TicketId);
        });

        modelBuilder.Entity<RepairNote>(e =>
        {
            e.ToTable("repair_notes");
            e.HasKey(x => x.Id);
            e.HasOne(x => x.Ticket).WithMany(t => t.Notes).HasForeignKey(x => x.TicketId);
            e.HasOne(x => x.Author).WithMany().HasForeignKey(x => x.AuthorId).OnDelete(DeleteBehavior.Restrict);
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
                db.Permissions.Add(new PermissionDefinition { Key = key, Group = group, Label = label });
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
                db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionKey = perm });
        }

        await EnsureRepairCatalogueAsync(db, ct);
        await db.SaveChangesAsync(ct);
    }

    public static async Task EnsureRepairCatalogueAsync(WorkshopDbContext db, CancellationToken ct = default)
    {
        (string Key, string Name, string Colour, int Sort, bool Completed, bool Cancelled, bool Parts, bool Approval, bool Pickup)[] statuses =
        [
            ("received", "Received", "#64748b", 10, false, false, false, false, false),
            ("diagnosing", "Diagnosing", "#2563eb", 20, false, false, false, false, false),
            ("awaiting_approval", "Awaiting Quote Approval", "#d97706", 30, false, false, false, true, false),
            ("waiting_parts", "Waiting for Parts", "#c2410c", 40, false, false, true, false, false),
            ("repair_scheduled", "Repair Scheduled", "#7c3aed", 50, false, false, false, false, false),
            ("repairing", "Repairing", "#4f46e5", 60, false, false, false, false, false),
            ("testing", "Testing", "#0f766e", 70, false, false, false, false, false),
            ("ready_pickup", "Ready for Pickup", "#15803d", 80, false, false, false, false, true),
            ("completed", "Completed", "#166534", 90, true, false, false, false, false),
            ("cancelled", "Cancelled", "#6b7280", 100, false, true, false, false, false),
            ("unrepairable", "Unrepairable", "#991b1b", 110, true, false, false, false, false)
        ];

        foreach (var s in statuses)
        {
            var row = await db.RepairStatuses.FirstOrDefaultAsync(x => x.Key == s.Key, ct);
            if (row is null)
            {
                db.RepairStatuses.Add(new RepairStatus
                {
                    Key = s.Key,
                    Name = s.Name,
                    Colour = s.Colour,
                    SortOrder = s.Sort,
                    IsSystem = true,
                    IsCompleted = s.Completed,
                    IsCancelled = s.Cancelled,
                    CountsAsWaitingForParts = s.Parts,
                    CountsAsAwaitingApproval = s.Approval,
                    CountsAsReadyForPickup = s.Pickup
                });
            }
        }

        (string Key, string Name, string Prefix, int Sort)[] types =
        [
            ("repair", "Repair", "REP", 10),
            ("diagnostic", "Diagnostic", "DIA", 20),
            ("warranty", "Warranty", "WAR", 30),
            ("data", "Data Recovery", "DAT", 40)
        ];
        foreach (var t in types)
        {
            if (!await db.RepairTypes.AnyAsync(x => x.Key == t.Key, ct))
            {
                db.RepairTypes.Add(new RepairType
                {
                    Key = t.Key,
                    Name = t.Name,
                    Prefix = t.Prefix,
                    SortOrder = t.Sort,
                    IsSystem = true
                });
            }
        }

        (string Key, string Name, int Sort, int Severity)[] priorities =
        [
            ("low", "Low", 10, 1),
            ("normal", "Normal", 20, 2),
            ("high", "High", 30, 3),
            ("urgent", "Urgent", 40, 4)
        ];
        foreach (var p in priorities)
        {
            if (!await db.RepairPriorities.AnyAsync(x => x.Key == p.Key, ct))
            {
                db.RepairPriorities.Add(new RepairPriority
                {
                    Key = p.Key,
                    Name = p.Name,
                    SortOrder = p.Sort,
                    Severity = p.Severity
                });
            }
        }
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
            db.Settings.Add(new BusinessSetting { Key = key, JsonValue = json, UpdatedById = userId, UpdatedAt = DateTimeOffset.UtcNow });
        else
        {
            row.JsonValue = json;
            row.UpdatedById = userId;
            row.UpdatedAt = DateTimeOffset.UtcNow;
        }
        await db.SaveChangesAsync(ct);
    }
}
