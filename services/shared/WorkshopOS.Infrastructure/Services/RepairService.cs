using Microsoft.EntityFrameworkCore;
using WorkshopOS.Application.Abstractions;
using WorkshopOS.Application.Common;
using WorkshopOS.Contracts.Workshop;
using WorkshopOS.Domain.Entities;
using WorkshopOS.Domain.Enums;
using WorkshopOS.Infrastructure.Persistence;

namespace WorkshopOS.Infrastructure.Services;

public sealed class RepairService : IRepairService
{
    private readonly WorkshopDbContext _db;
    private readonly IAuditService _audit;
    private readonly SecretProtector _secrets;
    private readonly IQaService _qa;

    public RepairService(WorkshopDbContext db, IAuditService audit, SecretProtector secrets, IQaService qa)
    {
        _db = db;
        _audit = audit;
        _secrets = secrets;
        _qa = qa;
    }

    public async Task<PagedResult<RepairListItemDto>> ListAsync(string? q, string? statusKey, string? priorityKey, Guid? assignedToId, bool? overdueOnly, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var now = DateTimeOffset.UtcNow;
        var query = _db.RepairTickets.AsNoTracking()
            .Include(r => r.Customer)
            .Include(r => r.Device)
            .Include(r => r.Type)
            .Include(r => r.Status)
            .Include(r => r.Priority)
            .Include(r => r.AssignedTo)
            .Where(r => r.ArchivedAt == null);

        if (!string.IsNullOrWhiteSpace(statusKey))
            query = query.Where(r => r.Status.Key == statusKey);
        if (!string.IsNullOrWhiteSpace(priorityKey))
            query = query.Where(r => r.Priority.Key == priorityKey);
        if (assignedToId is Guid tech)
            query = query.Where(r => r.AssignedToId == tech);
        if (overdueOnly == true)
            query = query.Where(r => r.DueAt != null && r.DueAt < now && !r.Status.IsCompleted && !r.Status.IsCancelled);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLowerInvariant();
            query = query.Where(r =>
                r.TicketNumber.ToLower().Contains(term) ||
                r.ReportedIssue.ToLower().Contains(term) ||
                r.Customer.DisplayName.ToLower().Contains(term) ||
                (r.Device != null && (r.Device.Brand + " " + r.Device.Model).ToLower().Contains(term)) ||
                (r.Device != null && r.Device.Serial != null && r.Device.Serial.ToLower().Contains(term)) ||
                (r.Device != null && r.Device.Imei != null && r.Device.Imei.ToLower().Contains(term)));
        }

        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedResult<RepairListItemDto>(rows.Select(CustomerService.MapRepairList).ToList(), total, page, pageSize);
    }

    public async Task<RepairDetailDto> GetAsync(Guid id, bool canViewCredentials, CancellationToken ct = default)
    {
        var r = await LoadTicketAsync(id, ct);
        return MapDetail(r, canViewCredentials);
    }

    public async Task<RepairDetailDto> CreateAsync(CreateRepairRequest request, Guid actorId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.ReportedIssue))
            throw new ValidationAppException("Reported issue is required.");

        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == request.CustomerId && c.ArchivedAt == null, ct)
            ?? throw new AppException("not_found", "Customer was not found.", 404);

        Guid? deviceId = request.DeviceId;
        if (deviceId is null && !string.IsNullOrWhiteSpace(request.NewDeviceBrand) && !string.IsNullOrWhiteSpace(request.NewDeviceModel))
        {
            var device = new Device
            {
                CustomerId = customer.Id,
                Category = request.NewDeviceCategory ?? WorkshopOS.Contracts.Workshop.DeviceCategory.Other,
                Brand = request.NewDeviceBrand.Trim(),
                Model = request.NewDeviceModel.Trim(),
                Serial = string.IsNullOrWhiteSpace(request.NewDeviceSerial) ? null : request.NewDeviceSerial.Trim(),
                Imei = string.IsNullOrWhiteSpace(request.NewDeviceImei) ? null : request.NewDeviceImei.Trim()
            };
            _db.Devices.Add(device);
            await _db.SaveChangesAsync(ct);
            deviceId = device.Id;
        }
        else if (deviceId is Guid did)
        {
            _ = await _db.Devices.FirstOrDefaultAsync(d => d.Id == did && d.CustomerId == customer.Id && d.ArchivedAt == null, ct)
                ?? throw new ValidationAppException("Device does not belong to that customer.");
        }

        var type = request.TypeId is Guid tid
            ? await _db.RepairTypes.FirstAsync(t => t.Id == tid, ct)
            : await _db.RepairTypes.OrderBy(t => t.SortOrder).FirstAsync(ct);
        var priority = request.PriorityId is Guid pid
            ? await _db.RepairPriorities.FirstAsync(p => p.Id == pid, ct)
            : await _db.RepairPriorities.FirstAsync(p => p.Key == "normal", ct);
        var status = await _db.RepairStatuses.FirstAsync(s => s.Key == "received", ct);
        var number = await NextTicketNumberAsync(type.Prefix, ct);

        var ticket = new RepairTicket
        {
            TicketNumber = number,
            CustomerId = customer.Id,
            DeviceId = deviceId,
            TypeId = type.Id,
            StatusId = status.Id,
            PriorityId = priority.Id,
            AssignedToId = request.AssignedToId,
            CreatedById = actorId,
            LocationId = customer.LocationId,
            ReportedIssue = request.ReportedIssue.Trim(),
            DamageDescription = request.DamageDescription?.Trim(),
            HasExistingCracks = request.HasExistingCracks,
            HasScratches = request.HasScratches,
            WaterDamageIndicators = request.WaterDamageIndicators,
            PowersOn = request.PowersOn,
            AccessoriesIncluded = request.AccessoriesIncluded,
            ChargerIncluded = request.ChargerIncluded,
            SimIncluded = request.SimIncluded,
            CaseIncluded = request.CaseIncluded,
            EstimatedPrice = request.EstimatedPrice,
            DepositAmount = request.DepositAmount,
            DueAt = request.DueAt,
            StartedAt = DateTimeOffset.UtcNow
        };
        if (!string.IsNullOrWhiteSpace(request.Passcode))
        {
            ticket.PasscodeEnc = _secrets.Encrypt(request.Passcode.Trim());
            ticket.PasscodeHint = "Stored";
        }

        _db.RepairTickets.Add(ticket);
        customer.LastVisitAt = DateTimeOffset.UtcNow;

        var selections = BuildServiceSelections(request);
        if (selections.Count > 0)
        {
            var ids = selections.Select(s => s.ServicePricingId).Distinct().ToList();
            var services = await _db.ServicePricings.Where(s => ids.Contains(s.Id) && s.ArchivedAt == null)
                .ToDictionaryAsync(s => s.Id, ct);
            var order = 0;
            decimal labourSum = 0m;
            var names = new List<string>();
            foreach (var sel in selections)
            {
                if (!services.TryGetValue(sel.ServicePricingId, out var svc)) continue;
                order += 10;
                var labour = sel.LabourOverride ?? svc.DefaultLabourFee + svc.ServiceFee;
                labourSum += labour;
                names.Add(svc.Name);
                _db.RepairServiceLines.Add(new RepairServiceLine
                {
                    RepairTicketId = ticket.Id,
                    ServicePricingId = svc.Id,
                    ServiceName = svc.Name,
                    Code = svc.Code,
                    LabourFee = labour,
                    ServiceFee = svc.ServiceFee,
                    EstimatedMinutes = svc.EstimatedMinutes,
                    WarrantyDays = svc.WarrantyDays,
                    Notes = sel.Notes,
                    PartsJson = sel.PartsJson,
                    SortOrder = order
                });
            }
            if (ticket.EstimatedPrice is null && labourSum > 0m)
                ticket.EstimatedPrice = labourSum;
            if (names.Count > 0 && string.IsNullOrWhiteSpace(ticket.RecommendedRepair))
                ticket.RecommendedRepair = string.Join(", ", names);
        }

        await _db.SaveChangesAsync(ct);
        await AddEventAsync(ticket.Id, actorId, "repair.created", $"Repair {ticket.TicketNumber} created", null, ticket.StatusId.ToString(), ct);
        if (request.AssignedToId is Guid assign)
            await AddEventAsync(ticket.Id, actorId, "repair.assigned", "Technician assigned", null, assign.ToString(), ct);
        await _audit.WriteAsync(actorId, "repair.create", "RepairTicket", ticket.Id.ToString(), newValue: new { ticket.TicketNumber }, ct: ct);
        return await GetAsync(ticket.Id, true, ct);
    }

    private static List<RepairServiceSelectionDto> BuildServiceSelections(CreateRepairRequest request)
    {
        var list = new List<RepairServiceSelectionDto>();
        if (request.SelectedServices is { Count: > 0 })
            list.AddRange(request.SelectedServices);
        if (request.ServicePricingIds is { Count: > 0 })
        {
            var existing = list.Select(s => s.ServicePricingId).ToHashSet();
            foreach (var id in request.ServicePricingIds)
            {
                if (existing.Add(id))
                    list.Add(new RepairServiceSelectionDto(id));
            }
        }
        return list;
    }

    public async Task<RepairDetailDto> ChangeStatusAsync(Guid id, Guid statusId, Guid actorId, CancellationToken ct = default)
    {
        var ticket = await _db.RepairTickets.Include(t => t.Status).FirstOrDefaultAsync(t => t.Id == id && t.ArchivedAt == null, ct)
            ?? throw new AppException("not_found", "Repair was not found.", 404);
        var status = await _db.RepairStatuses.FirstOrDefaultAsync(s => s.Id == statusId, ct)
            ?? throw new ValidationAppException("Invalid status.");
        var old = ticket.Status.Name;
        ticket.StatusId = status.Id;
        ticket.UpdatedAt = DateTimeOffset.UtcNow;
        if (status.IsCompleted) ticket.CompletedAt ??= DateTimeOffset.UtcNow;
        if (status.Key == "ready_pickup") ticket.CompletedAt ??= DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        if (status.Key is "testing" or "ready_pickup")
            await _qa.EnsureDefaultChecklistAsync(ticket.Id, ct);
        await AddEventAsync(ticket.Id, actorId, "repair.status", $"Status changed to {status.Name}", old, status.Name, ct);
        await _audit.WriteAsync(actorId, "repair.status", "RepairTicket", ticket.Id.ToString(), oldValue: old, newValue: status.Name, ct: ct);
        return await GetAsync(id, true, ct);
    }

    public async Task<RepairDetailDto> ChangePriorityAsync(Guid id, Guid priorityId, Guid actorId, CancellationToken ct = default)
    {
        var ticket = await _db.RepairTickets.Include(t => t.Priority).FirstOrDefaultAsync(t => t.Id == id && t.ArchivedAt == null, ct)
            ?? throw new AppException("not_found", "Repair was not found.", 404);
        var priority = await _db.RepairPriorities.FirstOrDefaultAsync(p => p.Id == priorityId, ct)
            ?? throw new ValidationAppException("Invalid priority.");
        var old = ticket.Priority.Name;
        ticket.PriorityId = priority.Id;
        ticket.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        await AddEventAsync(ticket.Id, actorId, "repair.priority", $"Priority changed to {priority.Name}", old, priority.Name, ct);
        await _audit.WriteAsync(actorId, "repair.priority", "RepairTicket", ticket.Id.ToString(), oldValue: old, newValue: priority.Name, ct: ct);
        return await GetAsync(id, true, ct);
    }

    public async Task<string> BuildPrintHtmlAsync(Guid id, CancellationToken ct = default)
    {
        var r = await LoadTicketAsync(id, ct);
        static string Esc(string? s) => System.Net.WebUtility.HtmlEncode(s ?? "");
        var accessories = string.Join(", ", new[]
        {
            r.ChargerIncluded ? "Charger" : null,
            r.SimIncluded ? "SIM" : null,
            r.CaseIncluded ? "Case" : null,
            r.AccessoriesIncluded ? "Other accessories" : null
        }.Where(x => x is not null));
        var notesHtml = r.Notes.Count == 0
            ? "<li>None</li>"
            : string.Join("", r.Notes.Take(8).Select(n =>
                "<li><strong>" + Esc(n.Author.DisplayName) + "</strong> (" + Esc(n.CreatedAt.ToLocalTime().ToString("g")) + ")" +
                (n.IsInternal ? " · internal" : "") + ": " + Esc(n.Body) + "</li>"));

        var deviceLabel = r.Device is null ? "—" : $"{r.Device.Brand} {r.Device.Model}".Trim();
        var serialLine = r.Device?.Serial is null ? "" : "<div>Serial: " + Esc(r.Device.Serial) + "</div>";
        var due = r.DueAt?.ToLocalTime().ToString("g") ?? "—";
        var acc = string.IsNullOrWhiteSpace(accessories) ? "None noted" : accessories;

        var sb = new System.Text.StringBuilder();
        sb.Append("<!DOCTYPE html><html><head><meta charset=\"utf-8\"/><title>")
          .Append(Esc(r.TicketNumber)).Append("</title><style>")
          .Append("body{font-family:Segoe UI,Arial,sans-serif;margin:24px;color:#111}")
          .Append("h1{margin:0 0 4px;font-size:22px}.meta{color:#444;margin-bottom:16px}")
          .Append(".box{border:1px solid #ccc;border-radius:8px;padding:12px;margin:12px 0}")
          .Append(".label{font-size:11px;text-transform:uppercase;letter-spacing:.04em;color:#666}")
          .Append("@media print{button{display:none}}")
          .Append("</style></head><body>")
          .Append("<button onclick=\"window.print()\">Print</button>")
          .Append("<h1>").Append(Esc(r.TicketNumber)).Append("</h1>")
          .Append("<div class=\"meta\">").Append(Esc(r.Customer.DisplayName)).Append(" · ")
          .Append(Esc(r.Type.Name)).Append(" · ").Append(Esc(r.Status.Name)).Append(" · ")
          .Append(Esc(r.Priority.Name)).Append("</div>")
          .Append("<div class=\"box\"><div class=\"label\">Device</div>").Append(Esc(deviceLabel))
          .Append(serialLine).Append("</div>")
          .Append("<div class=\"box\"><div class=\"label\">Reported fault</div><pre style=\"white-space:pre-wrap;font-family:inherit;margin:0\">")
          .Append(Esc(r.ReportedIssue)).Append("</pre></div>")
          .Append("<div class=\"box\"><div class=\"label\">Diagnosis / recommended</div><div>")
          .Append(Esc(r.Diagnosis ?? "—")).Append("</div><div>")
          .Append(Esc(r.RecommendedRepair ?? "")).Append("</div></div>");
        if (r.ServiceLines.Count > 0)
        {
            sb.Append("<div class=\"box\"><div class=\"label\">Services</div><ul>");
            foreach (var line in r.ServiceLines)
            {
                sb.Append("<li>").Append(Esc(line.ServiceName));
                if (line.WarrantyDays is int w && w > 0)
                    sb.Append(" — warranty ").Append(w).Append(" days");
                if (line.IsCompleted) sb.Append(" ✓");
                sb.Append("</li>");
            }
            sb.Append("</ul></div>");
        }
        sb.Append("<div class=\"box\"><div class=\"label\">Assigned / due</div>Tech: ")
          .Append(Esc(r.AssignedTo?.DisplayName ?? "Unassigned")).Append("<br/>Due: ")
          .Append(Esc(due)).Append("<br/>Accessories: ").Append(Esc(acc)).Append("</div>")
          .Append("<div class=\"box\"><div class=\"label\">Notes</div><ul>").Append(notesHtml).Append("</ul></div>")
          .Append("<script>window.onload=function(){setTimeout(function(){window.print()},300);}</script>")
          .Append("</body></html>");
        return sb.ToString();
    }

    public async Task<RepairDetailDto> AssignAsync(Guid id, Guid? assignedToId, Guid actorId, CancellationToken ct = default)
    {
        var ticket = await _db.RepairTickets.Include(t => t.AssignedTo).FirstOrDefaultAsync(t => t.Id == id && t.ArchivedAt == null, ct)
            ?? throw new AppException("not_found", "Repair was not found.", 404);
        if (assignedToId is Guid techId)
            _ = await _db.Users.FirstOrDefaultAsync(u => u.Id == techId && u.ArchivedAt == null, ct)
                ?? throw new ValidationAppException("Technician was not found.");
        var old = ticket.AssignedTo?.DisplayName;
        ticket.AssignedToId = assignedToId;
        ticket.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _db.Entry(ticket).Reference(t => t.AssignedTo).LoadAsync(ct);
        await AddEventAsync(ticket.Id, actorId, "repair.assigned",
            assignedToId is null ? "Unassigned" : $"Assigned to {ticket.AssignedTo!.DisplayName}",
            old, ticket.AssignedTo?.DisplayName, ct);
        return await GetAsync(id, true, ct);
    }

    public async Task<RepairNoteDto> AddNoteAsync(Guid id, AddNoteRequest request, Guid actorId, bool canInternal, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Body)) throw new ValidationAppException("Note body is required.");
        if (request.IsInternal && !canInternal) throw new ForbiddenAppException();
        _ = await _db.RepairTickets.FirstOrDefaultAsync(t => t.Id == id && t.ArchivedAt == null, ct)
            ?? throw new AppException("not_found", "Repair was not found.", 404);
        var note = new RepairNote
        {
            TicketId = id,
            AuthorId = actorId,
            Body = request.Body.Trim(),
            IsInternal = request.IsInternal
        };
        _db.RepairNotes.Add(note);
        await _db.SaveChangesAsync(ct);
        await _db.Entry(note).Reference(n => n.Author).LoadAsync(ct);
        await AddEventAsync(id, actorId, "repair.note", request.IsInternal ? "Internal note added" : "Customer note added", null, null, ct);
        return new RepairNoteDto(note.Id, note.Body, note.IsInternal, note.Author.DisplayName, note.CreatedAt);
    }

    public async Task<RepairDetailDto> UpdateDiagnosisAsync(Guid id, UpdateDiagnosisRequest request, Guid actorId, CancellationToken ct = default)
    {
        var ticket = await _db.RepairTickets.FirstOrDefaultAsync(t => t.Id == id && t.ArchivedAt == null, ct)
            ?? throw new AppException("not_found", "Repair was not found.", 404);
        ticket.Diagnosis = request.Diagnosis?.Trim();
        ticket.RecommendedRepair = request.RecommendedRepair?.Trim();
        ticket.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        await AddEventAsync(id, actorId, "repair.diagnosis", "Diagnosis updated", null, null, ct);
        return await GetAsync(id, true, ct);
    }

    public async Task<RepairDetailDto> CompleteServiceLineAsync(Guid repairId, Guid lineId, bool isCompleted, Guid actorId, CancellationToken ct = default)
    {
        _ = await _db.RepairTickets.FirstOrDefaultAsync(t => t.Id == repairId && t.ArchivedAt == null, ct)
            ?? throw new AppException("not_found", "Repair was not found.", 404);
        var line = await _db.RepairServiceLines.FirstOrDefaultAsync(l => l.Id == lineId && l.RepairTicketId == repairId && l.ArchivedAt == null, ct)
            ?? throw new AppException("not_found", "Service line was not found.", 404);
        line.IsCompleted = isCompleted;
        line.CompletedAt = isCompleted ? DateTimeOffset.UtcNow : null;
        line.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        await AddEventAsync(repairId, actorId, "repair.service_line",
            isCompleted ? $"Completed service: {line.ServiceName}" : $"Reopened service: {line.ServiceName}",
            null, line.ServiceName, ct);
        return await GetAsync(repairId, true, ct);
    }

    public async Task<RepairLookupsDto> GetLookupsAsync(CancellationToken ct = default)
    {
        var statuses = await _db.RepairStatuses.Where(s => s.ArchivedAt == null).OrderBy(s => s.SortOrder)
            .Select(s => new LookupDto(s.Id, s.Key, s.Name, s.Colour)).ToListAsync(ct);
        var types = await _db.RepairTypes.Where(t => t.ArchivedAt == null).OrderBy(t => t.SortOrder)
            .Select(t => new LookupDto(t.Id, t.Key, t.Name, t.Prefix)).ToListAsync(ct);
        var priorities = await _db.RepairPriorities.Where(p => p.ArchivedAt == null).OrderBy(p => p.SortOrder)
            .Select(p => new LookupDto(p.Id, p.Key, p.Name, null)).ToListAsync(ct);
        var techs = await _db.Users.Where(u => u.ArchivedAt == null && u.Status == UserStatus.Active)
            .OrderBy(u => u.DisplayName)
            .Select(u => new StaffLookupDto(u.Id, u.DisplayName, u.Email)).ToListAsync(ct);
        return new RepairLookupsDto(statuses, types, priorities, techs);
    }

    private async Task<string> NextTicketNumberAsync(string prefix, CancellationToken ct)
    {
        var year = DateTime.UtcNow.Year;
        var key = $"ticket:{prefix}:{year}";
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        var seq = await _db.DocumentSequences.FirstOrDefaultAsync(s => s.Key == key, ct);
        if (seq is null)
        {
            seq = new DocumentSequence { Key = key, Year = year, NextValue = 1 };
            _db.DocumentSequences.Add(seq);
        }
        var value = seq.NextValue;
        seq.NextValue++;
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return $"{prefix}-{year}-{value:D5}";
    }

    private async Task AddEventAsync(Guid ticketId, Guid? actorId, string type, string summary, string? oldValue, string? newValue, CancellationToken ct)
    {
        _db.RepairEvents.Add(new RepairEvent
        {
            TicketId = ticketId,
            ActorUserId = actorId,
            EventType = type,
            Summary = summary,
            OldValue = oldValue,
            NewValue = newValue
        });
        await _db.SaveChangesAsync(ct);
    }

    private async Task<RepairTicket> LoadTicketAsync(Guid id, CancellationToken ct) =>
        await _db.RepairTickets.AsNoTracking()
            .Include(r => r.Customer)
            .Include(r => r.Device)
            .Include(r => r.Type)
            .Include(r => r.Status)
            .Include(r => r.Priority)
            .Include(r => r.AssignedTo)
            .Include(r => r.Events.OrderByDescending(e => e.CreatedAt))
            .Include(r => r.Notes.OrderByDescending(n => n.CreatedAt)).ThenInclude(n => n.Author)
            .Include(r => r.ServiceLines.Where(l => l.ArchivedAt == null).OrderBy(l => l.SortOrder))
            .FirstOrDefaultAsync(r => r.Id == id && r.ArchivedAt == null, ct)
        ?? throw new AppException("not_found", "Repair was not found.", 404);

    private static RepairDetailDto MapDetail(RepairTicket r, bool canViewCredentials) =>
        new(
            r.Id, r.TicketNumber, r.CustomerId, r.Customer.DisplayName, r.DeviceId,
            r.Device is null ? null : $"{r.Device.Brand} {r.Device.Model}".Trim(),
            r.TypeId, r.Type.Name, r.StatusId, r.Status.Key, r.Status.Name, r.Status.Colour,
            r.PriorityId, r.Priority.Name, r.AssignedToId, r.AssignedTo?.DisplayName,
            r.ReportedIssue, r.Diagnosis, r.RecommendedRepair, r.DamageDescription,
            r.HasExistingCracks, r.HasScratches, r.WaterDamageIndicators, r.PowersOn,
            r.AccessoriesIncluded, r.ChargerIncluded, r.SimIncluded, r.CaseIncluded,
            canViewCredentials && !string.IsNullOrEmpty(r.PasscodeEnc),
            r.EstimatedPrice, r.DepositAmount, r.CreatedAt, r.DueAt, r.StartedAt, r.CompletedAt,
            r.Events.Select(e => new RepairEventDto(e.Id, e.EventType, e.Summary, e.OldValue, e.NewValue, e.CreatedAt, e.ActorUserId)).ToList(),
            r.Notes.Select(n => new RepairNoteDto(n.Id, n.Body, n.IsInternal, n.Author.DisplayName, n.CreatedAt)).ToList(),
            r.ServiceLines.Select(l => new RepairServiceLineDto(
                l.Id, l.ServicePricingId, l.ServiceName, l.Code, l.LabourFee, l.ServiceFee,
                l.EstimatedMinutes, l.WarrantyDays, l.Notes, l.PartsJson, l.IsCompleted, l.CompletedAt, l.SortOrder)).ToList());
}
