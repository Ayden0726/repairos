using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WorkshopOS.Infrastructure.Persistence;

#nullable disable

namespace WorkshopOS.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(WorkshopDbContext))]
    [Migration("20261027100000_QuoteDeviceDetailsFromImei")]
    public partial class QuoteDeviceDetailsFromImei : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DeviceColour",
                table: "quotes",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeviceStorage",
                table: "quotes",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeviceModelNumber",
                table: "quotes",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "DeviceColour", table: "quotes");
            migrationBuilder.DropColumn(name: "DeviceStorage", table: "quotes");
            migrationBuilder.DropColumn(name: "DeviceModelNumber", table: "quotes");
        }
    }
}
