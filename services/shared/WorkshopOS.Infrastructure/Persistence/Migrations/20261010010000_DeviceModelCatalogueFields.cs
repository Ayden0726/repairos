using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WorkshopOS.Infrastructure.Persistence;

#nullable disable

namespace WorkshopOS.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(WorkshopDbContext))]
    [Migration("20261010010000_DeviceModelCatalogueFields")]
    public partial class DeviceModelCatalogueFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "device_models",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(160)",
                oldMaxLength: 160);

            migrationBuilder.AddColumn<string>(
                name: "Aliases",
                table: "device_models",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsVerified",
                table: "device_models",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "ReleaseYear",
                table: "device_models",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Series",
                table: "device_models",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_device_models_DeviceType",
                table: "device_models",
                column: "DeviceType");

            migrationBuilder.CreateIndex(
                name: "IX_device_models_Series",
                table: "device_models",
                column: "Series");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_device_models_DeviceType",
                table: "device_models");

            migrationBuilder.DropIndex(
                name: "IX_device_models_Series",
                table: "device_models");

            migrationBuilder.DropColumn(
                name: "Aliases",
                table: "device_models");

            migrationBuilder.DropColumn(
                name: "IsVerified",
                table: "device_models");

            migrationBuilder.DropColumn(
                name: "ReleaseYear",
                table: "device_models");

            migrationBuilder.DropColumn(
                name: "Series",
                table: "device_models");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "device_models",
                type: "character varying(160)",
                maxLength: 160,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);
        }
    }
}
