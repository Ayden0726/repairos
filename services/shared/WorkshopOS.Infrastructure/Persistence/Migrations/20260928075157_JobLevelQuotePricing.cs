using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkshopOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class JobLevelQuotePricing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AdditionalTotal",
                table: "quotes",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "LabourFee",
                table: "quotes",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "MarkupAmount",
                table: "quotes",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "MarkupPercent",
                table: "quotes",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PartsCostTotal",
                table: "quotes",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PartsSellTotal",
                table: "quotes",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AdditionalTotal",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "LabourFee",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "MarkupAmount",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "MarkupPercent",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "PartsCostTotal",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "PartsSellTotal",
                table: "quotes");
        }
    }
}
