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
    private readonly IInvoiceService _invoices;

    public PosService(WorkshopDbContext db, IAuditService audit, ISettingsService settings, IInvoiceService invoices)
    {
        _db = db;
        _audit = audit;
        _settings = settings;
        _invoices = invoices;
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
                s.Total, s.CreatedAt, s.InvoiceId, s.RepairTicketId, s.IsDeposit))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<PosPayableRepairDto>> ListPayableRepairsAsync(
        string? q = null, CancellationToken ct = default)
    {
        await EnsurePosEnabledAsync(ct);
        var term = q?.Trim().ToLowerInvariant();

        var tickets = await _db.RepairTickets.AsNoTracking()
            .Include(t => t.Customer)
            .Include(t => t.Device)
            .Include(t => t.Status)
            .Where(t => t.ArchivedAt == null
                        && t.Status.Key != "cancelled"
                        && t.Status.Key != "completed"
                        && t.Status.Key != "collected")
            .OrderByDescending(t => t.CreatedAt)
            .Take(200)
            .ToListAsync(ct);

        if (!string.IsNullOrEmpty(term))
        {
            tickets = tickets.Where(t =>
                t.TicketNumber.ToLowerInvariant().Contains(term) ||
                t.Customer.DisplayName.ToLowerInvariant().Contains(term) ||
                (t.Device != null && ($"{t.Device.Brand} {t.Device.Model}").ToLowerInvariant().Contains(term))).ToList();
        }

        var ticketIds = tickets.Select(t => t.Id).ToList();
        var invoices = await _db.Invoices.AsNoTracking()
            .Where(i => i.ArchivedAt == null && i.RepairTicketId != null && ticketIds.Contains(i.RepairTicketId.Value)
                        && i.Status != "Cancelled" && i.Status != "Refunded")
            .ToListAsync(ct);
        var invByTicket = invoices
            .GroupBy(i => i.RepairTicketId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(i => i.CreatedAt).First());

        var result = new List<PosPayableRepairDto>();
        foreach (var t in tickets)
        {
            invByTicket.TryGetValue(t.Id, out var inv);
            var total = inv?.Total ?? t.EstimatedPrice ?? 0m;
            var paid = inv?.AmountPaid ?? 0m;
            var balance = Math.Max(0m, total - paid);
            // Include tickets with balance, or those with estimate/quote but no invoice yet
            if (balance <= 0 && inv is not null) continue;
            if (balance <= 0 && (t.EstimatedPrice is null or <= 0)) continue;

            var deviceLabel = t.Device is null ? null : $"{t.Device.Brand} {t.Device.Model}".Trim();
            result.Add(new PosPayableRepairDto(
                t.Id, t.TicketNumber, t.Customer.DisplayName, deviceLabel, t.Status.Name,
                inv?.Id, inv?.Number, total, paid,
                balance > 0 ? balance : total,
                t.EstimatedPrice));
        }

        return result.OrderByDescending(r => r.Balance).Take(80).ToList();
    }

    public async Task<PosSaleDetailDto> GetSaleAsync(Guid id, CancellationToken ct = default)
    {
        await EnsurePosEnabledAsync(ct);
        var sale = await _db.PosSales.AsNoTracking()
            .Include(s => s.Customer).Include(s => s.Lines).Include(s => s.Invoice)
            .Include(s => s.RepairTicket)
            .FirstOrDefaultAsync(s => s.Id == id && s.ArchivedAt == null, ct)
            ?? throw new AppException("not_found", "POS sale was not found.", 404);
        return Map(sale);
    }

    public async Task<PosSaleDetailDto> CreateSaleAsync(CreatePosSaleRequest request, Guid actorId, CancellationToken ct = default)
    {
        var posSettings = await EnsurePosEnabledAsync(ct);
        var hasLines = request.Lines is { Count: > 0 };
        var hasRepair = request.RepairTicketId is Guid;

        if (!hasLines && !hasRepair)
            throw new ValidationAppException("Add cart lines or import a repair ticket.");

        var method = NormalizePaymentMethod(request.PaymentMethod);
        var provider = posSettings.Provider;
        if (method == "Cash")
            provider = "None";
        else if (provider is "None" && method is "Eftpos" or "Card")
            provider = "ManualEftpos";

        var (discountPercent, discountLabel) = ResolveDiscount(request, posSettings);

        // --- Retail cart lines ---
        var prepared = new List<(Guid? ItemId, InventoryItem? Item, string Description, string? Sku, decimal Qty, decimal UnitPrice, decimal LineTotal)>();
        if (hasLines)
        {
            foreach (var line in request.Lines!)
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
            }
        }

        var cartGross = prepared.Sum(p => p.LineTotal);
        var cartDiscount = discountPercent > 0
            ? PricingCalculator.RoundMoney(cartGross * discountPercent / 100m)
            : 0m;
        var cartNet = Math.Max(0m, cartGross - cartDiscount);

        // --- Repair invoice / payment ---
        Invoice? repairInvoice = null;
        RepairTicket? repairTicket = null;
        decimal repairPayAmount = 0m;
        decimal repairDiscountAmount = 0m;
        var isDeposit = request.IsDeposit;

        if (hasRepair)
        {
            var ticketId = request.RepairTicketId!.Value;
            repairTicket = await _db.RepairTickets
                .Include(t => t.Customer)
                .FirstOrDefaultAsync(t => t.Id == ticketId && t.ArchivedAt == null, ct)
                ?? throw new AppException("not_found", "Repair ticket was not found.", 404);

            // Ensure invoice exists on this DbContext (avoid cross-service SaveChanges races).
            repairInvoice = await EnsureRepairInvoiceAsync(repairTicket, actorId, ct);
            var balance = Math.Max(0m, repairInvoice.Total - repairInvoice.AmountPaid);
            if (balance <= 0)
                throw new ValidationAppException($"Ticket {repairTicket.TicketNumber} is already paid in full.");

            // Apply discount by reducing invoice totals so the ticket can close at $0 balance.
            // Partial AmountToPay (deposit) skips reduction — discount is for final payoff.
            var applyingBalanceDiscount = discountPercent > 0 && request.AmountToPay is null;
            if (applyingBalanceDiscount)
            {
                repairDiscountAmount = PricingCalculator.RoundMoney(balance * discountPercent / 100m);
                if (repairDiscountAmount > 0)
                {
                    var newInclusive = Math.Max(0m, PricingCalculator.RoundMoney(repairInvoice.Total - repairDiscountAmount));
                    var (invSub, invGst, invTotal) = MoneyGst.FromInclusiveTotal(newInclusive);
                    repairInvoice.Subtotal = invSub;
                    repairInvoice.GstAmount = invGst;
                    repairInvoice.Total = invTotal;
                    repairInvoice.UpdatedAt = DateTimeOffset.UtcNow;
                    balance = Math.Max(0m, repairInvoice.Total - repairInvoice.AmountPaid);
                }
            }

            if (request.AmountToPay is decimal pay)
            {
                repairPayAmount = PricingCalculator.RoundMoney(pay);
                if (repairPayAmount <= 0)
                    throw new ValidationAppException("Payment amount must be positive.");
                if (repairPayAmount > balance + 0.01m)
                    throw new ValidationAppException($"Amount exceeds balance (${balance:0.00}).");
            }
            else
            {
                repairPayAmount = balance;
            }

            // If paying less than balance, treat as deposit unless staff said otherwise
            if (repairPayAmount + 0.01m < balance)
                isDeposit = true;
        }

        var chargeTotal = PricingCalculator.RoundMoney(cartNet + repairPayAmount);
        if (chargeTotal <= 0)
            throw new ValidationAppException("Sale total must be greater than zero.");

        var discountAmount = PricingCalculator.RoundMoney(cartDiscount + repairDiscountAmount);

        var business = await _settings.GetBusinessAsync(ct);
        var rate = business.GstRegistered ? (business.GstRate <= 0 ? 0.10m : business.GstRate) : 0m;

        // For POS sale record: GST split of the amount actually charged now
        var (sub, gst, total) = business.GstInclusive || !business.GstRegistered
            ? MoneyGst.FromInclusiveTotal(chargeTotal, rate)
            : MoneyGst.FromExclusiveTotal(chargeTotal, rate);

        var providerResult = await ResolveProviderPaymentAsync(posSettings, method, provider, total, request.ProviderReference, ct);

        Customer customer;
        if (repairTicket is not null)
            customer = repairTicket.Customer;
        else
            customer = await ResolveCustomerAsync(request.CustomerId, ct);

        // Snapshot ids before clearing the tracker / recording payment on a fresh graph.
        var customerId = customer.Id;
        var repairId = repairTicket?.Id;
        var repairNumber = repairTicket?.TicketNumber;
        var stockDeltas = prepared
            .Where(p => p.Item is not null)
            .Select(p => (ItemId: p.Item!.Id, Qty: p.Qty))
            .ToList();

        Guid invoiceId;
        string invoiceNumber;

        if (repairInvoice is not null && !hasLines)
        {
            // Persist new invoice / discount totals, then record payment on a clean tracker.
            await _db.SaveChangesAsync(ct);

            invoiceId = repairInvoice.Id;
            _db.ChangeTracker.Clear();
            var paid = await _invoices.RecordPaymentAsync(invoiceId,
                new RecordPaymentRequest(method, repairPayAmount, providerResult.Reference, isDeposit),
                actorId, ct);
            invoiceNumber = paid.Number;
        }
        else if (repairInvoice is not null && hasLines)
        {
            await _db.SaveChangesAsync(ct);

            invoiceId = repairInvoice.Id;
            _db.ChangeTracker.Clear();
            if (repairPayAmount > 0)
            {
                await _invoices.RecordPaymentAsync(invoiceId,
                    new RecordPaymentRequest(method, repairPayAmount, providerResult.Reference, isDeposit),
                    actorId, ct);
            }

            var (cartSub, cartGst, cartTotal) = business.GstInclusive || !business.GstRegistered
                ? MoneyGst.FromInclusiveTotal(cartNet, rate)
                : MoneyGst.FromExclusiveTotal(cartNet, rate);

            var invNumber = await DocumentNumbering.NextAsync(_db, "INV", ct);
            var cartInvoice = new Invoice
            {
                Number = invNumber,
                CustomerId = customerId,
                Status = "Paid",
                CreatedById = actorId,
                Subtotal = cartSub,
                GstAmount = cartGst,
                Total = cartTotal,
                AmountPaid = cartTotal,
                IssuedAt = DateTimeOffset.UtcNow,
                DueAt = DateTimeOffset.UtcNow
            };
            var i = 0;
            foreach (var p in prepared)
            {
                cartInvoice.Lines.Add(new InvoiceLine
                {
                    Type = p.ItemId.HasValue ? "PART" : "SERVICE",
                    Description = p.Sku is null ? p.Description : $"{p.Sku} — {p.Description}",
                    Quantity = p.Qty,
                    UnitPrice = p.UnitPrice,
                    SortOrder = i++
                });
            }
            if (cartDiscount > 0)
            {
                cartInvoice.Lines.Add(new InvoiceLine
                {
                    Type = "DISCOUNT",
                    Description = discountLabel ?? $"Discount {discountPercent:0.##}%",
                    Quantity = 1,
                    UnitPrice = -cartDiscount,
                    SortOrder = i
                });
            }
            cartInvoice.Payments.Add(new Payment
            {
                Method = method,
                Amount = cartTotal,
                Reference = providerResult.Reference,
                RecordedById = actorId,
                IsDeposit = false,
                PaidAt = DateTimeOffset.UtcNow
            });
            _db.Invoices.Add(cartInvoice);
            await _db.SaveChangesAsync(ct);
            // PosSale links to the cart invoice for combined sales.
            invoiceId = cartInvoice.Id;
            invoiceNumber = cartInvoice.Number;
            _db.ChangeTracker.Clear();
        }
        else
        {
            // Retail only
            var invNumber = await DocumentNumbering.NextAsync(_db, "INV", ct);
            var retailInvoice = new Invoice
            {
                Number = invNumber,
                CustomerId = customerId,
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
                retailInvoice.Lines.Add(new InvoiceLine
                {
                    Type = p.ItemId.HasValue ? "PART" : "SERVICE",
                    Description = p.Sku is null ? p.Description : $"{p.Sku} — {p.Description}",
                    Quantity = p.Qty,
                    UnitPrice = p.UnitPrice,
                    SortOrder = i++
                });
            }
            if (cartDiscount > 0)
            {
                retailInvoice.Lines.Add(new InvoiceLine
                {
                    Type = "DISCOUNT",
                    Description = discountLabel ?? $"Discount {discountPercent:0.##}%",
                    Quantity = 1,
                    UnitPrice = -cartDiscount,
                    SortOrder = i
                });
            }
            retailInvoice.Payments.Add(new Payment
            {
                Method = method,
                Amount = total,
                Reference = providerResult.Reference,
                RecordedById = actorId,
                IsDeposit = false,
                PaidAt = DateTimeOffset.UtcNow
            });
            _db.Invoices.Add(retailInvoice);
            await _db.SaveChangesAsync(ct);
            invoiceId = retailInvoice.Id;
            invoiceNumber = retailInvoice.Number;
            _db.ChangeTracker.Clear();
        }

        _db.ChangeTracker.Clear();

        var posNumber = await DocumentNumbering.NextAsync(_db, "POS", ct);
        var sale = new PosSale
        {
            Number = posNumber,
            InvoiceId = invoiceId,
            CustomerId = customerId,
            CreatedById = actorId,
            RepairTicketId = repairId,
            Status = "Completed",
            PaymentMethod = method,
            PaymentProvider = providerResult.Provider,
            ProviderReference = providerResult.Reference,
            ProviderStatus = providerResult.Status,
            Subtotal = sub,
            GstAmount = gst,
            Total = total,
            AmountPaid = total,
            DiscountLabel = discountLabel,
            DiscountPercent = discountPercent,
            DiscountAmount = discountAmount,
            IsDeposit = isDeposit && repairId is not null,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim()
        };

        var li = 0;
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
                SortOrder = li++
            });
        }

        foreach (var delta in stockDeltas)
        {
            var item = await _db.InventoryItems.FirstAsync(i => i.Id == delta.ItemId, ct);
            var qtyInt = (int)Math.Round(delta.Qty, MidpointRounding.AwayFromZero);
            if (qtyInt < 1) qtyInt = 1;
            item.QuantityOnHand -= qtyInt;
            item.UpdatedAt = DateTimeOffset.UtcNow;
            _db.InventoryTransactions.Add(new InventoryTransaction
            {
                ItemId = item.Id,
                Type = "Sale",
                QuantityDelta = -qtyInt,
                Reason = $"POS {posNumber}",
                ActorUserId = actorId
            });
        }

        if (repairId is not null && repairPayAmount > 0)
        {
            sale.Lines.Add(new PosSaleLine
            {
                Description = isDeposit
                    ? $"Deposit — {repairNumber}"
                    : $"Repair — {repairNumber}",
                Quantity = 1,
                UnitPrice = repairPayAmount,
                LineTotal = repairPayAmount,
                SortOrder = li
            });
        }

        _db.PosSales.Add(sale);
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(actorId, "pos.sale.create", "PosSale", sale.Id.ToString(),
            newValue: new
            {
                PosNumber = sale.Number,
                InvoiceNumber = invoiceNumber,
                sale.Total,
                sale.PaymentMethod,
                sale.PaymentProvider,
                sale.RepairTicketId,
                sale.IsDeposit,
                sale.DiscountPercent
            }, ct: ct);

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
        if (sale.Invoice is not null && sale.RepairTicketId is null)
        {
            sale.Invoice.Status = "Cancelled";
            sale.Invoice.UpdatedAt = DateTimeOffset.UtcNow;
        }
        else if (sale.Invoice is not null && sale.RepairTicketId is not null)
        {
            // Reverse the payment amount that was recorded for this POS sale
            var payAmount = sale.AmountPaid;
            if (payAmount > 0)
            {
                sale.Invoice.AmountPaid = Math.Max(0, sale.Invoice.AmountPaid - payAmount);
                sale.Invoice.Status = sale.Invoice.AmountPaid <= 0 ? "Unpaid"
                    : sale.Invoice.AmountPaid >= sale.Invoice.Total ? "Paid" : "PartiallyPaid";
                sale.Invoice.UpdatedAt = DateTimeOffset.UtcNow;
            }
        }

        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(actorId, "pos.sale.void", "PosSale", sale.Id.ToString(), newValue: new { sale.Number }, ct: ct);
        return await GetSaleAsync(id, ct);
    }

    private async Task<Invoice> EnsureRepairInvoiceAsync(RepairTicket ticket, Guid actorId, CancellationToken ct)
    {
        var existing = await _db.Invoices
            .FirstOrDefaultAsync(i => i.RepairTicketId == ticket.Id && i.ArchivedAt == null
                                      && i.Status != "Cancelled" && i.Status != "Refunded", ct);
        if (existing is not null)
            return existing;

        var acceptedQuote = await _db.Quotes.AsNoTracking()
            .Where(q => q.RepairTicketId == ticket.Id && q.ArchivedAt == null
                        && (q.Status == "Accepted" || q.Status == "Converted"))
            .OrderByDescending(q => q.AcceptedAt ?? q.UpdatedAt)
            .FirstOrDefaultAsync(ct);

        var labourInclusive = acceptedQuote?.AcceptedTotal
            ?? (acceptedQuote is not null && acceptedQuote.Total > 0 ? acceptedQuote.Total : (decimal?)null)
            ?? ticket.EstimatedPrice
            ?? 0m;
        if (labourInclusive <= 0)
            throw new ValidationAppException("Repair has no estimated price or accepted quote to invoice.");

        var (sub, gst, total) = MoneyGst.FromInclusiveTotal(labourInclusive);
        var number = await DocumentNumbering.NextAsync(_db, "INV", ct);
        var inv = new Invoice
        {
            Number = number,
            CustomerId = ticket.CustomerId,
            RepairTicketId = ticket.Id,
            QuoteId = acceptedQuote?.Id,
            Status = "Unpaid",
            CreatedById = actorId,
            Subtotal = sub,
            GstAmount = gst,
            Total = total,
            AmountPaid = 0,
            IssuedAt = DateTimeOffset.UtcNow,
            DueAt = DateTimeOffset.UtcNow.AddDays(7)
        };
        var lineDesc = acceptedQuote is not null
            ? $"Repair {ticket.TicketNumber} — quote {acceptedQuote.Number}"
            : $"Labour — {ticket.TicketNumber}";
        inv.Lines.Add(new InvoiceLine
        {
            Type = acceptedQuote is not null ? "SERVICE" : "LABOUR",
            Description = lineDesc,
            Quantity = 1,
            UnitPrice = labourInclusive,
            SortOrder = 0
        });
        _db.Invoices.Add(inv);
        // Defer SaveChanges to the caller so POS sale + payment land in one flush.
        return inv;
    }

    private static (decimal Percent, string? Label) ResolveDiscount(CreatePosSaleRequest request, PosSettingsDto settings)
    {
        if (request.DiscountPercent is decimal custom && custom > 0)
        {
            var pct = Math.Clamp(custom, 0m, 100m);
            var label = string.IsNullOrWhiteSpace(request.DiscountLabel)
                ? $"{pct:0.##}% discount"
                : request.DiscountLabel.Trim();
            return (pct, label);
        }

        if (!string.IsNullOrWhiteSpace(request.DiscountPresetKey))
        {
            var key = request.DiscountPresetKey.Trim().ToLowerInvariant();
            var preset = settings.DiscountPresets.FirstOrDefault(p =>
                p.IsActive && string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase));
            if (preset is null)
                throw new ValidationAppException($"Unknown discount preset '{request.DiscountPresetKey}'.");
            if (preset.Percent <= 0 && key is "other")
                throw new ValidationAppException("Enter a custom discount percent for Other.");
            return (preset.Percent, preset.Name);
        }

        return (0m, null);
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
        _ = amount;
        var reference = string.IsNullOrWhiteSpace(userReference) ? null : userReference.Trim();

        if (method == "Cash" || provider == "None")
            return Task.FromResult<(string, string, string?)>(("None", "Completed", reference ?? $"CASH-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}"));

        if (provider == "ManualEftpos")
        {
            if (string.IsNullOrWhiteSpace(reference))
                throw new ValidationAppException("Enter the EFTPOS approval / RRN from the terminal slip.");
            return Task.FromResult<(string, string, string?)>(("ManualEftpos", "Manual", reference));
        }

        if (!settings.ApiKeyConfigured && string.IsNullOrWhiteSpace(settings.MerchantId) && string.IsNullOrWhiteSpace(settings.TerminalId))
        {
            throw new ValidationAppException(
                $"{provider} is selected but not configured. Add Merchant/Terminal IDs and API key under Settings → POS, or switch to Manual EFTPOS.");
        }

        if (string.IsNullOrWhiteSpace(reference))
            reference = $"{provider.ToUpperInvariant()}-{Guid.NewGuid():N}"[..20];

        return Task.FromResult<(string, string, string?)>((provider, "Completed", reference));
    }

    private static PosSaleDetailDto Map(PosSale s)
    {
        var inv = s.Invoice;
        var invBalance = inv is null ? 0m : Math.Max(0m, inv.Total - inv.AmountPaid);
        return new(
            s.Id, s.Number, s.InvoiceId, inv?.Number ?? string.Empty, s.CustomerId, s.Customer.DisplayName,
            s.Status, s.PaymentMethod, s.PaymentProvider, s.ProviderReference, s.ProviderStatus,
            s.Subtotal, s.GstAmount, s.Total, s.AmountPaid, s.Notes, s.CreatedAt,
            s.Lines.OrderBy(l => l.SortOrder).Select(l => new PosSaleLineDto(
                l.Id, l.InventoryItemId, l.Description, l.Sku, l.Quantity, l.UnitPrice, l.LineTotal)).ToList(),
            s.RepairTicketId, s.RepairTicket?.TicketNumber,
            s.DiscountLabel, s.DiscountPercent, s.DiscountAmount, s.IsDeposit, invBalance);
    }
}
