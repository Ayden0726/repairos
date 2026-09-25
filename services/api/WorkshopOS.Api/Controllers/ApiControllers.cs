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
    private readonly IRoleService _roles;
    private readonly ISearchService _search;
    private readonly WorkshopDbContext _db;

    public MetaController(IRoleService roles, ISearchService search, WorkshopDbContext db)
    {
        _roles = roles;
        _search = search;
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
            typeof(MetaController).Assembly.GetName().Version?.ToString() ?? "1.0.0",
            dbOk,
            DateTimeOffset.UtcNow);
    }

    [HttpGet("roles")]
    [Authorize(Policy = "perm:staff.view")]
    public Task<IReadOnlyList<RoleDto>> Roles(CancellationToken ct) => _roles.ListRolesAsync(ct);

    [HttpGet("permissions")]
    [Authorize(Policy = "perm:roles.manage")]
    public Task<IReadOnlyList<PermissionDto>> Permissions(CancellationToken ct) => _roles.ListPermissionsAsync(ct);

    [HttpGet("search")]
    [Authorize]
    public Task<SearchResponse> Search([FromQuery] string q, CancellationToken ct) =>
        _search.SearchAsync(q ?? string.Empty, ct);
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
