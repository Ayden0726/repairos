using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WorkshopOS.Application.Abstractions;
using WorkshopOS.Application.Common;
using WorkshopOS.Contracts.Operations;
using WorkshopOS.Domain.Entities;
using WorkshopOS.Infrastructure.Persistence;

namespace WorkshopOS.Infrastructure.Services;

public sealed class PartFitmentService : IPartFitmentService
{
    private readonly WorkshopDbContext _db;
    private readonly IAuditService _audit;
    public PartFitmentService(WorkshopDbContext db, IAuditService audit) { _db = db; _audit = audit; }

    public async Task<IReadOnlyList<PartFitmentDto>> ListAsync(
        string? brand = null, string? model = null, string? q = null, CancellationToken ct = default)
    {
        var query = _db.PartFitments.AsNoTracking().Include(f => f.InventoryItem)
            .Where(f => f.ArchivedAt == null);
        if (!string.IsNullOrWhiteSpace(brand))
        {
            var b = brand.Trim().ToLowerInvariant();
            query = query.Where(f => f.Brand.ToLower() == b || f.Brand.ToLower().Contains(b));
        }
        if (!string.IsNullOrWhiteSpace(model))
        {
            var m = model.Trim().ToLowerInvariant();
            query = query.Where(f => f.Model.ToLower() == m || f.Model.ToLower().Contains(m));
        }
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLowerInvariant();
            query = query.Where(f =>
                f.PartName.ToLower().Contains(term) ||
                (f.PartSku != null && f.PartSku.ToLower().Contains(term)) ||
                (f.InventoryItem != null && f.InventoryItem.Sku.ToLower().Contains(term)) ||
                f.Brand.ToLower().Contains(term) ||
                f.Model.ToLower().Contains(term));
        }
        var rows = await query.OrderBy(f => f.Brand).ThenBy(f => f.Model).ThenBy(f => f.PartName).Take(200)
            .ToListAsync(ct);
        return rows.Select(Map).ToList();
    }

    public async Task<PartFitmentDto> UpsertAsync(UpsertPartFitmentRequest request, Guid actorId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.PartName))
            throw new ValidationAppException("Part name is required.");
        if (string.IsNullOrWhiteSpace(request.Brand) || string.IsNullOrWhiteSpace(request.Model))
            throw new ValidationAppException("Brand and model are required.");

        if (request.InventoryItemId is Guid itemId)
            _ = await _db.InventoryItems.FirstOrDefaultAsync(i => i.Id == itemId && i.ArchivedAt == null, ct)
                ?? throw new AppException("not_found", "Inventory item was not found.", 404);

        PartFitment row;
        if (request.Id is Guid id)
        {
            row = await _db.PartFitments.FirstOrDefaultAsync(f => f.Id == id && f.ArchivedAt == null, ct)
                ?? throw new AppException("not_found", "Fitment rule was not found.", 404);
        }
        else
        {
            row = new PartFitment();
            _db.PartFitments.Add(row);
        }

        row.InventoryItemId = request.InventoryItemId;
        row.PartSku = string.IsNullOrWhiteSpace(request.PartSku) ? null : request.PartSku.Trim();
        row.PartName = request.PartName.Trim();
        row.Brand = request.Brand.Trim();
        row.Model = request.Model.Trim();
        row.DeviceType = string.IsNullOrWhiteSpace(request.DeviceType) ? null : request.DeviceType.Trim();
        row.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        row.IsVerified = request.IsVerified;
        row.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(actorId, "fitment.upsert", "PartFitment", row.Id.ToString(), ct: ct);
        return (await ListAsync(ct: ct)).First(f => f.Id == row.Id);
    }

    public async Task DeleteAsync(Guid id, Guid actorId, CancellationToken ct = default)
    {
        var row = await _db.PartFitments.FirstOrDefaultAsync(f => f.Id == id && f.ArchivedAt == null, ct)
            ?? throw new AppException("not_found", "Fitment rule was not found.", 404);
        row.ArchivedAt = DateTimeOffset.UtcNow;
        row.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(actorId, "fitment.delete", "PartFitment", id.ToString(), ct: ct);
    }

    private static PartFitmentDto Map(PartFitment f) =>
        new(f.Id, f.InventoryItemId, f.InventoryItem?.Sku, f.PartSku, f.PartName,
            f.Brand, f.Model, f.DeviceType, f.Notes, f.IsVerified);
}

public sealed class SyncService : ISyncService
{
    private static readonly HashSet<string> AllowedMethods = new(StringComparer.OrdinalIgnoreCase)
        { "POST", "PUT", "DELETE" };
    private static readonly string[] AllowedPathPrefixes =
    [
        "/api/inventory", "/api/repairs", "/api/customers", "/api/quotes",
        "/api/bookings", "/api/notes", "/api/pos", "/api/used-tech", "/api/donors",
        "/api/loans", "/api/refunds", "/api/warranty-claims", "/api/fitment", "/api/stocktakes"
    ];

    private readonly WorkshopDbContext _db;
    private readonly IAuditService _audit;
    public SyncService(WorkshopDbContext db, IAuditService audit) { _db = db; _audit = audit; }

    public async Task<SyncPushResponse> PushAsync(SyncPushRequest request, Guid actorId, CancellationToken ct = default)
    {
        if (request.Mutations is null || request.Mutations.Count == 0)
            throw new ValidationAppException("No mutations to push.");
        if (request.Mutations.Count > 50)
            throw new ValidationAppException("Sync batch limit is 50 mutations.");

        var results = new List<SyncMutationResultDto>();
        foreach (var m in request.Mutations)
        {
            if (m.ClientMutationId == Guid.Empty)
            {
                results.Add(new SyncMutationResultDto(m.ClientMutationId, "Rejected", "ClientMutationId required.", null));
                continue;
            }

            var existing = await _db.SyncOutboxEntries.AsNoTracking()
                .FirstOrDefaultAsync(e => e.ClientMutationId == m.ClientMutationId && e.UserId == actorId, ct);
            if (existing is not null)
            {
                results.Add(new SyncMutationResultDto(m.ClientMutationId, "Duplicate", null, existing.Id));
                continue;
            }

            var method = (m.Method ?? "POST").Trim().ToUpperInvariant();
            if (!AllowedMethods.Contains(method))
            {
                results.Add(new SyncMutationResultDto(m.ClientMutationId, "Rejected", "Method not allowed for offline sync.", null));
                continue;
            }

            var path = (m.Path ?? "").Trim();
            if (!path.StartsWith('/')) path = "/" + path;
            if (!AllowedPathPrefixes.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            {
                results.Add(new SyncMutationResultDto(m.ClientMutationId, "Rejected", "Path not allowed for offline sync.", null));
                continue;
            }

            // Server records and acknowledges; heavy apply of arbitrary bodies is deferred —
            // client replays the same authenticated call after ack when online.
            var entry = new SyncOutboxEntry
            {
                ClientMutationId = m.ClientMutationId,
                UserId = actorId,
                Method = method,
                Path = path,
                BodyJson = string.IsNullOrWhiteSpace(m.BodyJson) ? null : m.BodyJson,
                Status = "Applied",
                AppliedAt = DateTimeOffset.UtcNow
            };
            _db.SyncOutboxEntries.Add(entry);
            await _db.SaveChangesAsync(ct);
            results.Add(new SyncMutationResultDto(m.ClientMutationId, "Applied", null, entry.Id));
        }

        await _audit.WriteAsync(actorId, "sync.push", "SyncOutbox", actorId.ToString(),
            newValue: new { count = results.Count }, ct: ct);
        return new SyncPushResponse(results);
    }

    public async Task<IReadOnlyList<SyncOutboxListItemDto>> ListAsync(string? status = null, CancellationToken ct = default)
    {
        var q = _db.SyncOutboxEntries.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(status) && status != "All")
            q = q.Where(e => e.Status == status.Trim());
        return await q.OrderByDescending(e => e.CreatedAt).Take(100)
            .Select(e => new SyncOutboxListItemDto(
                e.Id, e.ClientMutationId, e.Method, e.Path, e.Status, e.Error, e.CreatedAt, e.AppliedAt))
            .ToListAsync(ct);
    }
}

public sealed class WorkshopLocationService : IWorkshopLocationService
{
    private readonly WorkshopDbContext _db;
    private readonly IAuditService _audit;
    public WorkshopLocationService(WorkshopDbContext db, IAuditService audit) { _db = db; _audit = audit; }

    public async Task<IReadOnlyList<WorkshopLocationDto>> ListAsync(CancellationToken ct = default) =>
        await _db.Locations.AsNoTracking().Where(l => l.ArchivedAt == null)
            .OrderByDescending(l => l.IsDefault).ThenBy(l => l.Name)
            .Select(l => new WorkshopLocationDto(
                l.Id, l.Name, l.Phone, l.Email, l.AddressLine1, l.Suburb, l.State, l.Postcode, l.IsDefault))
            .ToListAsync(ct);

    public async Task<WorkshopLocationDto> UpsertAsync(UpsertWorkshopLocationRequest request, Guid actorId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ValidationAppException("Location name is required.");

        Location row;
        if (request.Id is Guid id)
        {
            row = await _db.Locations.FirstOrDefaultAsync(l => l.Id == id && l.ArchivedAt == null, ct)
                ?? throw new AppException("not_found", "Location was not found.", 404);
        }
        else
        {
            row = new Location();
            _db.Locations.Add(row);
        }

        row.Name = request.Name.Trim();
        row.Phone = Norm(request.Phone);
        row.Email = Norm(request.Email);
        row.AddressLine1 = Norm(request.AddressLine1);
        row.Suburb = Norm(request.Suburb);
        row.State = Norm(request.State);
        row.Postcode = Norm(request.Postcode);
        row.IsDefault = request.IsDefault;
        row.UpdatedAt = DateTimeOffset.UtcNow;

        if (row.IsDefault)
        {
            var others = await _db.Locations.Where(l => l.Id != row.Id && l.ArchivedAt == null && l.IsDefault).ToListAsync(ct);
            foreach (var o in others) o.IsDefault = false;
        }

        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(actorId, "location.upsert", "Location", row.Id.ToString(), ct: ct);
        return new WorkshopLocationDto(row.Id, row.Name, row.Phone, row.Email, row.AddressLine1,
            row.Suburb, row.State, row.Postcode, row.IsDefault);
    }

    private static string? Norm(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}

public sealed class InventoryBinService : IInventoryBinService
{
    private readonly WorkshopDbContext _db;
    private readonly IAuditService _audit;
    public InventoryBinService(WorkshopDbContext db, IAuditService audit) { _db = db; _audit = audit; }

    public async Task<IReadOnlyList<InventoryBinDto>> ListAsync(Guid? locationId = null, CancellationToken ct = default)
    {
        var q = _db.InventoryBins.AsNoTracking().Include(b => b.Location).Where(b => b.ArchivedAt == null);
        if (locationId is Guid loc) q = q.Where(b => b.LocationId == loc);
        return await q.OrderBy(b => b.Code)
            .Select(b => new InventoryBinDto(b.Id, b.LocationId, b.Location != null ? b.Location.Name : null,
                b.ParentBinId, b.Code, b.Name, b.Notes))
            .ToListAsync(ct);
    }

    public async Task<InventoryBinDto> UpsertAsync(UpsertInventoryBinRequest request, Guid actorId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
            throw new ValidationAppException("Bin code is required.");

        if (request.LocationId is Guid locId)
            _ = await _db.Locations.FirstOrDefaultAsync(l => l.Id == locId && l.ArchivedAt == null, ct)
                ?? throw new AppException("not_found", "Location was not found.", 404);
        if (request.ParentBinId is Guid parentId)
            _ = await _db.InventoryBins.FirstOrDefaultAsync(b => b.Id == parentId && b.ArchivedAt == null, ct)
                ?? throw new AppException("not_found", "Parent bin was not found.", 404);

        InventoryBin row;
        if (request.Id is Guid id)
        {
            row = await _db.InventoryBins.FirstOrDefaultAsync(b => b.Id == id && b.ArchivedAt == null, ct)
                ?? throw new AppException("not_found", "Bin was not found.", 404);
        }
        else
        {
            if (await _db.InventoryBins.AnyAsync(b => b.Code == request.Code.Trim() && b.ArchivedAt == null, ct))
                throw new ConflictAppException("Bin code already exists.");
            row = new InventoryBin();
            _db.InventoryBins.Add(row);
        }

        row.LocationId = request.LocationId;
        row.ParentBinId = request.ParentBinId;
        row.Code = request.Code.Trim();
        row.Name = string.IsNullOrWhiteSpace(request.Name) ? null : request.Name.Trim();
        row.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        row.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(actorId, "inventory.bin.upsert", "InventoryBin", row.Id.ToString(), ct: ct);
        return (await ListAsync(ct: ct)).First(b => b.Id == row.Id);
    }
}
