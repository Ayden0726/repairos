using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WorkshopOS.Infrastructure.Persistence;

#nullable disable

namespace WorkshopOS.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(WorkshopDbContext))]
    [Migration("20261103120000_V2PlatformFitmentOfflineSites")]
    public partial class V2PlatformFitmentOfflineSites : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "part_fitments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InventoryItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    PartSku = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    PartName = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    Brand = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Model = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    DeviceType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    IsVerified = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ArchivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_part_fitments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_part_fitments_inventory_items_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "inventory_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "sync_outbox",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientMutationId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Method = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Path = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    BodyJson = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Error = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AppliedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table => table.PrimaryKey("PK_sync_outbox", x => x.Id));

            migrationBuilder.CreateTable(
                name: "inventory_bins",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: true),
                    ParentBinId = table.Column<Guid>(type: "uuid", nullable: true),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "text", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ArchivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_bins", x => x.Id);
                    table.ForeignKey(
                        name: "FK_inventory_bins_locations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_inventory_bins_inventory_bins_ParentBinId",
                        column: x => x.ParentBinId,
                        principalTable: "inventory_bins",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(name: "IX_part_fitments_Brand_Model", table: "part_fitments", columns: new[] { "Brand", "Model" });
            migrationBuilder.CreateIndex(name: "IX_part_fitments_InventoryItemId", table: "part_fitments", column: "InventoryItemId");
            migrationBuilder.CreateIndex(name: "IX_sync_outbox_UserId_ClientMutationId", table: "sync_outbox", columns: new[] { "UserId", "ClientMutationId" }, unique: true);
            migrationBuilder.CreateIndex(name: "IX_sync_outbox_CreatedAt", table: "sync_outbox", column: "CreatedAt");
            migrationBuilder.CreateIndex(name: "IX_inventory_bins_Code", table: "inventory_bins", column: "Code", unique: true);
            migrationBuilder.CreateIndex(name: "IX_inventory_bins_LocationId", table: "inventory_bins", column: "LocationId");
            migrationBuilder.CreateIndex(name: "IX_inventory_bins_ParentBinId", table: "inventory_bins", column: "ParentBinId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "part_fitments");
            migrationBuilder.DropTable(name: "sync_outbox");
            migrationBuilder.DropTable(name: "inventory_bins");
        }
    }
}
