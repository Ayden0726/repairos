using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkshopOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PcBuildComponentReservations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_inventory_reservations_repair_tickets_TicketId",
                table: "inventory_reservations");

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "pc_builds",
                type: "character varying(240)",
                maxLength: 240,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Quantity",
                table: "pc_build_parts",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<Guid>(
                name: "ReservationId",
                table: "pc_build_parts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "TicketId",
                table: "inventory_reservations",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "PcBuildId",
                table: "inventory_reservations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ComponentType",
                table: "inventory_items",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Other");

            migrationBuilder.CreateIndex(
                name: "IX_pc_build_parts_InventoryItemId",
                table: "pc_build_parts",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_reservations_PcBuildId_Status",
                table: "inventory_reservations",
                columns: new[] { "PcBuildId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_inventory_items_ComponentType",
                table: "inventory_items",
                column: "ComponentType");

            migrationBuilder.AddForeignKey(
                name: "FK_inventory_reservations_pc_builds_PcBuildId",
                table: "inventory_reservations",
                column: "PcBuildId",
                principalTable: "pc_builds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_inventory_reservations_repair_tickets_TicketId",
                table: "inventory_reservations",
                column: "TicketId",
                principalTable: "repair_tickets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_pc_build_parts_inventory_items_InventoryItemId",
                table: "pc_build_parts",
                column: "InventoryItemId",
                principalTable: "inventory_items",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_inventory_reservations_pc_builds_PcBuildId",
                table: "inventory_reservations");

            migrationBuilder.DropForeignKey(
                name: "FK_inventory_reservations_repair_tickets_TicketId",
                table: "inventory_reservations");

            migrationBuilder.DropForeignKey(
                name: "FK_pc_build_parts_inventory_items_InventoryItemId",
                table: "pc_build_parts");

            migrationBuilder.DropIndex(
                name: "IX_pc_build_parts_InventoryItemId",
                table: "pc_build_parts");

            migrationBuilder.DropIndex(
                name: "IX_inventory_reservations_PcBuildId_Status",
                table: "inventory_reservations");

            migrationBuilder.DropIndex(
                name: "IX_inventory_items_ComponentType",
                table: "inventory_items");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "pc_builds");

            migrationBuilder.DropColumn(
                name: "Quantity",
                table: "pc_build_parts");

            migrationBuilder.DropColumn(
                name: "ReservationId",
                table: "pc_build_parts");

            migrationBuilder.DropColumn(
                name: "PcBuildId",
                table: "inventory_reservations");

            migrationBuilder.DropColumn(
                name: "ComponentType",
                table: "inventory_items");

            migrationBuilder.AlterColumn<Guid>(
                name: "TicketId",
                table: "inventory_reservations",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_inventory_reservations_repair_tickets_TicketId",
                table: "inventory_reservations",
                column: "TicketId",
                principalTable: "repair_tickets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
