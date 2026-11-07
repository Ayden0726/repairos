using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WorkshopOS.Infrastructure.Persistence;

#nullable disable

namespace WorkshopOS.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(WorkshopDbContext))]
    [Migration("20261102120000_V2RemainingOps")]
    public partial class V2RemainingOps : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "QuantityQuarantined",
                table: "inventory_items",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "UsedDeviceId",
                table: "pos_sale_lines",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_pos_sale_lines_UsedDeviceId",
                table: "pos_sale_lines",
                column: "UsedDeviceId");

            migrationBuilder.AddForeignKey(
                name: "FK_pos_sale_lines_used_devices_UsedDeviceId",
                table: "pos_sale_lines",
                column: "UsedDeviceId",
                principalTable: "used_devices",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.CreateTable(
                name: "donor_devices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Brand = table.Column<string>(type: "text", nullable: true),
                    Model = table.Column<string>(type: "text", nullable: true),
                    Imei = table.Column<string>(type: "text", nullable: true),
                    Serial = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    SourceUsedDeviceId = table.Column<Guid>(type: "uuid", nullable: true),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ArchivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_donor_devices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_donor_devices_used_devices_SourceUsedDeviceId",
                        column: x => x.SourceUsedDeviceId,
                        principalTable: "used_devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_donor_devices_customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "harvested_components",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DonorDeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    ConditionGrade = table.Column<string>(type: "text", nullable: true),
                    InventoryItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    HarvestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    HarvestedById = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_harvested_components", x => x.Id);
                    table.ForeignKey(
                        name: "FK_harvested_components_donor_devices_DonorDeviceId",
                        column: x => x.DonorDeviceId,
                        principalTable: "donor_devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_harvested_components_inventory_items_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "inventory_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "loan_devices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetTag = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Brand = table.Column<string>(type: "text", nullable: true),
                    Model = table.Column<string>(type: "text", nullable: true),
                    Imei = table.Column<string>(type: "text", nullable: true),
                    Serial = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ArchivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table => table.PrimaryKey("PK_loan_devices", x => x.Id));

            migrationBuilder.CreateTable(
                name: "loan_agreements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LoanDeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    RepairTicketId = table.Column<Guid>(type: "uuid", nullable: true),
                    CheckedOutAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DueAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReturnedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CheckedOutById = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_loan_agreements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_loan_agreements_loan_devices_LoanDeviceId",
                        column: x => x.LoanDeviceId,
                        principalTable: "loan_devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_loan_agreements_customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_loan_agreements_repair_tickets_RepairTicketId",
                        column: x => x.RepairTicketId,
                        principalTable: "repair_tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "warranty_claims",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    WarrantyRecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    RepairTicketId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Summary = table.Column<string>(type: "text", nullable: false),
                    ResolutionNotes = table.Column<string>(type: "text", nullable: true),
                    OpenedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ClosedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    OpenedById = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ArchivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_warranty_claims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_warranty_claims_warranties_WarrantyRecordId",
                        column: x => x.WarrantyRecordId,
                        principalTable: "warranties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_warranty_claims_customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_warranty_claims_repair_tickets_RepairTicketId",
                        column: x => x.RepairTicketId,
                        principalTable: "repair_tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "refund_requests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    PosSaleId = table.Column<Guid>(type: "uuid", nullable: true),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: true),
                    RequestedById = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovedById = table.Column<Guid>(type: "uuid", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    DecisionNotes = table.Column<string>(type: "text", nullable: true),
                    RequestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DecidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ArchivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refund_requests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_refund_requests_pos_sales_PosSaleId",
                        column: x => x.PosSaleId,
                        principalTable: "pos_sales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_refund_requests_invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "repair_attachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TicketId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    FileName = table.Column<string>(type: "text", nullable: false),
                    ContentType = table.Column<string>(type: "text", nullable: false),
                    StoragePath = table.Column<string>(type: "text", nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Caption = table.Column<string>(type: "text", nullable: true),
                    UploadedById = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_repair_attachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_repair_attachments_repair_tickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "repair_tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "repair_signatures",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TicketId = table.Column<Guid>(type: "uuid", nullable: false),
                    SignerName = table.Column<string>(type: "text", nullable: false),
                    Purpose = table.Column<string>(type: "text", nullable: false),
                    StoragePath = table.Column<string>(type: "text", nullable: false),
                    SignedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CapturedById = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_repair_signatures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_repair_signatures_repair_tickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "repair_tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "printer_profiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Kind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Connection = table.Column<string>(type: "text", nullable: true),
                    DefaultTemplate = table.Column<string>(type: "text", nullable: true),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ArchivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table => table.PrimaryKey("PK_printer_profiles", x => x.Id));

            migrationBuilder.CreateTable(
                name: "print_jobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PrinterProfileId = table.Column<Guid>(type: "uuid", nullable: true),
                    LabelType = table.Column<string>(type: "text", nullable: false),
                    EntityType = table.Column<string>(type: "text", nullable: false),
                    EntityId = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    OutputPath = table.Column<string>(type: "text", nullable: true),
                    RequestedById = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_print_jobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_print_jobs_printer_profiles_PrinterProfileId",
                        column: x => x.PrinterProfileId,
                        principalTable: "printer_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "integration_health_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Success = table.Column<bool>(type: "boolean", nullable: false),
                    Message = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table => table.PrimaryKey("PK_integration_health_events", x => x.Id));

            migrationBuilder.CreateIndex(name: "IX_donor_devices_Number", table: "donor_devices", column: "Number", unique: true);
            migrationBuilder.CreateIndex(name: "IX_donor_devices_SourceUsedDeviceId", table: "donor_devices", column: "SourceUsedDeviceId");
            migrationBuilder.CreateIndex(name: "IX_donor_devices_CustomerId", table: "donor_devices", column: "CustomerId");
            migrationBuilder.CreateIndex(name: "IX_harvested_components_DonorDeviceId", table: "harvested_components", column: "DonorDeviceId");
            migrationBuilder.CreateIndex(name: "IX_harvested_components_InventoryItemId", table: "harvested_components", column: "InventoryItemId");
            migrationBuilder.CreateIndex(name: "IX_loan_devices_AssetTag", table: "loan_devices", column: "AssetTag", unique: true);
            migrationBuilder.CreateIndex(name: "IX_loan_agreements_LoanDeviceId", table: "loan_agreements", column: "LoanDeviceId");
            migrationBuilder.CreateIndex(name: "IX_loan_agreements_CustomerId", table: "loan_agreements", column: "CustomerId");
            migrationBuilder.CreateIndex(name: "IX_loan_agreements_RepairTicketId", table: "loan_agreements", column: "RepairTicketId");
            migrationBuilder.CreateIndex(name: "IX_warranty_claims_Number", table: "warranty_claims", column: "Number", unique: true);
            migrationBuilder.CreateIndex(name: "IX_warranty_claims_WarrantyRecordId", table: "warranty_claims", column: "WarrantyRecordId");
            migrationBuilder.CreateIndex(name: "IX_warranty_claims_CustomerId", table: "warranty_claims", column: "CustomerId");
            migrationBuilder.CreateIndex(name: "IX_warranty_claims_RepairTicketId", table: "warranty_claims", column: "RepairTicketId");
            migrationBuilder.CreateIndex(name: "IX_refund_requests_Number", table: "refund_requests", column: "Number", unique: true);
            migrationBuilder.CreateIndex(name: "IX_refund_requests_PosSaleId", table: "refund_requests", column: "PosSaleId");
            migrationBuilder.CreateIndex(name: "IX_refund_requests_InvoiceId", table: "refund_requests", column: "InvoiceId");
            migrationBuilder.CreateIndex(name: "IX_repair_attachments_TicketId", table: "repair_attachments", column: "TicketId");
            migrationBuilder.CreateIndex(name: "IX_repair_signatures_TicketId", table: "repair_signatures", column: "TicketId");
            migrationBuilder.CreateIndex(name: "IX_print_jobs_PrinterProfileId", table: "print_jobs", column: "PrinterProfileId");
            migrationBuilder.CreateIndex(name: "IX_integration_health_events_CreatedAt", table: "integration_health_events", column: "CreatedAt");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_pos_sale_lines_used_devices_UsedDeviceId",
                table: "pos_sale_lines");
            migrationBuilder.DropIndex(
                name: "IX_pos_sale_lines_UsedDeviceId",
                table: "pos_sale_lines");
            migrationBuilder.DropColumn(
                name: "UsedDeviceId",
                table: "pos_sale_lines");

            migrationBuilder.DropTable(name: "harvested_components");
            migrationBuilder.DropTable(name: "loan_agreements");
            migrationBuilder.DropTable(name: "warranty_claims");
            migrationBuilder.DropTable(name: "refund_requests");
            migrationBuilder.DropTable(name: "repair_attachments");
            migrationBuilder.DropTable(name: "repair_signatures");
            migrationBuilder.DropTable(name: "print_jobs");
            migrationBuilder.DropTable(name: "integration_health_events");
            migrationBuilder.DropTable(name: "donor_devices");
            migrationBuilder.DropTable(name: "loan_devices");
            migrationBuilder.DropTable(name: "printer_profiles");
            migrationBuilder.DropColumn(name: "QuantityQuarantined", table: "inventory_items");
        }
    }
}
