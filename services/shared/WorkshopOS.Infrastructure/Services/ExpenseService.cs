using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using WorkshopOS.Application.Abstractions;
using WorkshopOS.Application.Common;
using WorkshopOS.Contracts.Finance;
using WorkshopOS.Domain.Entities;
using WorkshopOS.Infrastructure.Persistence;

namespace WorkshopOS.Infrastructure.Services;

public sealed class ExpenseService : IExpenseService
{
    private static readonly string[] Statuses =
        ["Draft", "Unpaid", "PartiallyPaid", "Paid", "Overdue", "Cancelled"];

    private static readonly string[] GstTreatments =
        ["Inclusive", "Exclusive", "GstFree", "InputTaxed", "NoCredit"];

    private readonly WorkshopDbContext _db;
    private readonly IAuditService _audit;
    private readonly IConfiguration _config;

    public ExpenseService(WorkshopDbContext db, IAuditService audit, IConfiguration config)
    {
        _db = db;
        _audit = audit;
        _config = config;
    }

    public async Task EnsureDefaultsAsync(CancellationToken ct = default)
    {
        if (await _db.ExpenseCategories.AnyAsync(ct)) return;
        var defs = new (string Name, string Kind, int Sort)[]
        {
            ("Replacement screens", "Stock", 10),
            ("Batteries", "Stock", 20),
            ("Charging ports", "Stock", 30),
            ("Other replacement parts", "Stock", 40),
            ("Repair tools", "Capital", 50),
            ("Consumables", "Operating", 60),
            ("Adhesives", "Operating", 70),
            ("Cleaning supplies", "Operating", 80),
            ("Software subscriptions", "Operating", 100),
            ("Website hosting", "Operating", 110),
            ("Domain registration", "Operating", 120),
            ("Advertising and marketing", "Operating", 130),
            ("Packaging", "Operating", 140),
            ("Shipping and postage", "Operating", 150),
            ("Internet and phone", "Operating", 160),
            ("Insurance", "Operating", 170),
            ("Accounting and bookkeeping", "Operating", 180),
            ("Office supplies", "Operating", 190),
            ("Equipment", "Capital", 200),
            ("Rent and utilities", "Operating", 210),
            ("Staff expenses", "Operating", 220),
            ("Other business expenses", "Operating", 999)
        };
        var i = 0;
        foreach (var d in defs)
        {
            _db.ExpenseCategories.Add(new ExpenseCategory
            {
                Name = d.Name,
                Kind = d.Kind,
                SortOrder = d.Sort,
                IsSystem = true
            });
            i++;
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<ExpenseCategoryDto>> ListCategoriesAsync(CancellationToken ct = default)
    {
        await EnsureDefaultsAsync(ct);
        return await _db.ExpenseCategories.AsNoTracking()
            .Where(c => c.ArchivedAt == null)
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .Select(c => new ExpenseCategoryDto(c.Id, c.Name, c.ParentCategoryId, c.Kind, c.Icon, c.MonthlyBudget, c.SortOrder, c.IsSystem))
            .ToListAsync(ct);
    }

    public async Task<ExpenseCategoryDto> UpsertCategoryAsync(UpsertExpenseCategoryRequest request, Guid actorId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ValidationAppException("Category name is required.");
        ExpenseCategory cat;
        if (request.Id is Guid id)
        {
            cat = await _db.ExpenseCategories.FirstOrDefaultAsync(c => c.Id == id && c.ArchivedAt == null, ct)
                ?? throw new AppException("not_found", "Category not found.", 404);
        }
        else
        {
            cat = new ExpenseCategory();
            _db.ExpenseCategories.Add(cat);
        }

        cat.Name = request.Name.Trim();
        cat.ParentCategoryId = request.ParentCategoryId;
        cat.Kind = string.IsNullOrWhiteSpace(request.Kind) ? "Operating" : request.Kind.Trim();
        cat.Icon = request.Icon;
        cat.MonthlyBudget = request.MonthlyBudget;
        cat.SortOrder = request.SortOrder;
        cat.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(actorId, "expenses.category.upsert", "ExpenseCategory", cat.Id.ToString(), ct: ct);
        return new ExpenseCategoryDto(cat.Id, cat.Name, cat.ParentCategoryId, cat.Kind, cat.Icon, cat.MonthlyBudget, cat.SortOrder, cat.IsSystem);
    }

    public static GstCalcResult CalculateGst(decimal amount, string treatment, decimal gstRate = 0.10m)
    {
        var t = GstTreatments.FirstOrDefault(x => x.Equals(treatment, StringComparison.OrdinalIgnoreCase))
                ?? throw new ValidationAppException($"GST treatment must be one of: {string.Join(", ", GstTreatments)}");
        gstRate = gstRate <= 0 ? 0.10m : gstRate;
        amount = Math.Round(amount, 2, MidpointRounding.AwayFromZero);

        return t switch
        {
            "Inclusive" => new GstCalcResult(amount,
                Math.Round(amount / (1 + gstRate), 2, MidpointRounding.AwayFromZero),
                Math.Round(amount - amount / (1 + gstRate), 2, MidpointRounding.AwayFromZero), t),
            "Exclusive" => new GstCalcResult(
                Math.Round(amount * (1 + gstRate), 2, MidpointRounding.AwayFromZero),
                amount,
                Math.Round(amount * gstRate, 2, MidpointRounding.AwayFromZero), t),
            "GstFree" or "InputTaxed" or "NoCredit" => new GstCalcResult(amount, amount, 0m, t),
            _ => new GstCalcResult(amount, amount, 0m, t)
        };
    }

    public Task<GstCalcResult> PreviewGstAsync(GstCalcRequest request, CancellationToken ct = default) =>
        Task.FromResult(CalculateGst(request.Amount, request.Treatment, request.GstRate));

    public async Task<IReadOnlyList<ExpenseListItemDto>> ListAsync(
        string? q, string? status, Guid? categoryId, DateOnly? from, DateOnly? to, CancellationToken ct = default)
    {
        await EnsureDefaultsAsync(ct);
        var query = _db.BusinessExpenses.AsNoTracking()
            .Include(e => e.Category)
            .Where(e => e.ArchivedAt == null);
        if (!string.IsNullOrWhiteSpace(status) && !status.Equals("All", StringComparison.OrdinalIgnoreCase))
            query = query.Where(e => e.Status == status);
        if (categoryId is Guid cid)
            query = query.Where(e => e.CategoryId == cid);
        if (from is DateOnly f)
            query = query.Where(e => e.PurchaseDate >= f);
        if (to is DateOnly t)
            query = query.Where(e => e.PurchaseDate <= t);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLowerInvariant();
            query = query.Where(e =>
                e.Title.ToLower().Contains(term) ||
                (e.SupplierName != null && e.SupplierName.ToLower().Contains(term)) ||
                (e.InvoiceReference != null && e.InvoiceReference.ToLower().Contains(term)) ||
                (e.Description != null && e.Description.ToLower().Contains(term)));
        }

        var rows = await query.OrderByDescending(e => e.PurchaseDate).ThenByDescending(e => e.CreatedAt).Take(500).ToListAsync(ct);
        return rows.Select(e => new ExpenseListItemDto(
            e.Id, e.Title, e.Status, e.Category?.Name, e.SupplierName, e.PurchaseDate,
            e.TotalAmount, e.GstAmount, e.AmountExGst, e.GstTreatment, e.PaymentMethod,
            e.IsInventoryOrCogsLinked, e.UpdatedAt)).ToList();
    }

    public async Task<ExpenseDetailDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var e = await _db.BusinessExpenses.AsNoTracking()
            .Include(x => x.Category).Include(x => x.Attachments)
            .FirstOrDefaultAsync(x => x.Id == id && x.ArchivedAt == null, ct)
            ?? throw new AppException("not_found", "Expense not found.", 404);
        return MapDetail(e);
    }

    public async Task<ExpenseDetailDto> UpsertAsync(Guid? id, UpsertExpenseRequest request, Guid actorId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            throw new ValidationAppException("Title is required.");
        if (request.TotalAmount < 0)
            throw new ValidationAppException("Total amount cannot be negative.");

        BusinessExpense e;
        if (id is Guid existing)
        {
            e = await _db.BusinessExpenses.Include(x => x.Attachments)
                .FirstOrDefaultAsync(x => x.Id == existing && x.ArchivedAt == null, ct)
                ?? throw new AppException("not_found", "Expense not found.", 404);
        }
        else
        {
            e = new BusinessExpense { RecordedById = actorId };
            _db.BusinessExpenses.Add(e);
        }

        var calc = CalculateGst(request.TotalAmount, request.GstTreatment);
        // Allow manual override of GST components when provided (invoice fidelity).
        var ex = request.AmountExGst ?? calc.AmountExGst;
        var gst = request.GstAmount ?? calc.GstAmount;
        var total = request.AmountExGst is not null || request.GstAmount is not null
            ? Math.Round(ex + (request.GstTreatment is "GstFree" or "InputTaxed" or "NoCredit" ? 0 : gst), 2)
            : calc.TotalAmount;
        if (request.GstTreatment is "Inclusive" or "Exclusive")
            total = request.GstTreatment == "Inclusive" ? request.TotalAmount : calc.TotalAmount;

        if (request.GstTreatment == "Inclusive")
        {
            total = request.TotalAmount;
            if (request.AmountExGst is null && request.GstAmount is null)
            {
                ex = calc.AmountExGst;
                gst = calc.GstAmount;
            }
        }
        else if (request.GstTreatment == "Exclusive")
        {
            ex = request.AmountExGst ?? request.TotalAmount;
            gst = request.GstAmount ?? calc.GstAmount;
            total = Math.Round(ex + gst, 2);
        }
        else
        {
            total = request.TotalAmount;
            ex = request.AmountExGst ?? total;
            gst = 0;
        }

        var status = string.IsNullOrWhiteSpace(request.Status) ? e.Status : NormalizeStatus(request.Status);
        e.Title = request.Title.Trim();
        e.Description = Trim(request.Description);
        e.CategoryId = request.CategoryId;
        e.SupplierId = request.SupplierId;
        e.SupplierName = Trim(request.SupplierName);
        e.PurchaseDate = request.PurchaseDate;
        e.PaymentDate = request.PaymentDate;
        e.InvoiceReference = Trim(request.InvoiceReference);
        e.Status = status;
        e.TotalAmount = total;
        e.AmountExGst = ex;
        e.GstAmount = gst;
        e.GstTreatment = calc.Treatment;
        e.PaymentMethod = Trim(request.PaymentMethod);
        e.AmountPaid = request.AmountPaid ?? e.AmountPaid;
        e.RefundAmount = request.RefundAmount ?? 0;
        e.BusinessUsePercent = Math.Clamp(request.BusinessUsePercent ?? 100m, 0, 100);
        e.Notes = Trim(request.Notes);
        e.RepairTicketId = request.RepairTicketId;
        e.InventoryItemId = request.InventoryItemId;
        e.PurchaseOrderId = request.PurchaseOrderId;
        e.IsInventoryOrCogsLinked = request.IsInventoryOrCogsLinked
                                    || request.InventoryItemId is not null
                                    || request.PurchaseOrderId is not null;
        e.UpdatedAt = DateTimeOffset.UtcNow;

        if (e.Status is "Unpaid" or "PartiallyPaid" && e.PurchaseDate < DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30)) && e.AmountPaid < e.TotalAmount)
            e.Status = "Overdue";

        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(actorId, id is null ? "expenses.create" : "expenses.update", "BusinessExpense", e.Id.ToString(), ct: ct);
        return await GetAsync(e.Id, ct);
    }

    public async Task SoftDeleteAsync(Guid id, Guid actorId, CancellationToken ct = default)
    {
        var e = await _db.BusinessExpenses.FirstOrDefaultAsync(x => x.Id == id && x.ArchivedAt == null, ct)
            ?? throw new AppException("not_found", "Expense not found.", 404);
        e.ArchivedAt = DateTimeOffset.UtcNow;
        e.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(actorId, "expenses.delete", "BusinessExpense", id.ToString(), ct: ct);
    }

    public async Task<ExpenseDashboardDto> DashboardAsync(DateOnly? from, DateOnly? to, CancellationToken ct = default)
    {
        await EnsureDefaultsAsync(ct);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var (fyStart, fyEnd) = AustralianFinancialYear(today);
        var rangeEnd = to ?? today;
        var rangeStart = from ?? new DateOnly(today.Year, today.Month, 1);

        var all = await _db.BusinessExpenses.AsNoTracking()
            .Include(e => e.Category)
            .Where(e => e.ArchivedAt == null && e.Status != "Cancelled")
            .ToListAsync(ct);

        decimal BusinessShare(BusinessExpense e) =>
            Math.Round(e.TotalAmount * (e.BusinessUsePercent / 100m), 2);

        var operating = all.Where(e => !e.IsInventoryOrCogsLinked).ToList();
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var totalMonth = operating.Where(e => e.PurchaseDate >= monthStart && e.PurchaseDate <= today).Sum(BusinessShare);
        var totalFy = operating.Where(e => e.PurchaseDate >= fyStart && e.PurchaseDate <= fyEnd).Sum(BusinessShare);
        var gstPaid = operating
            .Where(e => e.GstTreatment is "Inclusive" or "Exclusive" && e.PurchaseDate >= fyStart && e.PurchaseDate <= fyEnd)
            .Sum(e => Math.Round(e.GstAmount * (e.BusinessUsePercent / 100m), 2));
        var unpaid = operating.Where(e => e.Status is "Unpaid" or "PartiallyPaid" or "Overdue")
            .Sum(e => Math.Max(0, BusinessShare(e) - e.AmountPaid));
        var overdue = operating.Where(e => e.Status == "Overdue")
            .Sum(e => Math.Max(0, BusinessShare(e) - e.AmountPaid));
        var recurring = await _db.RecurringExpenses.CountAsync(r => r.ArchivedAt == null && !r.IsPaused, ct);

        var inRange = operating.Where(e => e.PurchaseDate >= rangeStart && e.PurchaseDate <= rangeEnd).ToList();
        var byCat = inRange.GroupBy(e => e.Category?.Name ?? "Uncategorised")
            .Select(g => new NamedMoneyDto(g.Key, g.Sum(BusinessShare)))
            .OrderByDescending(x => x.Amount).Take(12).ToList();
        var bySup = inRange.GroupBy(e => e.SupplierName ?? "Unknown")
            .Select(g => new NamedMoneyDto(g.Key, g.Sum(BusinessShare)))
            .OrderByDescending(x => x.Amount).Take(10).ToList();
        var trend = inRange.GroupBy(e => e.PurchaseDate.ToString("yyyy-MM"))
            .Select(g => new NamedMoneyDto(g.Key, g.Sum(BusinessShare)))
            .OrderBy(x => x.Name).ToList();

        return new ExpenseDashboardDto(
            totalMonth, totalFy, gstPaid, unpaid, overdue, recurring,
            byCat, bySup, trend, rangeStart, rangeEnd,
            "Cash/invoice basis for recorded expenses. Inventory-linked purchases are excluded from operating totals to avoid double-counting with stock/COGS. GST figures are supporting estimates for bookkeeping — not a certified BAS.");
    }

    public async Task<string> ExportCsvAsync(DateOnly? from, DateOnly? to, Guid? categoryId, CancellationToken ct = default)
    {
        var rows = await ListAsync(null, null, categoryId, from, to, ct);
        var sb = new StringBuilder();
        sb.AppendLine("PurchaseDate,Supplier,Category,Title,AmountExGst,GstAmount,TotalAmount,Status,PaymentMethod,InvoiceReference");
        foreach (var r in rows.Where(x => !x.IsInventoryOrCogsLinked))
        {
            sb.Append(Csv(r.PurchaseDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))).Append(',')
                .Append(Csv(r.SupplierName)).Append(',')
                .Append(Csv(r.CategoryName)).Append(',')
                .Append(Csv(r.Title)).Append(',')
                .Append(r.AmountExGst.ToString("0.00", CultureInfo.InvariantCulture)).Append(',')
                .Append(r.GstAmount.ToString("0.00", CultureInfo.InvariantCulture)).Append(',')
                .Append(r.TotalAmount.ToString("0.00", CultureInfo.InvariantCulture)).Append(',')
                .Append(Csv(r.Status)).Append(',')
                .Append(Csv(r.PaymentMethod)).Append(',')
                .Append(Csv(null))
                .AppendLine();
        }

        return sb.ToString();
    }

    public async Task<IReadOnlyList<RecurringExpenseDto>> ListRecurringAsync(CancellationToken ct = default) =>
        await _db.RecurringExpenses.AsNoTracking()
            .Where(r => r.ArchivedAt == null)
            .OrderBy(r => r.NextDueDate)
            .Select(r => new RecurringExpenseDto(
                r.Id, r.Title, r.CategoryId, r.SupplierName, r.TotalAmount, r.GstTreatment,
                r.Frequency, r.CustomIntervalDays, r.StartDate, r.NextDueDate, r.EndDate,
                r.AutoCreateDraft, r.IsPaused, r.Notes))
            .ToListAsync(ct);

    public async Task<RecurringExpenseDto> UpsertRecurringAsync(Guid? id, UpsertRecurringExpenseRequest request, Guid actorId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            throw new ValidationAppException("Title is required.");
        RecurringExpense r;
        if (id is Guid existing)
        {
            r = await _db.RecurringExpenses.FirstOrDefaultAsync(x => x.Id == existing && x.ArchivedAt == null, ct)
                ?? throw new AppException("not_found", "Recurring expense not found.", 404);
        }
        else
        {
            r = new RecurringExpense { CreatedById = actorId, StartDate = request.StartDate, NextDueDate = request.NextDueDate ?? request.StartDate };
            _db.RecurringExpenses.Add(r);
        }

        r.Title = request.Title.Trim();
        r.CategoryId = request.CategoryId;
        r.SupplierId = request.SupplierId;
        r.SupplierName = Trim(request.SupplierName);
        r.TotalAmount = request.TotalAmount;
        r.GstTreatment = CalculateGst(request.TotalAmount, request.GstTreatment).Treatment;
        r.PaymentMethod = Trim(request.PaymentMethod);
        r.Frequency = request.Frequency.Trim();
        r.CustomIntervalDays = Math.Max(0, request.CustomIntervalDays);
        r.StartDate = request.StartDate;
        r.NextDueDate = request.NextDueDate ?? r.NextDueDate;
        r.EndDate = request.EndDate;
        r.AutoCreateDraft = request.AutoCreateDraft;
        r.IsPaused = request.IsPaused;
        r.Notes = Trim(request.Notes);
        r.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return (await ListRecurringAsync(ct)).First(x => x.Id == r.Id);
    }

    public async Task<int> ProcessDueRecurringAsync(CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var due = await _db.RecurringExpenses
            .Where(r => r.ArchivedAt == null && !r.IsPaused && r.AutoCreateDraft && r.NextDueDate <= today
                        && (r.EndDate == null || r.NextDueDate <= r.EndDate))
            .ToListAsync(ct);
        var created = 0;
        foreach (var r in due)
        {
            var instanceKey = $"{r.Id:N}|{r.NextDueDate:yyyyMMdd}";
            if (await _db.BusinessExpenses.AnyAsync(e => e.RecurrenceInstanceKey == instanceKey, ct))
            {
                r.NextDueDate = Advance(r.NextDueDate, r.Frequency, r.CustomIntervalDays);
                continue;
            }

            var calc = CalculateGst(r.TotalAmount, r.GstTreatment);
            _db.BusinessExpenses.Add(new BusinessExpense
            {
                Title = r.Title,
                CategoryId = r.CategoryId,
                SupplierId = r.SupplierId,
                SupplierName = r.SupplierName,
                PurchaseDate = r.NextDueDate,
                Status = "Draft",
                RecordedById = r.CreatedById,
                TotalAmount = calc.TotalAmount,
                AmountExGst = calc.AmountExGst,
                GstAmount = calc.GstAmount,
                GstTreatment = calc.Treatment,
                PaymentMethod = r.PaymentMethod,
                Notes = r.Notes,
                RecurringExpenseId = r.Id,
                RecurrenceInstanceKey = instanceKey
            });
            r.NextDueDate = Advance(r.NextDueDate, r.Frequency, r.CustomIntervalDays);
            created++;
        }

        if (due.Count > 0)
            await _db.SaveChangesAsync(ct);
        return created;
    }

    public async Task<ExpenseAttachmentDto> AddAttachmentAsync(
        Guid expenseId, string fileName, string contentType, Stream content, Guid actorId, CancellationToken ct = default)
    {
        var e = await _db.BusinessExpenses.FirstOrDefaultAsync(x => x.Id == expenseId && x.ArchivedAt == null, ct)
            ?? throw new AppException("not_found", "Expense not found.", 404);

        var allowed = new[] { "application/pdf", "image/jpeg", "image/png", "image/webp", "image/gif" };
        if (!allowed.Contains(contentType.ToLowerInvariant()) &&
            !fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) &&
            !fileName.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) &&
            !fileName.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) &&
            !fileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            throw new ValidationAppException("Only PDF, JPG, and PNG receipts are allowed.");

        var dir = _config["Files:Root"] ?? Environment.GetEnvironmentVariable("FILES_DIR")
                  ?? Path.Combine(Path.GetTempPath(), "workshopos-files");
        var expenseDir = Path.Combine(dir, "expenses", expenseId.ToString("N"));
        Directory.CreateDirectory(expenseDir);
        var safeName = Path.GetFileName(fileName);
        var storedName = $"{Guid.NewGuid():N}_{safeName}";
        var fullPath = Path.Combine(expenseDir, storedName);

        await using (var fs = File.Create(fullPath))
        {
            await content.CopyToAsync(fs, ct);
            if (fs.Length > 15 * 1024 * 1024)
            {
                fs.Close();
                File.Delete(fullPath);
                throw new ValidationAppException("Receipt files must be 15 MB or smaller.");
            }

            var att = new ExpenseAttachment
            {
                ExpenseId = e.Id,
                FileName = safeName,
                ContentType = contentType,
                SizeBytes = fs.Length,
                StoragePath = fullPath,
                UploadedById = actorId
            };
            _db.ExpenseAttachments.Add(att);
            await _db.SaveChangesAsync(ct);
            await _audit.WriteAsync(actorId, "expenses.attachment", "ExpenseAttachment", att.Id.ToString(), ct: ct);
            return new ExpenseAttachmentDto(att.Id, att.FileName, att.ContentType, att.SizeBytes, att.CreatedAt);
        }
    }

    public async Task<(Stream Stream, string ContentType, string FileName)> OpenAttachmentAsync(Guid expenseId, Guid attachmentId, CancellationToken ct = default)
    {
        var att = await _db.ExpenseAttachments.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == attachmentId && a.ExpenseId == expenseId, ct)
            ?? throw new AppException("not_found", "Attachment not found.", 404);
        if (!File.Exists(att.StoragePath))
            throw new AppException("not_found", "Attachment file missing on disk.", 404);
        Stream stream = File.OpenRead(att.StoragePath);
        return (stream, att.ContentType, att.FileName);
    }

    private static ExpenseDetailDto MapDetail(BusinessExpense e) => new(
        e.Id, e.Title, e.Description, e.CategoryId, e.Category?.Name, e.SupplierId, e.SupplierName,
        e.PurchaseDate, e.PaymentDate, e.InvoiceReference, e.Status, e.RecordedById,
        e.TotalAmount, e.AmountExGst, e.GstAmount, e.GstTreatment, e.PaymentMethod,
        e.AmountPaid, e.RefundAmount, e.BusinessUsePercent, e.Notes,
        e.RepairTicketId, e.InventoryItemId, e.PurchaseOrderId, e.IsInventoryOrCogsLinked,
        e.RecurringExpenseId,
        e.Attachments.Select(a => new ExpenseAttachmentDto(a.Id, a.FileName, a.ContentType, a.SizeBytes, a.CreatedAt)).ToList(),
        e.CreatedAt, e.UpdatedAt);

    private static string NormalizeStatus(string status)
    {
        var match = Statuses.FirstOrDefault(s => s.Equals(status.Trim(), StringComparison.OrdinalIgnoreCase));
        return match ?? throw new ValidationAppException($"Status must be one of: {string.Join(", ", Statuses)}");
    }

    private static string? Trim(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();

    private static string Csv(string? v)
    {
        v ??= "";
        if (v.Contains('"') || v.Contains(',') || v.Contains('\n'))
            return "\"" + v.Replace("\"", "\"\"") + "\"";
        return v;
    }

    public static (DateOnly Start, DateOnly End) AustralianFinancialYear(DateOnly day)
    {
        // 1 July – 30 June
        var startYear = day.Month >= 7 ? day.Year : day.Year - 1;
        return (new DateOnly(startYear, 7, 1), new DateOnly(startYear + 1, 6, 30));
    }

    private static DateOnly Advance(DateOnly from, string frequency, int customDays) => frequency.ToLowerInvariant() switch
    {
        "weekly" => from.AddDays(7),
        "fortnightly" => from.AddDays(14),
        "monthly" => from.AddMonths(1),
        "quarterly" => from.AddMonths(3),
        "yearly" => from.AddYears(1),
        "custom" => from.AddDays(Math.Max(1, customDays)),
        _ => from.AddMonths(1)
    };
}
