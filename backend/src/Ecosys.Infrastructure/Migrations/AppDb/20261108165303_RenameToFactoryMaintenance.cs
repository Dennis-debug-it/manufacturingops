using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class RenameToFactoryMaintenance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "license_plans",
                keyColumn: "Id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555561"),
                column: "DisplayName",
                value: "Ecosys Factory Maintenance Essentials");

            migrationBuilder.UpdateData(
                table: "license_plans",
                keyColumn: "Id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555562"),
                column: "DisplayName",
                value: "Ecosys Factory Maintenance Professional");

            migrationBuilder.UpdateData(
                table: "license_plans",
                keyColumn: "Id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555563"),
                column: "DisplayName",
                value: "Ecosys Factory Maintenance Enterprise");

            // Names copied from the editions onto tenants, and the saved platform name if it was the old default.
            migrationBuilder.Sql("UPDATE tenants SET \"PlanName\" = replace(\"PlanName\", 'Ecosys ServiceOps ', 'Ecosys Factory Maintenance ') WHERE \"PlanName\" LIKE 'Ecosys ServiceOps %';");
            migrationBuilder.Sql("UPDATE platform_settings SET \"JsonValue\" = replace(\"JsonValue\", '\"Ecosys ServiceOps Platform\"', '\"Ecosys Factory Maintenance\"') WHERE \"JsonValue\" LIKE '%Ecosys ServiceOps Platform%';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "license_plans",
                keyColumn: "Id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555561"),
                column: "DisplayName",
                value: "Ecosys ServiceOps Essentials");

            migrationBuilder.UpdateData(
                table: "license_plans",
                keyColumn: "Id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555562"),
                column: "DisplayName",
                value: "Ecosys ServiceOps Professional");

            migrationBuilder.UpdateData(
                table: "license_plans",
                keyColumn: "Id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555563"),
                column: "DisplayName",
                value: "Ecosys ServiceOps Enterprise");
        }
    }
}
