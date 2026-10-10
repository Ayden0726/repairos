using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkshopOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PosSaleRepairDiscount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DiscountAmount",
                table: "pos_sales",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "DiscountLabel",
                table: "pos_sales",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountPercent",
                table: "pos_sales",
                type: "numeric(8,2)",
                precision: 8,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeposit",
                table: "pos_sales",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "RepairTicketId",
                table: "pos_sales",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_pos_sales_RepairTicketId",
                table: "pos_sales",
                column: "RepairTicketId");

            migrationBuilder.AddForeignKey(
                name: "FK_pos_sales_repair_tickets_RepairTicketId",
                table: "pos_sales",
                column: "RepairTicketId",
                principalTable: "repair_tickets",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_pos_sales_repair_tickets_RepairTicketId",
                table: "pos_sales");

            migrationBuilder.DropIndex(
                name: "IX_pos_sales_RepairTicketId",
                table: "pos_sales");

            migrationBuilder.DropColumn(
                name: "DiscountAmount",
                table: "pos_sales");

            migrationBuilder.DropColumn(
                name: "DiscountLabel",
                table: "pos_sales");

            migrationBuilder.DropColumn(
                name: "DiscountPercent",
                table: "pos_sales");

            migrationBuilder.DropColumn(
                name: "IsDeposit",
                table: "pos_sales");

            migrationBuilder.DropColumn(
                name: "RepairTicketId",
                table: "pos_sales");
        }
    }
}
