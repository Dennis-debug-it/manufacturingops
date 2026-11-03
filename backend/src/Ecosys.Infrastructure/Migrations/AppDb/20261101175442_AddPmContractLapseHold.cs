using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class AddPmContractLapseHold : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ContinueWithoutContract",
                table: "preventive_maintenance_plans",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "HeldAt",
                table: "preventive_maintenance_plans",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HoldReason",
                table: "preventive_maintenance_plans",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LapsedContractId",
                table: "preventive_maintenance_plans",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContinueWithoutContract",
                table: "preventive_maintenance_plans");

            migrationBuilder.DropColumn(
                name: "HeldAt",
                table: "preventive_maintenance_plans");

            migrationBuilder.DropColumn(
                name: "HoldReason",
                table: "preventive_maintenance_plans");

            migrationBuilder.DropColumn(
                name: "LapsedContractId",
                table: "preventive_maintenance_plans");
        }
    }
}
