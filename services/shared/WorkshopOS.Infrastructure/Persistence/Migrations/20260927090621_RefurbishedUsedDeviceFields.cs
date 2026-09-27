using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkshopOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RefurbishedUsedDeviceFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "ConditionGrade",
                table: "used_devices",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(8)",
                oldMaxLength: 8);

            migrationBuilder.AddColumn<string>(
                name: "Brand",
                table: "used_devices",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "used_devices",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Colour",
                table: "used_devices",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                table: "used_devices",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Model",
                table: "used_devices",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "used_devices",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RepairTicketId",
                table: "used_devices",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "used_devices",
                type: "character varying(240)",
                maxLength: 240,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Specs",
                table: "used_devices",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StorageCapacity",
                table: "used_devices",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_used_devices_CustomerId",
                table: "used_devices",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_used_devices_RepairTicketId",
                table: "used_devices",
                column: "RepairTicketId");

            migrationBuilder.Sql("""
                UPDATE used_devices SET "Status" = 'In stock'
                WHERE "Status" IN ('Purchased', 'Refurbishing', 'Ready');
                UPDATE used_devices SET "Status" = 'Listed'
                WHERE "Status" IN ('Listed for sale', 'ForSale');
                UPDATE used_devices SET "ConditionGrade" = 'Excellent' WHERE "ConditionGrade" = 'A';
                UPDATE used_devices SET "ConditionGrade" = 'Good' WHERE "ConditionGrade" = 'B';
                UPDATE used_devices SET "ConditionGrade" = 'Fair' WHERE "ConditionGrade" IN ('C', 'D');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_used_devices_CustomerId",
                table: "used_devices");

            migrationBuilder.DropIndex(
                name: "IX_used_devices_RepairTicketId",
                table: "used_devices");

            migrationBuilder.DropColumn(
                name: "Brand",
                table: "used_devices");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "used_devices");

            migrationBuilder.DropColumn(
                name: "Colour",
                table: "used_devices");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                table: "used_devices");

            migrationBuilder.DropColumn(
                name: "Model",
                table: "used_devices");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "used_devices");

            migrationBuilder.DropColumn(
                name: "RepairTicketId",
                table: "used_devices");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "used_devices");

            migrationBuilder.DropColumn(
                name: "Specs",
                table: "used_devices");

            migrationBuilder.DropColumn(
                name: "StorageCapacity",
                table: "used_devices");

            migrationBuilder.AlterColumn<string>(
                name: "ConditionGrade",
                table: "used_devices",
                type: "character varying(8)",
                maxLength: 8,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32);
        }
    }
}
