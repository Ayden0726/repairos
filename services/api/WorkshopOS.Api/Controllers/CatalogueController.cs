using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WorkshopOS.Application.Abstractions;
using WorkshopOS.Contracts.Operations;

namespace WorkshopOS.Api.Controllers;

[ApiController]
[Route("api/catalogue")]
public sealed class CatalogueController : ControllerBase
{
    private readonly IServiceCatalogueService _catalogue;
    public CatalogueController(IServiceCatalogueService catalogue) => _catalogue = catalogue;

    [HttpGet("categories")]
    [Authorize(Policy = "perm:pricing.view")]
    public Task<IReadOnlyList<CatalogueCategoryDto>> Categories(CancellationToken ct) =>
        _catalogue.ListCategoriesAsync(ct);

    [HttpPost("categories")]
    [Authorize(Policy = "perm:pricing.edit_settings")]
    public Task<CatalogueCategoryDto> UpsertCategory([FromBody] UpsertCatalogueCategoryRequest request, CancellationToken ct) =>
        _catalogue.UpsertCategoryAsync(request, UserId(), ct);

    [HttpDelete("categories/{id:guid}")]
    [Authorize(Policy = "perm:pricing.edit_settings")]
    public async Task<IActionResult> DeleteCategory(Guid id, CancellationToken ct)
    {
        await _catalogue.DeleteCategoryAsync(id, UserId(), ct);
        return NoContent();
    }

    [HttpPost("categories/reorder")]
    [Authorize(Policy = "perm:pricing.edit_settings")]
    public async Task<IActionResult> ReorderCategories([FromBody] ReorderCatalogueRequest request, CancellationToken ct)
    {
        await _catalogue.ReorderCategoriesAsync(request, UserId(), ct);
        return NoContent();
    }

    [HttpGet("services")]
    [Authorize(Policy = "perm:pricing.view")]
    public Task<IReadOnlyList<CatalogueServiceDto>> Services(
        [FromQuery] string? q,
        [FromQuery] string? category,
        [FromQuery] string? deviceType,
        [FromQuery] string? brand,
        [FromQuery] string? model,
        [FromQuery] bool activeOnly = true,
        CancellationToken ct = default) =>
        _catalogue.SearchServicesAsync(q, category, deviceType, brand, model, activeOnly, ct);

    [HttpPost("services")]
    [Authorize(Policy = "perm:pricing.edit_settings")]
    public Task<CatalogueServiceDto> UpsertService([FromBody] UpsertCatalogueServiceRequest request, CancellationToken ct) =>
        _catalogue.UpsertServiceAsync(request, UserId(), ct);

    [HttpDelete("services/{id:guid}")]
    [Authorize(Policy = "perm:pricing.edit_settings")]
    public async Task<IActionResult> DeleteService(Guid id, CancellationToken ct)
    {
        await _catalogue.DeleteServiceAsync(id, UserId(), ct);
        return NoContent();
    }

    [HttpPost("import")]
    [Authorize(Policy = "perm:pricing.edit_settings")]
    public Task<CatalogueImportResultDto> Import([FromQuery] bool force = false, CancellationToken ct = default) =>
        _catalogue.ImportAsync(force, UserId(), ct);

    [HttpGet("favourites")]
    [Authorize(Policy = "perm:pricing.view")]
    public Task<IReadOnlyList<CatalogueServiceDto>> Favourites(CancellationToken ct) =>
        _catalogue.ListFavouritesAsync(UserId(), ct);

    [HttpPost("favourites/{serviceId:guid}")]
    [Authorize(Policy = "perm:pricing.view")]
    public async Task<IActionResult> Favourite(Guid serviceId, CancellationToken ct)
    {
        await _catalogue.FavouriteAsync(UserId(), serviceId, ct);
        return NoContent();
    }

    [HttpDelete("favourites/{serviceId:guid}")]
    [Authorize(Policy = "perm:pricing.view")]
    public async Task<IActionResult> Unfavourite(Guid serviceId, CancellationToken ct)
    {
        await _catalogue.UnfavouriteAsync(UserId(), serviceId, ct);
        return NoContent();
    }

    [HttpGet("recent")]
    [Authorize(Policy = "perm:pricing.view")]
    public Task<IReadOnlyList<CatalogueServiceDto>> Recent([FromQuery] int take = 12, CancellationToken ct = default) =>
        _catalogue.ListRecentAsync(UserId(), take, ct);

    [HttpPost("recent/{serviceId:guid}")]
    [Authorize(Policy = "perm:pricing.view")]
    public async Task<IActionResult> RecordRecent(Guid serviceId, CancellationToken ct)
    {
        await _catalogue.RecordRecentAsync(UserId(), serviceId, ct);
        return NoContent();
    }

    [HttpGet("bundles")]
    [Authorize(Policy = "perm:pricing.view")]
    public Task<IReadOnlyList<CatalogueBundleDto>> Bundles([FromQuery] bool expand = true, CancellationToken ct = default) =>
        _catalogue.ListBundlesAsync(expand, ct);

    [HttpPost("bundles")]
    [Authorize(Policy = "perm:pricing.edit_settings")]
    public Task<CatalogueBundleDto> UpsertBundle([FromBody] UpsertCatalogueBundleRequest request, CancellationToken ct) =>
        _catalogue.UpsertBundleAsync(request, UserId(), ct);

    [HttpDelete("bundles/{id:guid}")]
    [Authorize(Policy = "perm:pricing.edit_settings")]
    public async Task<IActionResult> DeleteBundle(Guid id, CancellationToken ct)
    {
        await _catalogue.DeleteBundleAsync(id, UserId(), ct);
        return NoContent();
    }

    [HttpGet("brands")]
    [Authorize(Policy = "perm:pricing.view")]
    public Task<IReadOnlyList<DeviceBrandDto>> Brands([FromQuery] string? deviceType = null, CancellationToken ct = default) =>
        _catalogue.ListBrandsAsync(deviceType, ct);

    [HttpGet("models")]
    [Authorize(Policy = "perm:pricing.view")]
    public Task<IReadOnlyList<DeviceModelDto>> Models(
        [FromQuery] Guid? brandId = null,
        [FromQuery] string? deviceType = null,
        [FromQuery] string? brand = null,
        CancellationToken ct = default) =>
        _catalogue.ListModelsAsync(brandId, deviceType, brand, ct);

    private Guid UserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);
}
