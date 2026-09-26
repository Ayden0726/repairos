using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkshopOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class QuotePricingSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AcceptedAt",
                table: "quotes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AcceptedById",
                table: "quotes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AcceptedTotal",
                table: "quotes",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AcceptedVersion",
                table: "quotes",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CostTotal",
                table: "quotes",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "DeviceBrand",
                table: "quotes",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeviceCategory",
                table: "quotes",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeviceModel",
                table: "quotes",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeviceSerial",
                table: "quotes",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountTotal",
                table: "quotes",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "IsFrozen",
                table: "quotes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "LabourSubtotal",
                table: "quotes",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "MarginPercent",
                table: "quotes",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PartsSubtotal",
                table: "quotes",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PreRoundTotal",
                table: "quotes",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ProfitTotal",
                table: "quotes",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresApproval",
                table: "quotes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "RevisionNumber",
                table: "quotes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "RoundingMethod",
                table: "quotes",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ValidityDays",
                table: "quotes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "AdditionalAmount",
                table: "quote_lines",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "DifficultyLevelKey",
                table: "quote_lines",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountAmount",
                table: "quote_lines",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "InventoryItemId",
                table: "quote_lines",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LabourAmount",
                table: "quote_lines",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "LandedCost",
                table: "quote_lines",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "LineProfit",
                table: "quote_lines",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "LineSubtotal",
                table: "quote_lines",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "LineTotal",
                table: "quote_lines",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "MarkupAmount",
                table: "quote_lines",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "MarkupPercent",
                table: "quote_lines",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "OtherCost",
                table: "quote_lines",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PartCost",
                table: "quote_lines",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "PartName",
                table: "quote_lines",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PartSell",
                table: "quote_lines",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "ServiceName",
                table: "quote_lines",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ServicePricingId",
                table: "quote_lines",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ShippingCost",
                table: "quote_lines",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "Sku",
                table: "quote_lines",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SupplierName",
                table: "quote_lines",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "markup_tiers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MinCost = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    MaxCost = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    MarkupPercent = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ArchivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_markup_tiers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "quote_audit_log",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    QuoteId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Detail = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    OldValueJson = table.Column<string>(type: "text", nullable: true),
                    NewValueJson = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quote_audit_log", x => x.Id);
                    table.ForeignKey(
                        name: "FK_quote_audit_log_quotes_QuoteId",
                        column: x => x.QuoteId,
                        principalTable: "quotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "quote_revisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    QuoteId = table.Column<Guid>(type: "uuid", nullable: false),
                    RevisionNumber = table.Column<int>(type: "integer", nullable: false),
                    SnapshotJson = table.Column<string>(type: "text", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quote_revisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_quote_revisions_quotes_QuoteId",
                        column: x => x.QuoteId,
                        principalTable: "quotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "service_pricing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Category = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DefaultLabourFee = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    DefaultPartMarkupPercent = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ArchivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_service_pricing", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_quotes_ExpiresAt",
                table: "quotes",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_quote_lines_InventoryItemId",
                table: "quote_lines",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_quote_lines_ServicePricingId",
                table: "quote_lines",
                column: "ServicePricingId");

            migrationBuilder.CreateIndex(
                name: "IX_markup_tiers_SortOrder",
                table: "markup_tiers",
                column: "SortOrder");

            migrationBuilder.CreateIndex(
                name: "IX_quote_audit_log_QuoteId_CreatedAt",
                table: "quote_audit_log",
                columns: new[] { "QuoteId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_quote_revisions_QuoteId_RevisionNumber",
                table: "quote_revisions",
                columns: new[] { "QuoteId", "RevisionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_service_pricing_Name",
                table: "service_pricing",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_service_pricing_SortOrder",
                table: "service_pricing",
                column: "SortOrder");

            migrationBuilder.AddForeignKey(
                name: "FK_quote_lines_inventory_items_InventoryItemId",
                table: "quote_lines",
                column: "InventoryItemId",
                principalTable: "inventory_items",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_quote_lines_service_pricing_ServicePricingId",
                table: "quote_lines",
                column: "ServicePricingId",
                principalTable: "service_pricing",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_quote_lines_inventory_items_InventoryItemId",
                table: "quote_lines");

            migrationBuilder.DropForeignKey(
                name: "FK_quote_lines_service_pricing_ServicePricingId",
                table: "quote_lines");

            migrationBuilder.DropTable(
                name: "markup_tiers");

            migrationBuilder.DropTable(
                name: "quote_audit_log");

            migrationBuilder.DropTable(
                name: "quote_revisions");

            migrationBuilder.DropTable(
                name: "service_pricing");

            migrationBuilder.DropIndex(
                name: "IX_quotes_ExpiresAt",
                table: "quotes");

            migrationBuilder.DropIndex(
                name: "IX_quote_lines_InventoryItemId",
                table: "quote_lines");

            migrationBuilder.DropIndex(
                name: "IX_quote_lines_ServicePricingId",
                table: "quote_lines");

            migrationBuilder.DropColumn(
                name: "AcceptedAt",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "AcceptedById",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "AcceptedTotal",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "AcceptedVersion",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "CostTotal",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "DeviceBrand",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "DeviceCategory",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "DeviceModel",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "DeviceSerial",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "DiscountTotal",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "IsFrozen",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "LabourSubtotal",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "MarginPercent",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "PartsSubtotal",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "PreRoundTotal",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "ProfitTotal",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "RequiresApproval",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "RevisionNumber",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "RoundingMethod",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "ValidityDays",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "AdditionalAmount",
                table: "quote_lines");

            migrationBuilder.DropColumn(
                name: "DifficultyLevelKey",
                table: "quote_lines");

            migrationBuilder.DropColumn(
                name: "DiscountAmount",
                table: "quote_lines");

            migrationBuilder.DropColumn(
                name: "InventoryItemId",
                table: "quote_lines");

            migrationBuilder.DropColumn(
                name: "LabourAmount",
                table: "quote_lines");

            migrationBuilder.DropColumn(
                name: "LandedCost",
                table: "quote_lines");

            migrationBuilder.DropColumn(
                name: "LineProfit",
                table: "quote_lines");

            migrationBuilder.DropColumn(
                name: "LineSubtotal",
                table: "quote_lines");

            migrationBuilder.DropColumn(
                name: "LineTotal",
                table: "quote_lines");

            migrationBuilder.DropColumn(
                name: "MarkupAmount",
                table: "quote_lines");

            migrationBuilder.DropColumn(
                name: "MarkupPercent",
                table: "quote_lines");

            migrationBuilder.DropColumn(
                name: "OtherCost",
                table: "quote_lines");

            migrationBuilder.DropColumn(
                name: "PartCost",
                table: "quote_lines");

            migrationBuilder.DropColumn(
                name: "PartName",
                table: "quote_lines");

            migrationBuilder.DropColumn(
                name: "PartSell",
                table: "quote_lines");

            migrationBuilder.DropColumn(
                name: "ServiceName",
                table: "quote_lines");

            migrationBuilder.DropColumn(
                name: "ServicePricingId",
                table: "quote_lines");

            migrationBuilder.DropColumn(
                name: "ShippingCost",
                table: "quote_lines");

            migrationBuilder.DropColumn(
                name: "Sku",
                table: "quote_lines");

            migrationBuilder.DropColumn(
                name: "SupplierName",
                table: "quote_lines");
        }
    }
}
