using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WorkshopOS.Domain.Entities;
using WorkshopOS.Domain.Enums;
using WorkshopOS.Domain.Security;

namespace WorkshopOS.Infrastructure.Persistence;

public sealed class WorkshopDbContext : DbContext
{
    public WorkshopDbContext(DbContextOptions<WorkshopDbContext> options) : base(options) { }

    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<AppRole> Roles => Set<AppRole>();
    public DbSet<PermissionDefinition> Permissions => Set<PermissionDefinition>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<BusinessSetting> Settings => Set<BusinessSetting>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<RepairStatus> RepairStatuses => Set<RepairStatus>();
    public DbSet<RepairType> RepairTypes => Set<RepairType>();
    public DbSet<RepairPriority> RepairPriorities => Set<RepairPriority>();
    public DbSet<DocumentSequence> DocumentSequences => Set<DocumentSequence>();
    public DbSet<RepairTicket> RepairTickets => Set<RepairTicket>();
    public DbSet<RepairEvent> RepairEvents => Set<RepairEvent>();
    public DbSet<RepairNote> RepairNotes => Set<RepairNote>();

    public DbSet<Quote> Quotes => Set<Quote>();
    public DbSet<QuoteLine> QuoteLines => Set<QuoteLine>();
    public DbSet<QuoteRevision> QuoteRevisions => Set<QuoteRevision>();
    public DbSet<QuoteAuditEntry> QuoteAuditEntries => Set<QuoteAuditEntry>();
    public DbSet<MarkupTier> MarkupTiers => Set<MarkupTier>();
    public DbSet<ServicePricing> ServicePricings => Set<ServicePricing>();
    public DbSet<ServiceCategory> ServiceCategories => Set<ServiceCategory>();
    public DbSet<ServiceBundle> ServiceBundles => Set<ServiceBundle>();
    public DbSet<ServiceBundleItem> ServiceBundleItems => Set<ServiceBundleItem>();
    public DbSet<ServiceFavourite> ServiceFavourites => Set<ServiceFavourite>();
    public DbSet<ServiceRecentSelection> ServiceRecentSelections => Set<ServiceRecentSelection>();
    public DbSet<DeviceBrand> DeviceBrands => Set<DeviceBrand>();
    public DbSet<DeviceModel> DeviceModels => Set<DeviceModel>();
    public DbSet<RepairServiceLine> RepairServiceLines => Set<RepairServiceLine>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceLine> InvoiceLines => Set<InvoiceLine>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PosSale> PosSales => Set<PosSale>();
    public DbSet<PosSaleLine> PosSaleLines => Set<PosSaleLine>();
    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
    public DbSet<InventoryTransaction> InventoryTransactions => Set<InventoryTransaction>();
    public DbSet<InventoryReservation> InventoryReservations => Set<InventoryReservation>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<PurchaseOrderLine> PurchaseOrderLines => Set<PurchaseOrderLine>();
    public DbSet<AppNotification> Notifications => Set<AppNotification>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<KnowledgeArticle> KnowledgeArticles => Set<KnowledgeArticle>();
    public DbSet<PcBuild> PcBuilds => Set<PcBuild>();
    public DbSet<PcBuildPart> PcBuildParts => Set<PcBuildPart>();
    public DbSet<UsedDevice> UsedDevices => Set<UsedDevice>();
    public DbSet<QaChecklist> QaChecklists => Set<QaChecklist>();
    public DbSet<BackupRecord> BackupRecords => Set<BackupRecord>();

    // Website / portal / sell-tech
    public DbSet<CustomerPortalAccount> CustomerPortalAccounts => Set<CustomerPortalAccount>();
    public DbSet<WebsiteApiKey> WebsiteApiKeys => Set<WebsiteApiKey>();
    public DbSet<SellDeviceRequest> SellDeviceRequests => Set<SellDeviceRequest>();
    public DbSet<SellDeviceOffer> SellDeviceOffers => Set<SellDeviceOffer>();
    public DbSet<SellDeviceEvent> SellDeviceEvents => Set<SellDeviceEvent>();
    public DbSet<ConversationThread> ConversationThreads => Set<ConversationThread>();
    public DbSet<ConversationMessage> ConversationMessages => Set<ConversationMessage>();
    public DbSet<WarrantyRecord> Warranties => Set<WarrantyRecord>();
    public DbSet<NotificationTemplate> NotificationTemplates => Set<NotificationTemplate>();
    public DbSet<NotificationOutbox> NotificationOutboxItems => Set<NotificationOutbox>();
    public DbSet<QuoteApprovalRecord> QuoteApprovals => Set<QuoteApprovalRecord>();
    public DbSet<CustomPcRequest> CustomPcRequests => Set<CustomPcRequest>();
    public DbSet<RefurbDiagnosticChecklist> RefurbDiagnosticChecklists => Set<RefurbDiagnosticChecklist>();
    public DbSet<RefurbDiagnosticResult> RefurbDiagnosticResults => Set<RefurbDiagnosticResult>();

    // Refurbished acquisitions (outbound buying)
    public DbSet<AcquisitionLead> AcquisitionLeads => Set<AcquisitionLead>();
    public DbSet<AcquisitionOffer> AcquisitionOffers => Set<AcquisitionOffer>();
    public DbSet<AcquisitionEvent> AcquisitionEvents => Set<AcquisitionEvent>();

    // Secure credentials + business expenses
    public DbSet<IntegrationCredential> IntegrationCredentials => Set<IntegrationCredential>();
    public DbSet<ExpenseCategory> ExpenseCategories => Set<ExpenseCategory>();
    public DbSet<BusinessExpense> BusinessExpenses => Set<BusinessExpense>();
    public DbSet<ExpenseAttachment> ExpenseAttachments => Set<ExpenseAttachment>();
    public DbSet<RecurringExpense> RecurringExpenses => Set<RecurringExpense>();

    // Device identification / IMEI
    public DbSet<ImeiLookupLog> ImeiLookupLogs => Set<ImeiLookupLog>();
    public DbSet<ImeiTacCache> ImeiTacCaches => Set<ImeiTacCache>();

    // Per-user UI preferences (dashboard layout, …)
    public DbSet<UserUiPreference> UserUiPreferences => Set<UserUiPreference>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppUser>(e =>
        {
            e.ToTable("users");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.NormalizedEmail).IsUnique();
            e.Property(x => x.Email).HasMaxLength(320).IsRequired();
            e.Property(x => x.DisplayName).HasMaxLength(200).IsRequired();
            e.Property(x => x.PasswordHash).IsRequired();
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            e.HasOne(x => x.Role).WithMany(r => r.Users).HasForeignKey(x => x.RoleId);
            e.HasOne(x => x.Location).WithMany(l => l.Users).HasForeignKey(x => x.LocationId);
        });

        modelBuilder.Entity<AppRole>(e =>
        {
            e.ToTable("roles");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Key).IsUnique();
            e.Property(x => x.Key).HasMaxLength(64).IsRequired();
            e.Property(x => x.Name).HasMaxLength(120).IsRequired();
        });

        modelBuilder.Entity<PermissionDefinition>(e =>
        {
            e.ToTable("permissions");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Key).IsUnique();
            e.Property(x => x.Key).HasMaxLength(128).IsRequired();
        });

        modelBuilder.Entity<RolePermission>(e =>
        {
            e.ToTable("role_permissions");
            e.HasKey(x => new { x.RoleId, x.PermissionKey });
            e.Property(x => x.PermissionKey).HasMaxLength(128);
            e.HasOne(x => x.Role).WithMany(r => r.Permissions).HasForeignKey(x => x.RoleId);
        });

        modelBuilder.Entity<RefreshToken>(e =>
        {
            e.ToTable("refresh_tokens");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.TokenHash).IsUnique();
            e.HasOne(x => x.User).WithMany(u => u.RefreshTokens).HasForeignKey(x => x.UserId);
        });

        modelBuilder.Entity<Location>(e =>
        {
            e.ToTable("locations");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
        });

        modelBuilder.Entity<BusinessSetting>(e =>
        {
            e.ToTable("settings");
            e.HasKey(x => x.Key);
            e.Property(x => x.Key).HasMaxLength(128);
            e.Property(x => x.JsonValue).HasColumnType("jsonb");
        });

        modelBuilder.Entity<AuditEvent>(e =>
        {
            e.ToTable("audit_events");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.EntityType, x.EntityId, x.CreatedAt });
            e.HasIndex(x => x.CreatedAt);
        });

        modelBuilder.Entity<Customer>(e =>
        {
            e.ToTable("customers");
            e.HasKey(x => x.Id);
            e.Property(x => x.DisplayName).HasMaxLength(240).IsRequired();
            e.Property(x => x.Type).HasConversion<string>().HasMaxLength(32);
            e.Property(x => x.PreferredContact).HasConversion<string>().HasMaxLength(32);
            e.HasIndex(x => x.Phone);
            e.HasIndex(x => x.Email);
            e.HasIndex(x => x.DisplayName);
            e.HasOne(x => x.Location).WithMany().HasForeignKey(x => x.LocationId);
        });

        modelBuilder.Entity<Device>(e =>
        {
            e.ToTable("devices");
            e.HasKey(x => x.Id);
            e.Property(x => x.Category).HasConversion<string>().HasMaxLength(32);
            e.Property(x => x.Brand).HasMaxLength(120).IsRequired();
            e.Property(x => x.Model).HasMaxLength(160).IsRequired();
            e.Property(x => x.Manufacturer).HasMaxLength(120);
            e.Property(x => x.ModelNumber).HasMaxLength(120);
            e.Property(x => x.DisplaySpec).HasMaxLength(200);
            e.Property(x => x.Processor).HasMaxLength(160);
            e.Property(x => x.Chipset).HasMaxLength(160);
            e.Property(x => x.BatteryCapacity).HasMaxLength(80);
            e.Property(x => x.NetworkCapabilities).HasMaxLength(200);
            e.Property(x => x.Ram).HasMaxLength(80);
            e.Property(x => x.IdentificationProvider).HasMaxLength(80);
            e.Property(x => x.IdentificationStatus).HasMaxLength(40);
            e.HasIndex(x => x.Serial);
            e.HasIndex(x => x.Imei);
            e.HasOne(x => x.Customer).WithMany(c => c.Devices).HasForeignKey(x => x.CustomerId);
        });

        modelBuilder.Entity<RepairStatus>(e =>
        {
            e.ToTable("repair_statuses");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Key).IsUnique();
            e.Property(x => x.Key).HasMaxLength(64).IsRequired();
            e.Property(x => x.Name).HasMaxLength(120).IsRequired();
        });

        modelBuilder.Entity<RepairType>(e =>
        {
            e.ToTable("repair_types");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Key).IsUnique();
            e.Property(x => x.Prefix).HasMaxLength(8).IsRequired();
        });

        modelBuilder.Entity<RepairPriority>(e =>
        {
            e.ToTable("repair_priorities");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Key).IsUnique();
        });

        modelBuilder.Entity<DocumentSequence>(e =>
        {
            e.ToTable("document_sequences");
            e.HasKey(x => x.Key);
            e.Property(x => x.Key).HasMaxLength(64);
        });

        modelBuilder.Entity<RepairTicket>(e =>
        {
            e.ToTable("repair_tickets");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.TicketNumber).IsUnique();
            e.Property(x => x.TicketNumber).HasMaxLength(40).IsRequired();
            e.Property(x => x.ReportedIssue).IsRequired();
            e.Property(x => x.EstimatedPrice).HasPrecision(12, 2);
            e.Property(x => x.DepositAmount).HasPrecision(12, 2);
            e.Property(x => x.AccessPinHash).HasMaxLength(128);
            e.Property(x => x.PublicAccessToken).HasMaxLength(128);
            e.HasIndex(x => x.PublicAccessToken);
            e.HasIndex(x => x.DueAt);
            e.HasIndex(x => x.CreatedAt);
            e.HasOne(x => x.Customer).WithMany(c => c.Repairs).HasForeignKey(x => x.CustomerId);
            e.HasOne(x => x.Device).WithMany(d => d.Repairs).HasForeignKey(x => x.DeviceId);
            e.HasOne(x => x.Type).WithMany().HasForeignKey(x => x.TypeId);
            e.HasOne(x => x.Status).WithMany().HasForeignKey(x => x.StatusId);
            e.HasOne(x => x.Priority).WithMany().HasForeignKey(x => x.PriorityId);
            e.HasOne(x => x.AssignedTo).WithMany().HasForeignKey(x => x.AssignedToId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.CreatedBy).WithMany().HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Location).WithMany().HasForeignKey(x => x.LocationId);
        });

        modelBuilder.Entity<RepairEvent>(e =>
        {
            e.ToTable("repair_events");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.TicketId, x.CreatedAt });
            e.HasOne(x => x.Ticket).WithMany(t => t.Events).HasForeignKey(x => x.TicketId);
        });

        modelBuilder.Entity<RepairNote>(e =>
        {
            e.ToTable("repair_notes");
            e.HasKey(x => x.Id);
            e.HasOne(x => x.Ticket).WithMany(t => t.Notes).HasForeignKey(x => x.TicketId);
            e.HasOne(x => x.Author).WithMany().HasForeignKey(x => x.AuthorId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Quote>(e =>
        {
            e.ToTable("quotes");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Number).IsUnique();
            e.Property(x => x.Number).HasMaxLength(40).IsRequired();
            e.Property(x => x.Status).HasMaxLength(32).IsRequired();
            e.Property(x => x.DeviceBrand).HasMaxLength(120);
            e.Property(x => x.DeviceModel).HasMaxLength(120);
            e.Property(x => x.DeviceSerial).HasMaxLength(120);
            e.Property(x => x.DeviceImei).HasMaxLength(32);
            e.Property(x => x.DeviceCategory).HasMaxLength(64);
            e.Property(x => x.RoundingMethod).HasMaxLength(32);
            e.Property(x => x.Subtotal).HasPrecision(12, 2);
            e.Property(x => x.GstAmount).HasPrecision(12, 2);
            e.Property(x => x.Total).HasPrecision(12, 2);
            e.Property(x => x.PartsSubtotal).HasPrecision(12, 2);
            e.Property(x => x.LabourSubtotal).HasPrecision(12, 2);
            e.Property(x => x.DiscountTotal).HasPrecision(12, 2);
            e.Property(x => x.CostTotal).HasPrecision(12, 2);
            e.Property(x => x.ProfitTotal).HasPrecision(12, 2);
            e.Property(x => x.MarginPercent).HasPrecision(12, 2);
            e.Property(x => x.PreRoundTotal).HasPrecision(12, 2);
            e.Property(x => x.AcceptedTotal).HasPrecision(12, 2);
            e.Property(x => x.PartsCostTotal).HasPrecision(12, 2);
            e.Property(x => x.PartsSellTotal).HasPrecision(12, 2);
            e.Property(x => x.MarkupPercent).HasPrecision(12, 2);
            e.Property(x => x.MarkupAmount).HasPrecision(12, 2);
            e.Property(x => x.LabourFee).HasPrecision(12, 2);
            e.Property(x => x.AdditionalTotal).HasPrecision(12, 2);
            e.HasIndex(x => x.CreatedAt);
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.ExpiresAt);
            e.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId);
            e.HasOne(x => x.RepairTicket).WithMany().HasForeignKey(x => x.RepairTicketId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<QuoteLine>(e =>
        {
            e.ToTable("quote_lines");
            e.HasKey(x => x.Id);
            e.Property(x => x.Type).HasMaxLength(32);
            e.Property(x => x.Description).HasMaxLength(500).IsRequired();
            e.Property(x => x.ServiceName).HasMaxLength(200);
            e.Property(x => x.PartName).HasMaxLength(200);
            e.Property(x => x.SupplierName).HasMaxLength(200);
            e.Property(x => x.Sku).HasMaxLength(80);
            e.Property(x => x.DifficultyLevelKey).HasMaxLength(40);
            e.Property(x => x.Quantity).HasPrecision(12, 2);
            e.Property(x => x.PartCost).HasPrecision(12, 2);
            e.Property(x => x.ShippingCost).HasPrecision(12, 2);
            e.Property(x => x.OtherCost).HasPrecision(12, 2);
            e.Property(x => x.LandedCost).HasPrecision(12, 2);
            e.Property(x => x.MarkupPercent).HasPrecision(12, 2);
            e.Property(x => x.MarkupAmount).HasPrecision(12, 2);
            e.Property(x => x.PartSell).HasPrecision(12, 2);
            e.Property(x => x.LabourAmount).HasPrecision(12, 2);
            e.Property(x => x.AdditionalAmount).HasPrecision(12, 2);
            e.Property(x => x.DiscountAmount).HasPrecision(12, 2);
            e.Property(x => x.UnitPrice).HasPrecision(12, 2);
            e.Property(x => x.LineSubtotal).HasPrecision(12, 2);
            e.Property(x => x.LineTotal).HasPrecision(12, 2);
            e.Property(x => x.LineProfit).HasPrecision(12, 2);
            e.HasOne(x => x.Quote).WithMany(q => q.Lines).HasForeignKey(x => x.QuoteId);
            e.HasOne(x => x.InventoryItem).WithMany().HasForeignKey(x => x.InventoryItemId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.ServicePricing).WithMany().HasForeignKey(x => x.ServicePricingId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<QuoteRevision>(e =>
        {
            e.ToTable("quote_revisions");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.QuoteId, x.RevisionNumber }).IsUnique();
            e.Property(x => x.Reason).HasMaxLength(500);
            e.HasOne(x => x.Quote).WithMany(q => q.Revisions).HasForeignKey(x => x.QuoteId);
        });

        modelBuilder.Entity<QuoteAuditEntry>(e =>
        {
            e.ToTable("quote_audit_log");
            e.HasKey(x => x.Id);
            e.Property(x => x.Action).HasMaxLength(64).IsRequired();
            e.Property(x => x.Detail).HasMaxLength(1000);
            e.HasIndex(x => new { x.QuoteId, x.CreatedAt });
            e.HasOne(x => x.Quote).WithMany(q => q.AuditEntries).HasForeignKey(x => x.QuoteId);
        });

        modelBuilder.Entity<MarkupTier>(e =>
        {
            e.ToTable("markup_tiers");
            e.HasKey(x => x.Id);
            e.Property(x => x.MinCost).HasPrecision(12, 2);
            e.Property(x => x.MaxCost).HasPrecision(12, 2);
            e.Property(x => x.MarkupPercent).HasPrecision(12, 2);
            e.HasIndex(x => x.SortOrder);
        });

        modelBuilder.Entity<ServicePricing>(e =>
        {
            e.ToTable("service_pricing");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Category).HasMaxLength(120);
            e.Property(x => x.Description).HasMaxLength(1000);
            e.Property(x => x.DefaultLabourFee).HasPrecision(12, 2);
            e.Property(x => x.DefaultPartMarkupPercent).HasPrecision(12, 2);
            e.Property(x => x.Code).HasMaxLength(64);
            e.Property(x => x.Subcategory).HasMaxLength(120);
            e.Property(x => x.DeviceType).HasMaxLength(64);
            e.Property(x => x.CompatibleBrandsJson).HasColumnType("jsonb");
            e.Property(x => x.CompatibleModelsJson).HasColumnType("jsonb");
            e.Property(x => x.ServiceFee).HasPrecision(12, 2);
            e.Property(x => x.MinCharge).HasPrecision(12, 2);
            e.Property(x => x.DiagnosticFee).HasPrecision(12, 2);
            e.Property(x => x.TechNotes).HasMaxLength(2000);
            e.Property(x => x.CustomerDescription).HasMaxLength(2000);
            e.HasIndex(x => x.Name);
            e.HasIndex(x => x.SortOrder);
            e.HasIndex(x => x.Code);
            e.HasIndex(x => x.DeviceType);
            e.HasOne(x => x.ServiceCategory).WithMany(c => c.Services).HasForeignKey(x => x.CategoryId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ServiceCategory>(e =>
        {
            e.ToTable("service_categories");
            e.HasKey(x => x.Id);
            e.Property(x => x.Key).HasMaxLength(64).IsRequired();
            e.Property(x => x.Name).HasMaxLength(120).IsRequired();
            e.Property(x => x.DeviceType).HasMaxLength(64);
            e.Property(x => x.Icon).HasMaxLength(64);
            e.HasIndex(x => x.Key).IsUnique();
            e.HasIndex(x => x.SortOrder);
            e.HasOne(x => x.Parent).WithMany(x => x.Children).HasForeignKey(x => x.ParentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ServiceBundle>(e =>
        {
            e.ToTable("service_bundles");
            e.HasKey(x => x.Id);
            e.Property(x => x.Code).HasMaxLength(64).IsRequired();
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Description).HasMaxLength(1000);
            e.Property(x => x.BundlePrice).HasPrecision(12, 2);
            e.HasIndex(x => x.Code).IsUnique();
            e.HasIndex(x => x.SortOrder);
        });

        modelBuilder.Entity<ServiceBundleItem>(e =>
        {
            e.ToTable("service_bundle_items");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.BundleId, x.ServicePricingId }).IsUnique();
            e.HasOne(x => x.Bundle).WithMany(b => b.Items).HasForeignKey(x => x.BundleId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.ServicePricing).WithMany().HasForeignKey(x => x.ServicePricingId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ServiceFavourite>(e =>
        {
            e.ToTable("service_favourites");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.UserId, x.ServicePricingId }).IsUnique();
            e.HasOne(x => x.ServicePricing).WithMany().HasForeignKey(x => x.ServicePricingId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ServiceRecentSelection>(e =>
        {
            e.ToTable("service_recent_selections");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.UserId, x.SelectedAt });
            e.HasIndex(x => new { x.UserId, x.ServicePricingId });
            e.HasOne(x => x.ServicePricing).WithMany().HasForeignKey(x => x.ServicePricingId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DeviceBrand>(e =>
        {
            e.ToTable("device_brands");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(120).IsRequired();
            e.Property(x => x.DeviceTypes).HasMaxLength(200).IsRequired();
            e.HasIndex(x => x.Name).IsUnique();
            e.HasIndex(x => x.SortOrder);
        });

        modelBuilder.Entity<DeviceModel>(e =>
        {
            e.ToTable("device_models");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.DeviceType).HasMaxLength(64);
            e.Property(x => x.Series).HasMaxLength(120);
            e.Property(x => x.Aliases).HasMaxLength(1000);
            e.HasIndex(x => new { x.BrandId, x.Name }).IsUnique();
            e.HasIndex(x => x.SortOrder);
            e.HasIndex(x => x.DeviceType);
            e.HasIndex(x => x.Series);
            e.HasOne(x => x.Brand).WithMany(b => b.Models).HasForeignKey(x => x.BrandId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RepairServiceLine>(e =>
        {
            e.ToTable("repair_service_lines");
            e.HasKey(x => x.Id);
            e.Property(x => x.ServiceName).HasMaxLength(200).IsRequired();
            e.Property(x => x.Code).HasMaxLength(64);
            e.Property(x => x.LabourFee).HasPrecision(12, 2);
            e.Property(x => x.ServiceFee).HasPrecision(12, 2);
            e.Property(x => x.Notes).HasMaxLength(1000);
            e.Property(x => x.PartsJson).HasColumnType("jsonb");
            e.HasIndex(x => x.RepairTicketId);
            e.HasOne(x => x.RepairTicket).WithMany(t => t.ServiceLines).HasForeignKey(x => x.RepairTicketId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.ServicePricing).WithMany().HasForeignKey(x => x.ServicePricingId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Invoice>(e =>
        {
            e.ToTable("invoices");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Number).IsUnique();
            e.Property(x => x.Number).HasMaxLength(40).IsRequired();
            e.Property(x => x.Status).HasMaxLength(32).IsRequired();
            e.Property(x => x.Subtotal).HasPrecision(12, 2);
            e.Property(x => x.GstAmount).HasPrecision(12, 2);
            e.Property(x => x.Total).HasPrecision(12, 2);
            e.Property(x => x.AmountPaid).HasPrecision(12, 2);
            e.HasIndex(x => x.CreatedAt);
            e.HasIndex(x => x.Status);
            e.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId);
            e.HasOne(x => x.RepairTicket).WithMany().HasForeignKey(x => x.RepairTicketId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<InvoiceLine>(e =>
        {
            e.ToTable("invoice_lines");
            e.HasKey(x => x.Id);
            e.Property(x => x.Type).HasMaxLength(32);
            e.Property(x => x.Description).HasMaxLength(500).IsRequired();
            e.Property(x => x.Quantity).HasPrecision(12, 2);
            e.Property(x => x.UnitPrice).HasPrecision(12, 2);
            e.HasOne(x => x.Invoice).WithMany(i => i.Lines).HasForeignKey(x => x.InvoiceId);
        });

        modelBuilder.Entity<Payment>(e =>
        {
            e.ToTable("payments");
            e.HasKey(x => x.Id);
            e.Property(x => x.Method).HasMaxLength(32).IsRequired();
            e.Property(x => x.Amount).HasPrecision(12, 2);
            e.Property(x => x.Reference).HasMaxLength(200);
            e.HasIndex(x => x.PaidAt);
            e.HasOne(x => x.Invoice).WithMany(i => i.Payments).HasForeignKey(x => x.InvoiceId);
        });

        modelBuilder.Entity<PosSale>(e =>
        {
            e.ToTable("pos_sales");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Number).IsUnique();
            e.Property(x => x.Number).HasMaxLength(40).IsRequired();
            e.Property(x => x.Status).HasMaxLength(32).IsRequired();
            e.Property(x => x.PaymentMethod).HasMaxLength(32).IsRequired();
            e.Property(x => x.PaymentProvider).HasMaxLength(40).IsRequired();
            e.Property(x => x.ProviderReference).HasMaxLength(200);
            e.Property(x => x.ProviderStatus).HasMaxLength(32).IsRequired();
            e.Property(x => x.DiscountLabel).HasMaxLength(120);
            e.Property(x => x.Subtotal).HasPrecision(12, 2);
            e.Property(x => x.GstAmount).HasPrecision(12, 2);
            e.Property(x => x.Total).HasPrecision(12, 2);
            e.Property(x => x.AmountPaid).HasPrecision(12, 2);
            e.Property(x => x.DiscountPercent).HasPrecision(8, 2);
            e.Property(x => x.DiscountAmount).HasPrecision(12, 2);
            e.HasIndex(x => x.CreatedAt);
            e.HasIndex(x => x.RepairTicketId);
            e.HasOne(x => x.Invoice).WithMany().HasForeignKey(x => x.InvoiceId);
            e.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId);
            e.HasOne(x => x.RepairTicket).WithMany().HasForeignKey(x => x.RepairTicketId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<PosSaleLine>(e =>
        {
            e.ToTable("pos_sale_lines");
            e.HasKey(x => x.Id);
            e.Property(x => x.Description).HasMaxLength(500).IsRequired();
            e.Property(x => x.Sku).HasMaxLength(80);
            e.Property(x => x.Quantity).HasPrecision(12, 2);
            e.Property(x => x.UnitPrice).HasPrecision(12, 2);
            e.Property(x => x.LineTotal).HasPrecision(12, 2);
            e.HasOne(x => x.Sale).WithMany(s => s.Lines).HasForeignKey(x => x.SaleId);
            e.HasOne(x => x.InventoryItem).WithMany().HasForeignKey(x => x.InventoryItemId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Supplier>(e =>
        {
            e.ToTable("suppliers");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(240).IsRequired();
            e.HasIndex(x => x.Name);
        });

        modelBuilder.Entity<InventoryItem>(e =>
        {
            e.ToTable("inventory_items");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Sku).IsUnique();
            e.Property(x => x.Sku).HasMaxLength(64).IsRequired();
            e.Property(x => x.Barcode).HasMaxLength(64);
            e.Property(x => x.Name).HasMaxLength(240).IsRequired();
            e.Property(x => x.Category).HasMaxLength(64);
            e.Property(x => x.ComponentType).HasMaxLength(32).HasDefaultValue("Other");
            e.Property(x => x.Manufacturer).HasMaxLength(120);
            e.Property(x => x.LocationBin).HasMaxLength(64);
            e.Property(x => x.Cost).HasPrecision(12, 2);
            e.Property(x => x.SellPrice).HasPrecision(12, 2);
            e.Ignore(x => x.Available);
            e.HasIndex(x => x.ComponentType);
            e.HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<InventoryTransaction>(e =>
        {
            e.ToTable("inventory_transactions");
            e.HasKey(x => x.Id);
            e.Property(x => x.Type).HasMaxLength(32);
            e.Property(x => x.Reason).HasMaxLength(500);
            e.HasIndex(x => new { x.ItemId, x.CreatedAt });
            e.HasOne(x => x.Item).WithMany().HasForeignKey(x => x.ItemId);
        });

        modelBuilder.Entity<InventoryReservation>(e =>
        {
            e.ToTable("inventory_reservations");
            e.HasKey(x => x.Id);
            e.Property(x => x.Status).HasMaxLength(32);
            e.HasIndex(x => new { x.TicketId, x.Status });
            e.HasIndex(x => new { x.PcBuildId, x.Status });
            e.HasOne(x => x.Item).WithMany().HasForeignKey(x => x.ItemId);
            e.HasOne(x => x.Ticket).WithMany().HasForeignKey(x => x.TicketId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.PcBuild).WithMany().HasForeignKey(x => x.PcBuildId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PurchaseOrder>(e =>
        {
            e.ToTable("purchase_orders");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Number).IsUnique();
            e.Property(x => x.Number).HasMaxLength(40).IsRequired();
            e.Property(x => x.Status).HasMaxLength(32);
            e.Property(x => x.Shipping).HasPrecision(12, 2);
            e.Property(x => x.GstAmount).HasPrecision(12, 2);
            e.Property(x => x.Total).HasPrecision(12, 2);
            e.HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId);
        });

        modelBuilder.Entity<PurchaseOrderLine>(e =>
        {
            e.ToTable("purchase_order_lines");
            e.HasKey(x => x.Id);
            e.Property(x => x.Description).HasMaxLength(500).IsRequired();
            e.Property(x => x.UnitCost).HasPrecision(12, 2);
            e.HasOne(x => x.PurchaseOrder).WithMany(p => p.Lines).HasForeignKey(x => x.PurchaseOrderId);
            e.HasOne(x => x.Item).WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<AppNotification>(e =>
        {
            e.ToTable("notifications");
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).HasMaxLength(240).IsRequired();
            e.Property(x => x.Severity).HasMaxLength(32);
            e.Property(x => x.Route).HasMaxLength(240);
            e.HasIndex(x => new { x.UserId, x.IsRead, x.CreatedAt });
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId);
        });

        modelBuilder.Entity<Booking>(e =>
        {
            e.ToTable("bookings");
            e.HasKey(x => x.Id);
            e.Property(x => x.Type).HasMaxLength(32);
            e.Property(x => x.Status).HasMaxLength(32);
            e.HasIndex(x => x.StartsAt);
            e.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId);
            e.HasOne(x => x.Staff).WithMany().HasForeignKey(x => x.StaffId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<KnowledgeArticle>(e =>
        {
            e.ToTable("knowledge_articles");
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).HasMaxLength(240).IsRequired();
            e.Property(x => x.Category).HasMaxLength(64);
            e.Property(x => x.Tags).HasMaxLength(500);
            e.HasIndex(x => x.Category);
        });

        modelBuilder.Entity<PcBuild>(e =>
        {
            e.ToTable("pc_builds");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Number).IsUnique();
            e.Property(x => x.Number).HasMaxLength(40).IsRequired();
            e.Property(x => x.Name).HasMaxLength(240);
            e.Property(x => x.Status).HasMaxLength(32);
            e.Property(x => x.Budget).HasPrecision(12, 2);
            e.Property(x => x.CostTotal).HasPrecision(12, 2);
            e.Property(x => x.SellTotal).HasPrecision(12, 2);
            e.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<PcBuildPart>(e =>
        {
            e.ToTable("pc_build_parts");
            e.HasKey(x => x.Id);
            e.Property(x => x.Category).HasMaxLength(64);
            e.Property(x => x.Name).HasMaxLength(240).IsRequired();
            e.Property(x => x.Cost).HasPrecision(12, 2);
            e.Property(x => x.SellPrice).HasPrecision(12, 2);
            e.HasOne(x => x.PcBuild).WithMany(b => b.Parts).HasForeignKey(x => x.PcBuildId);
            e.HasOne(x => x.InventoryItem).WithMany().HasForeignKey(x => x.InventoryItemId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<UsedDevice>(e =>
        {
            e.ToTable("used_devices");
            e.HasKey(x => x.Id);
            e.Property(x => x.Summary).HasMaxLength(240).IsRequired();
            e.Property(x => x.Brand).HasMaxLength(120);
            e.Property(x => x.Model).HasMaxLength(120);
            e.Property(x => x.Category).HasMaxLength(64);
            e.Property(x => x.Serial).HasMaxLength(120);
            e.Property(x => x.Imei).HasMaxLength(32);
            e.Property(x => x.Colour).HasMaxLength(64);
            e.Property(x => x.StorageCapacity).HasMaxLength(64);
            e.Property(x => x.Specs).HasMaxLength(500);
            e.Property(x => x.ConditionGrade).HasMaxLength(32);
            e.Property(x => x.Status).HasMaxLength(32);
            e.Property(x => x.PurchasePrice).HasPrecision(12, 2);
            e.Property(x => x.ExpectedResale).HasPrecision(12, 2);
            e.Property(x => x.ExpectedRepairCost).HasPrecision(12, 2);
            e.Property(x => x.ActualSalePrice).HasPrecision(12, 2);
            e.Property(x => x.Notes).HasMaxLength(2000);
            e.Property(x => x.Source).HasMaxLength(240);
            e.Property(x => x.StockId).HasMaxLength(32);
            e.Property(x => x.Sku).HasMaxLength(64);
            e.Property(x => x.Barcode).HasMaxLength(64);
            e.Property(x => x.LocationBin).HasMaxLength(64);
            e.Property(x => x.ModelNumber).HasMaxLength(120);
            e.Property(x => x.Carrier).HasMaxLength(64);
            e.Property(x => x.SimConfiguration).HasMaxLength(32);
            e.Property(x => x.LabourCost).HasPrecision(12, 2);
            e.Property(x => x.ShippingInCost).HasPrecision(12, 2);
            e.Property(x => x.ShippingOutCost).HasPrecision(12, 2);
            e.Property(x => x.PackagingCost).HasPrecision(12, 2);
            e.Property(x => x.MarketplaceFees).HasPrecision(12, 2);
            e.Property(x => x.PaymentFees).HasPrecision(12, 2);
            e.Property(x => x.WarrantyAllowance).HasPrecision(12, 2);
            e.Property(x => x.FunctionalGrade).HasMaxLength(32);
            e.Property(x => x.ActivationLockStatus).HasMaxLength(32);
            e.Property(x => x.FrpStatus).HasMaxLength(32);
            e.Property(x => x.CarrierLockStatus).HasMaxLength(32);
            e.Property(x => x.LostStolenStatus).HasMaxLength(32);
            e.Property(x => x.SecurityCheckNotes).HasMaxLength(2000);
            e.Property(x => x.WooCommerceProductId).HasMaxLength(64);
            e.Property(x => x.ListingUrl).HasMaxLength(500);
            e.Ignore(x => x.TotalCost);
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.CustomerId);
            e.HasIndex(x => x.RepairTicketId);
            e.HasIndex(x => x.StockId);
            e.HasIndex(x => x.Imei);
            e.HasIndex(x => x.AcquisitionLeadId);
        });

        modelBuilder.Entity<AcquisitionLead>(e =>
        {
            e.ToTable("acquisition_leads");
            e.HasKey(x => x.Id);
            e.Property(x => x.Reference).HasMaxLength(32).IsRequired();
            e.HasIndex(x => x.Reference).IsUnique();
            e.Property(x => x.Status).HasMaxLength(40);
            e.Property(x => x.Brand).HasMaxLength(120).IsRequired();
            e.Property(x => x.Model).HasMaxLength(120).IsRequired();
            e.Property(x => x.ModelNumber).HasMaxLength(120);
            e.Property(x => x.Category).HasMaxLength(64);
            e.Property(x => x.StorageCapacity).HasMaxLength(64);
            e.Property(x => x.Colour).HasMaxLength(64);
            e.Property(x => x.Imei).HasMaxLength(32);
            e.Property(x => x.Serial).HasMaxLength(120);
            e.Property(x => x.Carrier).HasMaxLength(64);
            e.Property(x => x.SellerName).HasMaxLength(200);
            e.Property(x => x.SellerContact).HasMaxLength(240);
            e.Property(x => x.SellerEmail).HasMaxLength(320);
            e.Property(x => x.SellerPhone).HasMaxLength(40);
            e.Property(x => x.SellerType).HasMaxLength(32);
            e.Property(x => x.AcquisitionSource).HasMaxLength(64);
            e.Property(x => x.ListingUrl).HasMaxLength(1000);
            e.Property(x => x.AskingPrice).HasPrecision(12, 2);
            e.Property(x => x.ProposedOffer).HasPrecision(12, 2);
            e.Property(x => x.AcceptedPurchasePrice).HasPrecision(12, 2);
            e.Property(x => x.DeviceCondition).HasMaxLength(120);
            e.Property(x => x.ActivationLockStatus).HasMaxLength(32);
            e.Property(x => x.PaymentMethod).HasMaxLength(64);
            e.Property(x => x.ShippingOrCollection).HasMaxLength(500);
            e.Property(x => x.Notes).HasMaxLength(4000);
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.Imei);
            e.HasIndex(x => x.AssignedStaffId);
            e.HasIndex(x => x.FollowUpAt);
            e.HasOne(x => x.UsedDevice).WithMany().HasForeignKey(x => x.UsedDeviceId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<AcquisitionOffer>(e =>
        {
            e.ToTable("acquisition_offers");
            e.HasKey(x => x.Id);
            e.Property(x => x.Status).HasMaxLength(32);
            e.Property(x => x.Message).HasMaxLength(4000);
            e.Property(x => x.Notes).HasMaxLength(2000);
            e.Property(x => x.Amount).HasPrecision(12, 2);
            e.Property(x => x.EstimatedResalePrice).HasPrecision(12, 2);
            e.Property(x => x.EstimatedPartsCost).HasPrecision(12, 2);
            e.Property(x => x.LabourAllowance).HasPrecision(12, 2);
            e.Property(x => x.EstimatedProfit).HasPrecision(12, 2);
            e.Property(x => x.EstimatedMarginPercent).HasPrecision(8, 2);
            e.Property(x => x.AbsoluteMaxPurchasePrice).HasPrecision(12, 2);
            e.Property(x => x.BuyRecommendation).HasMaxLength(32);
            e.HasOne(x => x.AcquisitionLead).WithMany(l => l.Offers).HasForeignKey(x => x.AcquisitionLeadId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.AcquisitionLeadId);
        });

        modelBuilder.Entity<AcquisitionEvent>(e =>
        {
            e.ToTable("acquisition_events");
            e.HasKey(x => x.Id);
            e.Property(x => x.EventType).HasMaxLength(64);
            e.Property(x => x.Summary).HasMaxLength(500);
            e.HasOne(x => x.AcquisitionLead).WithMany(l => l.Events).HasForeignKey(x => x.AcquisitionLeadId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.AcquisitionLeadId, x.CreatedAt });
        });

        modelBuilder.Entity<QaChecklist>(e =>
        {
            e.ToTable("qa_checklists");
            e.HasKey(x => x.Id);
            e.Property(x => x.Item).HasMaxLength(240).IsRequired();
            e.Property(x => x.Result).HasMaxLength(32);
            e.HasIndex(x => new { x.TicketId, x.SortOrder });
            e.HasOne(x => x.Ticket).WithMany().HasForeignKey(x => x.TicketId);
        });

        modelBuilder.Entity<BackupRecord>(e =>
        {
            e.ToTable("backup_records");
            e.HasKey(x => x.Id);
            e.Property(x => x.Type).HasMaxLength(32);
            e.Property(x => x.Status).HasMaxLength(32);
            e.Property(x => x.Path).HasMaxLength(500);
            e.HasIndex(x => x.StartedAt);
        });

        modelBuilder.Entity<CustomerPortalAccount>(e =>
        {
            e.ToTable("customer_portal_accounts");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.NormalizedEmail).IsUnique();
            e.Property(x => x.Email).HasMaxLength(320).IsRequired();
            e.Property(x => x.NormalizedEmail).HasMaxLength(320).IsRequired();
            e.Property(x => x.PasswordHash).IsRequired();
            e.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId);
        });

        modelBuilder.Entity<WebsiteApiKey>(e =>
        {
            e.ToTable("website_api_keys");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.KeyPrefix);
            e.Property(x => x.Name).HasMaxLength(120).IsRequired();
            e.Property(x => x.KeyPrefix).HasMaxLength(32).IsRequired();
            e.Property(x => x.KeyHash).HasMaxLength(128).IsRequired();
        });

        modelBuilder.Entity<SellDeviceRequest>(e =>
        {
            e.ToTable("sell_device_requests");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Reference).IsUnique();
            e.HasIndex(x => x.Status);
            e.Property(x => x.Reference).HasMaxLength(40).IsRequired();
            e.Property(x => x.Status).HasMaxLength(40);
            e.Property(x => x.Category).HasMaxLength(64);
            e.Property(x => x.Brand).HasMaxLength(120);
            e.Property(x => x.Model).HasMaxLength(120);
            e.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Offer).WithOne(o => o.Request).HasForeignKey<SellDeviceOffer>(o => o.RequestId);
            e.HasOne(x => x.UsedDevice).WithMany().HasForeignKey(x => x.UsedDeviceId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<SellDeviceOffer>(e =>
        {
            e.ToTable("sell_device_offers");
            e.HasKey(x => x.Id);
            e.Property(x => x.Status).HasMaxLength(32);
            foreach (var p in new[]
                     {
                         nameof(SellDeviceOffer.EstimatedResalePrice), nameof(SellDeviceOffer.PurchaseCost),
                         nameof(SellDeviceOffer.EstimatedPartsCost), nameof(SellDeviceOffer.LabourAllowance),
                         nameof(SellDeviceOffer.SellingPlatformFees), nameof(SellDeviceOffer.ShippingPacking),
                         nameof(SellDeviceOffer.RiskAllowance), nameof(SellDeviceOffer.DesiredMinProfit),
                         nameof(SellDeviceOffer.DesiredMinMarginPercent), nameof(SellDeviceOffer.RecommendedOfferMin),
                         nameof(SellDeviceOffer.RecommendedOffer), nameof(SellDeviceOffer.RecommendedOfferMax),
                         nameof(SellDeviceOffer.AbsoluteMaxPurchasePrice), nameof(SellDeviceOffer.EstimatedTotalInvestment),
                         nameof(SellDeviceOffer.EstimatedResaleProfit), nameof(SellDeviceOffer.EstimatedMarginPercent),
                         nameof(SellDeviceOffer.CustomerFacingOffer)
                     })
                e.Property(p).HasPrecision(12, 2);
        });

        modelBuilder.Entity<SellDeviceEvent>(e =>
        {
            e.ToTable("sell_device_events");
            e.HasKey(x => x.Id);
            e.HasOne(x => x.Request).WithMany(r => r.Events).HasForeignKey(x => x.RequestId);
            e.Property(x => x.EventType).HasMaxLength(64);
            e.Property(x => x.Summary).HasMaxLength(500);
        });

        modelBuilder.Entity<ConversationThread>(e =>
        {
            e.ToTable("conversation_threads");
            e.HasKey(x => x.Id);
            e.Property(x => x.Subject).HasMaxLength(240);
            e.Property(x => x.Status).HasMaxLength(32);
            e.HasIndex(x => x.CustomerId);
            e.HasIndex(x => x.RepairTicketId);
        });

        modelBuilder.Entity<ConversationMessage>(e =>
        {
            e.ToTable("conversation_messages");
            e.HasKey(x => x.Id);
            e.Property(x => x.Visibility).HasMaxLength(32);
            e.Property(x => x.Channel).HasMaxLength(32);
            e.Property(x => x.AuthorName).HasMaxLength(200);
            e.HasOne(x => x.Thread).WithMany(t => t.Messages).HasForeignKey(x => x.ThreadId);
        });

        modelBuilder.Entity<WarrantyRecord>(e =>
        {
            e.ToTable("warranties");
            e.HasKey(x => x.Id);
            e.Property(x => x.CoverageSummary).HasMaxLength(500);
            e.Property(x => x.Status).HasMaxLength(32);
            e.HasIndex(x => x.CustomerId);
            e.HasIndex(x => x.ExpiresAt);
        });

        modelBuilder.Entity<NotificationTemplate>(e =>
        {
            e.ToTable("notification_templates");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Key).IsUnique();
            e.Property(x => x.Key).HasMaxLength(80).IsRequired();
            e.Property(x => x.Name).HasMaxLength(160);
            e.Property(x => x.Channel).HasMaxLength(16);
            e.Property(x => x.Subject).HasMaxLength(240);
        });

        modelBuilder.Entity<NotificationOutbox>(e =>
        {
            e.ToTable("notification_outbox");
            e.HasKey(x => x.Id);
            e.Property(x => x.TemplateKey).HasMaxLength(80);
            e.Property(x => x.Channel).HasMaxLength(16);
            e.Property(x => x.ToAddress).HasMaxLength(320);
            e.Property(x => x.Status).HasMaxLength(32);
            e.HasIndex(x => x.Status);
        });

        modelBuilder.Entity<QuoteApprovalRecord>(e =>
        {
            e.ToTable("quote_approvals");
            e.HasKey(x => x.Id);
            e.Property(x => x.Action).HasMaxLength(32);
            e.Property(x => x.AccessMethod).HasMaxLength(32);
            e.Property(x => x.IpAddress).HasMaxLength(64);
            e.HasIndex(x => x.QuoteId);
            e.HasOne(x => x.Quote).WithMany().HasForeignKey(x => x.QuoteId);
        });

        modelBuilder.Entity<CustomPcRequest>(e =>
        {
            e.ToTable("custom_pc_requests");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Reference).IsUnique();
            e.Property(x => x.Reference).HasMaxLength(40);
            e.Property(x => x.Status).HasMaxLength(40);
            e.Property(x => x.Budget).HasPrecision(12, 2);
        });

        modelBuilder.Entity<RefurbDiagnosticChecklist>(e =>
        {
            e.ToTable("refurb_diagnostic_checklists");
            e.HasKey(x => x.Id);
            e.Property(x => x.Category).HasMaxLength(64);
            e.Property(x => x.Name).HasMaxLength(160);
        });

        modelBuilder.Entity<RefurbDiagnosticResult>(e =>
        {
            e.ToTable("refurb_diagnostic_results");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.UsedDeviceId);
            e.Property(x => x.OverallGrade).HasMaxLength(32);
        });

        modelBuilder.Entity<IntegrationCredential>(e =>
        {
            e.ToTable("integration_credentials");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.ProviderKey).IsUnique()
                .HasFilter("\"ArchivedAt\" IS NULL");
            e.Property(x => x.ProviderKey).HasMaxLength(64).IsRequired();
            e.Property(x => x.DisplayName).HasMaxLength(160).IsRequired();
            e.Property(x => x.ConnectionStatus).HasMaxLength(32);
            e.Property(x => x.SecretHint).HasMaxLength(64);
            e.Property(x => x.PublicConfigJson).HasColumnType("jsonb");
            e.Ignore(x => x.HasSecret);
        });

        modelBuilder.Entity<ExpenseCategory>(e =>
        {
            e.ToTable("expense_categories");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(160).IsRequired();
            e.Property(x => x.Kind).HasMaxLength(32);
            e.Property(x => x.Icon).HasMaxLength(64);
            e.Property(x => x.MonthlyBudget).HasPrecision(12, 2);
            e.HasOne(x => x.ParentCategory).WithMany(x => x.Children).HasForeignKey(x => x.ParentCategoryId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.SortOrder);
        });

        modelBuilder.Entity<BusinessExpense>(e =>
        {
            e.ToTable("business_expenses");
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).HasMaxLength(240).IsRequired();
            e.Property(x => x.Status).HasMaxLength(32);
            e.Property(x => x.GstTreatment).HasMaxLength(32);
            e.Property(x => x.PaymentMethod).HasMaxLength(64);
            e.Property(x => x.SupplierName).HasMaxLength(200);
            e.Property(x => x.InvoiceReference).HasMaxLength(120);
            e.Property(x => x.RecurrenceInstanceKey).HasMaxLength(80);
            e.Property(x => x.TotalAmount).HasPrecision(12, 2);
            e.Property(x => x.AmountExGst).HasPrecision(12, 2);
            e.Property(x => x.GstAmount).HasPrecision(12, 2);
            e.Property(x => x.AmountPaid).HasPrecision(12, 2);
            e.Property(x => x.RefundAmount).HasPrecision(12, 2);
            e.Property(x => x.BusinessUsePercent).HasPrecision(5, 2);
            e.HasIndex(x => x.PurchaseDate);
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.RecurrenceInstanceKey).IsUnique()
                .HasFilter("\"RecurrenceInstanceKey\" IS NOT NULL");
            e.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.RecurringExpense).WithMany().HasForeignKey(x => x.RecurringExpenseId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ExpenseAttachment>(e =>
        {
            e.ToTable("expense_attachments");
            e.HasKey(x => x.Id);
            e.Property(x => x.FileName).HasMaxLength(260).IsRequired();
            e.Property(x => x.ContentType).HasMaxLength(120);
            e.Property(x => x.StoragePath).HasMaxLength(1024).IsRequired();
            e.HasOne(x => x.Expense).WithMany(x => x.Attachments).HasForeignKey(x => x.ExpenseId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RecurringExpense>(e =>
        {
            e.ToTable("recurring_expenses");
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).HasMaxLength(240).IsRequired();
            e.Property(x => x.Frequency).HasMaxLength(32);
            e.Property(x => x.GstTreatment).HasMaxLength(32);
            e.Property(x => x.PaymentMethod).HasMaxLength(64);
            e.Property(x => x.SupplierName).HasMaxLength(200);
            e.Property(x => x.TotalAmount).HasPrecision(12, 2);
            e.HasIndex(x => x.NextDueDate);
            e.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ImeiLookupLog>(e =>
        {
            e.ToTable("imei_lookup_logs");
            e.HasKey(x => x.Id);
            e.Property(x => x.ImeiHash).HasMaxLength(64).IsRequired();
            e.Property(x => x.Tac).HasMaxLength(8);
            e.Property(x => x.Provider).HasMaxLength(80).IsRequired();
            e.Property(x => x.ErrorCode).HasMaxLength(80);
            e.Property(x => x.VerificationStatus).HasMaxLength(40).IsRequired();
            e.HasIndex(x => x.ActorUserId);
            e.HasIndex(x => x.CreatedAt);
            e.HasIndex(x => x.ImeiHash);
        });

        modelBuilder.Entity<ImeiTacCache>(e =>
        {
            e.ToTable("imei_tac_cache");
            e.HasKey(x => x.Id);
            e.Property(x => x.Tac).HasMaxLength(8).IsRequired();
            e.Property(x => x.Provider).HasMaxLength(80).IsRequired();
            e.Property(x => x.Brand).HasMaxLength(120);
            e.Property(x => x.Manufacturer).HasMaxLength(120);
            e.Property(x => x.Model).HasMaxLength(200);
            e.Property(x => x.ModelNumber).HasMaxLength(120);
            e.Property(x => x.DeviceCategory).HasMaxLength(40);
            e.Property(x => x.VerificationStatus).HasMaxLength(40).IsRequired();
            e.HasIndex(x => x.ExpiresAt);
            e.HasIndex(x => new { x.Tac, x.Provider }).IsUnique();
        });

        modelBuilder.Entity<UserUiPreference>(e =>
        {
            e.ToTable("user_ui_preferences");
            e.HasKey(x => x.Id);
            e.Property(x => x.Key).HasMaxLength(80).IsRequired();
            e.Property(x => x.JsonValue).IsRequired();
            e.HasIndex(x => new { x.UserId, x.Key }).IsUnique();
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}

public static class SettingKeys
{
    public const string SetupCompleted = "setup.completed";
    public const string BusinessProfile = "business.profile";
    public const string Gst = "gst";
    public const string FinanceDefaults = "finance.defaults";
    public const string ModuleVisibility = "modules.visibility";
    public const string PairingCode = "setup.pairing_code";
    public const string PricingSettings = "pricing.settings";
    public const string ServiceCatalogueVersion = "seed.service_catalogue_version";
    public const string PosSettings = "pos.settings";
    public const string DeviceIdentification = "device.identification";
}

public static class DbSeed
{
    public static async Task EnsureFoundationAsync(WorkshopDbContext db, CancellationToken ct = default)
    {
        foreach (var (key, group, label) in PermissionKeys.Catalogue)
        {
            if (!await db.Permissions.AnyAsync(p => p.Key == key, ct))
                db.Permissions.Add(new PermissionDefinition { Key = key, Group = group, Label = label });
        }

        var roles = new (string Key, string Name, string Description)[]
        {
            ("owner", "Owner", "Founding owner with full access"),
            ("administrator", "Administrator", "Full administrative access"),
            ("manager", "Manager", "Operations and limited administration"),
            ("technician", "Technician", "Workshop and assigned jobs"),
            ("front_desk", "Front Desk", "Intake, customers and payments"),
            ("sales", "Sales", "Quotes and used technology sales"),
            ("read_only", "Read Only", "View-only access")
        };

        foreach (var (key, name, description) in roles)
        {
            var role = await db.Roles.Include(r => r.Permissions).FirstOrDefaultAsync(r => r.Key == key, ct);
            if (role is null)
            {
                role = new AppRole { Key = key, Name = name, Description = description, IsSystem = true };
                db.Roles.Add(role);
                await db.SaveChangesAsync(ct);

                var desired = PermissionKeys.DefaultRolePermissions[key];
                foreach (var perm in desired)
                    db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionKey = perm });
            }
            else
            {
                // Keep display metadata in sync; do not wipe custom permission edits on restart.
                if (string.IsNullOrWhiteSpace(role.Name)) role.Name = name;
                if (string.IsNullOrWhiteSpace(role.Description)) role.Description = description;
                role.IsSystem = true;

                // System roles gain newly catalogue'd default permissions on upgrade (never removes customs).
                var existing = role.Permissions.Select(p => p.PermissionKey).ToHashSet(StringComparer.OrdinalIgnoreCase);
                IEnumerable<string> desiredPerms = key is "owner" or "administrator"
                    ? PermissionKeys.AllKeys
                    : PermissionKeys.DefaultRolePermissions.TryGetValue(key, out var defs) ? defs : Array.Empty<string>();
                foreach (var perm in desiredPerms.Where(p => !existing.Contains(p)))
                    db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionKey = perm });
            }
        }

        await EnsureOperationsDefaultsAsync(db, ct);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Lightweight ops defaults — no heavy catalogue/demo seed. Repair catalogue remains the source of status/type/priority rows.
    /// Document sequences (QTE-/INV-/PO-/PCB-) are created on first use.
    /// </summary>
    public static Task EnsureOperationsDefaultsAsync(WorkshopDbContext db, CancellationToken ct = default) =>
        EnsureRepairCatalogueAsync(db, ct);

    public static async Task EnsureRepairCatalogueAsync(WorkshopDbContext db, CancellationToken ct = default)
    {
        (string Key, string Name, string Colour, int Sort, bool Completed, bool Cancelled, bool Parts, bool Approval, bool Pickup)[] statuses =
        [
            ("received", "Received", "#64748b", 10, false, false, false, false, false),
            ("diagnosing", "Diagnosing", "#2563eb", 20, false, false, false, false, false),
            ("awaiting_approval", "Awaiting Quote Approval", "#d97706", 30, false, false, false, true, false),
            ("waiting_parts", "Waiting for Parts", "#c2410c", 40, false, false, true, false, false),
            ("repair_scheduled", "Repair Scheduled", "#7c3aed", 50, false, false, false, false, false),
            ("repairing", "Repairing", "#4f46e5", 60, false, false, false, false, false),
            ("testing", "Testing", "#0f766e", 70, false, false, false, false, false),
            ("ready_pickup", "Ready for Pickup", "#15803d", 80, false, false, false, false, true),
            ("completed", "Completed", "#166534", 90, true, false, false, false, false),
            ("cancelled", "Cancelled", "#6b7280", 100, false, true, false, false, false),
            ("unrepairable", "Unrepairable", "#991b1b", 110, true, false, false, false, false)
        ];

        foreach (var s in statuses)
        {
            var row = await db.RepairStatuses.FirstOrDefaultAsync(x => x.Key == s.Key, ct);
            if (row is null)
            {
                db.RepairStatuses.Add(new RepairStatus
                {
                    Key = s.Key,
                    Name = s.Name,
                    Colour = s.Colour,
                    SortOrder = s.Sort,
                    IsSystem = true,
                    IsCompleted = s.Completed,
                    IsCancelled = s.Cancelled,
                    CountsAsWaitingForParts = s.Parts,
                    CountsAsAwaitingApproval = s.Approval,
                    CountsAsReadyForPickup = s.Pickup
                });
            }
        }

        (string Key, string Name, string Prefix, int Sort)[] types =
        [
            ("repair", "Repair", "REP", 10),
            ("diagnostic", "Diagnostic", "DIA", 20),
            ("warranty", "Warranty", "WAR", 30),
            ("data", "Data Recovery", "DAT", 40)
        ];
        foreach (var t in types)
        {
            if (!await db.RepairTypes.AnyAsync(x => x.Key == t.Key, ct))
            {
                db.RepairTypes.Add(new RepairType
                {
                    Key = t.Key,
                    Name = t.Name,
                    Prefix = t.Prefix,
                    SortOrder = t.Sort,
                    IsSystem = true
                });
            }
        }

        (string Key, string Name, int Sort, int Severity)[] priorities =
        [
            ("low", "Low", 10, 1),
            ("normal", "Normal", 20, 2),
            ("high", "High", 30, 3),
            ("urgent", "Urgent", 40, 4)
        ];
        foreach (var p in priorities)
        {
            if (!await db.RepairPriorities.AnyAsync(x => x.Key == p.Key, ct))
            {
                db.RepairPriorities.Add(new RepairPriority
                {
                    Key = p.Key,
                    Name = p.Name,
                    SortOrder = p.Sort,
                    Severity = p.Severity
                });
            }
        }
    }

    public static async Task<T> GetSettingAsync<T>(WorkshopDbContext db, string key, T fallback, CancellationToken ct = default)
    {
        var row = await db.Settings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == key, ct);
        if (row is null) return fallback;
        if (string.IsNullOrWhiteSpace(row.JsonValue)) return fallback;
        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var parsed = JsonSerializer.Deserialize<T>(row.JsonValue, options);
            return parsed ?? fallback;
        }
        catch (JsonException)
        {
            // Corrupt / unexpected jsonb — do not silently pretend defaults are "saved".
            throw new InvalidOperationException(
                $"Setting '{key}' has invalid JSON and cannot be loaded. Re-save from Settings to repair it.");
        }
    }

    public static async Task SetSettingAsync(WorkshopDbContext db, string key, object value, Guid? userId, CancellationToken ct = default)
        => await SetSettingAsync(db, key, value, userId, expectedUpdatedAt: null, ct);

    /// <summary>
    /// Persists a settings blob. When <paramref name="expectedUpdatedAt"/> is supplied and the row
    /// has been changed by another writer, throws <see cref="WorkshopOS.Application.Common.ConflictAppException"/>.
    /// Never inserts defaults over an existing row.
    /// </summary>
    public static async Task SetSettingAsync(
        WorkshopDbContext db, string key, object value, Guid? userId, DateTimeOffset? expectedUpdatedAt, CancellationToken ct = default)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };
        var json = JsonSerializer.Serialize(value, options);
        var row = await db.Settings.FirstOrDefaultAsync(s => s.Key == key, ct);
        if (row is null)
        {
            db.Settings.Add(new BusinessSetting { Key = key, JsonValue = json, UpdatedById = userId, UpdatedAt = DateTimeOffset.UtcNow });
        }
        else
        {
            if (expectedUpdatedAt is DateTimeOffset expected)
            {
                // Allow 1ms skew from JSON round-trips.
                if (Math.Abs((row.UpdatedAt - expected).TotalMilliseconds) > 5)
                    throw new WorkshopOS.Application.Common.ConflictAppException(
                        "This setting was changed by someone else. Reload and try again — your edits were not discarded on the server; re-apply them after refresh.");
            }

            row.JsonValue = json;
            row.UpdatedById = userId;
            row.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync(ct);
    }

    public static async Task<(T Value, DateTimeOffset? UpdatedAt)> GetSettingWithMetaAsync<T>(
        WorkshopDbContext db, string key, T fallback, CancellationToken ct = default)
    {
        var row = await db.Settings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == key, ct);
        if (row is null || string.IsNullOrWhiteSpace(row.JsonValue))
            return (fallback, null);
        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var parsed = JsonSerializer.Deserialize<T>(row.JsonValue, options);
            return (parsed ?? fallback, row.UpdatedAt);
        }
        catch (JsonException)
        {
            throw new InvalidOperationException(
                $"Setting '{key}' has invalid JSON and cannot be loaded. Re-save from Settings to repair it.");
        }
    }
}
