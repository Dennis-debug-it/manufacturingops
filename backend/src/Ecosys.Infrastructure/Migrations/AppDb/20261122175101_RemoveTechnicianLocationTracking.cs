using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class RemoveTechnicianLocationTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "work_order_events");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "work_order_events");

            migrationBuilder.DropColumn(
                name: "IsTrackingActive",
                table: "technicians");

            migrationBuilder.DropColumn(
                name: "LastKnownLatitude",
                table: "technicians");

            migrationBuilder.DropColumn(
                name: "LastKnownLongitude",
                table: "technicians");

            migrationBuilder.DropColumn(
                name: "LastLocationAt",
                table: "technicians");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Latitude",
                table: "work_order_events",
                type: "numeric(9,6)",
                precision: 9,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Longitude",
                table: "work_order_events",
                type: "numeric(9,6)",
                precision: 9,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsTrackingActive",
                table: "technicians",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "LastKnownLatitude",
                table: "technicians",
                type: "numeric(9,6)",
                precision: 9,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LastKnownLongitude",
                table: "technicians",
                type: "numeric(9,6)",
                precision: 9,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastLocationAt",
                table: "technicians",
                type: "timestamp with time zone",
                nullable: true);
        }
    }
}
