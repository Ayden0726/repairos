using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using WorkshopOS.Application.Abstractions;
using WorkshopOS.Application.Common;
using WorkshopOS.Contracts.Workshop;
using WorkshopOS.Domain.Entities;
using WorkshopOS.Infrastructure.Persistence;

namespace WorkshopOS.Infrastructure.Services;

public sealed class SecretProtector
{
    private readonly byte[] _key;

    public SecretProtector(IConfiguration config)
    {
        var raw = config["Encryption:Key"] ?? config["Jwt:SigningKey"]
            ?? throw new InvalidOperationException("Encryption:Key or Jwt:SigningKey is required.");
        _key = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
    }

    public string Encrypt(string plaintext)
    {
        var iv = RandomNumberGenerator.GetBytes(12);
        using var aes = new AesGcm(_key, 16);
        var plain = Encoding.UTF8.GetBytes(plaintext);
        var cipher = new byte[plain.Length];
        var tag = new byte[16];
        aes.Encrypt(iv, plain, cipher, tag);
        return $"v1:{Convert.ToBase64String(iv)}:{Convert.ToBase64String(tag)}:{Convert.ToBase64String(cipher)}";
    }
}

public sealed class CustomerService : ICustomerService
{
    private readonly WorkshopDbContext _db;
    private readonly IAuditService _audit;

    public CustomerService(WorkshopDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<PagedResult<CustomerListItemDto>> ListAsync(string? q, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = _db.Customers.AsNoTracking().Where(c => c.ArchivedAt == null);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLowerInvariant();
            query = query.Where(c =>
                c.DisplayName.ToLower().Contains(term) ||
                (c.Phone != null && c.Phone.ToLower().Contains(term)) ||
                (c.Email != null && c.Email.ToLower().Contains(term)) ||
                (c.CompanyName != null && c.CompanyName.ToLower().Contains(term)));
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(c => c.DisplayName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new CustomerListItemDto(
                c.Id,
                c.DisplayName,
                c.Type,
                c.Phone,
                c.Email,
                c.CreatedAt,
                c.LastVisitAt,
                c.Devices.Count(d => d.ArchivedAt == null),
                c.Repairs.Count(r => r.ArchivedAt == null && !r.Status.IsCompleted && !r.Status.IsCancelled)))
            .ToListAsync(ct);
        return new PagedResult<CustomerListItemDto>(items, total, page, pageSize);
    }

    public async Task<CustomerDetailDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var c = await _db.Customers.AsNoTracking()
            .Include(x => x.Devices.Where(d => d.ArchivedAt == null))
            .Include(x => x.Repairs.Where(r => r.ArchivedAt == null)).ThenInclude(r => r.Status)
            .Include(x => x.Repairs).ThenInclude(r => r.Type)
            .Include(x => x.Repairs).ThenInclude(r => r.Priority)
            .Include(x => x.Repairs).ThenInclude(r => r.AssignedTo)
            .Include(x => x.Repairs).ThenInclude(r => r.Device)
            .FirstOrDefaultAsync(x => x.Id == id && x.ArchivedAt == null, ct)
            ?? throw new AppException("not_found", "Customer was not found.", 404);

        var devices = c.Devices.OrderBy(d => d.Brand).ThenBy(d => d.Model)
            .Select(MapDevice).ToList();
        var repairs = c.Repairs.OrderByDescending(r => r.CreatedAt).Take(20)
            .Select(MapRepairList).ToList();

        return new CustomerDetailDto(
            c.Id, c.Type, c.FirstName, c.LastName, c.DisplayName, c.CompanyName, c.Phone, c.Email,
            c.AddressLine1, c.Suburb, c.State, c.Postcode, c.Notes, c.PreferredContact, c.MarketingConsent,
            c.CreatedAt, c.LastVisitAt, devices, repairs);
    }

    public async Task<CustomerDetailDto> UpsertAsync(UpsertCustomerRequest request, Guid actorId, CancellationToken ct = default)
    {
        var display = BuildDisplayName(request);
        if (string.IsNullOrWhiteSpace(display))
            throw new ValidationAppException("Customer name is required.");

        Customer customer;
        if (request.Id is Guid id)
        {
            customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == id && c.ArchivedAt == null, ct)
                ?? throw new AppException("not_found", "Customer was not found.", 404);
        }
        else
        {
            customer = new Customer();
            _db.Customers.Add(customer);
        }

        customer.Type = request.Type;
        customer.FirstName = Trim(request.FirstName);
        customer.LastName = Trim(request.LastName);
        customer.CompanyName = Trim(request.CompanyName);
        customer.DisplayName = display;
        customer.Phone = Trim(request.Phone);
        customer.Email = Trim(request.Email)?.ToLowerInvariant();
        customer.AddressLine1 = Trim(request.AddressLine1);
        customer.Suburb = Trim(request.Suburb);
        customer.State = Trim(request.State);
        customer.Postcode = Trim(request.Postcode);
        customer.Notes = Trim(request.Notes);
        customer.PreferredContact = request.PreferredContact;
        customer.MarketingConsent = request.MarketingConsent;
        customer.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(actorId, request.Id is null ? "customer.create" : "customer.update", "Customer", customer.Id.ToString(), ct: ct);
        return await GetAsync(customer.Id, ct);
    }

    internal static string BuildDisplayName(UpsertCustomerRequest request)
    {
        if (request.Type == CustomerType.Business && !string.IsNullOrWhiteSpace(request.CompanyName))
            return request.CompanyName.Trim();
        var name = $"{request.FirstName} {request.LastName}".Trim();
        if (!string.IsNullOrWhiteSpace(name)) return name;
        return request.CompanyName?.Trim() ?? request.Phone?.Trim() ?? request.Email?.Trim() ?? string.Empty;
    }

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    internal static DeviceListItemDto MapDevice(Device d) =>
        new(d.Id, d.CustomerId, d.Customer?.DisplayName ?? string.Empty, d.Category, d.Brand, d.Model, d.Variant, d.Serial, d.Imei,
            $"{d.Brand} {d.Model}".Trim());

    internal static RepairListItemDto MapRepairList(RepairTicket r)
    {
        var overdue = r.DueAt is DateTimeOffset due && due < DateTimeOffset.UtcNow && !r.Status.IsCompleted && !r.Status.IsCancelled;
        return new RepairListItemDto(
            r.Id, r.TicketNumber, r.Customer?.DisplayName ?? string.Empty,
            r.Device is null ? null : $"{r.Device.Brand} {r.Device.Model}".Trim(),
            r.Type.Name, r.Status.Key, r.Status.Name, r.Status.Colour, r.Priority.Name,
            r.AssignedTo?.DisplayName, r.ReportedIssue, r.CreatedAt, r.DueAt, overdue);
    }
}

public sealed class DeviceService : IDeviceService
{
    private readonly WorkshopDbContext _db;
    private readonly IAuditService _audit;

    public DeviceService(WorkshopDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<IReadOnlyList<DeviceListItemDto>> ListForCustomerAsync(Guid customerId, CancellationToken ct = default)
    {
        return await _db.Devices.AsNoTracking()
            .Where(d => d.CustomerId == customerId && d.ArchivedAt == null)
            .OrderBy(d => d.Brand).ThenBy(d => d.Model)
            .Select(d => new DeviceListItemDto(d.Id, d.CustomerId, d.Customer.DisplayName, d.Category, d.Brand, d.Model, d.Variant, d.Serial, d.Imei, (d.Brand + " " + d.Model).Trim()))
            .ToListAsync(ct);
    }

    public async Task<DeviceListItemDto> UpsertAsync(UpsertDeviceRequest request, Guid actorId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Brand) || string.IsNullOrWhiteSpace(request.Model))
            throw new ValidationAppException("Device brand and model are required.");
        _ = await _db.Customers.FirstOrDefaultAsync(c => c.Id == request.CustomerId && c.ArchivedAt == null, ct)
            ?? throw new AppException("not_found", "Customer was not found.", 404);

        Device device;
        if (request.Id is Guid id)
        {
            device = await _db.Devices.Include(d => d.Customer).FirstOrDefaultAsync(d => d.Id == id && d.ArchivedAt == null, ct)
                ?? throw new AppException("not_found", "Device was not found.", 404);
        }
        else
        {
            device = new Device { CustomerId = request.CustomerId };
            _db.Devices.Add(device);
        }

        device.Category = request.Category;
        device.Brand = request.Brand.Trim();
        device.Model = request.Model.Trim();
        device.Variant = string.IsNullOrWhiteSpace(request.Variant) ? null : request.Variant.Trim();
        device.Colour = string.IsNullOrWhiteSpace(request.Colour) ? null : request.Colour.Trim();
        device.Serial = string.IsNullOrWhiteSpace(request.Serial) ? null : request.Serial.Trim();
        device.Imei = string.IsNullOrWhiteSpace(request.Imei) ? null : request.Imei.Trim();
        device.StorageCapacity = string.IsNullOrWhiteSpace(request.StorageCapacity) ? null : request.StorageCapacity.Trim();
        device.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        device.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _db.Entry(device).Reference(d => d.Customer).LoadAsync(ct);
        await _audit.WriteAsync(actorId, request.Id is null ? "device.create" : "device.update", "Device", device.Id.ToString(), ct: ct);
        return CustomerService.MapDevice(device);
    }
}
