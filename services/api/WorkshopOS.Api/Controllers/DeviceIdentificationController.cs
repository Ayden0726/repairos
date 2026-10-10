using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WorkshopOS.Application.Abstractions;
using WorkshopOS.Contracts.Operations;

namespace WorkshopOS.Api.Controllers;

[ApiController]
[Route("api/device-identification")]
public sealed class DeviceIdentificationController : ControllerBase
{
    private readonly IDeviceIdentificationService _svc;
    public DeviceIdentificationController(IDeviceIdentificationService svc) => _svc = svc;

    [HttpGet("settings")]
    [Authorize(Policy = "perm:settings.view")]
    public Task<DeviceIdentificationSettingsDto> GetSettings(CancellationToken ct) =>
        _svc.GetSettingsAsync(ct);

    [HttpPut("settings")]
    [Authorize(Policy = "perm:settings.manage")]
    public Task<DeviceIdentificationSettingsDto> PutSettings(
        [FromBody] UpdateDeviceIdentificationSettingsRequest request, CancellationToken ct) =>
        _svc.UpdateSettingsAsync(request, UserId(), ct);

    [HttpPost("test")]
    [Authorize(Policy = "perm:settings.manage")]
    public Task<DeviceIdentificationTestResultDto> Test(CancellationToken ct) =>
        _svc.TestConnectionAsync(ct);

    [HttpPost("validate")]
    [Authorize(Policy = "perm:tickets.create")]
    public Task<ImeiValidateResultDto> Validate([FromBody] ImeiValidateRequest request, CancellationToken ct) =>
        _svc.ValidateAsync(request, ct);

    [HttpPost("lookup")]
    [Authorize(Policy = "perm:tickets.create")]
    public Task<ImeiLookupResultDto> Lookup([FromBody] ImeiLookupRequest request, CancellationToken ct) =>
        _svc.LookupAsync(request, UserId(), ct);

    private Guid UserId() =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);
}
