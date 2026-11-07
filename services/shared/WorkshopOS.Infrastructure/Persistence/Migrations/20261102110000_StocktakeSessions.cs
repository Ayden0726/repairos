using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WorkshopOS.Infrastructure.Persistence;

#nullable disable

namespace WorkshopOS.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(WorkshopDbContext))]
    [Migration("20261102110000_StocktakeSessions")]
    public partial class StocktakeSessions : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "stocktake_sessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    StartedById = table.Column<Guid>(type: "uuid", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ArchivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stocktake_sessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "stocktake_lines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    InventoryItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpectedQty = table.Column<int>(type: "integer", nullable: false),
                    CountedQty = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stocktake_lines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_stocktake_lines_stocktake_sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "stocktake_sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_stocktake_lines_inventory_items_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "inventory_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_stocktake_sessions_Number",
                table: "stocktake_sessions",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_stocktake_lines_SessionId_InventoryItemId",
                table: "stocktake_lines",
                columns: new[] { "SessionId", "InventoryItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_stocktake_lines_InventoryItemId",
                table: "stocktake_lines",
                column: "InventoryItemId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "stocktake_lines");
            migrationBuilder.DropTable(name: "stocktake_sessions");
        }
    }
}
