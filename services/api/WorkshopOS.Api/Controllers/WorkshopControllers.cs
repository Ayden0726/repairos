using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WorkshopOS.Application.Abstractions;
using WorkshopOS.Contracts.Workshop;

namespace WorkshopOS.Api.Controllers;

[ApiController]
[Route("api/customers")]
public sealed class CustomersController : ControllerBase
{
    private readonly ICustomerService _customers;
    public CustomersController(ICustomerService customers) => _customers = customers;

    [HttpGet]
    [Authorize(Policy = "perm:customers.view")]
    public Task<PagedResult<CustomerListItemDto>> List([FromQuery] string? q, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default) =>
        _customers.ListAsync(q, page, pageSize, ct);

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "perm:customers.view")]
    public Task<CustomerDetailDto> Get(Guid id, CancellationToken ct) => _customers.GetAsync(id, ct);

    [HttpPost]
    [Authorize(Policy = "perm:customers.manage")]
    public Task<CustomerDetailDto> Create([FromBody] UpsertCustomerRequest request, CancellationToken ct) =>
        _customers.UpsertAsync(request with { Id = null }, UserId(), ct);

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "perm:customers.manage")]
    public Task<CustomerDetailDto> Update(Guid id, [FromBody] UpsertCustomerRequest request, CancellationToken ct) =>
        _customers.UpsertAsync(request with { Id = id }, UserId(), ct);

    private Guid UserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);
}

[ApiController]
[Route("api/devices")]
public sealed class DevicesController : ControllerBase
{
    private readonly IDeviceService _devices;
    public DevicesController(IDeviceService devices) => _devices = devices;

    [HttpGet]
    [Authorize(Policy = "perm:devices.view")]
    public Task<IReadOnlyList<DeviceListItemDto>> ForCustomer([FromQuery] Guid customerId, CancellationToken ct) =>
        _devices.ListForCustomerAsync(customerId, ct);

    [HttpPost]
    [Authorize(Policy = "perm:devices.manage")]
    public Task<DeviceListItemDto> Create([FromBody] UpsertDeviceRequest request, CancellationToken ct) =>
        _devices.UpsertAsync(request with { Id = null }, UserId(), ct);

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "perm:devices.manage")]
    public Task<DeviceListItemDto> Update(Guid id, [FromBody] UpsertDeviceRequest request, CancellationToken ct) =>
        _devices.UpsertAsync(request with { Id = id }, UserId(), ct);

    private Guid UserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);
}

[ApiController]
[Route("api/repairs")]
public sealed class RepairsController : ControllerBase
{
    private readonly IRepairService _repairs;
    public RepairsController(IRepairService repairs) => _repairs = repairs;

    [HttpGet("lookups")]
    [Authorize(Policy = "perm:tickets.view")]
    public Task<RepairLookupsDto> Lookups(CancellationToken ct) => _repairs.GetLookupsAsync(ct);

    [HttpGet]
    [Authorize(Policy = "perm:tickets.view")]
    public Task<PagedResult<RepairListItemDto>> List(
        [FromQuery] string? q,
        [FromQuery] string? status,
        [FromQuery] string? priority,
        [FromQuery] Guid? assignedToId,
        [FromQuery] bool? overdue,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default) =>
        _repairs.ListAsync(q, status, priority, assignedToId, overdue, page, pageSize, ct);

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "perm:tickets.view")]
    public Task<RepairDetailDto> Get(Guid id, CancellationToken ct) =>
        _repairs.GetAsync(id, User.HasClaim("is_owner", "true") || User.HasClaim("permission", "tickets.credentials.view"), ct);

    [HttpGet("{id:guid}/print")]
    [Authorize(Policy = "perm:tickets.view")]
    public async Task<IActionResult> Print(Guid id, CancellationToken ct)
    {
        var html = await _repairs.BuildPrintHtmlAsync(id, ct);
        return Content(html, "text/html; charset=utf-8");
    }

    [HttpPost]
    [Authorize(Policy = "perm:tickets.create")]
    public Task<RepairDetailDto> Create([FromBody] CreateRepairRequest request, CancellationToken ct) =>
        _repairs.CreateAsync(request, UserId(), ct);

    [HttpPost("{id:guid}/status")]
    [Authorize(Policy = "perm:tickets.status")]
    public Task<RepairDetailDto> Status(Guid id, [FromBody] ChangeStatusRequest request, CancellationToken ct) =>
        _repairs.ChangeStatusAsync(id, request.StatusId, UserId(), ct);

    [HttpPost("{id:guid}/priority")]
    [Authorize(Policy = "perm:tickets.edit")]
    public Task<RepairDetailDto> Priority(Guid id, [FromBody] ChangePriorityRequest request, CancellationToken ct) =>
        _repairs.ChangePriorityAsync(id, request.PriorityId, UserId(), ct);

    [HttpPost("{id:guid}/assign")]
    [Authorize(Policy = "perm:tickets.assign")]
    public Task<RepairDetailDto> Assign(Guid id, [FromBody] AssignRepairRequest request, CancellationToken ct) =>
        _repairs.AssignAsync(id, request.AssignedToId, UserId(), ct);

    [HttpPost("{id:guid}/notes")]
    [Authorize(Policy = "perm:tickets.edit")]
    public Task<RepairNoteDto> Note(Guid id, [FromBody] AddNoteRequest request, CancellationToken ct) =>
        _repairs.AddNoteAsync(id, request, UserId(),
            User.HasClaim("is_owner", "true") || User.HasClaim("permission", "tickets.internal_notes"), ct);

    [HttpPost("{id:guid}/diagnosis")]
    [Authorize(Policy = "perm:tickets.edit")]
    public Task<RepairDetailDto> Diagnosis(Guid id, [FromBody] UpdateDiagnosisRequest request, CancellationToken ct) =>
        _repairs.UpdateDiagnosisAsync(id, request, UserId(), ct);

    private Guid UserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);
}
