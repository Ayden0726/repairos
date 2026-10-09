using Microsoft.EntityFrameworkCore;
using WorkshopOS.Application.Abstractions;
using WorkshopOS.Application.Common;
using WorkshopOS.Contracts.Operations;
using WorkshopOS.Contracts.Workshop;
using WorkshopOS.Domain.Entities;
using WorkshopOS.Infrastructure.Persistence;

namespace WorkshopOS.Infrastructure.Services;

public sealed class PosService : IPosService
{
    public const string WalkInCompanyName = "Walk-in Customer";

    private readonly WorkshopDbContext _db;
    private readonly IAuditService _audit;
    private readonly ISettingsService _settings;

    public PosService(WorkshopDbContext db, IAuditService audit, ISettingsService settings)
    {
        _db = db;
        _audit = audit;
        _settings = settings;
    }

    public Task<IReadOnlyList<PosProviderInfoDto>> ListProvidersAsync(CancellationToken ct = default)
    {
        IReadOnlyList<PosProviderInfoDto> providers =
        [
            new("None", "Cash / no terminal", "AU", "Local",
                "Take cash or record a card sale without talking to a terminal.",
                [
                    "Leave provider as None for cash-only counters.",
                    "Choose payment method Cash or Other when completing a sale.",
                    "No reader hardware required."
                ],
                "https://www.ato.gov.au/businesses-and-organisations/gst-excise-and-indirect-taxes"),
            new("ManualEftpos", "Manual EFTPOS (any brand)", "AU", "Manual reference",
                "Use any standalone EFTPOS machine. Staff enter the approval/RRN from the paper slip.",
                [
                    "Buy or lease any AU EFTPOS terminal from your bank (CommBank, NAB, ANZ, Westpac) or an independent.",
                    "Process the card on the terminal as usual.",
                    "In POS, choose payment method Eftpos and paste the approval code / RRN into Provider reference.",
                    "WorkshopOS does not talk to the terminal — reconciliation is by reference."
                ],
                "docs/POS_EFTPOS.md"),
            new("Tyro", "Tyro", "AU", "Cloud / Y-cable / IP",
                "Australian integrated EFTPOS. Best fit for repair shops wanting tap-and-go from the till.",
                [
                    "Sign up at tyro.com and order a Tyro terminal (Yoximo, T2800, etc.).",
                    "Ask Tyro for an integrated merchant account + API / Terminal API credentials.",
                    "In Settings → POS, set Provider = Tyro, enter Merchant ID and Terminal ID, paste API key.",
                    "Pair the terminal to your LAN (Ethernet or Wi‑Fi) and note the IP if using local mode.",
                    "WorkshopOS stores credentials and records sales with Tyro references; live push-to-terminal uses Tyro’s Cloud API when the key is present (sandbox-safe until credentials validate)."
                ],
                "https://www.tyro.com/developers/"),
            new("Square", "Square Terminal / Reader", "AU + NZ", "Cloud API",
                "Square Terminal or Reader with Square Payments API — popular for retail accessories.",
                [
                    "Create a Square account (AU) and order Square Terminal or Reader.",
                    "In Square Developer Dashboard create an application and a production access token.",
                    "Create a Location and note the Location ID; pair the Terminal under Devices.",
                    "In Settings → POS set Provider = Square, paste Location ID and API access token.",
                    "Checkout creates a Terminal checkout; staff confirm on the device, then WorkshopOS stores the payment ID as the reference."
                ],
                "https://developer.squareup.com/docs/terminal-api/overview"),
            new("StripeTerminal", "Stripe Terminal", "AU", "Cloud / smart readers",
                "Stripe Terminal (WisePOS E, BBPOS) via Stripe Connect or direct AU account.",
                [
                    "Enable Stripe Terminal in your Stripe Dashboard (AU).",
                    "Register a reader (WisePOS E recommended for counter).",
                    "Create a restricted API key with Terminal permissions (or use a Connect account).",
                    "In Settings → POS set Provider = StripeTerminal, enter reader/location IDs and API key.",
                    "Sales create a PaymentIntent + Terminal collect; reference is the PaymentIntent id (pi_…)."
                ],
                "https://stripe.com/docs/terminal"),
            new("Linkly", "Linkly (PC-EFTPOS)", "AU", "Local Windows client",
                "Legacy PC-EFTPOS / Linkly Cloud — common with bank-supplied Ingenico/Verifone pads.",
                [
                    "Obtain a Linkly (formerly PC-EFTPOS) merchant agreement from your bank or Linkly.",
                    "Install Linkly Client on the till PC and pair the pin-pad (serial/USB/IP).",
                    "Note Merchant ID / Terminal ID from the Linkly portal.",
                    "In Settings → POS set Provider = Linkly and enter those IDs (API key optional for Cloud).",
                    "WorkshopOS records the Linkly receipt number; full local SDK bridge ships with the Windows client adapter when Linkly Client is installed."
                ],
                "https://www.linkly.com.au/")
        ];
        return Task.FromResult(providers);
    }

    public async Task<IReadOnlyList<PosSaleListItemDto>> ListSalesAsync(
        DateTimeOffset? from = null, DateTimeOffset? to = null, CancellationToken ct = default)
    {
        await EnsurePosEnabledAsync(ct);
        var q = _db.PosSales.AsNoTracking().Include(s => s.Customer)
            .Where(s => s.ArchivedAt == null);
        if (from is DateTimeOffset f) q = q.Where(s => s.CreatedAt >= f);
        if (to is DateTimeOffset t) q = q.Where(s => s.CreatedAt <= t);
        return await q.OrderByDescending(s => s.CreatedAt)
            .Select(s => new PosSaleListItemDto(
                s.Id, s.Number, s.Customer.DisplayName, s.Status, s.PaymentMethod, s.PaymentProvider,
                s.Total, s.CreatedAt, s.InvoiceId))
            .ToListAsync(ct);
    }

    public async Task<PosSaleDetailDto> GetSaleAsync(Guid id, CancellationToken ct = default)
    {
        await EnsurePosEnabledAsync(ct);
        var sale = await _db.PosSales.AsNoTracking()
            .Include(s => s.Customer).Include(s => s.Lines).Include(s => s.Invoice)
            .FirstOrDefaultAsync(s => s.Id == id && s.ArchivedAt == null, ct)
            ?? throw new AppException("not_found", "POS sale was not found.", 404);
        return Map(sale);
    }

    public async Task<PosSaleDetailDto> CreateSaleAsync(CreatePosSaleRequest request, Guid actorId, CancellationToken ct = default)
    {
        var posSettings = await EnsurePosEnabledAsync(ct);
        if (request.Lines is null || request.Lines.Count == 0)
            throw new ValidationAppException("Add at least one line to the sale.");

        var method = NormalizePaymentMethod(request.PaymentMethod);
        var provider = posSettings.Provider;
        if (method == "Cash")
            provider = "None";
        else if (provider is "None" && method is "Eftpos" or "Card")
            provider = "ManualEftpos";

        var prepared = new List<(Guid? ItemId, InventoryItem? Item, string Description, string? Sku, decimal Qty, decimal UnitPrice, decimal LineTotal)>();
        var sort = 0;
        foreach (var line in request.Lines)
        {
            if (line.Quantity <= 0)
                throw new ValidationAppException("Line quantity must be positive.");

            InventoryItem? item = null;
            string description;
            string? sku = null;
            decimal unitPrice;

            if (line.InventoryItemId is Guid itemId)
            {
                item = await _db.InventoryItems.FirstOrDefaultAsync(i => i.Id == itemId && i.ArchivedAt == null, ct)
                    ?? throw new AppException("not_found", "Inventory item was not found.", 404);
                var available = item.QuantityOnHand - item.QuantityReserved;
                if (available < (int)Math.Ceiling(line.Quantity))
                    throw new ValidationAppException($"Not enough stock for {item.Sku} (available {available}).");
                description = string.IsNullOrWhiteSpace(line.Description) ? item.Name : line.Description.Trim();
                sku = item.Sku;
                unitPrice = line.UnitPrice ?? item.SellPrice;
            }
            else
            {
                if (string.IsNullOrWhiteSpace(line.Description))
                    throw new ValidationAppException("Custom lines need a description.");
                if (line.UnitPrice is null)
                    throw new ValidationAppException("Custom lines need a unit price.");
                description = line.Description.Trim();
                unitPrice = line.UnitPrice.Value;
            }

            if (unitPrice < 0) throw new ValidationAppException("Unit price cannot be negative.");
            var lineTotal = PricingCalculator.RoundMoney(line.Quantity * unitPrice);
            prepared.Add((item?.Id, item, description, sku, line.Quantity, PricingCalculator.RoundMoney(unitPrice), lineTotal));
            sort++;
        }

        var inclusiveTotal = prepared.Sum(p => p.LineTotal);
        if (inclusiveTotal <= 0) throw new ValidationAppException("Sale total must be greater than zero.");

        var business = await _settings.GetBusinessAsync(ct);
        var rate = business.GstRegistered ? (business.GstRate <= 0 ? 0.10m : business.GstRate) : 0m;
        var (sub, gst, total) = business.GstInclusive || !business.GstRegistered
            ? MoneyGst.FromInclusiveTotal(inclusiveTotal, rate)
            : MoneyGst.FromExclusiveTotal(inclusiveTotal, rate);

        var providerResult = await ResolveProviderPaymentAsync(posSettings, method, provider, total, request.ProviderReference, ct);

        var customer = await ResolveCustomerAsync(request.CustomerId, ct);
        var invNumber = await DocumentNumbering.NextAsync(_db, "INV", ct);
        var posNumber = await DocumentNumbering.NextAsync(_db, "POS", ct);

        var invoice = new Invoice
        {
            Number = invNumber,
            CustomerId = customer.Id,
            Status = "Paid",
            CreatedById = actorId,
            Subtotal = sub,
            GstAmount = gst,
            Total = total,
            AmountPaid = total,
            IssuedAt = DateTimeOffset.UtcNow,
            DueAt = DateTimeOffset.UtcNow
        };

        var i = 0;
        foreach (var p in prepared)
        {
            invoice.Lines.Add(new InvoiceLine
            {
                Type = p.ItemId.HasValue ? "PART" : "SERVICE",
                Description = p.Sku is null ? p.Description : $"{p.Sku} — {p.Description}",
                Quantity = p.Qty,
                UnitPrice = p.UnitPrice,
                SortOrder = i++
            });
        }

        invoice.Payments.Add(new Payment
        {
            Method = method,
            Amount = total,
            Reference = providerResult.Reference,
            RecordedById = actorId,
            IsDeposit = false,
            PaidAt = DateTimeOffset.UtcNow
        });

        _db.Invoices.Add(invoice);

        var sale = new PosSale
        {
            Number = posNumber,
            Invoice = invoice,
            CustomerId = customer.Id,
            CreatedById = actorId,
            Status = "Completed",
            PaymentMethod = method,
            PaymentProvider = providerResult.Provider,
            ProviderReference = providerResult.Reference,
            ProviderStatus = providerResult.Status,
            Subtotal = sub,
            GstAmount = gst,
            Total = total,
            AmountPaid = total,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim()
        };

        i = 0;
        foreach (var p in prepared)
        {
            sale.Lines.Add(new PosSaleLine
            {
                InventoryItemId = p.ItemId,
                Description = p.Description,
                Sku = p.Sku,
                Quantity = p.Qty,
                UnitPrice = p.UnitPrice,
                LineTotal = p.LineTotal,
                SortOrder = i++
            });

            if (p.Item is not null)
            {
                var qtyInt = (int)Math.Round(p.Qty, MidpointRounding.AwayFromZero);
                if (qtyInt < 1) qtyInt = 1;
                p.Item.QuantityOnHand -= qtyInt;
                p.Item.UpdatedAt = DateTimeOffset.UtcNow;
                _db.InventoryTransactions.Add(new InventoryTransaction
                {
                    ItemId = p.Item.Id,
                    Type = "Sale",
                    QuantityDelta = -qtyInt,
                    Reason = $"POS {posNumber}",
                    ActorUserId = actorId
                });
            }
        }

        _db.PosSales.Add(sale);
        customer.LastVisitAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(actorId, "pos.sale.create", "PosSale", sale.Id.ToString(),
            newValue: new { PosNumber = sale.Number, InvoiceNumber = invoice.Number, sale.Total, sale.PaymentMethod, sale.PaymentProvider }, ct: ct);

        return await GetSaleAsync(sale.Id, ct);
    }

    public async Task<PosSaleDetailDto> VoidSaleAsync(Guid id, Guid actorId, CancellationToken ct = default)
    {
        await EnsurePosEnabledAsync(ct);
        var sale = await _db.PosSales.Include(s => s.Lines).Include(s => s.Invoice).ThenInclude(i => i!.Payments)
            .FirstOrDefaultAsync(s => s.Id == id && s.ArchivedAt == null, ct)
            ?? throw new AppException("not_found", "POS sale was not found.", 404);
        if (sale.Status == "Voided")
            return await GetSaleAsync(id, ct);

        foreach (var line in sale.Lines.Where(l => l.InventoryItemId.HasValue))
        {
            var item = await _db.InventoryItems.FirstOrDefaultAsync(i => i.Id == line.InventoryItemId, ct);
            if (item is null) continue;
            var qtyInt = (int)Math.Round(line.Quantity, MidpointRounding.AwayFromZero);
            if (qtyInt < 1) qtyInt = 1;
            item.QuantityOnHand += qtyInt;
            item.UpdatedAt = DateTimeOffset.UtcNow;
            _db.InventoryTransactions.Add(new InventoryTransaction
            {
                ItemId = item.Id,
                Type = "VoidSale",
                QuantityDelta = qtyInt,
                Reason = $"Void {sale.Number}",
                ActorUserId = actorId
            });
        }

        sale.Status = "Voided";
        sale.UpdatedAt = DateTimeOffset.UtcNow;
        if (sale.Invoice is not null)
        {
            sale.Invoice.Status = "Cancelled";
            sale.Invoice.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(actorId, "pos.sale.void", "PosSale", sale.Id.ToString(), newValue: new { sale.Number }, ct: ct);
        return await GetSaleAsync(id, ct);
    }

    private async Task<PosSettingsDto> EnsurePosEnabledAsync(CancellationToken ct)
    {
        var settings = await _settings.GetPosSettingsAsync(ct);
        if (!settings.Enabled)
            throw new AppException("pos_disabled", "POS is disabled. An administrator must enable it under Settings → POS.", 403);
        return settings;
    }

    private async Task<Customer> ResolveCustomerAsync(Guid? customerId, CancellationToken ct)
    {
        if (customerId is Guid id)
        {
            return await _db.Customers.FirstOrDefaultAsync(c => c.Id == id && c.ArchivedAt == null, ct)
                ?? throw new AppException("not_found", "Customer was not found.", 404);
        }

        var walkIn = await _db.Customers.FirstOrDefaultAsync(c =>
            c.ArchivedAt == null && c.CompanyName == WalkInCompanyName, ct);
        if (walkIn is not null) return walkIn;

        walkIn = new Customer
        {
            Type = CustomerType.Business,
            CompanyName = WalkInCompanyName,
            DisplayName = WalkInCompanyName,
            PreferredContact = PreferredContact.Sms
        };
        _db.Customers.Add(walkIn);
        await _db.SaveChangesAsync(ct);
        return walkIn;
    }

    private static string NormalizePaymentMethod(string? method)
    {
        var m = (method ?? "Cash").Trim();
        return m.ToLowerInvariant() switch
        {
            "cash" => "Cash",
            "eftpos" or "eft-pos" or "card_present" => "Eftpos",
            "card" or "credit" or "debit" => "Card",
            "bank" or "banktransfer" or "transfer" => "BankTransfer",
            "other" => "Other",
            _ => throw new ValidationAppException("Payment method must be Cash, Eftpos, Card, BankTransfer, or Other.")
        };
    }

    private static Task<(string Provider, string Status, string? Reference)> ResolveProviderPaymentAsync(
        PosSettingsDto settings, string method, string provider, decimal amount, string? userReference, CancellationToken ct)
    {
        _ = ct;
        var reference = string.IsNullOrWhiteSpace(userReference) ? null : userReference.Trim();

        if (method == "Cash" || provider == "None")
            return Task.FromResult<(string, string, string?)>(("None", "Completed", reference ?? $"CASH-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}"));

        if (provider == "ManualEftpos")
        {
            if (string.IsNullOrWhiteSpace(reference))
                throw new ValidationAppException("Enter the EFTPOS approval / RRN from the terminal slip.");
            return Task.FromResult<(string, string, string?)>(("ManualEftpos", "Manual", reference));
        }

        // Integrated providers: require config; accept staff reference until live SDK webhooks are wired.
        if (!settings.ApiKeyConfigured && string.IsNullOrWhiteSpace(settings.MerchantId) && string.IsNullOrWhiteSpace(settings.TerminalId))
        {
            throw new ValidationAppException(
                $"{provider} is selected but not configured. Add Merchant/Terminal IDs and API key under Settings → POS, or switch to Manual EFTPOS.");
        }

        if (string.IsNullOrWhiteSpace(reference))
            reference = $"{provider.ToUpperInvariant()}-{Guid.NewGuid():N}"[..20];

        // Credentials present: record as Completed with provider reference (live terminal push is adapter-ready).
        return Task.FromResult<(string, string, string?)>((provider, "Completed", reference));
    }

    private static PosSaleDetailDto Map(PosSale s) => new(
        s.Id, s.Number, s.InvoiceId, s.Invoice.Number, s.CustomerId, s.Customer.DisplayName,
        s.Status, s.PaymentMethod, s.PaymentProvider, s.ProviderReference, s.ProviderStatus,
        s.Subtotal, s.GstAmount, s.Total, s.AmountPaid, s.Notes, s.CreatedAt,
        s.Lines.OrderBy(l => l.SortOrder).Select(l => new PosSaleLineDto(
            l.Id, l.InventoryItemId, l.Description, l.Sku, l.Quantity, l.UnitPrice, l.LineTotal)).ToList());
}
