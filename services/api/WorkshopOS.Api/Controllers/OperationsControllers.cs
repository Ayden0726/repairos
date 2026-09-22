using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WorkshopOS.Application.Abstractions;
using WorkshopOS.Contracts.Operations;

namespace WorkshopOS.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
public sealed class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboard;
    public DashboardController(IDashboardService dashboard) => _dashboard = dashboard;

    [HttpGet]
    [Authorize(Policy = "perm:tickets.view")]
    public Task<DashboardDto> Get(CancellationToken ct) => _dashboard.GetAsync(ct);
}

[ApiController]
[Route("api/quotes")]
public sealed class QuotesController : ControllerBase
{
    private readonly IQuoteService _quotes;
    public QuotesController(IQuoteService quotes) => _quotes = quotes;

    [HttpGet]
    [Authorize(Policy = "perm:quotes.view")]
    public Task<IReadOnlyList<QuoteListItemDto>> List(CancellationToken ct) => _quotes.ListAsync(ct);

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "perm:quotes.view")]
    public Task<QuoteDetailDto> Get(Guid id, CancellationToken ct) => _quotes.GetAsync(id, ct);

    [HttpPost]
    [Authorize(Policy = "perm:quotes.manage")]
    public Task<QuoteDetailDto> Create([FromBody] CreateQuoteRequest request, CancellationToken ct) =>
        _quotes.CreateAsync(request, UserId(), ct);

    [HttpPost("{id:guid}/status")]
    [Authorize(Policy = "perm:quotes.manage")]
    public Task<QuoteDetailDto> Status(Guid id, [FromBody] QuoteStatusRequest request, CancellationToken ct) =>
        _quotes.SetStatusAsync(id, request.Status, UserId(), ct);

    private Guid UserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);
}

[ApiController]
[Route("api/invoices")]
public sealed class InvoicesController : ControllerBase
{
    private readonly IInvoiceService _invoices;
    public InvoicesController(IInvoiceService invoices) => _invoices = invoices;

    [HttpGet]
    [Authorize(Policy = "perm:invoices.view")]
    public Task<IReadOnlyList<InvoiceListItemDto>> List(CancellationToken ct) => _invoices.ListAsync(ct);

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "perm:invoices.view")]
    public Task<InvoiceDetailDto> Get(Guid id, CancellationToken ct) => _invoices.GetAsync(id, ct);

    [HttpPost("from-repair")]
    [Authorize(Policy = "perm:invoices.manage")]
    public Task<InvoiceDetailDto> FromRepair([FromBody] CreateInvoiceFromRepairRequest request, CancellationToken ct) =>
        _invoices.CreateFromRepairAsync(request.RepairTicketId, UserId(), ct);

    [HttpPost("{id:guid}/payments")]
    [Authorize(Policy = "perm:payments.record")]
    public Task<InvoiceDetailDto> Pay(Guid id, [FromBody] RecordPaymentRequest request, CancellationToken ct) =>
        _invoices.RecordPaymentAsync(id, request, UserId(), ct);

    private Guid UserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);
}

[ApiController]
[Route("api/inventory")]
public sealed class InventoryController : ControllerBase
{
    private readonly IInventoryService _inventory;
    public InventoryController(IInventoryService inventory) => _inventory = inventory;

    [HttpGet]
    [Authorize(Policy = "perm:inventory.view")]
    public Task<IReadOnlyList<InventoryListItemDto>> List(CancellationToken ct) => _inventory.ListAsync(ct);

    [HttpPost]
    [Authorize(Policy = "perm:inventory.manage")]
    public Task<InventoryListItemDto> Upsert([FromBody] UpsertInventoryRequest request, CancellationToken ct) =>
        _inventory.UpsertAsync(request, UserId(), ct);

    [HttpPost("{id:guid}/adjust")]
    [Authorize(Policy = "perm:inventory.manage")]
    public Task<InventoryListItemDto> Adjust(Guid id, [FromBody] AdjustStockRequest request, CancellationToken ct) =>
        _inventory.AdjustAsync(id, request, UserId(), ct);

    [HttpPost("reserve")]
    [Authorize(Policy = "perm:inventory.manage")]
    public async Task<IActionResult> Reserve([FromBody] ReserveStockRequest request, CancellationToken ct)
    {
        await _inventory.ReserveAsync(request, UserId(), ct);
        return NoContent();
    }

    [HttpPost("reservations/{reservationId:guid}/consume")]
    [Authorize(Policy = "perm:inventory.manage")]
    public async Task<IActionResult> Consume(Guid reservationId, CancellationToken ct)
    {
        await _inventory.ConsumeReservationAsync(reservationId, UserId(), ct);
        return NoContent();
    }

    private Guid UserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);
}

[ApiController]
[Route("api/suppliers")]
public sealed class SuppliersController : ControllerBase
{
    private readonly IPurchasingService _purchasing;
    public SuppliersController(IPurchasingService purchasing) => _purchasing = purchasing;

    [HttpGet]
    [Authorize(Policy = "perm:inventory.view")]
    public Task<IReadOnlyList<SupplierDto>> List(CancellationToken ct) => _purchasing.ListSuppliersAsync(ct);

    [HttpPost]
    [Authorize(Policy = "perm:inventory.purchase_orders")]
    public Task<SupplierDto> Upsert([FromBody] UpsertSupplierRequest request, CancellationToken ct) =>
        _purchasing.UpsertSupplierAsync(request, UserId(), ct);

    private Guid UserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);
}

[ApiController]
[Route("api/purchase-orders")]
public sealed class PurchaseOrdersController : ControllerBase
{
    private readonly IPurchasingService _purchasing;
    public PurchaseOrdersController(IPurchasingService purchasing) => _purchasing = purchasing;

    [HttpGet]
    [Authorize(Policy = "perm:inventory.purchase_orders")]
    public Task<IReadOnlyList<PurchaseOrderListItemDto>> List(CancellationToken ct) => _purchasing.ListPurchaseOrdersAsync(ct);

    [HttpPost]
    [Authorize(Policy = "perm:inventory.purchase_orders")]
    public Task<PurchaseOrderListItemDto> Create([FromBody] CreatePurchaseOrderRequest request, CancellationToken ct) =>
        _purchasing.CreatePurchaseOrderAsync(request, UserId(), ct);

    [HttpPost("{id:guid}/receive")]
    [Authorize(Policy = "perm:inventory.purchase_orders")]
    public Task<PurchaseOrderListItemDto> Receive(Guid id, [FromBody] ReceivePoLineRequest request, CancellationToken ct) =>
        _purchasing.ReceiveLineAsync(id, request, UserId(), ct);

    private Guid UserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);
}

[ApiController]
[Route("api/notifications")]
public sealed class NotificationsController : ControllerBase
{
    private readonly INotificationService _notifications;
    public NotificationsController(INotificationService notifications) => _notifications = notifications;

    [HttpGet]
    [Authorize]
    public Task<IReadOnlyList<NotificationDto>> List(CancellationToken ct) =>
        _notifications.ListForUserAsync(UserId(), ct);

    [HttpPost("{id:guid}/read")]
    [Authorize]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct)
    {
        await _notifications.MarkReadAsync(id, UserId(), ct);
        return NoContent();
    }

    private Guid UserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);
}

[ApiController]
[Route("api/bookings")]
public sealed class BookingsController : ControllerBase
{
    private readonly IBookingService _bookings;
    public BookingsController(IBookingService bookings) => _bookings = bookings;

    [HttpGet]
    [Authorize(Policy = "perm:bookings.view")]
    public Task<IReadOnlyList<BookingDto>> List([FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to, CancellationToken ct) =>
        _bookings.ListAsync(from, to, ct);

    [HttpPost]
    [Authorize(Policy = "perm:bookings.manage")]
    public Task<BookingDto> Create([FromBody] CreateBookingRequest request, CancellationToken ct) =>
        _bookings.CreateAsync(request, UserId(), ct);

    private Guid UserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);
}

[ApiController]
[Route("api/knowledge")]
public sealed class KnowledgeController : ControllerBase
{
    private readonly IKnowledgeService _knowledge;
    public KnowledgeController(IKnowledgeService knowledge) => _knowledge = knowledge;

    [HttpGet]
    [Authorize(Policy = "perm:knowledge.view")]
    public Task<IReadOnlyList<KnowledgeDto>> List([FromQuery] string? q, CancellationToken ct) =>
        _knowledge.ListAsync(q, ct);

    [HttpPost]
    [Authorize(Policy = "perm:knowledge.manage")]
    public Task<KnowledgeDto> Upsert([FromBody] UpsertKnowledgeRequest request, CancellationToken ct) =>
        _knowledge.UpsertAsync(request, UserId(), ct);

    private Guid UserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);
}

[ApiController]
[Route("api/builds")]
public sealed class BuildsController : ControllerBase
{
    private readonly IPcBuildService _builds;
    public BuildsController(IPcBuildService builds) => _builds = builds;

    [HttpGet]
    [Authorize(Policy = "perm:builds.view")]
    public Task<IReadOnlyList<PcBuildListItemDto>> List(CancellationToken ct) => _builds.ListAsync(ct);

    [HttpPost]
    [Authorize(Policy = "perm:builds.manage")]
    public Task<PcBuildListItemDto> Create([FromBody] CreatePcBuildRequest request, CancellationToken ct) =>
        _builds.CreateAsync(request, UserId(), ct);

    private Guid UserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);
}

[ApiController]
[Route("api/used-tech")]
public sealed class UsedTechController : ControllerBase
{
    private readonly IUsedTechService _used;
    public UsedTechController(IUsedTechService used) => _used = used;

    [HttpGet]
    [Authorize(Policy = "perm:used.view")]
    public Task<IReadOnlyList<UsedDeviceDto>> List(CancellationToken ct) => _used.ListAsync(ct);

    [HttpPost]
    [Authorize(Policy = "perm:used.manage")]
    public Task<UsedDeviceDto> Create([FromBody] CreateUsedDeviceRequest request, CancellationToken ct) =>
        _used.CreateAsync(request, UserId(), ct);

    [HttpPost("{id:guid}/status")]
    [Authorize(Policy = "perm:used.manage")]
    public Task<UsedDeviceDto> Status(Guid id, [FromBody] UpdateUsedStatusRequest request, CancellationToken ct) =>
        _used.UpdateStatusAsync(id, request, UserId(), ct);

    private Guid UserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);
}

[ApiController]
[Route("api/repairs/{ticketId:guid}/qa")]
public sealed class RepairQaController : ControllerBase
{
    private readonly IQaService _qa;
    public RepairQaController(IQaService qa) => _qa = qa;

    [HttpGet]
    [Authorize(Policy = "perm:tickets.view")]
    public Task<IReadOnlyList<QaItemDto>> List(Guid ticketId, CancellationToken ct) =>
        _qa.ListForTicketAsync(ticketId, ct);

    [HttpPost("{itemId:guid}")]
    [Authorize(Policy = "perm:tickets.edit")]
    public Task<QaItemDto> Set(Guid ticketId, Guid itemId, [FromBody] SetQaResultRequest request, CancellationToken ct) =>
        _qa.SetResultAsync(ticketId, itemId, request.Result, UserId(), ct);

    private Guid UserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);
}

[ApiController]
[Route("api/reports")]
public sealed class ReportsController : ControllerBase
{
    private readonly IReportService _reports;
    public ReportsController(IReportService reports) => _reports = reports;

    [HttpGet("summary")]
    [Authorize(Policy = "perm:reports.view")]
    public Task<ReportSummaryDto> Summary([FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to, CancellationToken ct) =>
        _reports.SummaryAsync(from, to, ct);
}

[ApiController]
[Route("api/ai")]
public sealed class AiController : ControllerBase
{
    private readonly IAiService _ai;
    public AiController(IAiService ai) => _ai = ai;

    [HttpPost("assist")]
    [Authorize(Policy = "perm:ai.use")]
    public Task<AiAssistResponse> Assist([FromBody] AiAssistRequest request, CancellationToken ct) =>
        _ai.AssistAsync(request, UserId(), ct);

    private Guid UserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);
}

[ApiController]
[Route("api/backups")]
public sealed class BackupsController : ControllerBase
{
    private readonly IBackupService _backups;
    public BackupsController(IBackupService backups) => _backups = backups;

    [HttpGet]
    [Authorize(Policy = "perm:backups.manage")]
    public Task<IReadOnlyList<BackupDto>> List(CancellationToken ct) => _backups.ListAsync(ct);

    [HttpPost]
    [Authorize(Policy = "perm:backups.manage")]
    public Task<BackupDto> Create(CancellationToken ct) => _backups.CreateAsync(UserId(), ct);

    private Guid UserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);
}

[ApiController]
[Route("api/health")]
public sealed class HealthDetailController : ControllerBase
{
    private readonly IBackupService _backups;
    public HealthDetailController(IBackupService backups) => _backups = backups;

    [HttpGet("detail")]
    [Authorize(Policy = "perm:settings.view")]
    public Task<SystemHealthDetailDto> Detail(CancellationToken ct) => _backups.HealthDetailAsync(ct);
}
