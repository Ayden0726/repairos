using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WorkshopOS.Application.Abstractions;
using WorkshopOS.Contracts.Operations;
using WorkshopOS.Domain.Entities;
using WorkshopOS.Infrastructure.Persistence;

namespace WorkshopOS.Infrastructure.Services;

public sealed class UserPreferenceService : IUserPreferenceService
{
    public const string DashboardLayoutKey = "dashboard.layout";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    private readonly WorkshopDbContext _db;

    public UserPreferenceService(WorkshopDbContext db) => _db = db;

    public async Task<DashboardLayoutDto> GetDashboardLayoutAsync(Guid userId, CancellationToken ct = default)
    {
        var row = await _db.UserUiPreferences.AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId && p.Key == DashboardLayoutKey, ct);
        if (row is null || string.IsNullOrWhiteSpace(row.JsonValue))
            return DefaultLayout();

        try
        {
            var stored = JsonSerializer.Deserialize<StoredLayout>(row.JsonValue, JsonOpts);
            return Normalize(stored?.Widgets);
        }
        catch
        {
            return DefaultLayout();
        }
    }

    public async Task<DashboardLayoutDto> SaveDashboardLayoutAsync(
        Guid userId, UpdateDashboardLayoutRequest request, CancellationToken ct = default)
    {
        var layout = Normalize(request.Widgets);
        var json = JsonSerializer.Serialize(new StoredLayout(layout.Widgets), JsonOpts);

        var row = await _db.UserUiPreferences
            .FirstOrDefaultAsync(p => p.UserId == userId && p.Key == DashboardLayoutKey, ct);
        if (row is null)
        {
            _db.UserUiPreferences.Add(new UserUiPreference
            {
                UserId = userId,
                Key = DashboardLayoutKey,
                JsonValue = json,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        }
        else
        {
            row.JsonValue = json;
            row.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
        return layout;
    }

    public static DashboardLayoutDto DefaultLayout() =>
        Normalize(DashboardWidgetIds.All.Select((id, i) =>
            new DashboardWidgetPreferenceDto(id, DashboardWidgetIds.DisplayName(id), true, i)).ToList());

    private static DashboardLayoutDto Normalize(IReadOnlyList<DashboardWidgetPreferenceDto>? incoming)
    {
        var knownIds = new HashSet<string>(DashboardWidgetIds.All, StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var ordered = new List<DashboardWidgetPreferenceDto>();

        foreach (var w in (incoming ?? Array.Empty<DashboardWidgetPreferenceDto>())
                     .OrderBy(x => x.Order)
                     .ThenBy(x => x.Id, StringComparer.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(w.Id) || !knownIds.Contains(w.Id) || !seen.Add(w.Id))
                continue;
            ordered.Add(new DashboardWidgetPreferenceDto(
                w.Id, DashboardWidgetIds.DisplayName(w.Id), w.Visible, ordered.Count));
        }

        foreach (var id in DashboardWidgetIds.All)
        {
            if (seen.Contains(id))
                continue;
            ordered.Add(new DashboardWidgetPreferenceDto(
                id, DashboardWidgetIds.DisplayName(id), true, ordered.Count));
        }

        if (ordered.All(w => !w.Visible))
        {
            ordered = ordered
                .Select((w, i) => w with
                {
                    Visible = w.Id == DashboardWidgetIds.SummaryCards || w.Id == DashboardWidgetIds.Calendar,
                    Order = i
                })
                .ToList();
        }

        return new DashboardLayoutDto(ordered);
    }

    private sealed record StoredLayout(IReadOnlyList<DashboardWidgetPreferenceDto>? Widgets);
}
