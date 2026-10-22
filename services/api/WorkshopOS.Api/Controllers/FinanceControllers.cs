using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WorkshopOS.Application.Abstractions;
using WorkshopOS.Contracts.Finance;

namespace WorkshopOS.Api.Controllers;

[ApiController]
[Route("api/integrations/credentials")]
public sealed class IntegrationCredentialsController : ControllerBase
{
    private readonly IIntegrationCredentialService _credentials;
    public IntegrationCredentialsController(IIntegrationCredentialService credentials) => _credentials = credentials;

    [HttpGet("providers")]
    [Authorize(Policy = "perm:integrations.manage")]
    public IReadOnlyList<IntegrationProviderInfoDto> Providers() => _credentials.ListProviders();

    [HttpGet]
    [Authorize(Policy = "perm:integrations.manage")]
    public Task<IReadOnlyList<IntegrationCredentialDto>> List(CancellationToken ct) =>
        _credentials.ListAsync(ct);

    [HttpPut]
    [Authorize(Policy = "perm:integrations.manage")]
    public Task<IntegrationCredentialDto> Upsert([FromBody] UpsertIntegrationCredentialRequest request, CancellationToken ct) =>
        _credentials.UpsertAsync(request, UserId(), ct);

    [HttpPost("{providerKey}/test")]
    [Authorize(Policy = "perm:integrations.manage")]
    public Task<TestIntegrationCredentialResponse> Test(string providerKey, CancellationToken ct) =>
        _credentials.TestAsync(providerKey, UserId(), ct);

    private Guid UserId() =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);
}

[ApiController]
[Route("api/expenses")]
public sealed class ExpensesController : ControllerBase
{
    private readonly IExpenseService _expenses;
    public ExpensesController(IExpenseService expenses) => _expenses = expenses;

    [HttpGet("dashboard")]
    [Authorize(Policy = "perm:expenses.view")]
    public Task<ExpenseDashboardDto> Dashboard([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct) =>
        _expenses.DashboardAsync(from, to, ct);

    [HttpGet("categories")]
    [Authorize(Policy = "perm:expenses.view")]
    public Task<IReadOnlyList<ExpenseCategoryDto>> Categories(CancellationToken ct) =>
        _expenses.ListCategoriesAsync(ct);

    [HttpPut("categories")]
    [Authorize(Policy = "perm:expenses.categories")]
    public Task<ExpenseCategoryDto> UpsertCategory([FromBody] UpsertExpenseCategoryRequest request, CancellationToken ct) =>
        _expenses.UpsertCategoryAsync(request, UserId(), ct);

    [HttpPost("gst/preview")]
    [Authorize(Policy = "perm:expenses.view")]
    public Task<GstCalcResult> PreviewGst([FromBody] GstCalcRequest request, CancellationToken ct) =>
        _expenses.PreviewGstAsync(request, ct);

    [HttpGet]
    [Authorize(Policy = "perm:expenses.view")]
    public Task<IReadOnlyList<ExpenseListItemDto>> List(
        [FromQuery] string? q, [FromQuery] string? status, [FromQuery] Guid? categoryId,
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct) =>
        _expenses.ListAsync(q, status, categoryId, from, to, ct);

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "perm:expenses.view")]
    public Task<ExpenseDetailDto> Get(Guid id, CancellationToken ct) =>
        _expenses.GetAsync(id, ct);

    [HttpPost]
    [Authorize(Policy = "perm:expenses.manage")]
    public Task<ExpenseDetailDto> Create([FromBody] UpsertExpenseRequest request, CancellationToken ct) =>
        _expenses.UpsertAsync(null, request, UserId(), ct);

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "perm:expenses.manage")]
    public Task<ExpenseDetailDto> Update(Guid id, [FromBody] UpsertExpenseRequest request, CancellationToken ct) =>
        _expenses.UpsertAsync(id, request, UserId(), ct);

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "perm:expenses.delete")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _expenses.SoftDeleteAsync(id, UserId(), ct);
        return NoContent();
    }

    [HttpGet("export/csv")]
    [Authorize(Policy = "perm:expenses.export")]
    public async Task<IActionResult> ExportCsv(
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] Guid? categoryId, CancellationToken ct)
    {
        var csv = await _expenses.ExportCsvAsync(from, to, categoryId, ct);
        return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", $"expenses-{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    [HttpGet("recurring")]
    [Authorize(Policy = "perm:expenses.view")]
    public Task<IReadOnlyList<RecurringExpenseDto>> ListRecurring(CancellationToken ct) =>
        _expenses.ListRecurringAsync(ct);

    [HttpPost("recurring")]
    [Authorize(Policy = "perm:expenses.manage")]
    public Task<RecurringExpenseDto> CreateRecurring([FromBody] UpsertRecurringExpenseRequest request, CancellationToken ct) =>
        _expenses.UpsertRecurringAsync(null, request, UserId(), ct);

    [HttpPut("recurring/{id:guid}")]
    [Authorize(Policy = "perm:expenses.manage")]
    public Task<RecurringExpenseDto> UpdateRecurring(Guid id, [FromBody] UpsertRecurringExpenseRequest request, CancellationToken ct) =>
        _expenses.UpsertRecurringAsync(id, request, UserId(), ct);

    [HttpPost("recurring/process-due")]
    [Authorize(Policy = "perm:expenses.manage")]
    public async Task<object> ProcessDue(CancellationToken ct)
    {
        var created = await _expenses.ProcessDueRecurringAsync(ct);
        return new { created };
    }

    [HttpPost("{id:guid}/attachments")]
    [Authorize(Policy = "perm:expenses.manage")]
    [RequestSizeLimit(16 * 1024 * 1024)]
    public async Task<ExpenseAttachmentDto> UploadAttachment(Guid id, IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            throw new WorkshopOS.Application.Common.ValidationAppException("A receipt file is required.");
        await using var stream = file.OpenReadStream();
        return await _expenses.AddAttachmentAsync(id, file.FileName, file.ContentType ?? "application/octet-stream", stream, UserId(), ct);
    }

    [HttpGet("{id:guid}/attachments/{attachmentId:guid}")]
    [Authorize(Policy = "perm:expenses.view")]
    public async Task<IActionResult> DownloadAttachment(Guid id, Guid attachmentId, CancellationToken ct)
    {
        var (stream, contentType, fileName) = await _expenses.OpenAttachmentAsync(id, attachmentId, ct);
        return File(stream, contentType, fileName);
    }

    private Guid UserId() =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);
}
