using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WorkshopOS.Application.Abstractions;
using WorkshopOS.Application.Common;
using WorkshopOS.Contracts.Auth;
using WorkshopOS.Contracts.Operations;
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
        await DbSeed.SetSettingAsync(_db, SettingKeys.Gst, new { registered = profile.GstRegistered, rate = profile.GstRate, inclusive = true }, owner.Id, ct);
        await DbSeed.SetSettingAsync(_db, SettingKeys.FinanceDefaults, new { labourRate = profile.DefaultLabourRate, diagnosticFee = 89m }, owner.Id, ct);
        var pricingDefaults = PricingCalculator.DefaultSettings() with
        {
            Labour = PricingCalculator.DefaultSettings().Labour with
            {
                DefaultLabourFee = request.DefaultLabourRate > 0 ? Math.Min(request.DefaultLabourRate, 200m) : 50m
            },
            Tax = new TaxPricingDto(profile.GstRegistered, profile.GstRate <= 0 ? 0.10m : profile.GstRate, true)
        };
        // Seed labour default fee 50 as pricing default; business DefaultLabourRate remains hourly catalogue rate.
        pricingDefaults = pricingDefaults with
        {
            Labour = pricingDefaults.Labour with { DefaultLabourFee = 50m }
        };
        await DbSeed.SetSettingAsync(_db, SettingKeys.PricingSettings, pricingDefaults, owner.Id, ct);
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

    public async Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken ct = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId && u.ArchivedAt == null, ct)
            ?? throw new UnauthorizedAppException();

        if (!_tokens.VerifyPassword(user, request.CurrentPassword ?? string.Empty))
            throw new ValidationAppException("Current password is incorrect.");

        var problem = PasswordRules.Validate(request.NewPassword);
        if (problem is not null) throw new ValidationAppException(problem);

        user.PasswordHash = _tokens.HashPassword(user, request.NewPassword);
        user.MustResetPassword = false;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        await RevokeRefreshTokensAsync(user.Id, ct);
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(userId, "auth.password_change", "User", user.Id.ToString(), ct: ct);
    }

    private async Task RevokeRefreshTokensAsync(Guid userId, CancellationToken ct)
    {
        var active = await _db.RefreshTokens.Where(t => t.UserId == userId && t.RevokedAt == null).ToListAsync(ct);
        foreach (var t in active) t.RevokedAt = DateTimeOffset.UtcNow;
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
        var gstRate = profile.GstRate <= 0 ? 0.10m : profile.GstRate;
        profile = profile with { GstRate = gstRate };
        await DbSeed.SetSettingAsync(_db, SettingKeys.BusinessProfile, profile, actorId, ct);
        await DbSeed.SetSettingAsync(_db, SettingKeys.Gst,
            new { registered = profile.GstRegistered, rate = profile.GstRate, inclusive = profile.GstInclusive }, actorId, ct);

        // Keep pricing.settings Tax mirror in sync so quotes/preview always see the same GST.
        var pricing = await DbSeed.GetSettingAsync<PricingSettingsDto?>(_db, SettingKeys.PricingSettings, null, ct)
            ?? PricingCalculator.DefaultSettings();
        pricing = pricing with
        {
            Tax = new TaxPricingDto(profile.GstRegistered, profile.GstRate, profile.GstInclusive)
        };
        await DbSeed.SetSettingAsync(_db, SettingKeys.PricingSettings, pricing, actorId, ct);

        await _audit.WriteAsync(actorId, "settings.business.update", "Setting", SettingKeys.BusinessProfile, newValue: profile, ct: ct);
        return profile;
    }

    public async Task<IReadOnlyList<WorkshopOS.Contracts.Common.ModuleDto>> GetModulesAsync(CancellationToken ct = default)
    {
        var visibility = await DbSeed.GetSettingAsync(_db, SettingKeys.ModuleVisibility, new Dictionary<string, bool>(), ct);
        // Custom PC Builds removed. POS defaults OFF until an admin enables it in Settings.
        WorkshopOS.Contracts.Common.ModuleDto[] modules =
        [
            new("dashboard", "Main", "Dashboard", 1, IsVisible(visibility, "dashboard"), true),
            new("repairs", "Main", "Repairs", 1, IsVisible(visibility, "repairs"), true),
            new("calendar", "Main", "Bookings", 1, IsVisible(visibility, "calendar"), true),
            new("customers", "Main", "Customers", 1, IsVisible(visibility, "customers"), true),
            new("quotes", "Main", "Quotes", 1, IsVisible(visibility, "quotes"), true),
            new("invoices", "Main", "Invoices", 1, IsVisible(visibility, "invoices"), true),
            new("pos", "Main", "POS", 6, IsVisible(visibility, "pos", defaultVisible: false), true),
            new("inventory", "Main", "Inventory", 1, IsVisible(visibility, "inventory"), true),
            new("purchasing", "Main", "Purchasing", 1, IsVisible(visibility, "purchasing"), true),
            new("used", "Main", "Refurbished", 1, IsVisible(visibility, "used"), true),
            new("reports", "Main", "Reports", 1, IsVisible(visibility, "reports"), true),
            new("ai", "More", "AI Analyst", 1, IsVisible(visibility, "ai"), true),
            new("knowledge", "More", "Knowledge", 1, IsVisible(visibility, "knowledge"), true),
            new("notifications", "System", "Notifications", 1, IsVisible(visibility, "notifications"), true),
            new("backups", "System", "Backups", 1, IsVisible(visibility, "backups"), true),
            new("users", "System", "Staff", 1, IsVisible(visibility, "users"), true),
            new("settings", "System", "Settings", 1, IsVisible(visibility, "settings"), true)
        ];
        return modules;
    }

    public async Task<IReadOnlyList<WorkshopOS.Contracts.Common.ModuleDto>> UpdateModulesAsync(
        UpdateModuleVisibilityRequest request, Guid actorId, CancellationToken ct = default)
    {
        var current = await DbSeed.GetSettingAsync(_db, SettingKeys.ModuleVisibility, new Dictionary<string, bool>(), ct);
        foreach (var (key, visible) in request.Visibility)
        {
            if (string.IsNullOrWhiteSpace(key)) continue;
            current[key.Trim().ToLowerInvariant()] = visible;
        }

        await DbSeed.SetSettingAsync(_db, SettingKeys.ModuleVisibility, current, actorId, ct);

        // Keep POS settings.Enabled in sync when pos visibility changes.
        if (current.TryGetValue("pos", out var posVisible))
        {
            var existingPos = await DbSeed.GetSettingAsync<PosSettingsStored?>(_db, SettingKeys.PosSettings, null, ct)
                ?? new PosSettingsStored(false, "None", null, null, null, null, null, DefaultDiscountPresets());
            if (existingPos.Enabled != posVisible)
            {
                await DbSeed.SetSettingAsync(_db, SettingKeys.PosSettings,
                    existingPos with { Enabled = posVisible }, actorId, ct);
            }
        }

        await _audit.WriteAsync(actorId, "settings.modules.update", "Setting", SettingKeys.ModuleVisibility, newValue: current, ct: ct);
        return await GetModulesAsync(ct);
    }

    public Task<PosSettingsDto> GetPosSettingsAsync(CancellationToken ct = default) =>
        GetPosSettingsInternalAsync(ct);

    public async Task<PosSettingsDto> UpdatePosSettingsAsync(UpdatePosSettingsRequest request, Guid actorId, CancellationToken ct = default)
    {
        var provider = NormalizeProvider(request.Provider);
        var existing = await DbSeed.GetSettingAsync<PosSettingsStored?>(_db, SettingKeys.PosSettings, null, ct)
            ?? new PosSettingsStored(false, "None", null, null, null, null, null, DefaultDiscountPresets());

        var apiKey = existing.ApiKey;
        if (request.ClearApiKey)
            apiKey = null;
        else if (!string.IsNullOrWhiteSpace(request.ApiKey))
            apiKey = request.ApiKey.Trim();

        var presets = NormalizeDiscountPresets(request.DiscountPresets ?? existing.DiscountPresets);

        var stored = new PosSettingsStored(
            request.Enabled,
            provider,
            TrimOrNull(request.MerchantId),
            TrimOrNull(request.TerminalId),
            TrimOrNull(request.ProviderLocationId),
            apiKey,
            TrimOrNull(request.ConnectionNotes),
            presets);

        await DbSeed.SetSettingAsync(_db, SettingKeys.PosSettings, stored, actorId, ct);

        var visibility = await DbSeed.GetSettingAsync(_db, SettingKeys.ModuleVisibility, new Dictionary<string, bool>(), ct);
        visibility["pos"] = request.Enabled;
        await DbSeed.SetSettingAsync(_db, SettingKeys.ModuleVisibility, visibility, actorId, ct);

        await _audit.WriteAsync(actorId, "settings.pos.update", "Setting", SettingKeys.PosSettings,
            newValue: new { stored.Enabled, stored.Provider, stored.MerchantId, stored.TerminalId, ApiKeyConfigured = !string.IsNullOrEmpty(stored.ApiKey) },
            ct: ct);

        return ToPosDto(stored);
    }

    private async Task<PosSettingsDto> GetPosSettingsInternalAsync(CancellationToken ct)
    {
        var stored = await DbSeed.GetSettingAsync<PosSettingsStored?>(_db, SettingKeys.PosSettings, null, ct);
        if (stored is null)
        {
            var visibility = await DbSeed.GetSettingAsync(_db, SettingKeys.ModuleVisibility, new Dictionary<string, bool>(), ct);
            var enabled = visibility.TryGetValue("pos", out var v) && v;
            return new PosSettingsDto(enabled, "None", null, null, null, false, null, DefaultDiscountPresets());
        }
        return ToPosDto(stored);
    }

    private static PosSettingsDto ToPosDto(PosSettingsStored s) =>
        new(s.Enabled, s.Provider, s.MerchantId, s.TerminalId, s.ProviderLocationId,
            !string.IsNullOrWhiteSpace(s.ApiKey), s.ConnectionNotes,
            NormalizeDiscountPresets(s.DiscountPresets));

    internal static IReadOnlyList<PosDiscountPresetDto> DefaultDiscountPresets() =>
    [
        new("staff", "Staff", 10m, true),
        new("family_friends", "Family & Friends", 15m, true),
        new("loyalty", "Loyalty", 5m, true),
        new("other", "Other (custom %)", 0m, true)
    ];

    private static IReadOnlyList<PosDiscountPresetDto> NormalizeDiscountPresets(IReadOnlyList<PosDiscountPresetDto>? presets)
    {
        if (presets is null || presets.Count == 0)
            return DefaultDiscountPresets();

        var cleaned = new List<PosDiscountPresetDto>();
        foreach (var p in presets)
        {
            if (string.IsNullOrWhiteSpace(p.Key) || string.IsNullOrWhiteSpace(p.Name)) continue;
            var pct = Math.Clamp(p.Percent, 0m, 100m);
            cleaned.Add(new PosDiscountPresetDto(
                p.Key.Trim().ToLowerInvariant().Replace(' ', '_'),
                p.Name.Trim(),
                Math.Round(pct, 2, MidpointRounding.AwayFromZero),
                p.IsActive));
        }
        return cleaned.Count == 0 ? DefaultDiscountPresets() : cleaned;
    }
    private static string NormalizeProvider(string? provider)
    {
        var p = (provider ?? "None").Trim();
        return p.ToLowerInvariant() switch
        {
            "none" or "" => "None",
            "manual" or "manualeftpos" or "manual_eftpos" => "ManualEftpos",
            "tyro" => "Tyro",
            "square" => "Square",
            "stripe" or "stripeterminal" or "stripe_terminal" => "StripeTerminal",
            "linkly" or "pc-eftpos" or "pceftpos" => "Linkly",
            _ => throw new ValidationAppException("Unknown payment provider. Use None, ManualEftpos, Tyro, Square, StripeTerminal, or Linkly.")
        };
    }

    private static string? TrimOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>Visibility map: missing key → defaultVisible (POS defaults off).</summary>
    private static bool IsVisible(Dictionary<string, bool> visibility, string key, bool defaultVisible = true) =>
        visibility.TryGetValue(key, out var visible) ? visible : defaultVisible;

    private sealed record PosSettingsStored(
        bool Enabled,
        string Provider,
        string? MerchantId,
        string? TerminalId,
        string? ProviderLocationId,
        string? ApiKey,
        string? ConnectionNotes,
        IReadOnlyList<PosDiscountPresetDto>? DiscountPresets = null);
}

public sealed class RoleService : IRoleService
{
    private readonly WorkshopDbContext _db;
    private readonly IAuditService _audit;
    public RoleService(WorkshopDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<IReadOnlyList<WorkshopOS.Contracts.Common.RoleDto>> ListRolesAsync(CancellationToken ct = default)
    {
        var roles = await _db.Roles.Include(r => r.Permissions).OrderBy(r => r.Name).ToListAsync(ct);
        return roles.Select(ToDto).ToList();
    }

    public Task<IReadOnlyList<WorkshopOS.Contracts.Common.PermissionDto>> ListPermissionsAsync(CancellationToken ct = default)
    {
        var list = Domain.Security.PermissionKeys.Catalogue
            .Select(p => new WorkshopOS.Contracts.Common.PermissionDto(p.Key, p.Group, p.Label))
            .ToList();
        return Task.FromResult<IReadOnlyList<WorkshopOS.Contracts.Common.PermissionDto>>(list);
    }

    public async Task<WorkshopOS.Contracts.Common.RoleDto> CreateAsync(
        WorkshopOS.Contracts.Common.CreateRoleRequest request, Guid actorId, CancellationToken ct = default)
    {
        var name = (request.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new ValidationAppException("Role name is required.");

        var key = NormalizeRoleKey(string.IsNullOrWhiteSpace(request.Key) ? name : request.Key);
        if (string.IsNullOrWhiteSpace(key) || key is "owner")
            throw new ValidationAppException("Choose a role key other than Owner.");
        if (await _db.Roles.AnyAsync(r => r.Key == key, ct))
            throw new ConflictAppException("A role with that key already exists.");

        var perms = SanitizePermissions(request.Permissions);
        var role = new AppRole
        {
            Key = key,
            Name = name,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            IsSystem = false
        };
        _db.Roles.Add(role);
        await _db.SaveChangesAsync(ct);
        foreach (var perm in perms)
            _db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionKey = perm });
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(actorId, "role.create", "Role", role.Id.ToString(),
            newValue: new { role.Key, role.Name, Permissions = perms }, ct: ct);

        role = await _db.Roles.Include(r => r.Permissions).SingleAsync(r => r.Id == role.Id, ct);
        return ToDto(role);
    }

    public async Task<WorkshopOS.Contracts.Common.RoleDto> UpdateAsync(
        Guid id, WorkshopOS.Contracts.Common.UpdateRoleRequest request, Guid actorId, CancellationToken ct = default)
    {
        var role = await _db.Roles.Include(r => r.Permissions).FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new ValidationAppException("Role was not found.");
        if (string.Equals(role.Key, "owner", StringComparison.OrdinalIgnoreCase))
            throw new ValidationAppException("The Owner role cannot be edited.");

        if (!string.IsNullOrWhiteSpace(request.Name))
            role.Name = request.Name.Trim();
        if (request.Description is not null)
            role.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();

        if (request.Permissions is not null)
        {
            var perms = SanitizePermissions(request.Permissions);
            if (string.Equals(role.Key, "administrator", StringComparison.OrdinalIgnoreCase) &&
                perms.Length < Domain.Security.PermissionKeys.AllKeys.Count)
            {
                // Allow trimming admin, but never leave it empty.
                if (perms.Length == 0)
                    throw new ValidationAppException("Administrator must keep at least one permission.");
            }
            _db.RolePermissions.RemoveRange(role.Permissions);
            foreach (var perm in perms)
                _db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionKey = perm });
        }

        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(actorId, "role.update", "Role", role.Id.ToString(), newValue: request, ct: ct);
        role = await _db.Roles.Include(r => r.Permissions).SingleAsync(r => r.Id == role.Id, ct);
        return ToDto(role);
    }

    private static WorkshopOS.Contracts.Common.RoleDto ToDto(AppRole r) =>
        new(r.Id, r.Key, r.Name, r.Description,
            r.Permissions.Select(p => p.PermissionKey).OrderBy(x => x).ToArray(),
            r.IsSystem);

    private static string NormalizeRoleKey(string raw)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var ch in raw.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch)) sb.Append(ch);
            else if (ch is ' ' or '-' or '_') sb.Append('_');
        }
        var key = sb.ToString().Trim('_');
        while (key.Contains("__", StringComparison.Ordinal))
            key = key.Replace("__", "_", StringComparison.Ordinal);
        return key;
    }

    private static string[] SanitizePermissions(string[]? permissions) =>
        (permissions ?? [])
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p.Trim())
            .Where(p => Domain.Security.PermissionKeys.AllKeys.Contains(p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(p => p)
            .ToArray();
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

    public async Task ResetPasswordAsync(Guid id, ResetStaffPasswordRequest request, Guid actorId, CancellationToken ct = default)
    {
        var user = await _db.Users.Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == id && u.ArchivedAt == null, ct)
            ?? throw new ValidationAppException("User was not found.");

        var problem = PasswordRules.Validate(request.NewPassword);
        if (problem is not null) throw new ValidationAppException(problem);

        user.PasswordHash = _tokens.HashPassword(user, request.NewPassword);
        user.MustResetPassword = false;
        user.UpdatedAt = DateTimeOffset.UtcNow;

        var active = await _db.RefreshTokens.Where(t => t.UserId == user.Id && t.RevokedAt == null).ToListAsync(ct);
        foreach (var t in active) t.RevokedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(actorId, "staff.password_reset", "User", user.Id.ToString(),
            newValue: new { user.Email }, ct: ct);
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
        var commands = MatchCommands(q);

        if (q.Length < 2)
        {
            return new WorkshopOS.Contracts.Common.SearchResponse(q,
            [
                new("Commands", commands),
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
            .OrderByDescending(r => r.CreatedAt).Take(8)
            .Select(r => new WorkshopOS.Contracts.Common.SearchHitDto(r.Id.ToString(), r.TicketNumber, r.Customer.DisplayName + " · " + r.Status.Name, $"/repairs/{r.Id}"))
            .ToListAsync(ct);

        var customers = await _db.Customers.AsNoTracking()
            .Where(c => c.ArchivedAt == null && (
                c.DisplayName.ToLower().Contains(term) ||
                (c.Phone != null && c.Phone.ToLower().Contains(term)) ||
                (c.Email != null && c.Email.ToLower().Contains(term))))
            .OrderBy(c => c.DisplayName).Take(8)
            .Select(c => new WorkshopOS.Contracts.Common.SearchHitDto(c.Id.ToString(), c.DisplayName, c.Phone ?? c.Email, $"/customers/{c.Id}"))
            .ToListAsync(ct);

        var devices = await _db.Devices.AsNoTracking()
            .Where(d => d.ArchivedAt == null && (
                d.Brand.ToLower().Contains(term) ||
                d.Model.ToLower().Contains(term) ||
                (d.Serial != null && d.Serial.ToLower().Contains(term)) ||
                (d.Imei != null && d.Imei.ToLower().Contains(term))))
            .OrderBy(d => d.Brand).Take(8)
            .Select(d => new WorkshopOS.Contracts.Common.SearchHitDto(d.Id.ToString(), (d.Brand + " " + d.Model).Trim(),
                (d.Imei ?? d.Serial ?? d.Customer.DisplayName), $"/customers/{d.CustomerId}"))
            .ToListAsync(ct);

        var invoices = await _db.Invoices.AsNoTracking()
            .Where(i => i.ArchivedAt == null && (
                i.Number.ToLower().Contains(term) ||
                i.Customer.DisplayName.ToLower().Contains(term)))
            .OrderByDescending(i => i.CreatedAt).Take(6)
            .Select(i => new WorkshopOS.Contracts.Common.SearchHitDto(i.Id.ToString(), i.Number,
                i.Customer.DisplayName + " · " + i.Status, "/invoices"))
            .ToListAsync(ct);

        var quotes = await _db.Quotes.AsNoTracking()
            .Where(x => x.ArchivedAt == null && (
                x.Number.ToLower().Contains(term) ||
                x.Customer.DisplayName.ToLower().Contains(term)))
            .OrderByDescending(x => x.CreatedAt).Take(6)
            .Select(x => new WorkshopOS.Contracts.Common.SearchHitDto(x.Id.ToString(), x.Number,
                x.Customer.DisplayName + " · " + x.Status, $"/quotes/{x.Id}"))
            .ToListAsync(ct);

        var products = await _db.InventoryItems.AsNoTracking()
            .Where(i => i.ArchivedAt == null && (
                i.Sku.ToLower().Contains(term) ||
                i.Name.ToLower().Contains(term) ||
                (i.Barcode != null && i.Barcode.ToLower().Contains(term))))
            .OrderBy(i => i.Name).Take(8)
            .Select(i => new WorkshopOS.Contracts.Common.SearchHitDto(i.Id.ToString(), i.Name,
                i.Sku + " · OH " + (i.QuantityOnHand - i.QuantityReserved), "/inventory"))
            .ToListAsync(ct);

        var bookings = await _db.Bookings.AsNoTracking()
            .Where(b => b.Status != "Cancelled" && (
                (b.Customer != null && b.Customer.DisplayName.ToLower().Contains(term)) ||
                (b.Notes != null && b.Notes.ToLower().Contains(term)) ||
                b.Type.ToLower().Contains(term)))
            .OrderByDescending(b => b.StartsAt).Take(6)
            .Select(b => new WorkshopOS.Contracts.Common.SearchHitDto(b.Id.ToString(),
                b.Type + " · " + b.StartsAt.ToString("g"),
                b.Customer != null ? b.Customer.DisplayName : b.Notes, "/calendar"))
            .ToListAsync(ct);

        var used = await _db.UsedDevices.AsNoTracking()
            .Where(u => u.ArchivedAt == null && (
                u.Summary.ToLower().Contains(term) ||
                (u.Serial != null && u.Serial.ToLower().Contains(term)) ||
                (u.Imei != null && u.Imei.ToLower().Contains(term)) ||
                (u.Brand != null && u.Brand.ToLower().Contains(term))))
            .OrderByDescending(u => u.CreatedAt).Take(6)
            .Select(u => new WorkshopOS.Contracts.Common.SearchHitDto(u.Id.ToString(), u.Summary,
                u.Status + (u.Imei != null ? " · " + u.Imei : ""), "/used"))
            .ToListAsync(ct);

        var suppliers = await _db.Suppliers.AsNoTracking()
            .Where(s => s.ArchivedAt == null && (
                s.Name.ToLower().Contains(term) ||
                (s.Email != null && s.Email.ToLower().Contains(term))))
            .OrderBy(s => s.Name).Take(6)
            .Select(s => new WorkshopOS.Contracts.Common.SearchHitDto(s.Id.ToString(), s.Name, s.Email, "/purchasing"))
            .ToListAsync(ct);

        var staff = await _db.Users.AsNoTracking()
            .Where(u => u.ArchivedAt == null && (
                u.DisplayName.ToLower().Contains(term) ||
                u.Email.ToLower().Contains(term)))
            .OrderBy(u => u.DisplayName).Take(6)
            .Select(u => new WorkshopOS.Contracts.Common.SearchHitDto(u.Id.ToString(), u.DisplayName, u.Email, "/settings/users"))
            .ToListAsync(ct);

        return new WorkshopOS.Contracts.Common.SearchResponse(q,
        [
            new("Commands", commands),
            new("Repairs", repairs),
            new("Customers", customers),
            new("Devices", devices),
            new("Invoices", invoices),
            new("Quotes", quotes),
            new("Products", products),
            new("Bookings", bookings),
            new("Refurbished", used),
            new("Suppliers", suppliers),
            new("Staff", staff)
        ]);
    }

    private static IReadOnlyList<WorkshopOS.Contracts.Common.SearchHitDto> MatchCommands(string q)
    {
        (string Id, string Title, string Route, string[] Aliases)[] all =
        [
            ("cmd:new-repair", "New Repair", "/repairs/new", ["new repair", "repair", "ticket", "intake"]),
            ("cmd:new-customer", "New Customer", "/customers/new", ["new customer", "customer", "add customer"]),
            ("cmd:new-booking", "New Booking", "/calendar", ["new booking", "booking", "appointment", "calendar"]),
            ("cmd:create-quote", "Create Quote", "/quotes", ["create quote", "quote", "new quote"]),
            ("cmd:create-invoice", "Create Invoice", "/invoices", ["create invoice", "invoice"]),
            ("cmd:pos", "Open POS", "/pos", ["pos", "point of sale", "register", "till", "checkout"]),
            ("cmd:special-order", "Special Order (Purchasing)", "/purchasing", ["special order", "purchase", "po"]),
            ("cmd:trade-in", "Trade-In / Refurbished", "/used", ["trade-in", "trade in", "buyback", "refurbished"]),
            ("cmd:stock", "Stock Adjustment", "/inventory", ["stock", "inventory", "adjust", "sku"])
        ];
        if (string.IsNullOrWhiteSpace(q))
            return all.Take(6).Select(c => new WorkshopOS.Contracts.Common.SearchHitDto(c.Id, c.Title, "Command", c.Route)).ToList();
        var term = q.Trim().ToLowerInvariant();
        return all
            .Where(c => c.Title.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                        c.Aliases.Any(a => a.Contains(term) || term.Contains(a)))
            .Select(c => new WorkshopOS.Contracts.Common.SearchHitDto(c.Id, c.Title, "Command", c.Route))
            .ToList();
    }
}
