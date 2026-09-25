using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WorkshopOS.Application.Abstractions;
using WorkshopOS.Application.Common;
using WorkshopOS.Contracts.Auth;
using WorkshopOS.Domain.Entities;
using WorkshopOS.Domain.Enums;
using WorkshopOS.Infrastructure.Persistence;
using WorkshopOS.Infrastructure.Security;

namespace WorkshopOS.Infrastructure.Services;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

public sealed class AuditService : IAuditService
{
    private readonly WorkshopDbContext _db;
    public AuditService(WorkshopDbContext db) => _db = db;

    public async Task WriteAsync(Guid? actorUserId, string action, string? entityType = null, string? entityId = null, object? oldValue = null, object? newValue = null, string? ip = null, string? userAgent = null, CancellationToken ct = default)
    {
        _db.AuditEvents.Add(new AuditEvent
        {
            ActorUserId = actorUserId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            OldValueJson = oldValue is null ? null : JsonSerializer.Serialize(oldValue),
            NewValueJson = newValue is null ? null : JsonSerializer.Serialize(newValue),
            Ip = ip,
            UserAgent = userAgent
        });
        await _db.SaveChangesAsync(ct);
    }
}

public sealed class SetupService : ISetupService
{
    private readonly WorkshopDbContext _db;
    private readonly TokenService _tokens;
    private readonly IAuditService _audit;

    public SetupService(WorkshopDbContext db, TokenService tokens, IAuditService audit)
    {
        _db = db;
        _tokens = tokens;
        _audit = audit;
    }

    public async Task<SetupStatusDto> GetStatusAsync(CancellationToken ct = default)
    {
        var done = await DbSeed.GetSettingAsync(_db, SettingKeys.SetupCompleted, false, ct);
        return new SetupStatusDto(done, "WorkshopOS");
    }

    public async Task CompleteAsync(SetupRequest request, string? ip, CancellationToken ct = default)
    {
        if (await DbSeed.GetSettingAsync(_db, SettingKeys.SetupCompleted, false, ct))
            throw new ConflictAppException("Setup is already complete.");

        var problem = PasswordRules.Validate(request.OwnerPassword);
        if (problem is not null) throw new ValidationAppException(problem);
        if (string.IsNullOrWhiteSpace(request.BusinessName))
            throw new ValidationAppException("Business name is required.");
        if (string.IsNullOrWhiteSpace(request.OwnerEmail) || string.IsNullOrWhiteSpace(request.OwnerName))
            throw new ValidationAppException("Owner name and email are required.");

        await DbSeed.EnsureFoundationAsync(_db, ct);

        var ownerRole = await _db.Roles.SingleAsync(r => r.Key == "owner", ct);
        var location = new Location
        {
            Name = request.BusinessName.Trim(),
            Phone = request.Phone,
            Email = request.Email,
            AddressLine1 = request.AddressLine1,
            Suburb = request.Suburb,
            State = string.IsNullOrWhiteSpace(request.State) ? "VIC" : request.State,
            Postcode = request.Postcode,
            IsDefault = true
        };
        _db.Locations.Add(location);

        var email = request.OwnerEmail.Trim().ToLowerInvariant();
        if (await _db.Users.AnyAsync(u => u.NormalizedEmail == email.ToUpperInvariant(), ct))
            throw new ConflictAppException("A user with that email already exists.");

        var owner = new AppUser
        {
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            DisplayName = request.OwnerName.Trim(),
            RoleId = ownerRole.Id,
            Role = ownerRole,
            Location = location,
            IsOwner = true,
            Status = UserStatus.Active
        };
        owner.PasswordHash = _tokens.HashPassword(owner, request.OwnerPassword);
        _db.Users.Add(owner);
        await _db.SaveChangesAsync(ct);

        var profile = new BusinessProfileDto(
            request.BusinessName.Trim(),
            request.Phone,
            request.Email,
            request.Abn,
            request.AddressLine1,
            request.Suburb,
            location.State,
            request.Postcode,
            request.Website,
            string.IsNullOrWhiteSpace(request.AccentColour) ? "#0F766E" : request.AccentColour,
            request.GstRegistered,
            request.GstRate <= 0 ? 0.10m : request.GstRate,
            string.IsNullOrWhiteSpace(request.Currency) ? "AUD" : request.Currency,
            request.DefaultLabourRate <= 0 ? 110m : request.DefaultLabourRate);

        await DbSeed.SetSettingAsync(_db, SettingKeys.BusinessProfile, profile, owner.Id, ct);
        await DbSeed.SetSettingAsync(_db, SettingKeys.Gst, new { registered = profile.GstRegistered, rate = profile.GstRate }, owner.Id, ct);
        await DbSeed.SetSettingAsync(_db, SettingKeys.FinanceDefaults, new { labourRate = profile.DefaultLabourRate, diagnosticFee = 89m }, owner.Id, ct);
        await DbSeed.SetSettingAsync(_db, SettingKeys.SetupCompleted, true, owner.Id, ct);
        await _audit.WriteAsync(owner.Id, "setup.completed", "Business", owner.Id.ToString(), ip: ip, ct: ct);
    }
}

public sealed class AuthService : IAuthService
{
    private readonly WorkshopDbContext _db;
    private readonly TokenService _tokens;
    private readonly IAuditService _audit;
    private readonly JwtOptions _jwt;

    public AuthService(WorkshopDbContext db, TokenService tokens, IAuditService audit, Microsoft.Extensions.Options.IOptions<JwtOptions> jwt)
    {
        _db = db;
        _tokens = tokens;
        _audit = audit;
        _jwt = jwt.Value;
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, string? ip, string? userAgent, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.Include(u => u.Role).ThenInclude(r => r.Permissions)
            .FirstOrDefaultAsync(u => u.NormalizedEmail == email.ToUpperInvariant(), ct);

        var ok = user is not null && user.ArchivedAt is null && user.Status == UserStatus.Active && _tokens.VerifyPassword(user, request.Password);
        if (!ok || user is null)
        {
            await _audit.WriteAsync(null, "auth.login_failed", "User", email, ip: ip, userAgent: userAgent, ct: ct);
            throw new UnauthorizedAppException();
        }

        user.LastLoginAt = DateTimeOffset.UtcNow;
        user.LastLoginIp = ip;
        var perms = GetPermissions(user);
        var (access, expires) = _tokens.CreateAccessToken(user, perms);
        var refresh = TokenService.CreateRefreshToken();
        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = TokenService.HashToken(refresh),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(_jwt.RefreshTokenDays),
            CreatedByIp = ip
        });
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(user.Id, "auth.login", "User", user.Id.ToString(), ip: ip, userAgent: userAgent, ct: ct);

        var business = await DbSeed.GetSettingAsync<BusinessProfileDto?>(_db, SettingKeys.BusinessProfile, null, ct);
        return new AuthResponse(access, refresh, expires, ToUserDto(user, perms, business));
    }

    public async Task<AuthResponse> RefreshAsync(string refreshToken, string? ip, CancellationToken ct = default)
    {
        var hash = TokenService.HashToken(refreshToken);
        var existing = await _db.RefreshTokens.Include(t => t.User).ThenInclude(u => u.Role).ThenInclude(r => r.Permissions)
            .FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (existing is null || !existing.IsActive || existing.User.Status != UserStatus.Active || existing.User.ArchivedAt is not null)
            throw new UnauthorizedAppException("Session expired. Sign in again.");

        existing.RevokedAt = DateTimeOffset.UtcNow;
        var user = existing.User;
        var perms = GetPermissions(user);
        var (access, expires) = _tokens.CreateAccessToken(user, perms);
        var next = TokenService.CreateRefreshToken();
        var nextHash = TokenService.HashToken(next);
        existing.ReplacedByTokenHash = nextHash;
        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = nextHash,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(_jwt.RefreshTokenDays),
            CreatedByIp = ip
        });
        await _db.SaveChangesAsync(ct);
        var business = await DbSeed.GetSettingAsync<BusinessProfileDto?>(_db, SettingKeys.BusinessProfile, null, ct);
        return new AuthResponse(access, next, expires, ToUserDto(user, perms, business));
    }

    public async Task LogoutAsync(Guid userId, string? refreshToken, CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(refreshToken))
        {
            var hash = TokenService.HashToken(refreshToken);
            var row = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.UserId == userId && t.TokenHash == hash, ct);
            if (row is not null) row.RevokedAt = DateTimeOffset.UtcNow;
        }
        else
        {
            var active = await _db.RefreshTokens.Where(t => t.UserId == userId && t.RevokedAt == null).ToListAsync(ct);
            foreach (var t in active) t.RevokedAt = DateTimeOffset.UtcNow;
        }
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(userId, "auth.logout", "User", userId.ToString(), ct: ct);
    }

    public async Task<UserDto> GetMeAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _db.Users.Include(u => u.Role).ThenInclude(r => r.Permissions)
            .FirstOrDefaultAsync(u => u.Id == userId, ct) ?? throw new UnauthorizedAppException();
        var business = await DbSeed.GetSettingAsync<BusinessProfileDto?>(_db, SettingKeys.BusinessProfile, null, ct);
        return ToUserDto(user, GetPermissions(user), business);
    }

    private static List<string> GetPermissions(AppUser user) =>
        user.IsOwner
            ? Domain.Security.PermissionKeys.AllKeys.ToList()
            : user.Role.Permissions.Select(p => p.PermissionKey).Distinct().ToList();

    private static UserDto ToUserDto(AppUser user, IReadOnlyList<string> perms, BusinessProfileDto? business) =>
        new(user.Id, user.Email, user.DisplayName, user.Role.Key, user.Role.Name, user.IsOwner, perms, user.Theme, business);
}

public sealed class SettingsService : ISettingsService
{
    private readonly WorkshopDbContext _db;
    private readonly IAuditService _audit;

    public SettingsService(WorkshopDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public Task<BusinessProfileDto> GetBusinessAsync(CancellationToken ct = default) =>
        DbSeed.GetSettingAsync(_db, SettingKeys.BusinessProfile, new BusinessProfileDto("Workshop", null, null, null, null, null, null, null, null, "#0F766E", true, 0.10m, "AUD", 110m), ct);

    public async Task<BusinessProfileDto> UpdateBusinessAsync(BusinessProfileDto profile, Guid actorId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(profile.Name)) throw new ValidationAppException("Business name is required.");
        await DbSeed.SetSettingAsync(_db, SettingKeys.BusinessProfile, profile, actorId, ct);
        await _audit.WriteAsync(actorId, "settings.business.update", "Setting", SettingKeys.BusinessProfile, newValue: profile, ct: ct);
        return profile;
    }

    public async Task<IReadOnlyList<WorkshopOS.Contracts.Common.ModuleDto>> GetModulesAsync(CancellationToken ct = default)
    {
        var hidden = await DbSeed.GetSettingAsync(_db, SettingKeys.ModuleVisibility, new Dictionary<string, bool>(), ct);
        WorkshopOS.Contracts.Common.ModuleDto[] modules =
        [
            new("dashboard", "Workshop", "Dashboard", 3, IsVisible(hidden, "dashboard"), true),
            new("repairs", "Workshop", "Tickets", 2, IsVisible(hidden, "repairs"), true),
            new("customers", "Workshop", "Customers", 2, IsVisible(hidden, "customers"), true),
            new("calendar", "Workshop", "Calendar", 9, IsVisible(hidden, "calendar"), true),
            new("notifications", "Workshop", "Notifications", 7, IsVisible(hidden, "notifications"), true),
            new("quotes", "Sales", "Quotes", 4, IsVisible(hidden, "quotes"), true),
            new("invoices", "Sales", "Invoices", 6, IsVisible(hidden, "invoices"), true),
            new("used", "Sales", "Used Tech", 8, IsVisible(hidden, "used"), true),
            new("inventory", "Stock", "Inventory", 5, IsVisible(hidden, "inventory"), true),
            new("purchasing", "Stock", "Purchasing", 5, IsVisible(hidden, "purchasing"), true),
            new("builds", "Services", "PC Builds", 8, IsVisible(hidden, "builds"), true),
            new("knowledge", "Services", "Knowledge", 9, IsVisible(hidden, "knowledge"), true),
            new("ai", "Services", "AI Assist", 11, IsVisible(hidden, "ai"), true),
            new("reports", "Management", "Reports", 10, IsVisible(hidden, "reports"), true),
            new("backups", "Management", "Backups", 12, IsVisible(hidden, "backups"), true),
            // Users & Settings live under the client Settings hub (not top-level sidebar).
            new("users", "Management", "Users", 1, IsVisible(hidden, "users"), true),
            new("settings", "Management", "Settings", 1, IsVisible(hidden, "settings"), true)
        ];
        return modules;
    }

    private static bool IsVisible(Dictionary<string, bool> hidden, string key) =>
        !hidden.TryGetValue(key, out var visible) || visible;
}

public sealed class RoleService : IRoleService
{
    private readonly WorkshopDbContext _db;
    public RoleService(WorkshopDbContext db) => _db = db;

    public async Task<IReadOnlyList<WorkshopOS.Contracts.Common.RoleDto>> ListRolesAsync(CancellationToken ct = default)
    {
        var roles = await _db.Roles.Include(r => r.Permissions).OrderBy(r => r.Name).ToListAsync(ct);
        return roles.Select(r => new WorkshopOS.Contracts.Common.RoleDto(
            r.Id, r.Key, r.Name, r.Description,
            r.Permissions.Select(p => p.PermissionKey).OrderBy(x => x).ToArray())).ToList();
    }

    public Task<IReadOnlyList<WorkshopOS.Contracts.Common.PermissionDto>> ListPermissionsAsync(CancellationToken ct = default)
    {
        var list = Domain.Security.PermissionKeys.Catalogue
            .Select(p => new WorkshopOS.Contracts.Common.PermissionDto(p.Key, p.Group, p.Label))
            .ToList();
        return Task.FromResult<IReadOnlyList<WorkshopOS.Contracts.Common.PermissionDto>>(list);
    }
}

public sealed class StaffService : IStaffService
{
    private readonly WorkshopDbContext _db;
    private readonly TokenService _tokens;
    private readonly IAuditService _audit;

    public StaffService(WorkshopDbContext db, TokenService tokens, IAuditService audit)
    {
        _db = db;
        _tokens = tokens;
        _audit = audit;
    }

    public async Task<IReadOnlyList<StaffUserDto>> ListAsync(CancellationToken ct = default)
    {
        var users = await _db.Users.AsNoTracking()
            .Include(u => u.Role)
            .Where(u => u.ArchivedAt == null)
            .OrderBy(u => u.DisplayName)
            .ToListAsync(ct);
        return users.Select(ToDto).ToList();
    }

    public async Task<StaffUserDto> CreateAsync(CreateStaffUserRequest request, Guid actorId, CancellationToken ct = default)
    {
        var email = (request.Email ?? string.Empty).Trim().ToLowerInvariant();
        var display = (request.DisplayName ?? string.Empty).Trim();
        var roleKey = (request.RoleKey ?? string.Empty).Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(display))
            throw new ValidationAppException("Email and display name are required.");
        if (string.IsNullOrWhiteSpace(roleKey) || roleKey == "owner")
            throw new ValidationAppException("Choose a role other than Owner. The shop owner is created during setup.");

        var problem = PasswordRules.Validate(request.Password);
        if (problem is not null) throw new ValidationAppException(problem);

        if (await _db.Users.AnyAsync(u => u.NormalizedEmail == email.ToUpperInvariant() && u.ArchivedAt == null, ct))
            throw new ConflictAppException("A user with that email already exists.");

        var role = await _db.Roles.FirstOrDefaultAsync(r => r.Key == roleKey, ct)
            ?? throw new ValidationAppException("Unknown role.");

        var locationId = await _db.Locations.AsNoTracking()
            .Where(l => l.IsDefault)
            .Select(l => (Guid?)l.Id)
            .FirstOrDefaultAsync(ct)
            ?? await _db.Locations.AsNoTracking().Select(l => (Guid?)l.Id).FirstOrDefaultAsync(ct);

        var user = new AppUser
        {
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            DisplayName = display,
            Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim(),
            RoleId = role.Id,
            Role = role,
            LocationId = locationId,
            IsOwner = false,
            Status = UserStatus.Active
        };
        user.PasswordHash = _tokens.HashPassword(user, request.Password);
        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(actorId, "staff.create", "User", user.Id.ToString(), newValue: new { user.Email, role.Key }, ct: ct);
        return ToDto(user);
    }

    public async Task<StaffUserDto> UpdateAsync(Guid id, UpdateStaffUserRequest request, Guid actorId, CancellationToken ct = default)
    {
        var user = await _db.Users.Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == id && u.ArchivedAt == null, ct)
            ?? throw new ValidationAppException("User was not found.");

        if (!string.IsNullOrWhiteSpace(request.DisplayName))
            user.DisplayName = request.DisplayName.Trim();

        if (request.Phone is not null)
            user.Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();

        if (!string.IsNullOrWhiteSpace(request.RoleKey))
        {
            var roleKey = request.RoleKey.Trim().ToLowerInvariant();
            if (roleKey == "owner" || user.IsOwner)
                throw new ValidationAppException("Owner role cannot be reassigned through staff settings.");
            var role = await _db.Roles.FirstOrDefaultAsync(r => r.Key == roleKey, ct)
                ?? throw new ValidationAppException("Unknown role.");
            user.RoleId = role.Id;
            user.Role = role;
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            if (!Enum.TryParse<UserStatus>(request.Status, ignoreCase: true, out var status))
                throw new ValidationAppException("Status must be Active or Suspended.");
            if (user.IsOwner && status != UserStatus.Active)
                throw new ValidationAppException("The owner account cannot be suspended.");
            user.Status = status;
        }

        user.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(actorId, "staff.update", "User", user.Id.ToString(), newValue: request, ct: ct);
        return ToDto(user);
    }

    private static StaffUserDto ToDto(AppUser user) =>
        new(user.Id, user.Email, user.DisplayName, user.Phone, user.Role.Key, user.Role.Name,
            user.Status.ToString(), user.IsOwner, user.LastLoginAt, user.CreatedAt);
}

public sealed class SearchService : ISearchService
{
    private readonly WorkshopDbContext _db;
    public SearchService(WorkshopDbContext db) => _db = db;

    public async Task<WorkshopOS.Contracts.Common.SearchResponse> SearchAsync(string query, CancellationToken ct = default)
    {
        var q = query?.Trim() ?? string.Empty;
        if (q.Length < 2)
        {
            return new WorkshopOS.Contracts.Common.SearchResponse(q,
            [
                new("Repairs", Array.Empty<WorkshopOS.Contracts.Common.SearchHitDto>()),
                new("Customers", Array.Empty<WorkshopOS.Contracts.Common.SearchHitDto>()),
                new("Devices", Array.Empty<WorkshopOS.Contracts.Common.SearchHitDto>())
            ]);
        }

        var term = q.ToLowerInvariant();
        var repairs = await _db.RepairTickets.AsNoTracking()
            .Where(r => r.ArchivedAt == null && (
                r.TicketNumber.ToLower().Contains(term) ||
                r.Customer.DisplayName.ToLower().Contains(term) ||
                r.ReportedIssue.ToLower().Contains(term)))
            .OrderByDescending(r => r.CreatedAt).Take(10)
            .Select(r => new WorkshopOS.Contracts.Common.SearchHitDto(r.Id.ToString(), r.TicketNumber, r.Customer.DisplayName + " · " + r.Status.Name, $"/repairs/{r.Id}"))
            .ToListAsync(ct);

        var customers = await _db.Customers.AsNoTracking()
            .Where(c => c.ArchivedAt == null && (
                c.DisplayName.ToLower().Contains(term) ||
                (c.Phone != null && c.Phone.ToLower().Contains(term)) ||
                (c.Email != null && c.Email.ToLower().Contains(term))))
            .OrderBy(c => c.DisplayName).Take(10)
            .Select(c => new WorkshopOS.Contracts.Common.SearchHitDto(c.Id.ToString(), c.DisplayName, c.Phone ?? c.Email, $"/customers/{c.Id}"))
            .ToListAsync(ct);

        var devices = await _db.Devices.AsNoTracking()
            .Where(d => d.ArchivedAt == null && (
                d.Brand.ToLower().Contains(term) ||
                d.Model.ToLower().Contains(term) ||
                (d.Serial != null && d.Serial.ToLower().Contains(term)) ||
                (d.Imei != null && d.Imei.ToLower().Contains(term))))
            .OrderBy(d => d.Brand).Take(10)
            .Select(d => new WorkshopOS.Contracts.Common.SearchHitDto(d.Id.ToString(), (d.Brand + " " + d.Model).Trim(), d.Customer.DisplayName, $"/customers/{d.CustomerId}"))
            .ToListAsync(ct);

        return new WorkshopOS.Contracts.Common.SearchResponse(q,
        [
            new("Repairs", repairs),
            new("Customers", customers),
            new("Devices", devices)
        ]);
    }
}
