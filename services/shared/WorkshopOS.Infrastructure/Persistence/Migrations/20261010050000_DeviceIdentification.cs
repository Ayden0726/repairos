using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WorkshopOS.Infrastructure.Persistence;

#nullable disable

namespace WorkshopOS.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(WorkshopDbContext))]
    [Migration("20261010050000_DeviceIdentification")]
    public partial class DeviceIdentification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BatteryCapacity",
                table: "devices",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Chipset",
                table: "devices",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DisplaySpec",
                table: "devices",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdentificationJson",
                table: "devices",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdentificationProvider",
                table: "devices",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdentificationStatus",
                table: "devices",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "IdentificationVerifiedAt",
                table: "devices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Manufacturer",
                table: "devices",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModelNumber",
                table: "devices",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NetworkCapabilities",
                table: "devices",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Processor",
                table: "devices",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Ram",
                table: "devices",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "imei_lookup_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ImeiHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Tac = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    Provider = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ServiceId = table.Column<int>(type: "integer", nullable: true),
                    Advanced = table.Column<bool>(type: "boolean", nullable: false),
                    FromCache = table.Column<bool>(type: "boolean", nullable: false),
                    Success = table.Column<bool>(type: "boolean", nullable: false),
                    ErrorCode = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    VerificationStatus = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Cost = table.Column<decimal>(type: "numeric", nullable: true),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_imei_lookup_logs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "imei_tac_cache",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Tac = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Provider = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Brand = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Manufacturer = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Model = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ModelNumber = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    DeviceCategory = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Display = table.Column<string>(type: "text", nullable: true),
                    Processor = table.Column<string>(type: "text", nullable: true),
                    Chipset = table.Column<string>(type: "text", nullable: true),
                    BatteryCapacity = table.Column<string>(type: "text", nullable: true),
                    Network = table.Column<string>(type: "text", nullable: true),
                    StorageHint = table.Column<string>(type: "text", nullable: true),
                    RamHint = table.Column<string>(type: "text", nullable: true),
                    SpecsJson = table.Column<string>(type: "text", nullable: true),
                    VerificationStatus = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    LookedUpAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    HitCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_imei_tac_cache", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_imei_lookup_logs_ActorUserId",
                table: "imei_lookup_logs",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_imei_lookup_logs_CreatedAt",
                table: "imei_lookup_logs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_imei_lookup_logs_ImeiHash",
                table: "imei_lookup_logs",
                column: "ImeiHash");

            migrationBuilder.CreateIndex(
                name: "IX_imei_tac_cache_ExpiresAt",
                table: "imei_tac_cache",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_imei_tac_cache_Tac_Provider",
                table: "imei_tac_cache",
                columns: new[] { "Tac", "Provider" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "imei_lookup_logs");
            migrationBuilder.DropTable(name: "imei_tac_cache");

            migrationBuilder.DropColumn(name: "BatteryCapacity", table: "devices");
            migrationBuilder.DropColumn(name: "Chipset", table: "devices");
            migrationBuilder.DropColumn(name: "DisplaySpec", table: "devices");
            migrationBuilder.DropColumn(name: "IdentificationJson", table: "devices");
            migrationBuilder.DropColumn(name: "IdentificationProvider", table: "devices");
            migrationBuilder.DropColumn(name: "IdentificationStatus", table: "devices");
            migrationBuilder.DropColumn(name: "IdentificationVerifiedAt", table: "devices");
            migrationBuilder.DropColumn(name: "Manufacturer", table: "devices");
            migrationBuilder.DropColumn(name: "ModelNumber", table: "devices");
            migrationBuilder.DropColumn(name: "NetworkCapabilities", table: "devices");
            migrationBuilder.DropColumn(name: "Processor", table: "devices");
            migrationBuilder.DropColumn(name: "Ram", table: "devices");
        }
    }
}
