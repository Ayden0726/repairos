using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkshopOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RefurbishedPhase1Acquisitions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AcquisitionLeadId",
                table: "used_devices",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ActivationLockStatus",
                table: "used_devices",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AssignedTechnicianId",
                table: "used_devices",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Barcode",
                table: "used_devices",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BatteryHealthPercent",
                table: "used_devices",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Carrier",
                table: "used_devices",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CarrierLockStatus",
                table: "used_devices",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "DataSanitisedConfirmed",
                table: "used_devices",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "FactoryResetConfirmed",
                table: "used_devices",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "FindMyRemoved",
                table: "used_devices",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "FrpStatus",
                table: "used_devices",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FunctionalGrade",
                table: "used_devices",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPartsOnly",
                table: "used_devices",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "LabourCost",
                table: "used_devices",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ListedAt",
                table: "used_devices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ListingUrl",
                table: "used_devices",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LocationBin",
                table: "used_devices",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LostStolenStatus",
                table: "used_devices",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MarketplaceFees",
                table: "used_devices",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "ModelNumber",
                table: "used_devices",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "OwnershipDeclared",
                table: "used_devices",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "OwnershipEvidenceOnFile",
                table: "used_devices",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "PackagingCost",
                table: "used_devices",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PaymentFees",
                table: "used_devices",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PurchaseDate",
                table: "used_devices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReceivedAt",
                table: "used_devices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SecurityCheckNotes",
                table: "used_devices",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ShippingInCost",
                table: "used_devices",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ShippingOutCost",
                table: "used_devices",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "SimConfiguration",
                table: "used_devices",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Sku",
                table: "used_devices",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SoldAt",
                table: "used_devices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StockId",
                table: "used_devices",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "WarrantyAllowance",
                table: "used_devices",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "WooCommerceProductId",
                table: "used_devices",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "acquisition_leads",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Reference = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Brand = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Model = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ModelNumber = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Category = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    StorageCapacity = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Colour = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Imei = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Serial = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Carrier = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    SellerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SellerContact = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true),
                    SellerEmail = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    SellerPhone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    SellerType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    AcquisitionSource = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ListingUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    AskingPrice = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    ProposedOffer = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    AcceptedPurchasePrice = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    DeviceCondition = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    KnownFaults = table.Column<string>(type: "text", nullable: true),
                    SuspectedFaults = table.Column<string>(type: "text", nullable: true),
                    ActivationLockStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    PhotoUrlsJson = table.Column<string>(type: "text", nullable: true),
                    Notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    PurchaseDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PaymentMethod = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ShippingOrCollection = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AssignedStaffId = table.Column<Guid>(type: "uuid", nullable: true),
                    FollowUpAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UsedDeviceId = table.Column<Guid>(type: "uuid", nullable: true),
                    SellerCustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ArchivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_acquisition_leads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_acquisition_leads_used_devices_UsedDeviceId",
                        column: x => x.UsedDeviceId,
                        principalTable: "used_devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "acquisition_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AcquisitionLeadId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    EventType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Summary = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_acquisition_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_acquisition_events_acquisition_leads_AcquisitionLeadId",
                        column: x => x.AcquisitionLeadId,
                        principalTable: "acquisition_leads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "acquisition_offers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AcquisitionLeadId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Message = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RespondedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EstimatedResalePrice = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    EstimatedPartsCost = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    LabourAllowance = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    EstimatedProfit = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    EstimatedMarginPercent = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                    AbsoluteMaxPurchasePrice = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    BuyRecommendation = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_acquisition_offers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_acquisition_offers_acquisition_leads_AcquisitionLeadId",
                        column: x => x.AcquisitionLeadId,
                        principalTable: "acquisition_leads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_used_devices_AcquisitionLeadId",
                table: "used_devices",
                column: "AcquisitionLeadId");

            migrationBuilder.CreateIndex(
                name: "IX_used_devices_Imei",
                table: "used_devices",
                column: "Imei");

            migrationBuilder.CreateIndex(
                name: "IX_used_devices_StockId",
                table: "used_devices",
                column: "StockId");

            migrationBuilder.CreateIndex(
                name: "IX_acquisition_events_AcquisitionLeadId_CreatedAt",
                table: "acquisition_events",
                columns: new[] { "AcquisitionLeadId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_acquisition_leads_AssignedStaffId",
                table: "acquisition_leads",
                column: "AssignedStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_acquisition_leads_FollowUpAt",
                table: "acquisition_leads",
                column: "FollowUpAt");

            migrationBuilder.CreateIndex(
                name: "IX_acquisition_leads_Imei",
                table: "acquisition_leads",
                column: "Imei");

            migrationBuilder.CreateIndex(
                name: "IX_acquisition_leads_Reference",
                table: "acquisition_leads",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_acquisition_leads_Status",
                table: "acquisition_leads",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_acquisition_leads_UsedDeviceId",
                table: "acquisition_leads",
                column: "UsedDeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_acquisition_offers_AcquisitionLeadId",
                table: "acquisition_offers",
                column: "AcquisitionLeadId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "acquisition_events");

            migrationBuilder.DropTable(
                name: "acquisition_offers");

            migrationBuilder.DropTable(
                name: "acquisition_leads");

            migrationBuilder.DropIndex(
                name: "IX_used_devices_AcquisitionLeadId",
                table: "used_devices");

            migrationBuilder.DropIndex(
                name: "IX_used_devices_Imei",
                table: "used_devices");

            migrationBuilder.DropIndex(
                name: "IX_used_devices_StockId",
                table: "used_devices");

            migrationBuilder.DropColumn(
                name: "AcquisitionLeadId",
                table: "used_devices");

            migrationBuilder.DropColumn(
                name: "ActivationLockStatus",
                table: "used_devices");

            migrationBuilder.DropColumn(
                name: "AssignedTechnicianId",
                table: "used_devices");

            migrationBuilder.DropColumn(
                name: "Barcode",
                table: "used_devices");

            migrationBuilder.DropColumn(
                name: "BatteryHealthPercent",
                table: "used_devices");

            migrationBuilder.DropColumn(
                name: "Carrier",
                table: "used_devices");

            migrationBuilder.DropColumn(
                name: "CarrierLockStatus",
                table: "used_devices");

            migrationBuilder.DropColumn(
                name: "DataSanitisedConfirmed",
                table: "used_devices");

            migrationBuilder.DropColumn(
                name: "FactoryResetConfirmed",
                table: "used_devices");

            migrationBuilder.DropColumn(
                name: "FindMyRemoved",
                table: "used_devices");

            migrationBuilder.DropColumn(
                name: "FrpStatus",
                table: "used_devices");

            migrationBuilder.DropColumn(
                name: "FunctionalGrade",
                table: "used_devices");

            migrationBuilder.DropColumn(
                name: "IsPartsOnly",
                table: "used_devices");

            migrationBuilder.DropColumn(
                name: "LabourCost",
                table: "used_devices");

            migrationBuilder.DropColumn(
                name: "ListedAt",
                table: "used_devices");

            migrationBuilder.DropColumn(
                name: "ListingUrl",
                table: "used_devices");

            migrationBuilder.DropColumn(
                name: "LocationBin",
                table: "used_devices");

            migrationBuilder.DropColumn(
                name: "LostStolenStatus",
                table: "used_devices");

            migrationBuilder.DropColumn(
                name: "MarketplaceFees",
                table: "used_devices");

            migrationBuilder.DropColumn(
                name: "ModelNumber",
                table: "used_devices");

            migrationBuilder.DropColumn(
                name: "OwnershipDeclared",
                table: "used_devices");

            migrationBuilder.DropColumn(
                name: "OwnershipEvidenceOnFile",
                table: "used_devices");

            migrationBuilder.DropColumn(
                name: "PackagingCost",
                table: "used_devices");

            migrationBuilder.DropColumn(
                name: "PaymentFees",
                table: "used_devices");

            migrationBuilder.DropColumn(
                name: "PurchaseDate",
                table: "used_devices");

            migrationBuilder.DropColumn(
                name: "ReceivedAt",
                table: "used_devices");

            migrationBuilder.DropColumn(
                name: "SecurityCheckNotes",
                table: "used_devices");

            migrationBuilder.DropColumn(
                name: "ShippingInCost",
                table: "used_devices");

            migrationBuilder.DropColumn(
                name: "ShippingOutCost",
                table: "used_devices");

            migrationBuilder.DropColumn(
                name: "SimConfiguration",
                table: "used_devices");

            migrationBuilder.DropColumn(
                name: "Sku",
                table: "used_devices");

            migrationBuilder.DropColumn(
                name: "SoldAt",
                table: "used_devices");

            migrationBuilder.DropColumn(
                name: "StockId",
                table: "used_devices");

            migrationBuilder.DropColumn(
                name: "WarrantyAllowance",
                table: "used_devices");

            migrationBuilder.DropColumn(
                name: "WooCommerceProductId",
                table: "used_devices");
        }
    }
}
