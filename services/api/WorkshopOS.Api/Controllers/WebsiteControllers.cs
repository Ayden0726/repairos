using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using WorkshopOS.Application.Abstractions;
using WorkshopOS.Contracts.Website;

namespace WorkshopOS.Api.Controllers;

/// <summary>
/// Public WordPress / website endpoints. WorkshopOS remains the source of truth.
/// Authenticate with header: X-WorkshopOS-Key: wos_...
/// </summary>
[ApiController]
[Route("api/public")]
[EnableRateLimiting("public")]
public sealed class PublicWebsiteController : ControllerBase
{
    private readonly IWebsitePublicService _public;
    private readonly IWebsiteIntegrationService _integrations;

    public PublicWebsiteController(IWebsitePublicService pub, IWebsiteIntegrationService integrations)
    {
        _public = pub;
        _integrations = integrations;
    }

    private async Task EnsureApiKeyAsync(CancellationToken ct)
    {
        if (!Request.Headers.TryGetValue("X-WorkshopOS-Key", out var key) ||
            !await _integrations.ValidateApiKeyAsync(key.ToString(), ct))
            throw new WorkshopOS.Application.Common.AppException("unauthorized", "Valid X-WorkshopOS-Key required.", 401);
    }

    [HttpPost("repair-quote")]
    public async Task<PublicRepairQuoteResponse> RepairQuote([FromBody] PublicRepairQuoteRequest request, CancellationToken ct)
    {
        await EnsureApiKeyAsync(ct);
        return await _public.SubmitRepairQuoteAsync(request, HttpContext.Connection.RemoteIpAddress?.ToString(), ct);
    }

    [HttpPost("repair-booking")]
    public async Task<PublicRepairBookingResponse> RepairBooking([FromBody] PublicRepairBookingRequest request, CancellationToken ct)
    {
        await EnsureApiKeyAsync(ct);
        return await _public.SubmitRepairBookingAsync(request, HttpContext.Connection.RemoteIpAddress?.ToString(), ct);
    }

    [HttpPost("sell-device")]
    public async Task<PublicSellDeviceResponse> SellDevice([FromBody] PublicSellDeviceRequest request, CancellationToken ct)
    {
        await EnsureApiKeyAsync(ct);
        return await _public.SubmitSellDeviceAsync(request, HttpContext.Connection.RemoteIpAddress?.ToString(), ct);
    }

    /// <summary>Custom PC Builds module removed. Use ordinary repair intake for desktops.</summary>
    [HttpPost("custom-pc")]
    public IActionResult CustomPcGone() =>
        StatusCode(StatusCodes.Status410Gone, new
        {
            title = "Custom PC Builds has been removed.",
            code = "module_removed",
            status = 410,
            detail = "Desktop computers use the standard repair ticket workflow (Customer → Device → Repair). Component sales use Inventory / POS."
        });

    [HttpGet("repair-status/{trackingCode}")]
    public async Task<PublicRepairStatusDto> RepairStatus(string trackingCode, [FromQuery] string pin, CancellationToken ct)
    {
        await EnsureApiKeyAsync(ct);
        return await _public.GetRepairStatusAsync(trackingCode, pin, ct);
    }

    [HttpGet("repair-status/link/{accessToken}")]
    public async Task<PublicRepairStatusDto> RepairStatusByToken(string accessToken, CancellationToken ct)
    {
        await EnsureApiKeyAsync(ct);
        return await _public.GetRepairStatusByTokenAsync(accessToken, ct);
    }

    [HttpPost("repair/{quoteId:guid}/approve")]
    public async Task<PublicQuoteDecisionResponse> Approve(Guid quoteId, [FromBody] PublicQuoteDecisionRequest request, CancellationToken ct)
    {
        await EnsureApiKeyAsync(ct);
        return await _public.ApproveQuoteAsync(quoteId, request,
            HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString(), ct);
    }

    [HttpPost("repair/{quoteId:guid}/decline")]
    public async Task<PublicQuoteDecisionResponse> Decline(Guid quoteId, [FromBody] PublicQuoteDecisionRequest request, CancellationToken ct)
    {
        await EnsureApiKeyAsync(ct);
        return await _public.DeclineQuoteAsync(quoteId, request,
            HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString(), ct);
    }

    [HttpGet("booking-availability")]
    public async Task<IReadOnlyList<PublicAvailabilitySlotDto>> Availability([FromQuery] DateTimeOffset day, CancellationToken ct)
    {
        await EnsureApiKeyAsync(ct);
        return await _public.GetBookingAvailabilityAsync(day, ct);
    }

    [HttpGet("same-day-hint")]
    public async Task<PublicSameDayHintDto> SameDayHint([FromQuery] string? serviceCode, [FromQuery] string? category, CancellationToken ct)
    {
        await EnsureApiKeyAsync(ct);
        return await _public.GetSameDayHintAsync(serviceCode, category, ct);
    }

    [HttpPost("repair-status/{trackingCode}/message")]
    public async Task<PublicRepairMessageResponse> Message(string trackingCode, [FromBody] PublicRepairMessageRequest request, CancellationToken ct)
    {
        await EnsureApiKeyAsync(ct);
        return await _public.PostRepairMessageAsync(trackingCode, request, HttpContext.Connection.RemoteIpAddress?.ToString(), ct);
    }

    [HttpPost("refurbished/sync-status")]
    public async Task<SyncRefurbishedStatusResponse> SyncRefurb([FromBody] SyncRefurbishedStatusRequest request, CancellationToken ct)
    {
        await EnsureApiKeyAsync(ct);
        return await _public.SyncRefurbishedStatusAsync(request, ct);
    }
}

[ApiController]
[Route("api/customer")]
[EnableRateLimiting("public")]
public sealed class CustomerPortalController : ControllerBase
{
    private readonly ICustomerPortalService _portal;

    public CustomerPortalController(ICustomerPortalService portal) => _portal = portal;

    [HttpPost("login")]
    [AllowAnonymous]
    public Task<CustomerPortalAuthResponse> Login([FromBody] CustomerPortalLoginRequest request, CancellationToken ct) =>
        _portal.LoginAsync(request, ct);

    [HttpPost("register")]
    [AllowAnonymous]
    public Task<CustomerPortalAuthResponse> Register([FromBody] CustomerPortalRegisterRequest request, CancellationToken ct) =>
        _portal.RegisterAsync(request, ct);

    [Authorize(Policy = "portal:customer")]
    [HttpGet("repairs")]
    public Task<IReadOnlyList<CustomerRepairListItemDto>> Repairs(CancellationToken ct) =>
        _portal.ListRepairsAsync(CustomerId(), ct);

    [Authorize(Policy = "portal:customer")]
    [HttpGet("quotes")]
    public Task<IReadOnlyList<CustomerQuoteListItemDto>> Quotes(CancellationToken ct) =>
        _portal.ListQuotesAsync(CustomerId(), ct);

    [Authorize(Policy = "portal:customer")]
    [HttpGet("devices")]
    public Task<IReadOnlyList<CustomerDeviceListItemDto>> Devices(CancellationToken ct) =>
        _portal.ListDevicesAsync(CustomerId(), ct);

    [Authorize(Policy = "portal:customer")]
    [HttpGet("invoices")]
    public Task<IReadOnlyList<CustomerInvoiceListItemDto>> Invoices(CancellationToken ct) =>
        _portal.ListInvoicesAsync(CustomerId(), ct);

    [Authorize(Policy = "portal:customer")]
    [HttpGet("warranties")]
    public Task<IReadOnlyList<CustomerWarrantyListItemDto>> Warranties(CancellationToken ct) =>
        _portal.ListWarrantiesAsync(CustomerId(), ct);

    [Authorize(Policy = "portal:customer")]
    [HttpGet("messages")]
    public Task<IReadOnlyList<CustomerMessageListItemDto>> Messages(CancellationToken ct) =>
        _portal.ListThreadsAsync(CustomerId(), ct);

    [Authorize(Policy = "portal:customer")]
    [HttpGet("messages/{threadId:guid}")]
    public Task<IReadOnlyList<CustomerMessageDto>> Thread(Guid threadId, CancellationToken ct) =>
        _portal.ListMessagesAsync(CustomerId(), threadId, ct);

    [Authorize(Policy = "portal:customer")]
    [HttpPost("messages")]
    public Task<CustomerMessageListItemDto> PostMessage([FromBody] CustomerPostMessageRequest request, CancellationToken ct) =>
        _portal.PostMessageAsync(CustomerId(), request, ct);

    [Authorize(Policy = "portal:customer")]
    [HttpGet("sell-requests")]
    public Task<IReadOnlyList<CustomerSellRequestListItemDto>> SellRequests(CancellationToken ct) =>
        _portal.ListSellRequestsAsync(CustomerId(), ct);

    private Guid CustomerId()
    {
        var raw = User.FindFirstValue("customer_id") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.Parse(raw!);
    }
}

[ApiController]
[Route("api/sell-tech")]
[Authorize]
public sealed class SellTechController : ControllerBase
{
    private readonly ISellTechService _sell;
    public SellTechController(ISellTechService sell) => _sell = sell;

    [HttpGet]
    [Authorize(Policy = "perm:selltech.view")]
    public Task<IReadOnlyList<SellDeviceListItemDto>> List([FromQuery] string? q, [FromQuery] string? status, CancellationToken ct) =>
        _sell.ListAsync(q, status, ct);

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "perm:selltech.view")]
    public Task<SellDeviceDetailDto> Get(Guid id, CancellationToken ct) => _sell.GetAsync(id, ct);

    [HttpPost("{id:guid}/status")]
    [Authorize(Policy = "perm:selltech.manage")]
    public Task<SellDeviceDetailDto> Status(Guid id, [FromBody] UpdateSellDeviceStatusRequest request, CancellationToken ct) =>
        _sell.UpdateStatusAsync(id, request, UserId(), ct);

    [HttpPost("{id:guid}/offer")]
    [Authorize(Policy = "perm:selltech.manage")]
    public Task<SellDeviceOfferDto> Offer(Guid id, [FromBody] UpsertSellDeviceOfferRequest request, CancellationToken ct) =>
        _sell.UpsertOfferAsync(id, request, UserId(), ct);

    [HttpPost("{id:guid}/offer/send")]
    [Authorize(Policy = "perm:selltech.manage")]
    public Task<SellDeviceOfferDto> SendOffer(Guid id, CancellationToken ct) =>
        _sell.SendOfferAsync(id, UserId(), ct);

    [HttpPost("{id:guid}/create-refurb")]
    [Authorize(Policy = "perm:selltech.manage")]
    public Task<WorkshopOS.Contracts.Operations.UsedDeviceDetailDto> CreateRefurb(Guid id, CancellationToken ct) =>
        _sell.CreateRefurbFromPurchaseAsync(id, UserId(), ct);

    [HttpGet("offer-defaults")]
    [Authorize(Policy = "perm:selltech.manage")]
    public Task<OfferCalculatorDefaultsDto> Defaults(CancellationToken ct) => _sell.GetOfferDefaultsAsync(ct);

    [HttpPut("offer-defaults")]
    [Authorize(Policy = "perm:selltech.manage")]
    public Task<OfferCalculatorDefaultsDto> SaveDefaults([FromBody] OfferCalculatorDefaultsDto body, CancellationToken ct) =>
        _sell.SaveOfferDefaultsAsync(body, UserId(), ct);

    private Guid UserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);
}

[ApiController]
[Route("api/messages")]
[Authorize]
public sealed class MessagingController : ControllerBase
{
    private readonly IMessagingService _messages;
    public MessagingController(IMessagingService messages) => _messages = messages;

    [HttpGet]
    [Authorize(Policy = "perm:messages.view")]
    public Task<IReadOnlyList<StaffThreadDto>> List(CancellationToken ct) => _messages.ListThreadsAsync(ct);

    [HttpGet("{threadId:guid}")]
    [Authorize(Policy = "perm:messages.view")]
    public Task<IReadOnlyList<StaffMessageDto>> Get(Guid threadId, CancellationToken ct) =>
        _messages.ListMessagesAsync(threadId, includeInternal: true, ct);

    [HttpPost("{threadId:guid}")]
    [Authorize(Policy = "perm:messages.manage")]
    public Task<StaffMessageDto> Post(Guid threadId, [FromBody] StaffPostMessageRequest request, CancellationToken ct) =>
        _messages.PostStaffMessageAsync(threadId, request, UserId(), ct);

    [HttpPost("repair/{ticketId:guid}/ensure")]
    [Authorize(Policy = "perm:messages.manage")]
    public async Task<StaffThreadDto> EnsureRepair(Guid ticketId, CancellationToken ct)
    {
        var t = await _messages.EnsureRepairThreadAsync(ticketId, ct);
        return new StaffThreadDto(t.Id, t.Subject, t.Status, null, null, t.UpdatedAt);
    }

    private Guid UserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);
}

[ApiController]
[Route("api/website")]
[Authorize]
public sealed class WebsiteIntegrationsController : ControllerBase
{
    private readonly IWebsiteIntegrationService _integrations;
    private readonly INotificationTemplateService _templates;

    public WebsiteIntegrationsController(IWebsiteIntegrationService integrations, INotificationTemplateService templates)
    {
        _integrations = integrations;
        _templates = templates;
    }

    [HttpGet("api-keys")]
    [Authorize(Policy = "perm:website.manage")]
    public Task<IReadOnlyList<WebsiteApiKeyDto>> Keys(CancellationToken ct) => _integrations.ListApiKeysAsync(ct);

    [HttpPost("api-keys")]
    [Authorize(Policy = "perm:website.manage")]
    public Task<CreateWebsiteApiKeyResponse> CreateKey([FromBody] CreateWebsiteApiKeyRequest request, CancellationToken ct) =>
        _integrations.CreateApiKeyAsync(request, UserId(), ct);

    [HttpDelete("api-keys/{id:guid}")]
    [Authorize(Policy = "perm:website.manage")]
    public Task RevokeKey(Guid id, CancellationToken ct) => _integrations.RevokeApiKeyAsync(id, ct);

    [HttpGet("woocommerce")]
    [Authorize(Policy = "perm:website.manage")]
    public Task<WooCommerceSettingsDto> Woo(CancellationToken ct) => _integrations.GetWooSettingsAsync(ct);

    [HttpPut("woocommerce")]
    [Authorize(Policy = "perm:website.manage")]
    public Task<WooCommerceSettingsDto> SaveWoo([FromBody] UpdateWooCommerceSettingsRequest request, CancellationToken ct) =>
        _integrations.SaveWooSettingsAsync(request, UserId(), ct);

    [HttpPost("woocommerce/publish")]
    [Authorize(Policy = "perm:website.manage")]
    public Task<PublishRefurbishedResponse> Publish([FromBody] PublishRefurbishedRequest request, CancellationToken ct) =>
        _integrations.PublishRefurbishedAsync(request, UserId(), ct);

    [HttpGet("dashboard")]
    [Authorize(Policy = "perm:tickets.view")]
    public Task<WebsiteDashboardCardsDto> Dashboard(CancellationToken ct) =>
        _integrations.GetWebsiteDashboardAsync(ct);

    [HttpGet("qr/{kind}/{id:guid}")]
    [Authorize]
    public Task<QrPayloadDto> Qr(string kind, Guid id, CancellationToken ct) =>
        _integrations.BuildQrAsync(kind, id, ct);

    [HttpGet("notification-templates")]
    [Authorize(Policy = "perm:settings.manage")]
    public Task<IReadOnlyList<NotificationTemplateDto>> Templates(CancellationToken ct) => _templates.ListAsync(ct);

    [HttpPut("notification-templates/{key}")]
    [Authorize(Policy = "perm:settings.manage")]
    public Task<NotificationTemplateDto> UpsertTemplate(string key, [FromBody] UpsertNotificationTemplateRequest request, CancellationToken ct) =>
        _templates.UpsertAsync(key, request, ct);

    [HttpGet("webhooks")]
    [Authorize(Policy = "perm:website.manage")]
    public Task<WebsiteWebhookSettingsDto> Webhooks(CancellationToken ct) => _integrations.GetWebhookSettingsAsync(ct);

    [HttpPut("webhooks")]
    [Authorize(Policy = "perm:website.manage")]
    public Task<WebsiteWebhookSettingsDto> SaveWebhooks([FromBody] UpdateWebsiteWebhookSettingsRequest request, CancellationToken ct) =>
        _integrations.SaveWebhookSettingsAsync(request, UserId(), ct);

    [HttpGet("booking-availability-settings")]
    [Authorize(Policy = "perm:website.manage")]
    public Task<BookingAvailabilitySettingsDto> BookingSettings(CancellationToken ct) =>
        _integrations.GetBookingAvailabilitySettingsAsync(ct);

    [HttpPut("booking-availability-settings")]
    [Authorize(Policy = "perm:website.manage")]
    public Task<BookingAvailabilitySettingsDto> SaveBookingSettings([FromBody] BookingAvailabilitySettingsDto body, CancellationToken ct) =>
        _integrations.SaveBookingAvailabilitySettingsAsync(body, UserId(), ct);

    private Guid UserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);
}

[ApiController]
[Route("api/custom-pc")]
[Authorize]
public sealed class CustomPcStaffController : ControllerBase
{
    [HttpGet]
    [HttpGet("{id:guid}")]
    [HttpPost("{id:guid}/status")]
    [HttpPost("{id:guid}/convert-to-build")]
    public IActionResult Gone() =>
        StatusCode(StatusCodes.Status410Gone, new
        {
            title = "Custom PC Builds has been removed.",
            code = "module_removed",
            status = 410,
            detail = "Use standard repair tickets for desktop computers. Historical custom_pc_requests rows remain in the database."
        });
}

[ApiController]
[Route("api/refurb-diagnostics")]
[Authorize]
public sealed class RefurbDiagnosticsController : ControllerBase
{
    private readonly IRefurbDiagnosticService _diag;
    public RefurbDiagnosticsController(IRefurbDiagnosticService diag) => _diag = diag;

    [HttpGet("checklists")]
    [Authorize(Policy = "perm:used.view")]
    public Task<IReadOnlyList<RefurbChecklistDto>> Checklists(CancellationToken ct) => _diag.ListChecklistsAsync(ct);

    [HttpPost("checklists")]
    [Authorize(Policy = "perm:used.manage")]
    public Task<RefurbChecklistDto> UpsertChecklist([FromBody] UpsertRefurbChecklistRequest request, CancellationToken ct) =>
        _diag.UpsertChecklistAsync(null, request, ct);

    [HttpPut("checklists/{id:guid}")]
    [Authorize(Policy = "perm:used.manage")]
    public Task<RefurbChecklistDto> UpdateChecklist(Guid id, [FromBody] UpsertRefurbChecklistRequest request, CancellationToken ct) =>
        _diag.UpsertChecklistAsync(id, request, ct);

    [HttpGet("results/{usedDeviceId:guid}")]
    [Authorize(Policy = "perm:used.view")]
    public Task<IReadOnlyList<RefurbDiagnosticResultDto>> Results(Guid usedDeviceId, CancellationToken ct) =>
        _diag.ListResultsAsync(usedDeviceId, ct);

    [HttpPost("results")]
    [Authorize(Policy = "perm:used.manage")]
    public Task<RefurbDiagnosticResultDto> Save([FromBody] SaveRefurbDiagnosticRequest request, CancellationToken ct) =>
        _diag.SaveResultAsync(request, UserId(), ct);

    private Guid UserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);
}
