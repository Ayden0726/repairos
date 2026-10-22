using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WorkshopOS.Infrastructure.Persistence;

#nullable disable

namespace WorkshopOS.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(WorkshopDbContext))]
    [Migration("20261022130000_UserPrefsAndQuoteImei")]
    public partial class UserPrefsAndQuoteImei : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DeviceImei",
                table: "quotes",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "user_ui_preferences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    JsonValue = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_ui_preferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_ui_preferences_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_user_ui_preferences_UserId_Key",
                table: "user_ui_preferences",
                columns: new[] { "UserId", "Key" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "user_ui_preferences");
            migrationBuilder.DropColumn(name: "DeviceImei", table: "quotes");
        }
    }
}
