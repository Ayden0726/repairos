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
    public Task<IReadOnlyList<QuoteListItemDto>> List(
        [FromQuery] string? q, [FromQuery] string? status,
        [FromQuery] Guid? customerId, [FromQuery] Guid? repairTicketId, CancellationToken ct) =>
        _quotes.ListAsync(q, status, customerId, repairTicketId, ct);

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "perm:quotes.view")]
    public Task<QuoteDetailDto> Get(Guid id, CancellationToken ct) =>
        _quotes.GetAsync(id, IncludeInternal(), ct);

    [HttpPost]
    [Authorize(Policy = "perm:quotes.manage")]
    public Task<QuoteDetailDto> Create([FromBody] CreateQuoteRequest request, CancellationToken ct) =>
        _quotes.CreateAsync(request, UserId(), Permissions(), ct);

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "perm:quotes.manage")]
    public Task<QuoteDetailDto> Update(Guid id, [FromBody] UpdateQuoteRequest request, CancellationToken ct) =>
        _quotes.UpdateAsync(id, request, UserId(), Permissions(), ct);

    [HttpPost("{id:guid}/status")]
    [Authorize(Policy = "perm:quotes.manage")]
    public Task<QuoteDetailDto> Status(Guid id, [FromBody] QuoteStatusRequest request, CancellationToken ct) =>
        _quotes.SetStatusAsync(id, request.Status, UserId(), Permissions(), ct);

    [HttpPost("{id:guid}/send")]
    [Authorize(Policy = "perm:quotes.manage")]
    public Task<QuoteDetailDto> Send(Guid id, CancellationToken ct) =>
        _quotes.SetStatusAsync(id, "Sent", UserId(), Permissions(), ct);

    [HttpPost("{id:guid}/accept")]
    [Authorize(Policy = "perm:quotes.manage")]
    public Task<QuoteDetailDto> Accept(Guid id, CancellationToken ct) =>
        _quotes.SetStatusAsync(id, "Accepted", UserId(), Permissions(), ct);

    [HttpPost("{id:guid}/decline")]
    [Authorize(Policy = "perm:quotes.manage")]
    public Task<QuoteDetailDto> Decline(Guid id, CancellationToken ct) =>
        _quotes.SetStatusAsync(id, "Declined", UserId(), Permissions(), ct);

    [HttpPost("{id:guid}/revise")]
    [Authorize(Policy = "perm:quotes.manage")]
    public Task<QuoteDetailDto> Revise(Guid id, [FromBody] UpdateQuoteRequest request, CancellationToken ct) =>
        _quotes.ReviseAsync(id, request, UserId(), Permissions(), ct);

    [HttpPost("{id:guid}/convert-to-repair")]
    [Authorize(Policy = "perm:quotes.manage")]
    public Task<QuoteDetailDto> Convert(Guid id, [FromBody] ConvertQuoteToRepairRequest? request, CancellationToken ct) =>
        _quotes.ConvertToRepairAsync(id, request ?? new ConvertQuoteToRepairRequest(null), UserId(), ct);

    [HttpGet("{id:guid}/print")]
    [Authorize(Policy = "perm:quotes.view")]
    public async Task<IActionResult> Print(Guid id, CancellationToken ct)
    {
        var html = await _quotes.BuildCustomerPrintHtmlAsync(id, ct);
        return Content(html, "text/html; charset=utf-8");
    }

    private Guid UserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);

    private HashSet<string> Permissions()
    {
        var set = User.FindAll("permission").Select(c => c.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (User.HasClaim("is_owner", "true")) set.Add("*");
        return set;
    }

    private bool IncludeInternal() =>
        User.HasClaim("is_owner", "true") ||
        User.HasClaim("permission", "pricing.view_cost") ||
        User.HasClaim("permission", "pricing.view_profit") ||
        User.HasClaim("permission", "pricing.view") ||
        User.HasClaim("permission", "pricing.edit") ||
        User.HasClaim("permission", "pricing.edit_settings");
}

[ApiController]
[Route("api/pricing")]
public sealed class PricingController : ControllerBase
{
    private readonly IPricingSettingsService _pricing;
    public PricingController(IPricingSettingsService pricing) => _pricing = pricing;

    [HttpGet("settings")]
    [Authorize(Policy = "perm:pricing.view")]
    public Task<PricingSettingsDto> GetSettings(CancellationToken ct) => _pricing.GetAsync(ct);

    [HttpPut("settings")]
    [Authorize]
    public async Task<ActionResult<PricingSettingsDto>> PutSettings([FromBody] PricingSettingsDto? settings, CancellationToken ct)
    {
        if (settings is null)
            return BadRequest(new { title = "Pricing settings body is required." });
        if (!User.HasClaim("is_owner", "true") &&
            !User.HasClaim("permission", "pricing.edit_settings") &&
            !User.HasClaim("permission", "pricing.edit"))
            return StatusCode(StatusCodes.Status403Forbidden,
                new { title = "Forbidden — need pricing.edit_settings (or owner) to save pricing." });
        return await _pricing.UpdateAsync(settings, UserId(), ct);
    }

    [HttpPost("preview")]
    [Authorize(Policy = "perm:quotes.view")]
    public Task<PricingPreviewResponse> Preview([FromBody] PricingPreviewRequest request, CancellationToken ct) =>
        _pricing.PreviewAsync(request, ct);

    [HttpGet("tiers")]
    [Authorize(Policy = "perm:pricing.view")]
    public Task<IReadOnlyList<MarkupTierDto>> ListTiers(CancellationToken ct) => _pricing.ListTiersAsync(ct);

    [HttpPost("tiers")]
    [Authorize(Policy = "perm:pricing.edit_settings")]
    public Task<MarkupTierDto> UpsertTier([FromBody] UpsertMarkupTierRequest request, CancellationToken ct) =>
        _pricing.UpsertTierAsync(request, UserId(), ct);

    [HttpDelete("tiers/{id:guid}")]
    [Authorize(Policy = "perm:pricing.edit_settings")]
    public async Task<IActionResult> DeleteTier(Guid id, CancellationToken ct)
    {
        await _pricing.DeleteTierAsync(id, UserId(), ct);
        return NoContent();
    }

    [HttpGet("services")]
    [Authorize(Policy = "perm:pricing.view")]
    public Task<IReadOnlyList<ServicePricingDto>> ListServices(CancellationToken ct) => _pricing.ListServicesAsync(ct);

    [HttpPost("services")]
    [Authorize(Policy = "perm:pricing.edit_settings")]
    public Task<ServicePricingDto> UpsertService([FromBody] UpsertServicePricingRequest request, CancellationToken ct) =>
        _pricing.UpsertServiceAsync(request, UserId(), ct);

    [HttpDelete("services/{id:guid}")]
    [Authorize(Policy = "perm:pricing.edit_settings")]
    public async Task<IActionResult> DeleteService(Guid id, CancellationToken ct)
    {
        await _pricing.DeleteServiceAsync(id, UserId(), ct);
        return NoContent();
    }

    private Guid UserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);
}

[ApiController]
[Route("api/pos")]
public sealed class PosController : ControllerBase
{
    private readonly IPosService _pos;
    public PosController(IPosService pos) => _pos = pos;

    [HttpGet("providers")]
    [Authorize(Policy = "perm:pos.view")]
    public Task<IReadOnlyList<PosProviderInfoDto>> Providers(CancellationToken ct) => _pos.ListProvidersAsync(ct);

    [HttpGet("repairs-payable")]
    [Authorize(Policy = "perm:pos.view")]
    public Task<IReadOnlyList<PosPayableRepairDto>> PayableRepairs([FromQuery] string? q, CancellationToken ct) =>
        _pos.ListPayableRepairsAsync(q, ct);

    [HttpGet("sales")]
    [Authorize(Policy = "perm:pos.view")]
    public Task<IReadOnlyList<PosSaleListItemDto>> List(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken ct) =>
        _pos.ListSalesAsync(from, to, ct);

    [HttpGet("sales/{id:guid}")]
    [Authorize(Policy = "perm:pos.view")]
    public Task<PosSaleDetailDto> Get(Guid id, CancellationToken ct) => _pos.GetSaleAsync(id, ct);

    [HttpPost("sales")]
    [Authorize(Policy = "perm:pos.sell")]
    public Task<PosSaleDetailDto> Create([FromBody] CreatePosSaleRequest request, CancellationToken ct) =>
        _pos.CreateSaleAsync(request, UserId(), ct);

    [HttpPost("sales/{id:guid}/void")]
    [Authorize(Policy = "perm:pos.manage")]
    public Task<PosSaleDetailDto> Void(Guid id, CancellationToken ct) =>
        _pos.VoidSaleAsync(id, UserId(), ct);

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
    public Task<IReadOnlyList<InventoryListItemDto>> List(
        [FromQuery] string? componentType,
        [FromQuery] bool availableOnly = false,
        CancellationToken ct = default) =>
        _inventory.ListAsync(componentType, availableOnly, ct);

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

    [HttpPost("reservations/{reservationId:guid}/release")]
    [Authorize(Policy = "perm:inventory.manage")]
    public async Task<IActionResult> Release(Guid reservationId, CancellationToken ct)
    {
        await _inventory.ReleaseReservationAsync(reservationId, UserId(), ct);
        return NoContent();
    }

    [HttpGet("reservations")]
    [Authorize(Policy = "perm:inventory.view")]
    public Task<IReadOnlyList<InventoryReservationDto>> Reservations([FromQuery] Guid? itemId, CancellationToken ct) =>
        _inventory.ListReservationsAsync(itemId, ct);

    [HttpGet("barcode/{code}")]
    [Authorize(Policy = "perm:inventory.view")]
    public async Task<ActionResult<InventoryListItemDto>> ByBarcode(string code, CancellationToken ct)
    {
        var item = await _inventory.FindByBarcodeAsync(code, ct);
        return item is null ? NotFound() : item;
    }

    [HttpPost("{id:guid}/transfer-bin")]
    [Authorize(Policy = "perm:inventory.manage")]
    public Task<InventoryListItemDto> TransferBin(Guid id, [FromBody] TransferBinRequest request, CancellationToken ct) =>
        _inventory.TransferBinAsync(id, request, UserId(), ct);

    [HttpPost("{id:guid}/quarantine")]
    [Authorize(Policy = "perm:inventory.manage")]
    public Task<InventoryListItemDto> Quarantine(Guid id, [FromBody] QuarantineStockRequest request, CancellationToken ct) =>
        _inventory.QuarantineAsync(id, request, UserId(), ct);

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

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "perm:inventory.purchase_orders")]
    public Task<PurchaseOrderDetailDto> Get(Guid id, CancellationToken ct) => _purchasing.GetPurchaseOrderAsync(id, ct);

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
[Route("api/stocktakes")]
public sealed class StocktakesController : ControllerBase
{
    private readonly IStocktakeService _stocktakes;
    public StocktakesController(IStocktakeService stocktakes) => _stocktakes = stocktakes;

    [HttpGet]
    [Authorize(Policy = "perm:inventory.manage")]
    public Task<IReadOnlyList<StocktakeListItemDto>> List(CancellationToken ct) => _stocktakes.ListAsync(ct);

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "perm:inventory.manage")]
    public Task<StocktakeDetailDto> Get(Guid id, CancellationToken ct) => _stocktakes.GetAsync(id, ct);

    [HttpPost]
    [Authorize(Policy = "perm:inventory.manage")]
    public Task<StocktakeDetailDto> Create([FromBody] CreateStocktakeRequest? request, CancellationToken ct) =>
        _stocktakes.CreateAsync(request ?? new CreateStocktakeRequest(null), UserId(), ct);

    [HttpPost("{id:guid}/lines/{lineId:guid}")]
    [Authorize(Policy = "perm:inventory.manage")]
    public Task<StocktakeLineDto> SetCount(Guid id, Guid lineId, [FromBody] SetStocktakeCountRequest request, CancellationToken ct) =>
        _stocktakes.SetCountAsync(id, lineId, request, UserId(), ct);

    [HttpPost("{id:guid}/complete")]
    [Authorize(Policy = "perm:inventory.manage")]
    public Task<StocktakeDetailDto> Complete(Guid id, CancellationToken ct) =>
        _stocktakes.CompleteAsync(id, UserId(), ct);

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = "perm:inventory.manage")]
    public Task<StocktakeDetailDto> Cancel(Guid id, CancellationToken ct) =>
        _stocktakes.CancelAsync(id, UserId(), ct);

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

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "perm:bookings.view")]
    public Task<BookingDto> Get(Guid id, CancellationToken ct) =>
        _bookings.GetAsync(id, ct);

    [HttpPost]
    [Authorize(Policy = "perm:bookings.manage")]
    public Task<BookingDto> Create([FromBody] CreateBookingRequest request, CancellationToken ct) =>
        _bookings.CreateAsync(request, UserId(), ct);

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "perm:bookings.manage")]
    public Task<BookingDto> Update(Guid id, [FromBody] UpdateBookingRequest request, CancellationToken ct) =>
        _bookings.UpdateAsync(id, request, UserId(), ct);

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = "perm:bookings.manage")]
    public Task<BookingDto> Cancel(Guid id, CancellationToken ct) =>
        _bookings.CancelAsync(id, UserId(), ct);

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
[Authorize]
public sealed class BuildsController : ControllerBase
{
    /// <summary>Custom PC Builds module removed from the product. Tables retained for data safety.</summary>
    [HttpGet]
    [HttpGet("{id:guid}")]
    [HttpPost]
    [HttpPut("{id:guid}")]
    [HttpPost("{id:guid}/status")]
    [HttpDelete("{id:guid}")]
    public IActionResult Gone() =>
        StatusCode(StatusCodes.Status410Gone, new
        {
            title = "Custom PC Builds has been removed.",
            code = "module_removed",
            status = 410,
            detail = "Desktop repairs use Customer → Device → Repair Ticket. Component stock remains in Inventory."
        });
}

[ApiController]
[Route("api/used-tech")]
public sealed class UsedTechController : ControllerBase
{
    private readonly IUsedTechService _used;
    private readonly IRefurbDashboardService _dashboard;
    private readonly IRefurbBuyCalculatorService _buyCalc;

    public UsedTechController(
        IUsedTechService used, IRefurbDashboardService dashboard, IRefurbBuyCalculatorService buyCalc)
    {
        _used = used;
        _dashboard = dashboard;
        _buyCalc = buyCalc;
    }

    [HttpGet]
    [Authorize(Policy = "perm:used.view")]
    public Task<IReadOnlyList<UsedDeviceDto>> List([FromQuery] string? q, [FromQuery] string? status, CancellationToken ct) =>
        _used.ListAsync(q, status, ct);

    [HttpGet("dashboard")]
    [Authorize(Policy = "perm:used.view")]
    public Task<RefurbDashboardDto> Dashboard([FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to, CancellationToken ct) =>
        _dashboard.GetAsync(from, to, ct);

    [HttpPost("buy-calculator")]
    [Authorize(Policy = "perm:used.view")]
    public Task<RefurbBuyCalcResult> BuyCalculator([FromBody] RefurbBuyCalcRequest request, CancellationToken ct) =>
        _buyCalc.CalculateAsync(request, ct);

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "perm:used.view")]
    public Task<UsedDeviceDetailDto> Get(Guid id, CancellationToken ct) => _used.GetAsync(id, ct);

    [HttpPost]
    [Authorize(Policy = "perm:used.manage")]
    public Task<UsedDeviceDetailDto> Create([FromBody] CreateUsedDeviceRequest request, CancellationToken ct) =>
        _used.CreateAsync(request, UserId(), ct);

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "perm:used.manage")]
    public Task<UsedDeviceDetailDto> Update(Guid id, [FromBody] UpdateUsedDeviceRequest request, CancellationToken ct) =>
        _used.UpdateAsync(id, request, UserId(), ct);

    [HttpPost("{id:guid}/status")]
    [Authorize(Policy = "perm:used.manage")]
    public Task<UsedDeviceDetailDto> Status(Guid id, [FromBody] UpdateUsedStatusRequest request, CancellationToken ct) =>
        _used.UpdateStatusAsync(id, request, UserId(), ct);

    private Guid UserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);
}

[ApiController]
[Route("api/acquisitions")]
public sealed class AcquisitionsController : ControllerBase
{
    private readonly IAcquisitionService _acquisitions;
    public AcquisitionsController(IAcquisitionService acquisitions) => _acquisitions = acquisitions;

    [HttpGet]
    [Authorize(Policy = "perm:acquisitions.view")]
    public Task<IReadOnlyList<AcquisitionLeadListItemDto>> List([FromQuery] string? q, [FromQuery] string? status, CancellationToken ct) =>
        _acquisitions.ListAsync(q, status, ct);

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "perm:acquisitions.view")]
    public Task<AcquisitionLeadDetailDto> Get(Guid id, CancellationToken ct) => _acquisitions.GetAsync(id, ct);

    [HttpPost]
    [Authorize(Policy = "perm:acquisitions.manage")]
    public Task<AcquisitionLeadDetailDto> Create([FromBody] CreateAcquisitionLeadRequest request, CancellationToken ct) =>
        _acquisitions.CreateAsync(request, UserId(), ct);

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "perm:acquisitions.manage")]
    public Task<AcquisitionLeadDetailDto> Update(Guid id, [FromBody] UpdateAcquisitionLeadRequest request, CancellationToken ct) =>
        _acquisitions.UpdateAsync(id, request, UserId(), ct);

    [HttpPost("{id:guid}/status")]
    [Authorize(Policy = "perm:acquisitions.manage")]
    public Task<AcquisitionLeadDetailDto> Status(Guid id, [FromBody] UpdateAcquisitionStatusRequest request, CancellationToken ct) =>
        _acquisitions.UpdateStatusAsync(id, request, UserId(), ct);

    [HttpPost("{id:guid}/offers")]
    [Authorize(Policy = "perm:acquisitions.manage")]
    public Task<AcquisitionOfferDto> Offer(Guid id, [FromBody] CreateAcquisitionOfferRequest request, CancellationToken ct) =>
        _acquisitions.AddOfferAsync(id, request, UserId(), ct);

    [HttpPost("{id:guid}/convert")]
    [Authorize(Policy = "perm:acquisitions.manage")]
    public Task<UsedDeviceDetailDto> Convert(Guid id, [FromBody] ConvertAcquisitionRequest? request, CancellationToken ct) =>
        _acquisitions.ConvertToInventoryAsync(id, request ?? new ConvertAcquisitionRequest(null, null, null, null, null), UserId(), ct);

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

    [HttpPost("{id:guid}/restore-test")]
    [Authorize(Policy = "perm:backups.manage")]
    public Task<BackupRestoreTestDto> RestoreTest(Guid id, CancellationToken ct) =>
        _backups.RestoreTestAsync(id, UserId(), ct);

    private Guid UserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);
}

[ApiController]
[Route("api/audit")]
public sealed class AuditController : ControllerBase
{
    private readonly IAuditService _audit;
    public AuditController(IAuditService audit) => _audit = audit;

    [HttpGet]
    [Authorize(Policy = "perm:audit.view")]
    public Task<AuditSearchResponse> Search(
        [FromQuery] string? action,
        [FromQuery] Guid? actorUserId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default) =>
        _audit.SearchAsync(action, actorUserId, from, to, page, pageSize, ct);
}

[ApiController]
[Route("api/labels")]
public sealed class LabelsController : ControllerBase
{
    private readonly ILabelPrintService _labels;
    public LabelsController(ILabelPrintService labels) => _labels = labels;

    [HttpGet("repair/{id:guid}")]
    [Authorize(Policy = "perm:tickets.view")]
    public async Task<IActionResult> Repair(Guid id, CancellationToken ct)
    {
        var html = await _labels.RenderRepairLabelHtmlAsync(id, ct);
        return Content(html, "text/html; charset=utf-8");
    }

    [HttpGet("used/{id:guid}")]
    [Authorize(Policy = "perm:used.view")]
    public async Task<IActionResult> Used(Guid id, [FromQuery] bool includePrice = false, CancellationToken ct = default)
    {
        var html = await _labels.RenderUsedDeviceLabelHtmlAsync(id, includePrice, ct);
        return Content(html, "text/html; charset=utf-8");
    }

    [HttpGet("part/{id:guid}")]
    [Authorize(Policy = "perm:inventory.view")]
    public async Task<IActionResult> Part(Guid id, CancellationToken ct)
    {
        var html = await _labels.RenderPartLabelHtmlAsync(id, ct);
        return Content(html, "text/html; charset=utf-8");
    }

    [HttpPost("batch")]
    [Authorize(Policy = "perm:inventory.view")]
    public async Task<IActionResult> Batch(
        [FromBody] BatchLabelRequest request,
        [FromServices] IPrinterProfileService printers,
        CancellationToken ct)
    {
        var result = await printers.BatchRenderAsync(request, UserId(), ct);
        return Content(result.CombinedHtml, "text/html; charset=utf-8");
    }

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

[ApiController]
[Route("api/donors")]
[Authorize]
public sealed class DonorsController : ControllerBase
{
    private readonly IDonorService _donors;
    public DonorsController(IDonorService donors) => _donors = donors;

    [HttpGet]
    [Authorize(Policy = "perm:used.view")]
    public Task<IReadOnlyList<DonorDeviceDto>> List(CancellationToken ct) => _donors.ListAsync(ct);

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "perm:used.view")]
    public Task<DonorDeviceDetailDto> Get(Guid id, CancellationToken ct) => _donors.GetAsync(id, ct);

    [HttpPost]
    [Authorize(Policy = "perm:used.manage")]
    public Task<DonorDeviceDetailDto> Create([FromBody] CreateDonorDeviceRequest request, CancellationToken ct) =>
        _donors.CreateAsync(request, UserId(), ct);

    [HttpPost("{id:guid}/harvest")]
    [Authorize(Policy = "perm:used.manage")]
    public Task<DonorDeviceDetailDto> Harvest(Guid id, [FromBody] HarvestComponentRequest request, CancellationToken ct) =>
        _donors.HarvestAsync(id, request, UserId(), ct);

    private Guid UserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);
}

[ApiController]
[Route("api/loans")]
[Authorize]
public sealed class LoansController : ControllerBase
{
    private readonly ILoanService _loans;
    public LoansController(ILoanService loans) => _loans = loans;

    [HttpGet("devices")]
    [Authorize(Policy = "perm:tickets.view")]
    public Task<IReadOnlyList<LoanDeviceDto>> Devices(CancellationToken ct) => _loans.ListDevicesAsync(ct);

    [HttpPost("devices")]
    [Authorize(Policy = "perm:tickets.edit")]
    public Task<LoanDeviceDto> UpsertDevice([FromBody] UpsertLoanDeviceRequest request, CancellationToken ct) =>
        _loans.UpsertDeviceAsync(request, UserId(), ct);

    [HttpGet("agreements")]
    [Authorize(Policy = "perm:tickets.view")]
    public Task<IReadOnlyList<LoanAgreementDto>> Agreements([FromQuery] string? status, CancellationToken ct) =>
        _loans.ListAgreementsAsync(status, ct);

    [HttpPost("checkout")]
    [Authorize(Policy = "perm:tickets.edit")]
    public Task<LoanAgreementDto> Checkout([FromBody] CheckoutLoanRequest request, CancellationToken ct) =>
        _loans.CheckoutAsync(request, UserId(), ct);

    [HttpPost("agreements/{id:guid}/return")]
    [Authorize(Policy = "perm:tickets.edit")]
    public Task<LoanAgreementDto> Return(Guid id, [FromBody] ReturnLoanRequest request, CancellationToken ct) =>
        _loans.ReturnAsync(id, request, UserId(), ct);

    private Guid UserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);
}

[ApiController]
[Route("api/warranty-claims")]
[Authorize]
public sealed class WarrantyClaimsController : ControllerBase
{
    private readonly IWarrantyClaimService _claims;
    public WarrantyClaimsController(IWarrantyClaimService claims) => _claims = claims;

    [HttpGet("warranties")]
    [Authorize(Policy = "perm:tickets.view")]
    public Task<IReadOnlyList<StaffWarrantyDto>> Warranties(CancellationToken ct) => _claims.ListWarrantiesAsync(ct);

    [HttpGet]
    [Authorize(Policy = "perm:tickets.view")]
    public Task<IReadOnlyList<WarrantyClaimDto>> List([FromQuery] string? status, CancellationToken ct) =>
        _claims.ListClaimsAsync(status, ct);

    [HttpPost]
    [Authorize(Policy = "perm:tickets.edit")]
    public Task<WarrantyClaimDto> Create([FromBody] CreateWarrantyClaimRequest request, CancellationToken ct) =>
        _claims.CreateClaimAsync(request, UserId(), ct);

    [HttpPost("{id:guid}/update")]
    [Authorize(Policy = "perm:tickets.edit")]
    public Task<WarrantyClaimDto> Update(Guid id, [FromBody] UpdateWarrantyClaimRequest request, CancellationToken ct) =>
        _claims.UpdateClaimAsync(id, request, UserId(), ct);

    private Guid UserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);
}

[ApiController]
[Route("api/refunds")]
[Authorize]
public sealed class RefundsController : ControllerBase
{
    private readonly IRefundService _refunds;
    public RefundsController(IRefundService refunds) => _refunds = refunds;

    [HttpGet]
    [Authorize(Policy = "perm:payments.view")]
    public Task<IReadOnlyList<RefundRequestDto>> List([FromQuery] string? status, CancellationToken ct) =>
        _refunds.ListAsync(status, ct);

    [HttpPost]
    [Authorize(Policy = "perm:payments.refund")]
    public Task<RefundRequestDto> Create([FromBody] CreateRefundRequestDto request, CancellationToken ct) =>
        _refunds.CreateAsync(request, UserId(), ct);

    [HttpPost("{id:guid}/decide")]
    [Authorize(Policy = "perm:payments.refund")]
    public Task<RefundRequestDto> Decide(Guid id, [FromBody] DecideRefundRequest request, CancellationToken ct) =>
        _refunds.DecideAsync(id, request, UserId(), ct);

    private Guid UserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);
}

[ApiController]
[Route("api/repairs/{ticketId:guid}/media")]
[Authorize]
public sealed class RepairMediaController : ControllerBase
{
    private readonly IRepairMediaService _media;
    public RepairMediaController(IRepairMediaService media) => _media = media;

    [HttpGet("attachments")]
    [Authorize(Policy = "perm:tickets.view")]
    public Task<IReadOnlyList<RepairAttachmentDto>> Attachments(Guid ticketId, CancellationToken ct) =>
        _media.ListAttachmentsAsync(ticketId, ct);

    [HttpPost("attachments")]
    [Authorize(Policy = "perm:tickets.edit")]
    public Task<RepairAttachmentDto> Upload(Guid ticketId, [FromBody] UploadRepairAttachmentRequest request, CancellationToken ct) =>
        _media.UploadAttachmentAsync(ticketId, request, UserId(), ct);

    [HttpGet("signatures")]
    [Authorize(Policy = "perm:tickets.view")]
    public Task<IReadOnlyList<RepairSignatureDto>> Signatures(Guid ticketId, CancellationToken ct) =>
        _media.ListSignaturesAsync(ticketId, ct);

    [HttpPost("signatures")]
    [Authorize(Policy = "perm:tickets.edit")]
    public Task<RepairSignatureDto> Capture(Guid ticketId, [FromBody] CaptureRepairSignatureRequest request, CancellationToken ct) =>
        _media.CaptureSignatureAsync(ticketId, request, UserId(), ct);

    [HttpGet("condition-report")]
    [Authorize(Policy = "perm:tickets.view")]
    public async Task<IActionResult> ConditionReport(Guid ticketId, CancellationToken ct)
    {
        var html = await _media.BuildConditionReportHtmlAsync(ticketId, ct);
        return Content(html, "text/html; charset=utf-8");
    }

    private Guid UserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);
}

[ApiController]
[Route("api/printers")]
[Authorize]
public sealed class PrintersController : ControllerBase
{
    private readonly IPrinterProfileService _printers;
    public PrintersController(IPrinterProfileService printers) => _printers = printers;

    [HttpGet]
    [Authorize(Policy = "perm:settings.view")]
    public Task<IReadOnlyList<PrinterProfileDto>> List(CancellationToken ct) => _printers.ListAsync(ct);

    [HttpPost]
    [Authorize(Policy = "perm:settings.manage")]
    public Task<PrinterProfileDto> Upsert([FromBody] UpsertPrinterProfileRequest request, CancellationToken ct) =>
        _printers.UpsertAsync(request, UserId(), ct);

    private Guid UserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);
}

[ApiController]
[Route("api/integration-health")]
[Authorize]
public sealed class IntegrationHealthController : ControllerBase
{
    private readonly IIntegrationHealthService _health;
    public IntegrationHealthController(IIntegrationHealthService health) => _health = health;

    [HttpGet]
    [Authorize(Policy = "perm:integrations.manage")]
    public Task<IReadOnlyList<IntegrationHealthEventDto>> List(CancellationToken ct) => _health.ListAsync(ct);
}

[ApiController]
[Route("api/fitment")]
[Authorize]
public sealed class FitmentController : ControllerBase
{
    private readonly IPartFitmentService _fitment;
    public FitmentController(IPartFitmentService fitment) => _fitment = fitment;

    [HttpGet]
    [Authorize(Policy = "perm:inventory.view")]
    public Task<IReadOnlyList<PartFitmentDto>> List(
        [FromQuery] string? brand, [FromQuery] string? model, [FromQuery] string? q, CancellationToken ct) =>
        _fitment.ListAsync(brand, model, q, ct);

    [HttpPost]
    [Authorize(Policy = "perm:inventory.manage")]
    public Task<PartFitmentDto> Upsert([FromBody] UpsertPartFitmentRequest request, CancellationToken ct) =>
        _fitment.UpsertAsync(request, UserId(), ct);

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "perm:inventory.manage")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _fitment.DeleteAsync(id, UserId(), ct);
        return NoContent();
    }

    private Guid UserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);
}

[ApiController]
[Route("api/sync")]
[Authorize]
public sealed class SyncController : ControllerBase
{
    private readonly ISyncService _sync;
    public SyncController(ISyncService sync) => _sync = sync;

    [HttpPost("push")]
    [Authorize(Policy = "perm:tickets.edit")]
    public Task<SyncPushResponse> Push([FromBody] SyncPushRequest request, CancellationToken ct) =>
        _sync.PushAsync(request, UserId(), ct);

    [HttpGet("outbox")]
    [Authorize(Policy = "perm:settings.view")]
    public Task<IReadOnlyList<SyncOutboxListItemDto>> Outbox([FromQuery] string? status, CancellationToken ct) =>
        _sync.ListAsync(status, ct);

    private Guid UserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);
}

[ApiController]
[Route("api/locations")]
[Authorize]
public sealed class WorkshopLocationsController : ControllerBase
{
    private readonly IWorkshopLocationService _locations;
    public WorkshopLocationsController(IWorkshopLocationService locations) => _locations = locations;

    [HttpGet]
    [Authorize(Policy = "perm:settings.view")]
    public Task<IReadOnlyList<WorkshopLocationDto>> List(CancellationToken ct) => _locations.ListAsync(ct);

    [HttpPost]
    [Authorize(Policy = "perm:settings.manage")]
    public Task<WorkshopLocationDto> Upsert([FromBody] UpsertWorkshopLocationRequest request, CancellationToken ct) =>
        _locations.UpsertAsync(request, UserId(), ct);

    private Guid UserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);
}

[ApiController]
[Route("api/inventory-bins")]
[Authorize]
public sealed class InventoryBinsController : ControllerBase
{
    private readonly IInventoryBinService _bins;
    public InventoryBinsController(IInventoryBinService bins) => _bins = bins;

    [HttpGet]
    [Authorize(Policy = "perm:inventory.view")]
    public Task<IReadOnlyList<InventoryBinDto>> List([FromQuery] Guid? locationId, CancellationToken ct) =>
        _bins.ListAsync(locationId, ct);

    [HttpPost]
    [Authorize(Policy = "perm:inventory.manage")]
    public Task<InventoryBinDto> Upsert([FromBody] UpsertInventoryBinRequest request, CancellationToken ct) =>
        _bins.UpsertAsync(request, UserId(), ct);

    private Guid UserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);
}
