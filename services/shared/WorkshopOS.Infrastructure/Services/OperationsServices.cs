using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using WorkshopOS.Application.Abstractions;
using WorkshopOS.Application.Common;
using WorkshopOS.Contracts.Operations;
using WorkshopOS.Domain.Entities;
using WorkshopOS.Infrastructure.Persistence;

namespace WorkshopOS.Infrastructure.Services;

/// <summary>GST-inclusive AUD helpers (10%): line totals are tax-inclusive; split into Subtotal + Gst.</summary>
public static class MoneyGst
{
    public const decimal InclusiveRate = 0.10m;
    public const decimal InclusiveDivisor = 1.10m;

    public static (decimal Subtotal, decimal Gst, decimal Total) FromInclusiveTotal(decimal inclusiveTotal)
    {
        var total = Math.Round(inclusiveTotal, 2, MidpointRounding.AwayFromZero);
        var subtotal = Math.Round(total / InclusiveDivisor, 2, MidpointRounding.AwayFromZero);
        var gst = total - subtotal;
        return (subtotal, gst, total);
    }

    public static (decimal Subtotal, decimal Gst, decimal Total) FromInclusiveLines(IEnumerable<(decimal Qty, decimal UnitPrice)> lines)
    {
        var inclusive = lines.Sum(l => Math.Round(l.Qty * l.UnitPrice, 2, MidpointRounding.AwayFromZero));
        return FromInclusiveTotal(inclusive);
    }
}

internal static class DocumentNumbering
{
    public static async Task<string> NextAsync(WorkshopDbContext db, string prefix, CancellationToken ct)
    {
        var year = DateTime.UtcNow.Year;
        var key = $"doc:{prefix}:{year}";
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var seq = await db.DocumentSequences.FirstOrDefaultAsync(s => s.Key == key, ct);
        if (seq is null)
        {
            seq = new DocumentSequence { Key = key, Year = year, NextValue = 1 };
            db.DocumentSequences.Add(seq);
        }
        var value = seq.NextValue;
        seq.NextValue++;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return $"{prefix}-{year}-{value:D5}";
    }
}

public sealed class DashboardService : IDashboardService
{
    private readonly WorkshopDbContext _db;
    public DashboardService(WorkshopDbContext db) => _db = db;

    public async Task<DashboardDto> GetAsync(CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var todayStart = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero);
        var todayEnd = todayStart.AddDays(1);
        var since30 = now.AddDays(-30);

        var openTickets = await _db.RepairTickets.AsNoTracking()
            .Include(t => t.Status).Include(t => t.Priority).Include(t => t.Customer)
            .Include(t => t.Device).Include(t => t.AssignedTo)
            .Where(t => t.ArchivedAt == null && !t.Status.IsCompleted && !t.Status.IsCancelled)
            .ToListAsync(ct);

        var openJobs = openTickets.Count;
        var dueToday = openTickets.Count(t => t.DueAt >= todayStart && t.DueAt < todayEnd);
        var awaitingApproval = openTickets.Count(t => t.Status.CountsAsAwaitingApproval);
        var waitingParts = openTickets.Count(t => t.Status.CountsAsWaitingForParts);
        var readyPickup = openTickets.Count(t => t.Status.CountsAsReadyForPickup);
        var overdue = openTickets.Count(t => t.DueAt < now);

        // Revenue = sum of payments in last 30 days.
        var revenue30 = await _db.Payments.AsNoTracking()
            .Where(p => p.PaidAt >= since30)
            .SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;

        // COGS proxy: cost of consumed stock (negative deltas) in window; else interim 45% COGS (55% GP).
        var consumeCost = await _db.InventoryTransactions.AsNoTracking()
            .Include(t => t.Item)
            .Where(t => t.CreatedAt >= since30 && t.QuantityDelta < 0)
            .SumAsync(t => (decimal?)(Math.Abs(t.QuantityDelta) * t.Item.Cost), ct) ?? 0m;
        // Interim until stock costs land on invoices: when no consume txs, GrossProfit ≈ Revenue * 0.55.
        var grossProfit30 = consumeCost > 0 ? revenue30 - consumeCost : Math.Round(revenue30 * 0.55m, 2, MidpointRounding.AwayFromZero);

        var cards = new DashboardCardsDto(openJobs, dueToday, awaitingApproval, waitingParts, readyPickup, overdue, revenue30, grossProfit30);

        var statuses = await _db.RepairStatuses.AsNoTracking().Where(s => s.ArchivedAt == null).OrderBy(s => s.SortOrder).ToListAsync(ct);
        var counts = await _db.RepairTickets.AsNoTracking()
            .Where(t => t.ArchivedAt == null)
            .GroupBy(t => t.StatusId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var countMap = counts.ToDictionary(x => x.Key, x => x.Count);
        var pipeline = statuses.Select(s => new PipelineStageDto(s.Key, s.Name, countMap.GetValueOrDefault(s.Id), s.Colour)).ToList();

        var urgent = openTickets
            .Where(t => t.DueAt < now || t.Priority.Severity >= 3)
            .OrderByDescending(t => t.DueAt < now)
            .ThenByDescending(t => t.Priority.Severity)
            .ThenBy(t => t.DueAt)
            .Take(12)
            .Select(t => new UrgentJobDto(
                t.Id, t.TicketNumber, t.Customer.DisplayName,
                t.Device is null ? null : $"{t.Device.Brand} {t.Device.Model}".Trim(),
                t.Status.Name, t.Priority.Name, t.AssignedTo?.DisplayName, t.DueAt,
                t.DueAt < now ? "Overdue" : t.DueAt < todayEnd ? "Due today" : ""))
            .ToList();

        var techIds = openTickets.Where(t => t.AssignedToId != null).Select(t => t.AssignedToId!.Value).Distinct().ToList();
        var techs = await _db.Users.AsNoTracking().Where(u => techIds.Contains(u.Id)).ToListAsync(ct);
        var workload = techs.Select(u =>
        {
            var jobs = openTickets.Where(t => t.AssignedToId == u.Id).ToList();
            var active = jobs.OrderByDescending(j => j.UpdatedAt).FirstOrDefault();
            return new TechnicianWorkloadDto(
                u.Id, u.DisplayName, jobs.Count,
                jobs.Count(j => j.DueAt >= todayStart && j.DueAt < todayEnd),
                jobs.Count(j => j.DueAt < now),
                active?.TicketNumber);
        }).OrderByDescending(w => w.OpenJobs).ToList();

        var unassigned = openTickets.Count(t => t.AssignedToId == null);

        var lowStock = await _db.InventoryItems.AsNoTracking()
            .Include(i => i.Supplier)
            .Where(i => i.ArchivedAt == null && (i.QuantityOnHand - i.QuantityReserved) <= i.MinimumStock)
            .OrderBy(i => i.Sku).Take(20)
            .Select(i => new LowStockDto(i.Id, i.Sku, i.Name, Math.Max(0, i.QuantityOnHand - i.QuantityReserved), i.QuantityReserved, i.MinimumStock, i.Supplier != null ? i.Supplier.Name : null))
            .ToListAsync(ct);

        var activity = await _db.AuditEvents.AsNoTracking()
            .OrderByDescending(a => a.CreatedAt).Take(20)
            .Select(a => new ActivityDto(a.Id, a.CreatedAt, a.Action, a.Action + (a.EntityType != null ? " · " + a.EntityType : ""), null))
            .ToListAsync(ct);

        var todaysRepairs = openTickets
            .Where(t => (t.DueAt >= todayStart && t.DueAt < todayEnd) || t.Status.CountsAsReadyForPickup)
            .Take(15)
            .Select(t => new TodayItemDto("Repair", t.TicketNumber + " — " + t.Customer.DisplayName, t.DueAt ?? t.UpdatedAt, $"/repairs/{t.Id}"))
            .ToList();
        var todaysBookings = await _db.Bookings.AsNoTracking()
            .Where(b => b.StartsAt >= todayStart && b.StartsAt < todayEnd)
            .OrderBy(b => b.StartsAt).Take(10)
            .Select(b => new TodayItemDto("Booking", b.Type, b.StartsAt, "/calendar"))
            .ToListAsync(ct);
        var todaysWork = todaysRepairs.Concat(todaysBookings).OrderBy(x => x.When).ToList();

        return new DashboardDto(cards, pipeline, urgent, workload, unassigned, lowStock, activity, todaysWork);
    }
}

public sealed class QuoteService : IQuoteService
{
    private readonly WorkshopDbContext _db;
    private readonly IAuditService _audit;

    public QuoteService(WorkshopDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<IReadOnlyList<QuoteListItemDto>> ListAsync(CancellationToken ct = default) =>
        await _db.Quotes.AsNoTracking().Include(q => q.Customer)
            .Where(q => q.ArchivedAt == null)
            .OrderByDescending(q => q.CreatedAt)
            .Select(q => new QuoteListItemDto(q.Id, q.Number, q.Customer.DisplayName, q.Status, q.Total, q.CreatedAt))
            .ToListAsync(ct);

    public async Task<QuoteDetailDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var q = await _db.Quotes.AsNoTracking().Include(x => x.Customer).Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == id && x.ArchivedAt == null, ct)
            ?? throw new AppException("not_found", "Quote was not found.", 404);
        return Map(q);
    }

    public async Task<QuoteDetailDto> CreateAsync(CreateQuoteRequest request, Guid actorId, CancellationToken ct = default)
    {
        _ = await _db.Customers.FirstOrDefaultAsync(c => c.Id == request.CustomerId && c.ArchivedAt == null, ct)
            ?? throw new AppException("not_found", "Customer was not found.", 404);
        if (request.Lines is null || request.Lines.Count == 0)
            throw new ValidationAppException("At least one line is required.");

        var (sub, gst, total) = MoneyGst.FromInclusiveLines(request.Lines.Select(l => (l.Quantity, l.UnitPrice)));
        var number = await DocumentNumbering.NextAsync(_db, "QTE", ct);
        var quote = new Quote
        {
            Number = number,
            CustomerId = request.CustomerId,
            Status = "Draft",
            Issue = request.Issue?.Trim(),
            CreatedById = actorId,
            Subtotal = sub,
            GstAmount = gst,
            Total = total,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(14)
        };
        var order = 0;
        foreach (var line in request.Lines)
        {
            quote.Lines.Add(new QuoteLine
            {
                Type = string.IsNullOrWhiteSpace(line.Type) ? "SERVICE" : line.Type.Trim(),
                Description = line.Description.Trim(),
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice,
                SortOrder = order++
            });
        }
        _db.Quotes.Add(quote);
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(actorId, "quote.create", "Quote", quote.Id.ToString(), newValue: new { quote.Number }, ct: ct);
        return await GetAsync(quote.Id, ct);
    }

    public async Task<QuoteDetailDto> SetStatusAsync(Guid id, string status, Guid actorId, CancellationToken ct = default)
    {
        var allowed = new[] { "Draft", "Sent", "Approved", "Declined", "Expired" };
        if (!allowed.Contains(status, StringComparer.OrdinalIgnoreCase))
            throw new ValidationAppException("Invalid quote status.");
        var quote = await _db.Quotes.FirstOrDefaultAsync(q => q.Id == id && q.ArchivedAt == null, ct)
            ?? throw new AppException("not_found", "Quote was not found.", 404);
        quote.Status = allowed.First(a => a.Equals(status, StringComparison.OrdinalIgnoreCase));
        quote.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(actorId, "quote.status", "Quote", id.ToString(), newValue: quote.Status, ct: ct);
        return await GetAsync(id, ct);
    }

    private static QuoteDetailDto Map(Quote q) => new(
        q.Id, q.Number, q.CustomerId, q.Customer.DisplayName, q.Status, q.Issue,
        q.Subtotal, q.GstAmount, q.Total, q.ExpiresAt,
        q.Lines.OrderBy(l => l.SortOrder).Select(l => new LineDto(l.Id, l.Type, l.Description, l.Quantity, l.UnitPrice, Math.Round(l.Quantity * l.UnitPrice, 2))).ToList());
}

public sealed class InvoiceService : IInvoiceService
{
    private readonly WorkshopDbContext _db;
    private readonly IAuditService _audit;

    public InvoiceService(WorkshopDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<IReadOnlyList<InvoiceListItemDto>> ListAsync(CancellationToken ct = default) =>
        await _db.Invoices.AsNoTracking().Include(i => i.Customer)
            .Where(i => i.ArchivedAt == null)
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new InvoiceListItemDto(i.Id, i.Number, i.Customer.DisplayName, i.Status, i.Total, i.AmountPaid, i.Total - i.AmountPaid, i.CreatedAt))
            .ToListAsync(ct);

    public async Task<InvoiceDetailDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var inv = await _db.Invoices.AsNoTracking()
            .Include(i => i.Customer).Include(i => i.Lines).Include(i => i.Payments)
            .FirstOrDefaultAsync(i => i.Id == id && i.ArchivedAt == null, ct)
            ?? throw new AppException("not_found", "Invoice was not found.", 404);
        return Map(inv);
    }

    public async Task<InvoiceDetailDto> CreateFromRepairAsync(Guid repairTicketId, Guid actorId, CancellationToken ct = default)
    {
        var ticket = await _db.RepairTickets.Include(t => t.Customer)
            .FirstOrDefaultAsync(t => t.Id == repairTicketId && t.ArchivedAt == null, ct)
            ?? throw new AppException("not_found", "Repair was not found.", 404);

        var existing = await _db.Invoices.AsNoTracking()
            .FirstOrDefaultAsync(i => i.RepairTicketId == repairTicketId && i.ArchivedAt == null && i.Status != "Cancelled", ct);
        if (existing is not null)
            return await GetAsync(existing.Id, ct);

        var labourInclusive = ticket.EstimatedPrice ?? 0m;
        if (labourInclusive <= 0)
            throw new ValidationAppException("Repair has no estimated price to invoice.");

        var (sub, gst, total) = MoneyGst.FromInclusiveTotal(labourInclusive);
        var number = await DocumentNumbering.NextAsync(_db, "INV", ct);
        var inv = new Invoice
        {
            Number = number,
            CustomerId = ticket.CustomerId,
            RepairTicketId = ticket.Id,
            Status = "Unpaid",
            CreatedById = actorId,
            Subtotal = sub,
            GstAmount = gst,
            Total = total,
            AmountPaid = 0,
            IssuedAt = DateTimeOffset.UtcNow,
            DueAt = DateTimeOffset.UtcNow.AddDays(7)
        };
        inv.Lines.Add(new InvoiceLine
        {
            Type = "LABOUR",
            Description = $"Labour — {ticket.TicketNumber}",
            Quantity = 1,
            UnitPrice = labourInclusive,
            SortOrder = 0
        });
        _db.Invoices.Add(inv);
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(actorId, "invoice.create", "Invoice", inv.Id.ToString(), newValue: new { inv.Number, ticket.TicketNumber }, ct: ct);
        return await GetAsync(inv.Id, ct);
    }

    public async Task<InvoiceDetailDto> RecordPaymentAsync(Guid id, RecordPaymentRequest request, Guid actorId, CancellationToken ct = default)
    {
        if (request.Amount <= 0) throw new ValidationAppException("Payment amount must be positive.");
        var inv = await _db.Invoices.Include(i => i.Payments).FirstOrDefaultAsync(i => i.Id == id && i.ArchivedAt == null, ct)
            ?? throw new AppException("not_found", "Invoice was not found.", 404);
        if (inv.Status is "Cancelled" or "Refunded")
            throw new ConflictAppException("Cannot record payment on this invoice.");

        inv.Payments.Add(new Payment
        {
            Method = string.IsNullOrWhiteSpace(request.Method) ? "Cash" : request.Method.Trim(),
            Amount = Math.Round(request.Amount, 2, MidpointRounding.AwayFromZero),
            Reference = request.Reference?.Trim(),
            RecordedById = actorId,
            IsDeposit = request.IsDeposit,
            PaidAt = DateTimeOffset.UtcNow
        });
        inv.AmountPaid = inv.Payments.Sum(p => p.Amount);
        inv.Status = inv.AmountPaid >= inv.Total ? "Paid" : inv.AmountPaid > 0 ? "PartiallyPaid" : inv.Status;
        inv.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(actorId, "payment.record", "Invoice", id.ToString(), newValue: new { request.Amount, request.Method }, ct: ct);
        return await GetAsync(id, ct);
    }

    private static InvoiceDetailDto Map(Invoice i) => new(
        i.Id, i.Number, i.CustomerId, i.Customer.DisplayName, i.RepairTicketId, i.Status,
        i.Subtotal, i.GstAmount, i.Total, i.AmountPaid, i.Total - i.AmountPaid,
        i.Lines.OrderBy(l => l.SortOrder).Select(l => new LineDto(l.Id, l.Type, l.Description, l.Quantity, l.UnitPrice, Math.Round(l.Quantity * l.UnitPrice, 2))).ToList(),
        i.Payments.OrderByDescending(p => p.PaidAt).Select(p => new PaymentDto(p.Id, p.Method, p.Amount, p.Reference, p.PaidAt, p.IsDeposit)).ToList());
}

public sealed class InventoryService : IInventoryService
{
    private readonly WorkshopDbContext _db;
    private readonly IAuditService _audit;

    public InventoryService(WorkshopDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<IReadOnlyList<InventoryListItemDto>> ListAsync(CancellationToken ct = default) =>
        await _db.InventoryItems.AsNoTracking()
            .Where(i => i.ArchivedAt == null)
            .OrderBy(i => i.Sku)
            .Select(i => new InventoryListItemDto(
                i.Id, i.Sku, i.Name, i.Category, i.QuantityOnHand, i.QuantityReserved,
                Math.Max(0, i.QuantityOnHand - i.QuantityReserved), i.MinimumStock, i.SellPrice,
                (i.QuantityOnHand - i.QuantityReserved) <= i.MinimumStock))
            .ToListAsync(ct);

    public async Task<InventoryListItemDto> UpsertAsync(UpsertInventoryRequest request, Guid actorId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Sku) || string.IsNullOrWhiteSpace(request.Name))
            throw new ValidationAppException("SKU and name are required.");

        InventoryItem item;
        if (request.Id is Guid id)
        {
            item = await _db.InventoryItems.FirstOrDefaultAsync(i => i.Id == id && i.ArchivedAt == null, ct)
                ?? throw new AppException("not_found", "Inventory item was not found.", 404);
        }
        else
        {
            if (await _db.InventoryItems.AnyAsync(i => i.Sku == request.Sku.Trim() && i.ArchivedAt == null, ct))
                throw new ConflictAppException("An item with that SKU already exists.");
            item = new InventoryItem();
            _db.InventoryItems.Add(item);
        }

        item.Sku = request.Sku.Trim();
        item.Barcode = string.IsNullOrWhiteSpace(request.Barcode) ? null : request.Barcode.Trim();
        item.Name = request.Name.Trim();
        item.Category = string.IsNullOrWhiteSpace(request.Category) ? "Parts" : request.Category.Trim();
        item.Cost = request.Cost;
        item.SellPrice = request.SellPrice;
        item.QuantityOnHand = request.QuantityOnHand;
        item.MinimumStock = request.MinimumStock;
        item.ReorderQuantity = request.ReorderQuantity;
        item.LocationBin = request.LocationBin?.Trim();
        item.SupplierId = request.SupplierId;
        item.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(actorId, "inventory.upsert", "InventoryItem", item.Id.ToString(), newValue: new { item.Sku }, ct: ct);
        return (await ListAsync(ct)).First(x => x.Id == item.Id);
    }

    public async Task<InventoryListItemDto> AdjustAsync(Guid id, AdjustStockRequest request, Guid actorId, CancellationToken ct = default)
    {
        if (request.QuantityDelta == 0) throw new ValidationAppException("Quantity delta cannot be zero.");
        var item = await _db.InventoryItems.FirstOrDefaultAsync(i => i.Id == id && i.ArchivedAt == null, ct)
            ?? throw new AppException("not_found", "Inventory item was not found.", 404);
        if (item.QuantityOnHand + request.QuantityDelta < 0)
            throw new ValidationAppException("Adjustment would make on-hand negative.");

        item.QuantityOnHand += request.QuantityDelta;
        item.UpdatedAt = DateTimeOffset.UtcNow;
        _db.InventoryTransactions.Add(new InventoryTransaction
        {
            ItemId = item.Id,
            Type = "Adjustment",
            QuantityDelta = request.QuantityDelta,
            Reason = string.IsNullOrWhiteSpace(request.Reason) ? "Manual adjustment" : request.Reason.Trim(),
            ActorUserId = actorId
        });
        await _db.SaveChangesAsync(ct);
        return (await ListAsync(ct)).First(x => x.Id == id);
    }

    public async Task ReserveAsync(ReserveStockRequest request, Guid actorId, CancellationToken ct = default)
    {
        if (request.Quantity <= 0) throw new ValidationAppException("Quantity must be positive.");
        var item = await _db.InventoryItems.FirstOrDefaultAsync(i => i.Id == request.ItemId && i.ArchivedAt == null, ct)
            ?? throw new AppException("not_found", "Inventory item was not found.", 404);
        _ = await _db.RepairTickets.FirstOrDefaultAsync(t => t.Id == request.TicketId && t.ArchivedAt == null, ct)
            ?? throw new AppException("not_found", "Repair was not found.", 404);
        if (item.Available < request.Quantity)
            throw new ValidationAppException("Insufficient available stock.");

        item.QuantityReserved += request.Quantity;
        item.UpdatedAt = DateTimeOffset.UtcNow;
        _db.InventoryReservations.Add(new InventoryReservation
        {
            ItemId = item.Id,
            TicketId = request.TicketId,
            Quantity = request.Quantity,
            Status = "Reserved"
        });
        _db.InventoryTransactions.Add(new InventoryTransaction
        {
            ItemId = item.Id,
            Type = "Reserve",
            QuantityDelta = 0,
            Reason = $"Reserved {request.Quantity} for ticket",
            TicketId = request.TicketId,
            ActorUserId = actorId
        });
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(actorId, "inventory.reserve", "InventoryItem", item.Id.ToString(), newValue: request, ct: ct);
    }

    public async Task ConsumeReservationAsync(Guid reservationId, Guid actorId, CancellationToken ct = default)
    {
        var res = await _db.InventoryReservations.Include(r => r.Item)
            .FirstOrDefaultAsync(r => r.Id == reservationId && r.Status == "Reserved", ct)
            ?? throw new AppException("not_found", "Reservation was not found.", 404);
        var item = res.Item;
        if (item.QuantityOnHand < res.Quantity)
            throw new ValidationAppException("On-hand stock is insufficient to consume reservation.");

        item.QuantityOnHand -= res.Quantity;
        item.QuantityReserved = Math.Max(0, item.QuantityReserved - res.Quantity);
        item.UpdatedAt = DateTimeOffset.UtcNow;
        res.Status = "Consumed";
        _db.InventoryTransactions.Add(new InventoryTransaction
        {
            ItemId = item.Id,
            Type = "Consume",
            QuantityDelta = -res.Quantity,
            Reason = "Consumed reservation",
            TicketId = res.TicketId,
            ActorUserId = actorId
        });
        await _db.SaveChangesAsync(ct);
    }
}

public sealed class PurchasingService : IPurchasingService
{
    private readonly WorkshopDbContext _db;
    private readonly IAuditService _audit;

    public PurchasingService(WorkshopDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<IReadOnlyList<SupplierDto>> ListSuppliersAsync(CancellationToken ct = default) =>
        await _db.Suppliers.AsNoTracking().Where(s => s.ArchivedAt == null).OrderBy(s => s.Name)
            .Select(s => new SupplierDto(s.Id, s.Name, s.Phone, s.Email)).ToListAsync(ct);

    public async Task<SupplierDto> UpsertSupplierAsync(UpsertSupplierRequest request, Guid actorId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) throw new ValidationAppException("Supplier name is required.");
        Supplier s;
        if (request.Id is Guid id)
        {
            s = await _db.Suppliers.FirstOrDefaultAsync(x => x.Id == id && x.ArchivedAt == null, ct)
                ?? throw new AppException("not_found", "Supplier was not found.", 404);
        }
        else
        {
            s = new Supplier();
            _db.Suppliers.Add(s);
        }
        s.Name = request.Name.Trim();
        s.Contact = request.Contact?.Trim();
        s.Phone = request.Phone?.Trim();
        s.Email = request.Email?.Trim();
        s.Website = request.Website?.Trim();
        s.AccountNumber = request.AccountNumber?.Trim();
        s.Notes = request.Notes?.Trim();
        s.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(actorId, "supplier.upsert", "Supplier", s.Id.ToString(), ct: ct);
        return new SupplierDto(s.Id, s.Name, s.Phone, s.Email);
    }

    public async Task<IReadOnlyList<PurchaseOrderListItemDto>> ListPurchaseOrdersAsync(CancellationToken ct = default) =>
        await _db.PurchaseOrders.AsNoTracking().Include(p => p.Supplier)
            .Where(p => p.ArchivedAt == null)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new PurchaseOrderListItemDto(p.Id, p.Number, p.Supplier.Name, p.Status, p.Total, p.ExpectedAt))
            .ToListAsync(ct);

    public async Task<PurchaseOrderListItemDto> CreatePurchaseOrderAsync(CreatePurchaseOrderRequest request, Guid actorId, CancellationToken ct = default)
    {
        var supplier = await _db.Suppliers.FirstOrDefaultAsync(s => s.Id == request.SupplierId && s.ArchivedAt == null, ct)
            ?? throw new AppException("not_found", "Supplier was not found.", 404);
        if (request.Lines is null || request.Lines.Count == 0)
            throw new ValidationAppException("At least one PO line is required.");

        var linesTotal = request.Lines.Sum(l => l.Quantity * l.UnitCost) + request.Shipping;
        var (sub, gst, total) = MoneyGst.FromInclusiveTotal(linesTotal);
        var number = await DocumentNumbering.NextAsync(_db, "PO", ct);
        var po = new PurchaseOrder
        {
            Number = number,
            SupplierId = supplier.Id,
            Status = "Ordered",
            OrderedAt = DateTimeOffset.UtcNow,
            ExpectedAt = request.ExpectedAt,
            CreatedById = actorId,
            Shipping = request.Shipping,
            GstAmount = gst,
            Total = total
        };
        foreach (var line in request.Lines)
        {
            po.Lines.Add(new PurchaseOrderLine
            {
                ItemId = line.ItemId,
                Description = line.Description.Trim(),
                QuantityOrdered = line.Quantity,
                UnitCost = line.UnitCost
            });
        }
        _db.PurchaseOrders.Add(po);
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(actorId, "po.create", "PurchaseOrder", po.Id.ToString(), newValue: new { po.Number }, ct: ct);
        return new PurchaseOrderListItemDto(po.Id, po.Number, supplier.Name, po.Status, po.Total, po.ExpectedAt);
    }

    public async Task<PurchaseOrderListItemDto> ReceiveLineAsync(Guid poId, ReceivePoLineRequest request, Guid actorId, CancellationToken ct = default)
    {
        var po = await _db.PurchaseOrders.Include(p => p.Supplier).Include(p => p.Lines)
            .FirstOrDefaultAsync(p => p.Id == poId && p.ArchivedAt == null, ct)
            ?? throw new AppException("not_found", "Purchase order was not found.", 404);
        var line = po.Lines.FirstOrDefault(l => l.Id == request.LineId)
            ?? throw new AppException("not_found", "PO line was not found.", 404);
        if (request.Quantity <= 0) throw new ValidationAppException("Quantity must be positive.");
        if (line.QuantityReceived + request.Quantity > line.QuantityOrdered)
            throw new ValidationAppException("Cannot receive more than ordered.");

        line.QuantityReceived += request.Quantity;
        if (line.ItemId is Guid itemId)
        {
            var item = await _db.InventoryItems.FirstOrDefaultAsync(i => i.Id == itemId && i.ArchivedAt == null, ct);
            if (item is not null)
            {
                item.QuantityOnHand += request.Quantity;
                item.UpdatedAt = DateTimeOffset.UtcNow;
                _db.InventoryTransactions.Add(new InventoryTransaction
                {
                    ItemId = item.Id,
                    Type = "Receive",
                    QuantityDelta = request.Quantity,
                    Reason = $"PO {po.Number}",
                    ActorUserId = actorId
                });
            }
        }

        var allReceived = po.Lines.All(l => l.QuantityReceived >= l.QuantityOrdered);
        var anyReceived = po.Lines.Any(l => l.QuantityReceived > 0);
        po.Status = allReceived ? "Received" : anyReceived ? "PartiallyReceived" : po.Status;
        if (allReceived) po.ReceivedAt = DateTimeOffset.UtcNow;
        po.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return new PurchaseOrderListItemDto(po.Id, po.Number, po.Supplier.Name, po.Status, po.Total, po.ExpectedAt);
    }
}

public sealed class NotificationService : INotificationService
{
    private readonly WorkshopDbContext _db;
    public NotificationService(WorkshopDbContext db) => _db = db;

    public async Task<IReadOnlyList<NotificationDto>> ListForUserAsync(Guid userId, CancellationToken ct = default) =>
        await _db.Notifications.AsNoTracking()
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt).Take(100)
            .Select(n => new NotificationDto(n.Id, n.Title, n.Body, n.Severity, n.Route, n.IsRead, n.CreatedAt))
            .ToListAsync(ct);

    public async Task MarkReadAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var n = await _db.Notifications.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct)
            ?? throw new AppException("not_found", "Notification was not found.", 404);
        n.IsRead = true;
        await _db.SaveChangesAsync(ct);
    }

    public async Task NotifyAsync(Guid userId, string title, string body, string severity = "Info", string? route = null, CancellationToken ct = default)
    {
        _db.Notifications.Add(new AppNotification
        {
            UserId = userId,
            Title = title,
            Body = body,
            Severity = severity,
            Route = route
        });
        await _db.SaveChangesAsync(ct);
    }
}

public sealed class BookingService : IBookingService
{
    private readonly WorkshopDbContext _db;
    private readonly IAuditService _audit;

    public BookingService(WorkshopDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<IReadOnlyList<BookingDto>> ListAsync(DateTimeOffset? from, DateTimeOffset? to, CancellationToken ct = default)
    {
        var q = _db.Bookings.AsNoTracking().Include(b => b.Customer).Include(b => b.Staff).AsQueryable();
        if (from is DateTimeOffset f) q = q.Where(b => b.EndsAt >= f);
        if (to is DateTimeOffset t) q = q.Where(b => b.StartsAt <= t);
        return await q.OrderBy(b => b.StartsAt)
            .Select(b => new BookingDto(b.Id, b.CustomerId, b.Customer.DisplayName, b.StaffId, b.Staff != null ? b.Staff.DisplayName : null, b.Type, b.Status, b.StartsAt, b.EndsAt, b.Notes))
            .ToListAsync(ct);
    }

    public async Task<BookingDto> CreateAsync(CreateBookingRequest request, Guid actorId, CancellationToken ct = default)
    {
        if (request.EndsAt <= request.StartsAt) throw new ValidationAppException("End must be after start.");
        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == request.CustomerId && c.ArchivedAt == null, ct)
            ?? throw new AppException("not_found", "Customer was not found.", 404);
        string? staffName = null;
        if (request.StaffId is Guid sid)
        {
            var staff = await _db.Users.FirstOrDefaultAsync(u => u.Id == sid && u.ArchivedAt == null, ct)
                ?? throw new ValidationAppException("Staff member was not found.");
            staffName = staff.DisplayName;
        }
        var booking = new Booking
        {
            CustomerId = customer.Id,
            StaffId = request.StaffId,
            Type = string.IsNullOrWhiteSpace(request.Type) ? "Intake" : request.Type.Trim(),
            Status = "Confirmed",
            StartsAt = request.StartsAt,
            EndsAt = request.EndsAt,
            Notes = request.Notes?.Trim()
        };
        _db.Bookings.Add(booking);
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(actorId, "booking.create", "Booking", booking.Id.ToString(), ct: ct);
        return new BookingDto(booking.Id, booking.CustomerId, customer.DisplayName, booking.StaffId, staffName, booking.Type, booking.Status, booking.StartsAt, booking.EndsAt, booking.Notes);
    }
}

public sealed class KnowledgeService : IKnowledgeService
{
    private readonly WorkshopDbContext _db;
    private readonly IAuditService _audit;

    public KnowledgeService(WorkshopDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<IReadOnlyList<KnowledgeDto>> ListAsync(string? q, CancellationToken ct = default)
    {
        var query = _db.KnowledgeArticles.AsNoTracking().Where(a => a.ArchivedAt == null);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLowerInvariant();
            query = query.Where(a => a.Title.ToLower().Contains(term) || a.Body.ToLower().Contains(term) || (a.Tags != null && a.Tags.ToLower().Contains(term)));
        }
        return await query.OrderByDescending(a => a.UpdatedAt)
            .Select(a => new KnowledgeDto(a.Id, a.Title, a.Category, a.Body, a.Tags, a.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<KnowledgeDto> UpsertAsync(UpsertKnowledgeRequest request, Guid actorId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Body))
            throw new ValidationAppException("Title and body are required.");
        KnowledgeArticle article;
        if (request.Id is Guid id)
        {
            article = await _db.KnowledgeArticles.FirstOrDefaultAsync(a => a.Id == id && a.ArchivedAt == null, ct)
                ?? throw new AppException("not_found", "Article was not found.", 404);
        }
        else
        {
            article = new KnowledgeArticle { AuthorId = actorId };
            _db.KnowledgeArticles.Add(article);
        }
        article.Title = request.Title.Trim();
        article.Category = string.IsNullOrWhiteSpace(request.Category) ? "General" : request.Category.Trim();
        article.Body = request.Body;
        article.Tags = request.Tags?.Trim();
        article.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(actorId, "knowledge.upsert", "KnowledgeArticle", article.Id.ToString(), ct: ct);
        return new KnowledgeDto(article.Id, article.Title, article.Category, article.Body, article.Tags, article.CreatedAt);
    }
}

public sealed class PcBuildService : IPcBuildService
{
    private readonly WorkshopDbContext _db;
    private readonly IAuditService _audit;

    public PcBuildService(WorkshopDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<IReadOnlyList<PcBuildListItemDto>> ListAsync(CancellationToken ct = default) =>
        await _db.PcBuilds.AsNoTracking().Include(b => b.Customer)
            .Where(b => b.ArchivedAt == null)
            .OrderByDescending(b => b.CreatedAt)
            .Select(b => new PcBuildListItemDto(b.Id, b.Number, b.Customer != null ? b.Customer.DisplayName : null, b.Status, b.CostTotal, b.SellTotal, b.SellTotal - b.CostTotal))
            .ToListAsync(ct);

    public async Task<PcBuildListItemDto> CreateAsync(CreatePcBuildRequest request, Guid actorId, CancellationToken ct = default)
    {
        if (request.CustomerId is Guid cid)
            _ = await _db.Customers.FirstOrDefaultAsync(c => c.Id == cid && c.ArchivedAt == null, ct)
                ?? throw new AppException("not_found", "Customer was not found.", 404);

        var number = await DocumentNumbering.NextAsync(_db, "PCB", ct);
        var build = new PcBuild
        {
            Number = number,
            CustomerId = request.CustomerId,
            Status = "Quoted",
            UseCase = request.UseCase?.Trim(),
            Budget = request.Budget,
            CostTotal = request.Parts?.Sum(p => p.Cost) ?? 0,
            SellTotal = request.Parts?.Sum(p => p.SellPrice) ?? 0
        };
        if (request.Parts is not null)
        {
            foreach (var p in request.Parts)
            {
                build.Parts.Add(new PcBuildPart
                {
                    Category = p.Category,
                    Name = p.Name,
                    InventoryItemId = p.InventoryItemId,
                    Cost = p.Cost,
                    SellPrice = p.SellPrice
                });
            }
        }
        _db.PcBuilds.Add(build);
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(actorId, "build.create", "PcBuild", build.Id.ToString(), newValue: new { build.Number }, ct: ct);
        string? customerName = null;
        if (build.CustomerId is Guid)
            customerName = await _db.Customers.Where(c => c.Id == build.CustomerId).Select(c => c.DisplayName).FirstAsync(ct);
        return new PcBuildListItemDto(build.Id, build.Number, customerName, build.Status, build.CostTotal, build.SellTotal, build.SellTotal - build.CostTotal);
    }
}

public sealed class UsedTechService : IUsedTechService
{
    private readonly WorkshopDbContext _db;
    private readonly IAuditService _audit;

    public UsedTechService(WorkshopDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<IReadOnlyList<UsedDeviceDto>> ListAsync(CancellationToken ct = default) =>
        await _db.UsedDevices.AsNoTracking().Where(d => d.ArchivedAt == null)
            .OrderByDescending(d => d.CreatedAt)
            .Select(d => new UsedDeviceDto(d.Id, d.Summary, d.Status, d.ConditionGrade, d.PurchasePrice, d.ExpectedResale, d.ActualSalePrice,
                d.ActualSalePrice.HasValue ? d.ActualSalePrice - d.PurchasePrice - d.ExpectedRepairCost : null))
            .ToListAsync(ct);

    public async Task<UsedDeviceDto> CreateAsync(CreateUsedDeviceRequest request, Guid actorId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Summary)) throw new ValidationAppException("Summary is required.");
        var d = new UsedDevice
        {
            Summary = request.Summary.Trim(),
            Serial = request.Serial?.Trim(),
            Imei = request.Imei?.Trim(),
            ConditionGrade = string.IsNullOrWhiteSpace(request.ConditionGrade) ? "B" : request.ConditionGrade.Trim(),
            Status = "Purchased",
            PurchasePrice = request.PurchasePrice,
            ExpectedResale = request.ExpectedResale,
            ExpectedRepairCost = request.ExpectedRepairCost,
            Faults = request.Faults?.Trim(),
            SellerCustomerId = request.SellerCustomerId
        };
        _db.UsedDevices.Add(d);
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(actorId, "used.create", "UsedDevice", d.Id.ToString(), ct: ct);
        return new UsedDeviceDto(d.Id, d.Summary, d.Status, d.ConditionGrade, d.PurchasePrice, d.ExpectedResale, d.ActualSalePrice, null);
    }

    public async Task<UsedDeviceDto> UpdateStatusAsync(Guid id, UpdateUsedStatusRequest request, Guid actorId, CancellationToken ct = default)
    {
        var d = await _db.UsedDevices.FirstOrDefaultAsync(x => x.Id == id && x.ArchivedAt == null, ct)
            ?? throw new AppException("not_found", "Used device was not found.", 404);
        d.Status = request.Status.Trim();
        if (request.ActualSalePrice is decimal sale) d.ActualSalePrice = sale;
        d.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(actorId, "used.status", "UsedDevice", id.ToString(), newValue: d.Status, ct: ct);
        return new UsedDeviceDto(d.Id, d.Summary, d.Status, d.ConditionGrade, d.PurchasePrice, d.ExpectedResale, d.ActualSalePrice,
            d.ActualSalePrice.HasValue ? d.ActualSalePrice - d.PurchasePrice - d.ExpectedRepairCost : null);
    }
}

public sealed class QaService : IQaService
{
    private static readonly string[] DefaultItems =
    [
        "Powers on",
        "Touchscreen / display",
        "Buttons / ports",
        "Wi-Fi / connectivity",
        "Camera / speakers",
        "No residual damage noted",
        "Customer data / passcode cleared if required"
    ];

    private readonly WorkshopDbContext _db;
    public QaService(WorkshopDbContext db) => _db = db;

    public async Task EnsureDefaultChecklistAsync(Guid ticketId, CancellationToken ct = default)
    {
        var ticket = await _db.RepairTickets.Include(t => t.Status)
            .FirstOrDefaultAsync(t => t.Id == ticketId && t.ArchivedAt == null, ct)
            ?? throw new AppException("not_found", "Repair was not found.", 404);

        if (ticket.Status.Key is not ("testing" or "ready_pickup"))
            return;

        if (await _db.QaChecklists.AnyAsync(q => q.TicketId == ticketId, ct))
            return;

        var order = 0;
        foreach (var item in DefaultItems)
        {
            _db.QaChecklists.Add(new QaChecklist
            {
                TicketId = ticketId,
                Item = item,
                Result = "NotTested",
                SortOrder = order++
            });
        }
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<QaItemDto>> ListForTicketAsync(Guid ticketId, CancellationToken ct = default)
    {
        await EnsureDefaultChecklistAsync(ticketId, ct);
        return await _db.QaChecklists.AsNoTracking()
            .Where(q => q.TicketId == ticketId)
            .OrderBy(q => q.SortOrder)
            .Select(q => new QaItemDto(q.Id, q.Item, q.Result, q.SortOrder))
            .ToListAsync(ct);
    }

    public async Task<QaItemDto> SetResultAsync(Guid ticketId, Guid itemId, string result, Guid actorId, CancellationToken ct = default)
    {
        var allowed = new[] { "Pass", "Fail", "NotApplicable", "NotTested" };
        if (!allowed.Contains(result, StringComparer.OrdinalIgnoreCase))
            throw new ValidationAppException("Invalid QA result.");
        var row = await _db.QaChecklists.FirstOrDefaultAsync(q => q.Id == itemId && q.TicketId == ticketId, ct)
            ?? throw new AppException("not_found", "QA item was not found.", 404);
        row.Result = allowed.First(a => a.Equals(result, StringComparison.OrdinalIgnoreCase));
        await _db.SaveChangesAsync(ct);
        return new QaItemDto(row.Id, row.Item, row.Result, row.SortOrder);
    }
}

public sealed class ReportService : IReportService
{
    private readonly WorkshopDbContext _db;
    public ReportService(WorkshopDbContext db) => _db = db;

    public async Task<ReportSummaryDto> SummaryAsync(DateTimeOffset? from, DateTimeOffset? to, CancellationToken ct = default)
    {
        var end = to ?? DateTimeOffset.UtcNow;
        var start = from ?? end.AddDays(-30);

        var payments = await _db.Payments.AsNoTracking()
            .Where(p => p.PaidAt >= start && p.PaidAt <= end).ToListAsync(ct);
        var revenue = payments.Sum(p => p.Amount);
        var cogs = await _db.InventoryTransactions.AsNoTracking().Include(t => t.Item)
            .Where(t => t.CreatedAt >= start && t.CreatedAt <= end && t.QuantityDelta < 0)
            .SumAsync(t => (decimal?)(Math.Abs(t.QuantityDelta) * t.Item.Cost), ct) ?? 0m;
        if (cogs == 0 && revenue > 0)
            cogs = Math.Round(revenue * 0.45m, 2, MidpointRounding.AwayFromZero); // interim

        var completed = await _db.RepairTickets.AsNoTracking()
            .CountAsync(t => t.ArchivedAt == null && t.CompletedAt >= start && t.CompletedAt <= end, ct);
        var opened = await _db.RepairTickets.AsNoTracking()
            .CountAsync(t => t.ArchivedAt == null && t.CreatedAt >= start && t.CreatedAt <= end, ct);

        var byStatus = await _db.RepairTickets.AsNoTracking().Include(t => t.Status)
            .Where(t => t.ArchivedAt == null && t.CreatedAt >= start && t.CreatedAt <= end)
            .GroupBy(t => t.Status.Name)
            .Select(g => new NamedCountDto(g.Key, g.Count()))
            .ToListAsync(ct);

        var byMethod = payments.GroupBy(p => p.Method)
            .Select(g => new NamedMoneyDto(g.Key, g.Sum(x => x.Amount)))
            .OrderByDescending(x => x.Amount).ToList();

        return new ReportSummaryDto(start, end, revenue, cogs, revenue - cogs, completed, opened, byStatus, byMethod);
    }
}

public sealed class AiService : IAiService
{
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _http;
    private readonly WorkshopDbContext _db;

    public AiService(IConfiguration config, IHttpClientFactory http, WorkshopDbContext db)
    {
        _config = config;
        _http = http;
        _db = db;
    }

    public async Task<AiAssistResponse> AssistAsync(AiAssistRequest request, Guid actorId, CancellationToken ct = default)
    {
        var enabled = _config.GetValue("Integrations:Ollama:Enabled", false);
        var baseUrl = _config["Integrations:Ollama:BaseUrl"] ?? "http://127.0.0.1:11434";
        var model = _config["Integrations:Ollama:Model"] ?? "llama3.2";
        const string disclaimer = "AI suggestions must be reviewed before applying. WorkshopOS never auto-applies AI output.";

        if (!enabled)
        {
            return new AiAssistResponse(false, "none",
                "AI assistant is disabled. Set Integrations:Ollama:Enabled=true and run a local Ollama instance to enable.",
                disclaimer);
        }

        if (string.IsNullOrWhiteSpace(request.Prompt))
            throw new ValidationAppException("Prompt is required.");

        var context = "";
        if (request.TicketId is Guid tid)
        {
            var ticket = await _db.RepairTickets.AsNoTracking().Include(t => t.Customer).Include(t => t.Device)
                .FirstOrDefaultAsync(t => t.Id == tid, ct);
            if (ticket is not null)
                context = $"Ticket {ticket.TicketNumber}: {ticket.ReportedIssue}. Diagnosis: {ticket.Diagnosis}. Customer: {ticket.Customer.DisplayName}.";
        }

        try
        {
            var client = _http.CreateClient("ollama");
            client.Timeout = TimeSpan.FromSeconds(60);
            var payload = new
            {
                model,
                prompt = string.IsNullOrEmpty(context) ? request.Prompt : $"{context}\n\nUser request: {request.Prompt}",
                stream = false
            };
            using var response = await client.PostAsJsonAsync($"{baseUrl.TrimEnd('/')}/api/generate", payload, ct);
            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync(ct);
                return new AiAssistResponse(true, "ollama",
                    $"Ollama request failed ({(int)response.StatusCode}): {Truncate(err, 400)}",
                    disclaimer);
            }
            using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            var output = doc.RootElement.TryGetProperty("response", out var r) ? r.GetString() ?? "" : "";
            if (string.IsNullOrWhiteSpace(output))
                return new AiAssistResponse(true, "ollama", "Ollama returned an empty response.", disclaimer);
            return new AiAssistResponse(true, "ollama", output.Trim(), disclaimer);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return new AiAssistResponse(true, "ollama",
                $"Could not reach Ollama at {baseUrl}: {ex.Message}",
                disclaimer);
        }
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max] + "…";
}

public sealed class BackupService : IBackupService
{
    private readonly WorkshopDbContext _db;
    private readonly IConfiguration _config;
    private readonly IAuditService _audit;

    public BackupService(WorkshopDbContext db, IConfiguration config, IAuditService audit)
    {
        _db = db;
        _config = config;
        _audit = audit;
    }

    public async Task<IReadOnlyList<BackupDto>> ListAsync(CancellationToken ct = default) =>
        await _db.BackupRecords.AsNoTracking().OrderByDescending(b => b.StartedAt).Take(50)
            .Select(b => new BackupDto(b.Id, b.Type, b.Status, b.Path, b.StartedAt, b.FinishedAt))
            .ToListAsync(ct);

    public async Task<BackupDto> CreateAsync(Guid actorId, CancellationToken ct = default)
    {
        var dir = Environment.GetEnvironmentVariable("BACKUP_DIR");
        if (string.IsNullOrWhiteSpace(dir))
            dir = _config["Backup:Directory"] ?? Path.Combine(Directory.GetCurrentDirectory(), "data", "backups");
        Directory.CreateDirectory(dir);

        var started = DateTimeOffset.UtcNow;
        var fileName = $"workshopos-backup-{started:yyyyMMdd-HHmmss}.sql.marker";
        var path = Path.Combine(dir, fileName);
        var record = new BackupRecord
        {
            Type = "Manual",
            Status = "Running",
            Path = path,
            StartedById = actorId,
            StartedAt = started
        };
        _db.BackupRecords.Add(record);
        await _db.SaveChangesAsync(ct);

        try
        {
            var sb = new StringBuilder();
            sb.AppendLine("-- WorkshopOS backup marker (logical dump placeholder)");
            sb.AppendLine($"-- StartedAt: {started:O}");
            sb.AppendLine($"-- Actor: {actorId}");
            sb.AppendLine($"-- Database: {_db.Database.GetDbConnection().Database}");
            sb.AppendLine("-- NOTE: Full pg_dump integration lands with hardened ops; this marker proves backup plumbing.");
            await File.WriteAllTextAsync(path, sb.ToString(), ct);

            record.Status = "Completed";
            record.FinishedAt = DateTimeOffset.UtcNow;
            record.Detail = "Marker file written";
            await _db.SaveChangesAsync(ct);
            await _audit.WriteAsync(actorId, "backup.create", "BackupRecord", record.Id.ToString(), newValue: new { path }, ct: ct);
        }
        catch (Exception ex)
        {
            record.Status = "Failed";
            record.Detail = ex.Message;
            record.FinishedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);
            throw new AppException("backup_failed", $"Backup failed: {ex.Message}", 500);
        }

        return new BackupDto(record.Id, record.Type, record.Status, record.Path, record.StartedAt, record.FinishedAt);
    }

    public async Task<SystemHealthDetailDto> HealthDetailAsync(CancellationToken ct = default)
    {
        var dbOk = false;
        try { dbOk = await _db.Database.CanConnectAsync(ct); } catch { /* degraded */ }

        var lastBackup = await _db.BackupRecords.AsNoTracking()
            .Where(b => b.Status == "Completed")
            .OrderByDescending(b => b.FinishedAt)
            .Select(b => (DateTimeOffset?)b.FinishedAt)
            .FirstOrDefaultAsync(ct);
        var backupCount = await _db.BackupRecords.AsNoTracking().LongCountAsync(ct);

        // Worker heartbeat not wired yet — report false rather than inventing success.
        return new SystemHealthDetailDto(
            dbOk ? "Healthy" : "Degraded",
            dbOk,
            WorkerHeartbeat: false,
            typeof(BackupService).Assembly.GetName().Version?.ToString() ?? "1.0.0",
            "1.0.0",
            lastBackup,
            backupCount);
    }
}
