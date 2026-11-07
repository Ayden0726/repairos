using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using WorkshopOS.Application.Abstractions;
using WorkshopOS.Application.Common;
using WorkshopOS.Contracts.Operations;
using WorkshopOS.Domain.Entities;
using WorkshopOS.Infrastructure.Persistence;

namespace WorkshopOS.Infrastructure.Services;

public sealed class DonorService : IDonorService
{
    private readonly WorkshopDbContext _db;
    private readonly IAuditService _audit;
    public DonorService(WorkshopDbContext db, IAuditService audit) { _db = db; _audit = audit; }

    public async Task<IReadOnlyList<DonorDeviceDto>> ListAsync(CancellationToken ct = default) =>
        await _db.DonorDevices.AsNoTracking().Where(d => d.ArchivedAt == null)
            .OrderByDescending(d => d.CreatedAt).Take(100)
            .Select(d => new DonorDeviceDto(d.Id, d.Number, d.Brand, d.Model, d.Imei, d.Status,
                d.Components.Count, d.SourceUsedDeviceId, d.CreatedAt))
            .ToListAsync(ct);

    public async Task<DonorDeviceDetailDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var d = await _db.DonorDevices.AsNoTracking().Include(x => x.Components).ThenInclude(c => c.InventoryItem)
            .FirstOrDefaultAsync(x => x.Id == id && x.ArchivedAt == null, ct)
            ?? throw new AppException("not_found", "Donor device was not found.", 404);
        return Map(d);
    }

    public async Task<DonorDeviceDetailDto> CreateAsync(CreateDonorDeviceRequest request, Guid actorId, CancellationToken ct = default)
    {
        if (request.SourceUsedDeviceId is Guid usedId)
        {
            var used = await _db.UsedDevices.FirstOrDefaultAsync(u => u.Id == usedId && u.ArchivedAt == null, ct)
                ?? throw new AppException("not_found", "Source refurbished device was not found.", 404);
            used.IsPartsOnly = true;
            if (!string.Equals(used.Status, "Sold", StringComparison.OrdinalIgnoreCase))
                used.Status = "Parts Only";
            used.UpdatedAt = DateTimeOffset.UtcNow;
        }

        var number = await DocumentNumbering.NextAsync(_db, "DNR", ct);
        var d = new DonorDevice
        {
            Number = number,
            Brand = Norm(request.Brand),
            Model = Norm(request.Model),
            Imei = Norm(request.Imei),
            Serial = Norm(request.Serial),
            Notes = Norm(request.Notes),
            SourceUsedDeviceId = request.SourceUsedDeviceId,
            CustomerId = request.CustomerId,
            Status = "Open"
        };
        _db.DonorDevices.Add(d);
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(actorId, "donor.create", "DonorDevice", d.Id.ToString(), ct: ct);
        return await GetAsync(d.Id, ct);
    }

    public async Task<DonorDeviceDetailDto> HarvestAsync(Guid id, HarvestComponentRequest request, Guid actorId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Description))
            throw new ValidationAppException("Component description is required.");
        if (request.Quantity <= 0) throw new ValidationAppException("Quantity must be positive.");

        var donor = await _db.DonorDevices.Include(d => d.Components)
            .FirstOrDefaultAsync(d => d.Id == id && d.ArchivedAt == null, ct)
            ?? throw new AppException("not_found", "Donor device was not found.", 404);

        InventoryItem item;
        if (request.ExistingInventoryItemId is Guid existingId)
        {
            item = await _db.InventoryItems.FirstOrDefaultAsync(i => i.Id == existingId && i.ArchivedAt == null, ct)
                ?? throw new AppException("not_found", "Inventory item was not found.", 404);
            item.QuantityOnHand += request.Quantity;
            item.UpdatedAt = DateTimeOffset.UtcNow;
        }
        else
        {
            var sku = string.IsNullOrWhiteSpace(request.NewSku)
                ? $"HV-{donor.Number}-{donor.Components.Count + 1}"
                : request.NewSku.Trim();
            if (await _db.InventoryItems.AnyAsync(i => i.Sku == sku && i.ArchivedAt == null, ct))
                throw new ConflictAppException("SKU already exists — choose another or link an existing item.");
            item = new InventoryItem
            {
                Sku = sku,
                Name = request.Description.Trim(),
                Category = "Harvested",
                ComponentType = "Other",
                Cost = request.Cost ?? 0m,
                SellPrice = request.SellPrice ?? 0m,
                QuantityOnHand = request.Quantity,
                LocationBin = Norm(request.LocationBin),
                Description = $"Harvested from donor {donor.Number}"
            };
            _db.InventoryItems.Add(item);
            await _db.SaveChangesAsync(ct);
        }

        _db.InventoryTransactions.Add(new InventoryTransaction
        {
            ItemId = item.Id,
            Type = "Harvest",
            QuantityDelta = request.Quantity,
            Reason = $"Donor {donor.Number}",
            ActorUserId = actorId
        });

        _db.HarvestedComponents.Add(new HarvestedComponent
        {
            DonorDeviceId = donor.Id,
            Description = request.Description.Trim(),
            ConditionGrade = Norm(request.ConditionGrade),
            Quantity = request.Quantity,
            InventoryItemId = item.Id,
            HarvestedById = actorId
        });
        donor.Status = "Harvesting";
        donor.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(actorId, "donor.harvest", "DonorDevice", id.ToString(),
            newValue: new { item.Sku, request.Quantity }, ct: ct);
        return await GetAsync(id, ct);
    }

    private static DonorDeviceDetailDto Map(DonorDevice d) =>
        new(d.Id, d.Number, d.Brand, d.Model, d.Imei, d.Serial, d.Status, d.Notes,
            d.SourceUsedDeviceId, d.CustomerId,
            d.Components.OrderByDescending(c => c.HarvestedAt).Select(c =>
                new HarvestedComponentDto(c.Id, c.Description, c.ConditionGrade, c.Quantity,
                    c.InventoryItemId, c.InventoryItem?.Sku, c.HarvestedAt)).ToList(),
            d.CreatedAt);

    private static string? Norm(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}

public sealed class LoanService : ILoanService
{
    private readonly WorkshopDbContext _db;
    private readonly IAuditService _audit;
    public LoanService(WorkshopDbContext db, IAuditService audit) { _db = db; _audit = audit; }

    public async Task<IReadOnlyList<LoanDeviceDto>> ListDevicesAsync(CancellationToken ct = default) =>
        await _db.LoanDevices.AsNoTracking().Where(d => d.ArchivedAt == null).OrderBy(d => d.AssetTag)
            .Select(d => new LoanDeviceDto(d.Id, d.AssetTag, d.Brand, d.Model, d.Imei, d.Status, d.Notes))
            .ToListAsync(ct);

    public async Task<LoanDeviceDto> UpsertDeviceAsync(UpsertLoanDeviceRequest request, Guid actorId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.AssetTag))
            throw new ValidationAppException("Asset tag is required.");
        LoanDevice d;
        if (request.Id is Guid id)
        {
            d = await _db.LoanDevices.FirstOrDefaultAsync(x => x.Id == id && x.ArchivedAt == null, ct)
                ?? throw new AppException("not_found", "Loan device was not found.", 404);
        }
        else
        {
            if (await _db.LoanDevices.AnyAsync(x => x.AssetTag == request.AssetTag.Trim() && x.ArchivedAt == null, ct))
                throw new ConflictAppException("Asset tag already exists.");
            d = new LoanDevice();
            _db.LoanDevices.Add(d);
        }
        d.AssetTag = request.AssetTag.Trim();
        d.Brand = Norm(request.Brand);
        d.Model = Norm(request.Model);
        d.Imei = Norm(request.Imei);
        d.Serial = Norm(request.Serial);
        d.Notes = Norm(request.Notes);
        d.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(actorId, "loan.device.upsert", "LoanDevice", d.Id.ToString(), ct: ct);
        return new LoanDeviceDto(d.Id, d.AssetTag, d.Brand, d.Model, d.Imei, d.Status, d.Notes);
    }

    public async Task<IReadOnlyList<LoanAgreementDto>> ListAgreementsAsync(string? status = null, CancellationToken ct = default)
    {
        var q = _db.LoanAgreements.AsNoTracking()
            .Include(a => a.LoanDevice).Include(a => a.Customer).Include(a => a.RepairTicket)
            .AsQueryable();
        if (!string.IsNullOrWhiteSpace(status) && status != "All")
            q = q.Where(a => a.Status == status.Trim());
        return await q.OrderByDescending(a => a.CheckedOutAt).Take(100)
            .Select(a => MapAgreement(a)).ToListAsync(ct);
    }

    public async Task<LoanAgreementDto> CheckoutAsync(CheckoutLoanRequest request, Guid actorId, CancellationToken ct = default)
    {
        var device = await _db.LoanDevices.FirstOrDefaultAsync(d => d.Id == request.LoanDeviceId && d.ArchivedAt == null, ct)
            ?? throw new AppException("not_found", "Loan device was not found.", 404);
        if (device.Status != "Available")
            throw new ValidationAppException($"Device is {device.Status}, not Available.");
        _ = await _db.Customers.FirstOrDefaultAsync(c => c.Id == request.CustomerId && c.ArchivedAt == null, ct)
            ?? throw new AppException("not_found", "Customer was not found.", 404);

        device.Status = "OnLoan";
        device.UpdatedAt = DateTimeOffset.UtcNow;
        var a = new LoanAgreement
        {
            LoanDeviceId = device.Id,
            CustomerId = request.CustomerId,
            RepairTicketId = request.RepairTicketId,
            DueAt = request.DueAt,
            Notes = Norm(request.Notes),
            CheckedOutById = actorId,
            Status = "Active"
        };
        _db.LoanAgreements.Add(a);
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(actorId, "loan.checkout", "LoanAgreement", a.Id.ToString(), ct: ct);
        return (await ListAgreementsAsync(ct: ct)).First(x => x.Id == a.Id);
    }

    public async Task<LoanAgreementDto> ReturnAsync(Guid agreementId, ReturnLoanRequest request, Guid actorId, CancellationToken ct = default)
    {
        var a = await _db.LoanAgreements.Include(x => x.LoanDevice).Include(x => x.Customer).Include(x => x.RepairTicket)
            .FirstOrDefaultAsync(x => x.Id == agreementId, ct)
            ?? throw new AppException("not_found", "Loan agreement was not found.", 404);
        if (a.Status != "Active")
            throw new ValidationAppException("Agreement is not active.");
        a.Status = "Returned";
        a.ReturnedAt = DateTimeOffset.UtcNow;
        if (!string.IsNullOrWhiteSpace(request.Notes))
            a.Notes = string.IsNullOrWhiteSpace(a.Notes) ? request.Notes.Trim() : $"{a.Notes}\n{request.Notes.Trim()}";
        a.LoanDevice.Status = "Available";
        a.LoanDevice.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(actorId, "loan.return", "LoanAgreement", agreementId.ToString(), ct: ct);
        return MapAgreement(a);
    }

    private static LoanAgreementDto MapAgreement(LoanAgreement a) =>
        new(a.Id, a.LoanDeviceId, a.LoanDevice.AssetTag, a.CustomerId, a.Customer.DisplayName,
            a.RepairTicketId, a.RepairTicket?.TicketNumber, a.CheckedOutAt, a.DueAt, a.ReturnedAt, a.Status, a.Notes);

    private static string? Norm(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}

public sealed class WarrantyClaimService : IWarrantyClaimService
{
    private readonly WorkshopDbContext _db;
    private readonly IAuditService _audit;
    public WarrantyClaimService(WorkshopDbContext db, IAuditService audit) { _db = db; _audit = audit; }

    public async Task<IReadOnlyList<StaffWarrantyDto>> ListWarrantiesAsync(CancellationToken ct = default) =>
        await _db.Warranties.AsNoTracking().Include(w => w.Customer).Include(w => w.RepairTicket)
            .OrderByDescending(w => w.CreatedAt).Take(200)
            .Select(w => new StaffWarrantyDto(
                w.Id, w.CustomerId, w.Customer.DisplayName, w.CoverageSummary, w.WarrantyDays,
                w.StartsAt, w.ExpiresAt, w.Status, w.RepairTicketId, w.RepairTicket != null ? w.RepairTicket.TicketNumber : null))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<WarrantyClaimDto>> ListClaimsAsync(string? status = null, CancellationToken ct = default)
    {
        var q = _db.WarrantyClaims.AsNoTracking()
            .Include(c => c.Customer).Include(c => c.RepairTicket).Include(c => c.WarrantyRecord)
            .Where(c => c.ArchivedAt == null);
        if (!string.IsNullOrWhiteSpace(status) && status != "All")
            q = q.Where(c => c.Status == status.Trim());
        return await q.OrderByDescending(c => c.OpenedAt).Take(100)
            .Select(c => Map(c)).ToListAsync(ct);
    }

    public async Task<WarrantyClaimDto> CreateClaimAsync(CreateWarrantyClaimRequest request, Guid actorId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Summary))
            throw new ValidationAppException("Claim summary is required.");
        var warranty = await _db.Warranties.Include(w => w.Customer)
            .FirstOrDefaultAsync(w => w.Id == request.WarrantyRecordId, ct)
            ?? throw new AppException("not_found", "Warranty record was not found.", 404);
        if (warranty.Status != "Active" || warranty.ExpiresAt < DateTimeOffset.UtcNow)
            throw new ValidationAppException("Warranty is not active or has expired.");

        var number = await DocumentNumbering.NextAsync(_db, "WCL", ct);
        var claim = new WarrantyClaim
        {
            Number = number,
            WarrantyRecordId = warranty.Id,
            CustomerId = warranty.CustomerId,
            RepairTicketId = request.RepairTicketId,
            Summary = request.Summary.Trim(),
            OpenedById = actorId,
            Status = "Open"
        };
        _db.WarrantyClaims.Add(claim);
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(actorId, "warranty.claim.create", "WarrantyClaim", claim.Id.ToString(), ct: ct);
        return (await ListClaimsAsync(ct: ct)).First(x => x.Id == claim.Id);
    }

    public async Task<WarrantyClaimDto> UpdateClaimAsync(Guid id, UpdateWarrantyClaimRequest request, Guid actorId, CancellationToken ct = default)
    {
        var allowed = new[] { "Open", "Approved", "Rejected", "Completed" };
        var status = allowed.FirstOrDefault(s => s.Equals(request.Status.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? throw new ValidationAppException($"Status must be one of: {string.Join(", ", allowed)}");
        var claim = await _db.WarrantyClaims.Include(c => c.Customer).Include(c => c.RepairTicket).Include(c => c.WarrantyRecord)
            .FirstOrDefaultAsync(c => c.Id == id && c.ArchivedAt == null, ct)
            ?? throw new AppException("not_found", "Warranty claim was not found.", 404);
        claim.Status = status;
        claim.ResolutionNotes = string.IsNullOrWhiteSpace(request.ResolutionNotes) ? claim.ResolutionNotes : request.ResolutionNotes.Trim();
        if (status is "Rejected" or "Completed")
            claim.ClosedAt = DateTimeOffset.UtcNow;
        claim.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(actorId, "warranty.claim.update", "WarrantyClaim", id.ToString(), newValue: status, ct: ct);
        return Map(claim);
    }

    private static WarrantyClaimDto Map(WarrantyClaim c) =>
        new(c.Id, c.Number, c.WarrantyRecordId, c.CustomerId, c.Customer.DisplayName,
            c.RepairTicketId, c.RepairTicket?.TicketNumber, c.Status, c.Summary, c.ResolutionNotes,
            c.OpenedAt, c.ClosedAt, c.WarrantyRecord.ExpiresAt);
}

public sealed class RefundService : IRefundService
{
    private readonly WorkshopDbContext _db;
    private readonly IAuditService _audit;
    public RefundService(WorkshopDbContext db, IAuditService audit) { _db = db; _audit = audit; }

    public async Task<IReadOnlyList<RefundRequestDto>> ListAsync(string? status = null, CancellationToken ct = default)
    {
        var q = _db.RefundRequests.AsNoTracking()
            .Include(r => r.PosSale).Include(r => r.Invoice)
            .Where(r => r.ArchivedAt == null);
        if (!string.IsNullOrWhiteSpace(status) && status != "All")
            q = q.Where(r => r.Status == status.Trim());
        var rows = await q.OrderByDescending(r => r.RequestedAt).Take(100).ToListAsync(ct);
        var actors = rows.Select(r => r.RequestedById).Distinct().ToList();
        var names = await _db.Users.AsNoTracking().Where(u => actors.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);
        return rows.Select(r => new RefundRequestDto(
            r.Id, r.Number, r.PosSaleId, r.PosSale?.Number, r.InvoiceId, r.Invoice?.Number,
            r.Amount, r.Reason, r.Status, r.DecisionNotes, r.RequestedAt, r.DecidedAt,
            names.GetValueOrDefault(r.RequestedById))).ToList();
    }

    public async Task<RefundRequestDto> CreateAsync(CreateRefundRequestDto request, Guid actorId, CancellationToken ct = default)
    {
        if (request.Amount <= 0) throw new ValidationAppException("Refund amount must be positive.");
        if (string.IsNullOrWhiteSpace(request.Reason)) throw new ValidationAppException("Reason is required.");
        if (request.PosSaleId is null && request.InvoiceId is null)
            throw new ValidationAppException("Provide a POS sale or invoice.");

        if (request.PosSaleId is Guid saleId)
            _ = await _db.PosSales.FirstOrDefaultAsync(s => s.Id == saleId && s.ArchivedAt == null, ct)
                ?? throw new AppException("not_found", "POS sale was not found.", 404);
        if (request.InvoiceId is Guid invId)
            _ = await _db.Invoices.FirstOrDefaultAsync(i => i.Id == invId && i.ArchivedAt == null, ct)
                ?? throw new AppException("not_found", "Invoice was not found.", 404);

        var number = await DocumentNumbering.NextAsync(_db, "RFD", ct);
        var row = new RefundRequest
        {
            Number = number,
            PosSaleId = request.PosSaleId,
            InvoiceId = request.InvoiceId,
            Amount = request.Amount,
            Reason = request.Reason.Trim(),
            RequestedById = actorId,
            Status = "Pending"
        };
        _db.RefundRequests.Add(row);
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(actorId, "refund.request", "RefundRequest", row.Id.ToString(), ct: ct);
        return (await ListAsync(ct: ct)).First(x => x.Id == row.Id);
    }

    public async Task<RefundRequestDto> DecideAsync(Guid id, DecideRefundRequest request, Guid actorId, CancellationToken ct = default)
    {
        var row = await _db.RefundRequests.Include(r => r.PosSale).Include(r => r.Invoice)
            .FirstOrDefaultAsync(r => r.Id == id && r.ArchivedAt == null, ct)
            ?? throw new AppException("not_found", "Refund request was not found.", 404);
        if (row.Status != "Pending")
            throw new ValidationAppException("Only pending refunds can be decided.");

        row.ApprovedById = actorId;
        row.DecidedAt = DateTimeOffset.UtcNow;
        row.DecisionNotes = string.IsNullOrWhiteSpace(request.DecisionNotes) ? null : request.DecisionNotes.Trim();
        row.UpdatedAt = DateTimeOffset.UtcNow;

        if (!request.Approve)
        {
            row.Status = "Rejected";
            await _db.SaveChangesAsync(ct);
            await _audit.WriteAsync(actorId, "refund.reject", "RefundRequest", id.ToString(), ct: ct);
            return (await ListAsync(ct: ct)).First(x => x.Id == id);
        }

        var invoiceId = row.InvoiceId ?? row.PosSale?.InvoiceId
            ?? throw new ValidationAppException("Refund has no linked invoice.");
        var invoice = await _db.Invoices.FirstOrDefaultAsync(i => i.Id == invoiceId && i.ArchivedAt == null, ct)
            ?? throw new AppException("not_found", "Invoice was not found.", 404);
        if (row.Amount > invoice.AmountPaid)
            throw new ValidationAppException("Refund exceeds amount paid on the invoice.");

        invoice.AmountPaid = Math.Max(0m, invoice.AmountPaid - row.Amount);
        if (invoice.AmountPaid <= 0) invoice.Status = "Refunded";
        else if (invoice.AmountPaid < invoice.Total) invoice.Status = "PartiallyPaid";
        invoice.UpdatedAt = DateTimeOffset.UtcNow;
        _db.Payments.Add(new Payment
        {
            InvoiceId = invoice.Id,
            Method = "Refund",
            Amount = -row.Amount,
            Reference = row.Number,
            RecordedById = actorId,
            IsDeposit = false
        });
        row.Status = "Completed";
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(actorId, "refund.approve", "RefundRequest", id.ToString(),
            newValue: new { row.Amount, invoice.Id }, ct: ct);
        return (await ListAsync(ct: ct)).First(x => x.Id == id);
    }
}

public sealed class RepairMediaService : IRepairMediaService
{
    private readonly WorkshopDbContext _db;
    private readonly IConfiguration _config;
    private readonly IAuditService _audit;

    public RepairMediaService(WorkshopDbContext db, IConfiguration config, IAuditService audit)
    {
        _db = db;
        _config = config;
        _audit = audit;
    }

    public async Task<IReadOnlyList<RepairAttachmentDto>> ListAttachmentsAsync(Guid ticketId, CancellationToken ct = default)
    {
        await EnsureTicket(ticketId, ct);
        return await _db.RepairAttachments.AsNoTracking().Where(a => a.TicketId == ticketId)
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new RepairAttachmentDto(a.Id, a.Kind, a.FileName, a.ContentType, a.SizeBytes, a.Caption, a.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<RepairAttachmentDto> UploadAttachmentAsync(Guid ticketId, UploadRepairAttachmentRequest request, Guid actorId, CancellationToken ct = default)
    {
        await EnsureTicket(ticketId, ct);
        if (string.IsNullOrWhiteSpace(request.FileName) || string.IsNullOrWhiteSpace(request.Base64Content))
            throw new ValidationAppException("File name and content are required.");
        var bytes = DecodeBase64(request.Base64Content);
        if (bytes.Length > 8 * 1024 * 1024)
            throw new ValidationAppException("Attachment max size is 8 MB.");

        var dir = GetMediaDir("repair-attachments", ticketId);
        Directory.CreateDirectory(dir);
        var safe = string.Join("_", request.FileName.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
        var path = Path.Combine(dir, $"{Guid.NewGuid():N}-{safe}");
        await File.WriteAllBytesAsync(path, bytes, ct);

        var row = new RepairAttachment
        {
            TicketId = ticketId,
            Kind = string.IsNullOrWhiteSpace(request.Kind) ? "Photo" : request.Kind.Trim(),
            FileName = safe,
            ContentType = string.IsNullOrWhiteSpace(request.ContentType) ? "application/octet-stream" : request.ContentType.Trim(),
            StoragePath = path,
            SizeBytes = bytes.Length,
            Caption = string.IsNullOrWhiteSpace(request.Caption) ? null : request.Caption.Trim(),
            UploadedById = actorId
        };
        _db.RepairAttachments.Add(row);
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(actorId, "repair.attachment", "RepairTicket", ticketId.ToString(), ct: ct);
        return new RepairAttachmentDto(row.Id, row.Kind, row.FileName, row.ContentType, row.SizeBytes, row.Caption, row.CreatedAt);
    }

    public async Task<IReadOnlyList<RepairSignatureDto>> ListSignaturesAsync(Guid ticketId, CancellationToken ct = default)
    {
        await EnsureTicket(ticketId, ct);
        return await _db.RepairSignatures.AsNoTracking().Where(s => s.TicketId == ticketId)
            .OrderByDescending(s => s.SignedAt)
            .Select(s => new RepairSignatureDto(s.Id, s.SignerName, s.Purpose, s.SignedAt))
            .ToListAsync(ct);
    }

    public async Task<RepairSignatureDto> CaptureSignatureAsync(Guid ticketId, CaptureRepairSignatureRequest request, Guid actorId, CancellationToken ct = default)
    {
        await EnsureTicket(ticketId, ct);
        if (string.IsNullOrWhiteSpace(request.SignerName) || string.IsNullOrWhiteSpace(request.Base64Png))
            throw new ValidationAppException("Signer name and signature image are required.");
        var bytes = DecodeBase64(request.Base64Png);
        var dir = GetMediaDir("repair-signatures", ticketId);
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"{Guid.NewGuid():N}.png");
        await File.WriteAllBytesAsync(path, bytes, ct);
        var row = new RepairSignature
        {
            TicketId = ticketId,
            SignerName = request.SignerName.Trim(),
            Purpose = string.IsNullOrWhiteSpace(request.Purpose) ? "Intake" : request.Purpose.Trim(),
            StoragePath = path,
            CapturedById = actorId
        };
        _db.RepairSignatures.Add(row);
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(actorId, "repair.signature", "RepairTicket", ticketId.ToString(), ct: ct);
        return new RepairSignatureDto(row.Id, row.SignerName, row.Purpose, row.SignedAt);
    }

    public async Task<string> BuildConditionReportHtmlAsync(Guid ticketId, CancellationToken ct = default)
    {
        var t = await _db.RepairTickets.AsNoTracking()
            .Include(x => x.Customer).Include(x => x.Device).Include(x => x.Status)
            .FirstOrDefaultAsync(x => x.Id == ticketId && x.ArchivedAt == null, ct)
            ?? throw new AppException("not_found", "Repair was not found.", 404);
        var attachments = await _db.RepairAttachments.AsNoTracking().CountAsync(a => a.TicketId == ticketId, ct);
        var sigs = await _db.RepairSignatures.AsNoTracking().Where(s => s.TicketId == ticketId)
            .OrderByDescending(s => s.SignedAt).Take(5).ToListAsync(ct);

        var sb = new StringBuilder();
        sb.Append("<!DOCTYPE html><html><head><meta charset=\"utf-8\"/><title>Condition report ")
            .Append(System.Net.WebUtility.HtmlEncode(t.TicketNumber))
            .Append("</title><style>body{font-family:Segoe UI,Arial,sans-serif;padding:24px;color:#111}")
            .Append("h1{font-size:20px}table{border-collapse:collapse;width:100%}td,th{border:1px solid #ccc;padding:8px;text-align:left}")
            .Append("@media print{button{display:none}}</style></head><body>");
        sb.Append("<button onclick=\"window.print()\">Print / Save PDF</button>");
        sb.Append("<h1>Condition report — ").Append(System.Net.WebUtility.HtmlEncode(t.TicketNumber)).Append("</h1>");
        sb.Append("<p>Customer: <b>").Append(System.Net.WebUtility.HtmlEncode(t.Customer.DisplayName)).Append("</b><br/>");
        sb.Append("Device: <b>").Append(System.Net.WebUtility.HtmlEncode($"{t.Device?.Brand} {t.Device?.Model}".Trim())).Append("</b><br/>");
        sb.Append("Status: ").Append(System.Net.WebUtility.HtmlEncode(t.Status.Name)).Append("</p>");
        sb.Append("<h2>Reported fault</h2><p>").Append(System.Net.WebUtility.HtmlEncode(t.ReportedIssue)).Append("</p>");
        sb.Append("<h2>Condition flags</h2><table><tr><th>Flag</th><th>Value</th></tr>");
        sb.Append(Row("Powers on", t.PowersOn?.ToString() ?? "—"));
        sb.Append(Row("Existing cracks", t.HasExistingCracks?.ToString() ?? "—"));
        sb.Append(Row("Scratches", t.HasScratches?.ToString() ?? "—"));
        sb.Append(Row("Water indicators", t.WaterDamageIndicators?.ToString() ?? "—"));
        sb.Append(Row("Damage notes", t.DamageDescription ?? "—"));
        sb.Append("</table>");
        sb.Append("<p>Photos on file: ").Append(attachments).Append("</p>");
        if (sigs.Count > 0)
        {
            sb.Append("<h2>Signatures</h2><ul>");
            foreach (var s in sigs)
                sb.Append("<li>").Append(System.Net.WebUtility.HtmlEncode($"{s.Purpose}: {s.SignerName} @ {s.SignedAt:u}")).Append("</li>");
            sb.Append("</ul>");
        }
        sb.Append("<p style=\"opacity:.7;font-size:12px\">Customer copy — WorkshopOS condition report</p></body></html>");
        return sb.ToString();

        static string Row(string k, string v) =>
            $"<tr><td>{System.Net.WebUtility.HtmlEncode(k)}</td><td>{System.Net.WebUtility.HtmlEncode(v)}</td></tr>";
    }

    private async Task EnsureTicket(Guid ticketId, CancellationToken ct)
    {
        if (!await _db.RepairTickets.AnyAsync(t => t.Id == ticketId && t.ArchivedAt == null, ct))
            throw new AppException("not_found", "Repair was not found.", 404);
    }

    private string GetMediaDir(string kind, Guid ticketId)
    {
        var root = Environment.GetEnvironmentVariable("MEDIA_DIR")
            ?? _config["Media:Directory"]
            ?? Path.Combine(Path.GetTempPath(), "workshopos-media");
        return Path.Combine(root, kind, ticketId.ToString("N"));
    }

    private static byte[] DecodeBase64(string raw)
    {
        var s = raw.Trim();
        var idx = s.IndexOf("base64,", StringComparison.OrdinalIgnoreCase);
        if (idx >= 0) s = s[(idx + 7)..];
        try { return Convert.FromBase64String(s); }
        catch { throw new ValidationAppException("Invalid base64 content."); }
    }
}

public sealed class PrinterProfileService : IPrinterProfileService
{
    private readonly WorkshopDbContext _db;
    private readonly IAuditService _audit;
    private readonly ILabelPrintService _labels;

    public PrinterProfileService(WorkshopDbContext db, IAuditService audit, ILabelPrintService labels)
    {
        _db = db;
        _audit = audit;
        _labels = labels;
    }

    public async Task<IReadOnlyList<PrinterProfileDto>> ListAsync(CancellationToken ct = default) =>
        await _db.PrinterProfiles.AsNoTracking().Where(p => p.ArchivedAt == null).OrderBy(p => p.Name)
            .Select(p => new PrinterProfileDto(p.Id, p.Name, p.Kind, p.Connection, p.DefaultTemplate, p.IsDefault))
            .ToListAsync(ct);

    public async Task<PrinterProfileDto> UpsertAsync(UpsertPrinterProfileRequest request, Guid actorId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) throw new ValidationAppException("Name is required.");
        PrinterProfile p;
        if (request.Id is Guid id)
        {
            p = await _db.PrinterProfiles.FirstOrDefaultAsync(x => x.Id == id && x.ArchivedAt == null, ct)
                ?? throw new AppException("not_found", "Printer profile was not found.", 404);
        }
        else
        {
            p = new PrinterProfile();
            _db.PrinterProfiles.Add(p);
        }
        p.Name = request.Name.Trim();
        p.Kind = string.IsNullOrWhiteSpace(request.Kind) ? "PdfFallback" : request.Kind.Trim();
        p.Connection = string.IsNullOrWhiteSpace(request.Connection) ? null : request.Connection.Trim();
        p.DefaultTemplate = string.IsNullOrWhiteSpace(request.DefaultTemplate) ? null : request.DefaultTemplate.Trim();
        p.IsDefault = request.IsDefault;
        p.UpdatedAt = DateTimeOffset.UtcNow;
        if (p.IsDefault)
        {
            var others = await _db.PrinterProfiles.Where(x => x.Id != p.Id && x.ArchivedAt == null && x.IsDefault).ToListAsync(ct);
            foreach (var o in others) o.IsDefault = false;
        }
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(actorId, "printer.upsert", "PrinterProfile", p.Id.ToString(), ct: ct);
        return new PrinterProfileDto(p.Id, p.Name, p.Kind, p.Connection, p.DefaultTemplate, p.IsDefault);
    }

    public async Task<BatchLabelResult> BatchRenderAsync(BatchLabelRequest request, Guid actorId, CancellationToken ct = default)
    {
        if (request.Ids is null || request.Ids.Count == 0)
            throw new ValidationAppException("Select at least one id.");
        if (request.Ids.Count > 50)
            throw new ValidationAppException("Batch limit is 50 labels.");

        var parts = new List<string>();
        foreach (var id in request.Ids)
        {
            var html = request.LabelType.ToLowerInvariant() switch
            {
                "repair" => await _labels.RenderRepairLabelHtmlAsync(id, ct),
                "used" => await _labels.RenderUsedDeviceLabelHtmlAsync(id, request.IncludePrice, ct),
                "part" => await _labels.RenderPartLabelHtmlAsync(id, ct),
                _ => throw new ValidationAppException("LabelType must be repair, used, or part.")
            };
            // Strip outer html wrappers for concatenate — keep label div only
            parts.Add(html);
            _db.PrintJobs.Add(new PrintJob
            {
                LabelType = request.LabelType,
                EntityType = request.LabelType,
                EntityId = id.ToString(),
                Status = "Rendered",
                RequestedById = actorId
            });
        }
        await _db.SaveChangesAsync(ct);
        var combined = "<!DOCTYPE html><html><head><meta charset=\"utf-8\"/><title>Batch labels</title>" +
                       "<style>@media print{.break{page-break-after:always}}</style></head><body>" +
                       string.Join("<div class=\"break\"></div>", parts) + "</body></html>";
        return new BatchLabelResult(combined, parts.Count);
    }
}

public sealed class IntegrationHealthService : IIntegrationHealthService
{
    private readonly WorkshopDbContext _db;
    public IntegrationHealthService(WorkshopDbContext db) => _db = db;

    public async Task<IReadOnlyList<IntegrationHealthEventDto>> ListAsync(CancellationToken ct = default) =>
        await _db.IntegrationHealthEvents.AsNoTracking().OrderByDescending(e => e.CreatedAt).Take(100)
            .Select(e => new IntegrationHealthEventDto(e.Id, e.ProviderKey, e.Success, e.Message, e.CreatedAt))
            .ToListAsync(ct);

    public async Task RecordAsync(string providerKey, bool success, string message, Guid? actorId, CancellationToken ct = default)
    {
        _db.IntegrationHealthEvents.Add(new IntegrationHealthEvent
        {
            ProviderKey = providerKey,
            Success = success,
            Message = message.Length > 500 ? message[..500] : message,
            ActorUserId = actorId
        });
        await _db.SaveChangesAsync(ct);
    }
}
