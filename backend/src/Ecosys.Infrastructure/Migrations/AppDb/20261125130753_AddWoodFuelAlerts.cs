using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class AddWoodFuelAlerts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LowCoverAlertSentAt",
                table: "wood_fuel_settings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "WetAlertMoisturePct",
                table: "wood_fuel_settings",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                // Rows that already exist start at 35 %, the same as a new factory's settings.
                defaultValue: 35m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LowCoverAlertSentAt",
                table: "wood_fuel_settings");

            migrationBuilder.DropColumn(
                name: "WetAlertMoisturePct",
                table: "wood_fuel_settings");
        }
    }
}
