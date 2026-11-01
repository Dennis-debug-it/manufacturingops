using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class AddStalledDeals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "StalledAfterDays",
                table: "tenant_sales_settings",
                type: "integer",
                nullable: false,
                defaultValue: 14);

            migrationBuilder.AddColumn<DateTime>(
                name: "StallReminderSentAt",
                table: "sales_opportunities",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StalledAfterDays",
                table: "tenant_sales_settings");

            migrationBuilder.DropColumn(
                name: "StallReminderSentAt",
                table: "sales_opportunities");
        }
    }
}
