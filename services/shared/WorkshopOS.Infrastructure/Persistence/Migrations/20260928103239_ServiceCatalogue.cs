using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkshopOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ServiceCatalogue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CategoryId",
                table: "service_pricing",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "service_pricing",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompatibleBrandsJson",
                table: "service_pricing",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompatibleModelsJson",
                table: "service_pricing",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomerDescription",
                table: "service_pricing",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeviceType",
                table: "service_pricing",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DiagnosticFee",
                table: "service_pricing",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EstimatedMinutes",
                table: "service_pricing",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsSystem",
                table: "service_pricing",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "MinCharge",
                table: "service_pricing",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PartsRequired",
                table: "service_pricing",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SerialRequired",
                table: "service_pricing",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "ServiceFee",
                table: "service_pricing",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "Subcategory",
                table: "service_pricing",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TechNotes",
                table: "service_pricing",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WarrantyDays",
                table: "service_pricing",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "device_brands",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    DeviceTypes = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsSystem = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ArchivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_device_brands", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "repair_service_lines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RepairTicketId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServicePricingId = table.Column<Guid>(type: "uuid", nullable: true),
                    ServiceName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    LabourFee = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    ServiceFee = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    EstimatedMinutes = table.Column<int>(type: "integer", nullable: true),
                    WarrantyDays = table.Column<int>(type: "integer", nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    PartsJson = table.Column<string>(type: "jsonb", nullable: true),
                    IsCompleted = table.Column<bool>(type: "boolean", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ArchivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_repair_service_lines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_repair_service_lines_repair_tickets_RepairTicketId",
                        column: x => x.RepairTicketId,
                        principalTable: "repair_tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_repair_service_lines_service_pricing_ServicePricingId",
                        column: x => x.ServicePricingId,
                        principalTable: "service_pricing",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "service_bundles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    BundlePrice = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsSystem = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ArchivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_service_bundles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "service_categories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    DeviceType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ParentId = table.Column<Guid>(type: "uuid", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsSystem = table.Column<bool>(type: "boolean", nullable: false),
                    Icon = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ArchivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_service_categories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_service_categories_service_categories_ParentId",
                        column: x => x.ParentId,
                        principalTable: "service_categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "service_favourites",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServicePricingId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_service_favourites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_service_favourites_service_pricing_ServicePricingId",
                        column: x => x.ServicePricingId,
                        principalTable: "service_pricing",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "service_recent_selections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServicePricingId = table.Column<Guid>(type: "uuid", nullable: false),
                    SelectedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_service_recent_selections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_service_recent_selections_service_pricing_ServicePricingId",
                        column: x => x.ServicePricingId,
                        principalTable: "service_pricing",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "device_models",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BrandId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    DeviceType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsSystem = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ArchivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_device_models", x => x.Id);
                    table.ForeignKey(
                        name: "FK_device_models_device_brands_BrandId",
                        column: x => x.BrandId,
                        principalTable: "device_brands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "service_bundle_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BundleId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServicePricingId = table.Column<Guid>(type: "uuid", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_service_bundle_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_service_bundle_items_service_bundles_BundleId",
                        column: x => x.BundleId,
                        principalTable: "service_bundles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_service_bundle_items_service_pricing_ServicePricingId",
                        column: x => x.ServicePricingId,
                        principalTable: "service_pricing",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_service_pricing_CategoryId",
                table: "service_pricing",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_service_pricing_Code",
                table: "service_pricing",
                column: "Code");

            migrationBuilder.CreateIndex(
                name: "IX_service_pricing_DeviceType",
                table: "service_pricing",
                column: "DeviceType");

            migrationBuilder.CreateIndex(
                name: "IX_device_brands_Name",
                table: "device_brands",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_device_brands_SortOrder",
                table: "device_brands",
                column: "SortOrder");

            migrationBuilder.CreateIndex(
                name: "IX_device_models_BrandId_Name",
                table: "device_models",
                columns: new[] { "BrandId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_device_models_SortOrder",
                table: "device_models",
                column: "SortOrder");

            migrationBuilder.CreateIndex(
                name: "IX_repair_service_lines_RepairTicketId",
                table: "repair_service_lines",
                column: "RepairTicketId");

            migrationBuilder.CreateIndex(
                name: "IX_repair_service_lines_ServicePricingId",
                table: "repair_service_lines",
                column: "ServicePricingId");

            migrationBuilder.CreateIndex(
                name: "IX_service_bundle_items_BundleId_ServicePricingId",
                table: "service_bundle_items",
                columns: new[] { "BundleId", "ServicePricingId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_service_bundle_items_ServicePricingId",
                table: "service_bundle_items",
                column: "ServicePricingId");

            migrationBuilder.CreateIndex(
                name: "IX_service_bundles_Code",
                table: "service_bundles",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_service_bundles_SortOrder",
                table: "service_bundles",
                column: "SortOrder");

            migrationBuilder.CreateIndex(
                name: "IX_service_categories_Key",
                table: "service_categories",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_service_categories_ParentId",
                table: "service_categories",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_service_categories_SortOrder",
                table: "service_categories",
                column: "SortOrder");

            migrationBuilder.CreateIndex(
                name: "IX_service_favourites_ServicePricingId",
                table: "service_favourites",
                column: "ServicePricingId");

            migrationBuilder.CreateIndex(
                name: "IX_service_favourites_UserId_ServicePricingId",
                table: "service_favourites",
                columns: new[] { "UserId", "ServicePricingId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_service_recent_selections_ServicePricingId",
                table: "service_recent_selections",
                column: "ServicePricingId");

            migrationBuilder.CreateIndex(
                name: "IX_service_recent_selections_UserId_SelectedAt",
                table: "service_recent_selections",
                columns: new[] { "UserId", "SelectedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_service_recent_selections_UserId_ServicePricingId",
                table: "service_recent_selections",
                columns: new[] { "UserId", "ServicePricingId" });

            migrationBuilder.AddForeignKey(
                name: "FK_service_pricing_service_categories_CategoryId",
                table: "service_pricing",
                column: "CategoryId",
                principalTable: "service_categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_service_pricing_service_categories_CategoryId",
                table: "service_pricing");

            migrationBuilder.DropTable(
                name: "device_models");

            migrationBuilder.DropTable(
                name: "repair_service_lines");

            migrationBuilder.DropTable(
                name: "service_bundle_items");

            migrationBuilder.DropTable(
                name: "service_categories");

            migrationBuilder.DropTable(
                name: "service_favourites");

            migrationBuilder.DropTable(
                name: "service_recent_selections");

            migrationBuilder.DropTable(
                name: "device_brands");

            migrationBuilder.DropTable(
                name: "service_bundles");

            migrationBuilder.DropIndex(
                name: "IX_service_pricing_CategoryId",
                table: "service_pricing");

            migrationBuilder.DropIndex(
                name: "IX_service_pricing_Code",
                table: "service_pricing");

            migrationBuilder.DropIndex(
                name: "IX_service_pricing_DeviceType",
                table: "service_pricing");

            migrationBuilder.DropColumn(
                name: "CategoryId",
                table: "service_pricing");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "service_pricing");

            migrationBuilder.DropColumn(
                name: "CompatibleBrandsJson",
                table: "service_pricing");

            migrationBuilder.DropColumn(
                name: "CompatibleModelsJson",
                table: "service_pricing");

            migrationBuilder.DropColumn(
                name: "CustomerDescription",
                table: "service_pricing");

            migrationBuilder.DropColumn(
                name: "DeviceType",
                table: "service_pricing");

            migrationBuilder.DropColumn(
                name: "DiagnosticFee",
                table: "service_pricing");

            migrationBuilder.DropColumn(
                name: "EstimatedMinutes",
                table: "service_pricing");

            migrationBuilder.DropColumn(
                name: "IsSystem",
                table: "service_pricing");

            migrationBuilder.DropColumn(
                name: "MinCharge",
                table: "service_pricing");

            migrationBuilder.DropColumn(
                name: "PartsRequired",
                table: "service_pricing");

            migrationBuilder.DropColumn(
                name: "SerialRequired",
                table: "service_pricing");

            migrationBuilder.DropColumn(
                name: "ServiceFee",
                table: "service_pricing");

            migrationBuilder.DropColumn(
                name: "Subcategory",
                table: "service_pricing");

            migrationBuilder.DropColumn(
                name: "TechNotes",
                table: "service_pricing");

            migrationBuilder.DropColumn(
                name: "WarrantyDays",
                table: "service_pricing");
        }
    }
}
