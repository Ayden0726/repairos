using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WorkshopOS.Infrastructure.Persistence;

#nullable disable

namespace WorkshopOS.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(WorkshopDbContext))]
    [Migration("20261102100000_BackupVerifyAndAudit")]
    public partial class BackupVerifyAndAudit : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Method",
                table: "backup_records",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "SizeBytes",
                table: "backup_records",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Sha256",
                table: "backup_records",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Verified",
                table: "backup_records",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "Method", table: "backup_records");
            migrationBuilder.DropColumn(name: "SizeBytes", table: "backup_records");
            migrationBuilder.DropColumn(name: "Sha256", table: "backup_records");
            migrationBuilder.DropColumn(name: "Verified", table: "backup_records");
        }
    }
}
