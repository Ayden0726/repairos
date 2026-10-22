using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WorkshopOS.Application.Abstractions;
using WorkshopOS.Contracts.Operations;

namespace WorkshopOS.Api.Controllers;

[ApiController]
[Route("api/me/preferences")]
[Authorize]
public sealed class UserPreferencesController : ControllerBase
{
    private readonly IUserPreferenceService _prefs;

    public UserPreferencesController(IUserPreferenceService prefs) => _prefs = prefs;

    [HttpGet("dashboard")]
    public Task<DashboardLayoutDto> GetDashboard(CancellationToken ct) =>
        _prefs.GetDashboardLayoutAsync(UserId(), ct);

    [HttpPut("dashboard")]
    public Task<DashboardLayoutDto> PutDashboard(
        [FromBody] UpdateDashboardLayoutRequest request, CancellationToken ct) =>
        _prefs.SaveDashboardLayoutAsync(UserId(), request, ct);

    private Guid UserId() =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);
}
