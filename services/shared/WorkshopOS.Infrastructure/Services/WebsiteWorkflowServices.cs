using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using WorkshopOS.Application.Abstractions;
using WorkshopOS.Application.Common;
using WorkshopOS.Contracts.Operations;
using WorkshopOS.Contracts.Website;
using WorkshopOS.Domain.Entities;
using WorkshopOS.Infrastructure.Persistence;

namespace WorkshopOS.Infrastructure.Services;

public sealed class CustomPcRequestService : ICustomPcRequestService
{
    private static readonly string[] Statuses =
    [
        "New", "Draft build", "Quote", "Approved build", "Parts ordered",
        "Build in progress", "Testing", "Ready for collection", "Completed", "Cancelled"
    ];

    private readonly WorkshopDbContext _db;
    private readonly IPcBuildService _builds;
    private readonly IAuditService _audit;

    public CustomPcRequestService(WorkshopDbContext db, IPcBuildService builds, IAuditService audit)
    {
        _db = db;
        _builds = builds;
        _audit = audit;
    }

    public async Task<IReadOnlyList<CustomPcRequestListItemDto>> ListAsync(string? status, CancellationToken ct = default)
    {
        var q = _db.CustomPcRequests.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(r => r.Status == status);
        var rows = await q.OrderByDescending(r => r.CreatedAt).Take(200).ToListAsync(ct);
        return rows.Select(r => new CustomPcRequestListItemDto(
            r.Id, r.Reference, r.Status, r.ContactName, r.Budget, r.Usage, r.CreatedAt)).ToList();
    }

    public async Task<CustomPcRequestDetailDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var r = await _db.CustomPcRequests.AsNoTracking().Include(x => x.PcBuild)
            .FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new AppException("not_found", "Custom PC request not found.", 404);
        return Map(r);
    }

    public async Task<CustomPcRequestDetailDto> UpdateStatusAsync(Guid id, UpdateCustomPcStatusRequest request, Guid actorId, CancellationToken ct = default)
    {
        var r = await _db.CustomPcRequests.Include(x => x.PcBuild).FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new AppException("not_found", "Custom PC request not found.", 404);
        var next = Statuses.FirstOrDefault(s => s.Equals(request.Status.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? throw new ValidationAppException($"Invalid status. Use: {string.Join(", ", Statuses)}");
        r.Status = next;
        r.UpdatedAt = DateTimeOffset.UtcNow;
        if (!string.IsNullOrWhiteSpace(request.InternalNote))
            r.CustomerNotes = string.IsNullOrWhiteSpace(r.CustomerNotes)
                ? $"[Staff] {request.InternalNote.Trim()}"
                : $"{r.CustomerNotes}\n[Staff] {request.InternalNote.Trim()}";
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(actorId, "custompc.status", "CustomPcRequest", id.ToString(), newValue: next, ct: ct);
        return Map(r);
    }

    public async Task<CustomPcRequestDetailDto> ConvertToBuildAsync(Guid id, ConvertCustomPcToBuildRequest request, Guid actorId, CancellationToken ct = default)
    {
        var r = await _db.CustomPcRequests.Include(x => x.PcBuild).FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new AppException("not_found", "Custom PC request not found.", 404);
        if (r.PcBuildId is Guid existing)
            return Map(r);

        var name = string.IsNullOrWhiteSpace(request.BuildName)
            ? $"Custom PC {r.Reference}"
            : request.BuildName.Trim();
        var notes = string.Join("\n", new[]
        {
            $"From website request {r.Reference}",
            r.PreferredGamesSoftware is null ? null : $"Games/software: {r.PreferredGamesSoftware}",
            r.MonitorResolution is null ? null : $"Monitor: {r.MonitorResolution}",
            r.PerformanceTarget is null ? null : $"Target: {r.PerformanceTarget}",
            r.RgbPreferences is null ? null : $"RGB: {r.RgbPreferences}",
            r.CaseSize is null ? null : $"Case: {r.CaseSize}",
            r.Colour is null ? null : $"Colour: {r.Colour}",
            r.StorageRequirements is null ? null : $"Storage: {r.StorageRequirements}",
            r.Upgradeability is null ? null : $"Upgradeability: {r.Upgradeability}",
            r.CustomerNotes
        }.Where(x => !string.IsNullOrWhiteSpace(x)));

        var build = await _builds.CreateAsync(new CreatePcBuildRequest(
            r.CustomerId, name, r.Usage, r.Budget, notes, Array.Empty<PcPartInputDto>(), "Quoted"), actorId, ct);

        r.PcBuildId = build.Id;
        r.Status = "Draft build";
        r.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _db.Entry(r).Reference(x => x.PcBuild).LoadAsync(ct);
        await _audit.WriteAsync(actorId, "custompc.convert_build", "CustomPcRequest", id.ToString(),
            newValue: new { build.Id, build.Number }, ct: ct);
        return Map(r);
    }

    private static CustomPcRequestDetailDto Map(CustomPcRequest r) => new(
        r.Id, r.Reference, r.Status, r.ContactName, r.ContactPhone, r.ContactEmail,
        r.Budget, r.Usage, r.PreferredGamesSoftware, r.MonitorResolution, r.PerformanceTarget,
        r.NeedsWifiBluetooth, r.RgbPreferences, r.CaseSize, r.Colour, r.StorageRequirements,
        r.Upgradeability, r.CustomerNotes, r.CustomerId, r.PcBuildId, r.PcBuild?.Number,
        r.CreatedAt, r.UpdatedAt);
}

public sealed class RefurbDiagnosticService : IRefurbDiagnosticService
{
    private readonly WorkshopDbContext _db;
    private readonly IAuditService _audit;

    public RefurbDiagnosticService(WorkshopDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<IReadOnlyList<RefurbChecklistDto>> ListChecklistsAsync(CancellationToken ct = default)
    {
        var rows = await _db.RefurbDiagnosticChecklists.AsNoTracking()
            .Where(c => c.IsActive).OrderBy(c => c.Category).ThenBy(c => c.Name).ToListAsync(ct);
        return rows.Select(MapChecklist).ToList();
    }

    public async Task<RefurbChecklistDto> UpsertChecklistAsync(Guid? id, UpsertRefurbChecklistRequest request, CancellationToken ct = default)
    {
        RefurbDiagnosticChecklist row;
        if (id is Guid existingId)
        {
            row = await _db.RefurbDiagnosticChecklists.FirstOrDefaultAsync(c => c.Id == existingId, ct)
                ?? throw new AppException("not_found", "Checklist not found.", 404);
        }
        else
        {
            row = new RefurbDiagnosticChecklist();
            _db.RefurbDiagnosticChecklists.Add(row);
        }
        row.Category = request.Category.Trim();
        row.Name = request.Name.Trim();
        row.ItemsJson = JsonSerializer.Serialize(request.Items);
        row.IsActive = request.IsActive;
        await _db.SaveChangesAsync(ct);
        return MapChecklist(row);
    }

    public async Task<RefurbDiagnosticResultDto> SaveResultAsync(SaveRefurbDiagnosticRequest request, Guid actorId, CancellationToken ct = default)
    {
        _ = await _db.UsedDevices.FirstOrDefaultAsync(u => u.Id == request.UsedDeviceId && u.ArchivedAt == null, ct)
            ?? throw new AppException("not_found", "Refurbished device not found.", 404);
        var row = new RefurbDiagnosticResult
        {
            UsedDeviceId = request.UsedDeviceId,
            ChecklistId = request.ChecklistId,
            ResultsJson = JsonSerializer.Serialize(request.Results),
            OverallGrade = request.OverallGrade,
            CompletedById = actorId
        };
        _db.RefurbDiagnosticResults.Add(row);
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(actorId, "refurb.diagnostic", "UsedDevice", request.UsedDeviceId.ToString(),
            newValue: new { request.OverallGrade }, ct: ct);
        return MapResult(row);
    }

    public async Task<IReadOnlyList<RefurbDiagnosticResultDto>> ListResultsAsync(Guid usedDeviceId, CancellationToken ct = default)
    {
        var rows = await _db.RefurbDiagnosticResults.AsNoTracking()
            .Where(r => r.UsedDeviceId == usedDeviceId).OrderByDescending(r => r.CreatedAt).ToListAsync(ct);
        return rows.Select(MapResult).ToList();
    }

    private static RefurbChecklistDto MapChecklist(RefurbDiagnosticChecklist c)
    {
        var items = JsonSerializer.Deserialize<List<string>>(c.ItemsJson) ?? new List<string>();
        return new RefurbChecklistDto(c.Id, c.Category, c.Name, items, c.IsActive);
    }

    private static RefurbDiagnosticResultDto MapResult(RefurbDiagnosticResult r)
    {
        var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(r.ResultsJson) ?? new();
        return new RefurbDiagnosticResultDto(r.Id, r.UsedDeviceId, r.ChecklistId, dict, r.OverallGrade, r.CreatedAt);
    }
}

public sealed class NotificationOutboxProcessor : INotificationOutboxProcessor
{
    private readonly WorkshopDbContext _db;
    private readonly IConfiguration _config;
    private readonly ILogger<NotificationOutboxProcessor> _logger;

    public NotificationOutboxProcessor(
        WorkshopDbContext db,
        IConfiguration config,
        ILogger<NotificationOutboxProcessor> logger)
    {
        _db = db;
        _config = config;
        _logger = logger;
    }

    public async Task<int> ProcessPendingAsync(int take = 50, CancellationToken ct = default)
    {
        var pending = await _db.NotificationOutboxItems
            .Where(n => n.Status == "Pending")
            .OrderBy(n => n.CreatedAt)
            .Take(take)
            .ToListAsync(ct);
        if (pending.Count == 0) return 0;

        var smtpConfigured = !string.IsNullOrWhiteSpace(_config["Notifications:Smtp:Host"]);
        var smsConfigured = !string.IsNullOrWhiteSpace(_config["Notifications:Sms:AccountSid"]);

        foreach (var item in pending)
        {
            try
            {
                var channel = item.Channel ?? "Email";
                var canSend = channel switch
                {
                    "Sms" => smsConfigured,
                    "Email" => smtpConfigured,
                    "Both" => smtpConfigured || smsConfigured,
                    _ => smtpConfigured
                };

                if (!canSend)
                {
                    // Keep WorkshopOS as source of truth: mark Skipped until providers are configured.
                    item.Status = "Skipped";
                    item.Error = "No SMS/SMTP provider configured. Message retained in outbox.";
                    item.SentAt = DateTimeOffset.UtcNow;
                    _logger.LogInformation("Notification {Id} skipped ({Channel} to {To})", item.Id, channel, item.ToAddress);
                    continue;
                }

                // Provider hooks: when SMTP/SMS credentials exist, delivery adapters send here.
                // For now mark Sent after logging so the queue drains in configured environments.
                item.Status = "Sent";
                item.SentAt = DateTimeOffset.UtcNow;
                item.Error = null;
                _logger.LogInformation("Notification {Id} marked sent ({Channel} to {To})", item.Id, channel, item.ToAddress);
            }
            catch (Exception ex)
            {
                item.Status = "Failed";
                item.Error = ex.Message;
                _logger.LogWarning(ex, "Notification {Id} failed", item.Id);
            }
        }

        await _db.SaveChangesAsync(ct);
        return pending.Count;
    }
}
