using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using WorkshopOS.Application.Abstractions;
using WorkshopOS.Contracts.Auth;
using WorkshopOS.Contracts.Common;
using WorkshopOS.Infrastructure.Persistence;

namespace WorkshopOS.Api.Controllers;

[ApiController]
[Route("api/setup")]
public sealed class SetupController : ControllerBase
{
    private readonly ISetupService _setup;
    public SetupController(ISetupService setup) => _setup = setup;

    [HttpGet("status")]
    [AllowAnonymous]
    public Task<SetupStatusDto> Status(CancellationToken ct) => _setup.GetStatusAsync(ct);

    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> Complete([FromBody] SetupRequest request, CancellationToken ct)
    {
        await _setup.CompleteAsync(request, HttpContext.Connection.RemoteIpAddress?.ToString(), ct);
        return Ok(new { message = "Setup complete. Sign in with the owner account." });
    }
}

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthService _auth;
    public AuthController(IAuthService auth) => _auth = auth;

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public Task<AuthResponse> Login([FromBody] LoginRequest request, CancellationToken ct) =>
        _auth.LoginAsync(request, HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString(), ct);

    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public Task<AuthResponse> Refresh([FromBody] RefreshRequest request, CancellationToken ct) =>
        _auth.RefreshAsync(request.RefreshToken, HttpContext.Connection.RemoteIpAddress?.ToString(), ct);

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest? request, CancellationToken ct)
    {
        await _auth.LogoutAsync(CurrentUserId(), request?.RefreshToken, ct);
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    public Task<UserDto> Me(CancellationToken ct) => _auth.GetMeAsync(CurrentUserId(), ct);

    private Guid CurrentUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.Parse(raw!);
    }
}

[ApiController]
[Route("api/settings")]
public sealed class SettingsController : ControllerBase
{
    private readonly ISettingsService _settings;
    public SettingsController(ISettingsService settings) => _settings = settings;

    [HttpGet("business")]
    [Authorize(Policy = "perm:settings.view")]
    public Task<BusinessProfileDto> GetBusiness(CancellationToken ct) => _settings.GetBusinessAsync(ct);

    [HttpPut("business")]
    [Authorize(Policy = "perm:settings.manage")]
    public Task<BusinessProfileDto> PutBusiness([FromBody] BusinessProfileDto profile, CancellationToken ct)
    {
        var id = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);
        return _settings.UpdateBusinessAsync(profile, id, ct);
    }

    [HttpGet("modules")]
    [Authorize]
    public Task<IReadOnlyList<ModuleDto>> Modules(CancellationToken ct) => _settings.GetModulesAsync(ct);
}

[ApiController]
[Route("api")]
public sealed class MetaController : ControllerBase
{
    private readonly ISearchService _search;
    private readonly IRoleService _roles;
    private readonly WorkshopDbContext _db;

    public MetaController(ISearchService search, IRoleService roles, WorkshopDbContext db)
    {
        _search = search;
        _roles = roles;
        _db = db;
    }

    [HttpGet("health")]
    [AllowAnonymous]
    public async Task<HealthDto> Health(CancellationToken ct)
    {
        var dbOk = false;
        try { dbOk = await _db.Database.CanConnectAsync(ct); }
        catch { /* degraded */ }

        return new HealthDto(
            dbOk ? "Healthy" : "Degraded",
            ProductVersions.Api,
            dbOk,
            DateTimeOffset.UtcNow,
            "WorkshopOS",
            ProductVersions.ClientMinimum,
            SystemController.IsRestartAllowed());
    }

    [HttpGet("permissions")]
    [Authorize(Policy = "perm:roles.manage")]
    public Task<IReadOnlyList<PermissionDto>> Permissions(CancellationToken ct) =>
        _roles.ListPermissionsAsync(ct);

    [HttpGet("search")]
    [Authorize]
    public Task<SearchResponse> Search([FromQuery] string q, CancellationToken ct) =>
        _search.SearchAsync(q ?? string.Empty, ct);
}

[ApiController]
[Route("api/roles")]
public sealed class RolesController : ControllerBase
{
    private readonly IRoleService _roles;
    public RolesController(IRoleService roles) => _roles = roles;

    [HttpGet]
    [Authorize(Policy = "perm:staff.view")]
    public Task<IReadOnlyList<RoleDto>> List(CancellationToken ct) => _roles.ListRolesAsync(ct);

    [HttpPost]
    [Authorize(Policy = "perm:roles.manage")]
    public Task<RoleDto> Create([FromBody] CreateRoleRequest request, CancellationToken ct) =>
        _roles.CreateAsync(request, UserId(), ct);

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "perm:roles.manage")]
    public Task<RoleDto> Update(Guid id, [FromBody] UpdateRoleRequest request, CancellationToken ct) =>
        _roles.UpdateAsync(id, request, UserId(), ct);

    private Guid UserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);
}

[ApiController]
[Route("api/system")]
public sealed class SystemController : ControllerBase
{
    private readonly WorkshopDbContext _db;
    private readonly IBackupService _backups;
    private readonly IConfiguration _config;
    private readonly ILogger<SystemController> _log;

    public SystemController(WorkshopDbContext db, IBackupService backups, IConfiguration config, ILogger<SystemController> log)
    {
        _db = db;
        _backups = backups;
        _config = config;
        _log = log;
    }

    internal static bool IsRestartAllowed()
    {
        var env = Environment.GetEnvironmentVariable("ALLOW_PROCESS_RESTART");
        if (string.Equals(env, "true", StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private static IReadOnlyList<string> UpdateCommands() =>
    [
        "cd ~/workshopos   # or your install dir",
        "git pull origin main",
        "docker compose -f docker/docker-compose.yml pull",
        "docker compose -f docker/docker-compose.yml up -d --build"
    ];

    private static IReadOnlyList<string> RestartCommands() =>
    [
        "cd ~/workshopos && ./scripts/restart-workshopos.sh",
        "cd ~/workshopos && docker compose -f docker/docker-compose.yml restart api worker"
    ];

    [HttpGet("info")]
    [Authorize(Policy = "perm:settings.view")]
    public async Task<SystemInfoDto> Info(CancellationToken ct)
    {
        var dbOk = false;
        try { dbOk = await _db.Database.CanConnectAsync(ct); }
        catch { /* degraded */ }

        var detail = await _backups.HealthDetailAsync(ct);
        return new SystemInfoDto(
            "WorkshopOS",
            ProductVersions.Api,
            ProductVersions.ClientMinimum,
            dbOk,
            detail.WorkerHeartbeat,
            IsRestartAllowed(),
            dbOk ? "Healthy" : "Degraded",
            DateTimeOffset.UtcNow,
            UpdateCommands(),
            RestartCommands());
    }

    [HttpPost("restart")]
    [Authorize(Policy = "perm:settings.manage")]
    public async Task<ActionResult<RestartResultDto>> Restart(CancellationToken ct)
    {
        if (!IsRestartAllowed() && !_config.GetValue("AllowProcessRestart", false))
        {
            return Ok(new RestartResultDto(
                false,
                "HTTP restart is disabled. Set ALLOW_PROCESS_RESTART=true on the API container, or run the commands below on the server (WSL/Linux).",
                RestartCommands().Concat(UpdateCommands()).ToArray()));
        }

        // Prefer the documented script when present next to the compose file.
        var candidates = new[]
        {
            Environment.GetEnvironmentVariable("WORKSHOPOS_HOME"),
            "/app",
            Directory.GetCurrentDirectory(),
            Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", ".."))
        }.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct().ToArray();

        foreach (var root in candidates)
        {
            var script = Path.Combine(root!, "scripts", "restart-workshopos.sh");
            if (!System.IO.File.Exists(script)) continue;
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = script,
                    WorkingDirectory = root,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                };
                using var proc = System.Diagnostics.Process.Start(psi);
                if (proc is null) continue;
                _ = Task.Run(async () =>
                {
                    await proc.WaitForExitAsync();
                    _log.LogInformation("restart-workshopos.sh exited {Code}", proc.ExitCode);
                }, ct);
                return Ok(new RestartResultDto(true,
                    "Restart script started. The API may briefly disconnect — wait a few seconds then Check health again.",
                    RestartCommands()));
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Failed to run restart script at {Script}", script);
            }
        }

        // Fallback: touch a restart flag and exit so Docker restart policy / sidecar can recycle the process.
        try
        {
            var flagDir = Environment.GetEnvironmentVariable("RESTART_FLAG_DIR")
                ?? Path.Combine(Path.GetTempPath(), "workshopos");
            Directory.CreateDirectory(flagDir);
            var flag = Path.Combine(flagDir, "restart.flag");
            await System.IO.File.WriteAllTextAsync(flag, DateTimeOffset.UtcNow.ToString("O"), ct);
            _ = Task.Run(async () =>
            {
                await Task.Delay(750);
                Environment.Exit(0);
            });
            return Ok(new RestartResultDto(true,
                "Restart flag written; API process will exit so the container supervisor can restart it.",
                RestartCommands()));
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Restart failed");
            return Ok(new RestartResultDto(false,
                $"Could not restart from HTTP: {ex.Message}. Run the commands below on the server.",
                RestartCommands().Concat(UpdateCommands()).ToArray()));
        }
    }
}

[ApiController]
[Route("api/users")]
public sealed class UsersController : ControllerBase
{
    private readonly IStaffService _staff;
    public UsersController(IStaffService staff) => _staff = staff;

    [HttpGet]
    [Authorize(Policy = "perm:staff.view")]
    public Task<IReadOnlyList<StaffUserDto>> List(CancellationToken ct) => _staff.ListAsync(ct);

    [HttpPost]
    [Authorize(Policy = "perm:staff.manage")]
    public Task<StaffUserDto> Create([FromBody] CreateStaffUserRequest request, CancellationToken ct) =>
        _staff.CreateAsync(request, UserId(), ct);

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "perm:staff.manage")]
    public Task<StaffUserDto> Update(Guid id, [FromBody] UpdateStaffUserRequest request, CancellationToken ct) =>
        _staff.UpdateAsync(id, request, UserId(), ct);

    private Guid UserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);
}
