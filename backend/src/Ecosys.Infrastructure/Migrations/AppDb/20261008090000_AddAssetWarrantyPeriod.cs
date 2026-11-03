using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class AddAssetWarrantyPeriod : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "WarrantyPeriodMonths",
                table: "assets",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "WarrantyStartDate",
                table: "assets",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WarrantyPeriodMonths",
                table: "assets");

            migrationBuilder.DropColumn(
                name: "WarrantyStartDate",
                table: "assets");
        }
    }
}
