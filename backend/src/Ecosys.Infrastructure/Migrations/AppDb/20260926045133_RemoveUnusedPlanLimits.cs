using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class RemoveUnusedPlanLimits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AdvancedReportsOverride",
                table: "tenant_licenses");

            migrationBuilder.DropColumn(
                name: "ClientPortalOverride",
                table: "tenant_licenses");

            migrationBuilder.DropColumn(
                name: "MaxAssetsOverride",
                table: "tenant_licenses");

            migrationBuilder.DropColumn(
                name: "MonthlyWorkOrdersOverride",
                table: "tenant_licenses");

            migrationBuilder.DropColumn(
                name: "AdvancedReports",
                table: "license_plans");

            migrationBuilder.DropColumn(
                name: "ClientPortal",
                table: "license_plans");

            migrationBuilder.DropColumn(
                name: "MaxAssets",
                table: "license_plans");

            migrationBuilder.DropColumn(
                name: "MonthlyWorkOrders",
                table: "license_plans");

            migrationBuilder.UpdateData(
                table: "license_plans",
                keyColumn: "Id",
                keyValue: new Guid("33fe16d8-5b55-45aa-89e2-1af0f2e7367d"),
                column: "ModulesIncluded",
                value: "Core,EmailIngestion,MonitoringIntegration");

            migrationBuilder.UpdateData(
                table: "license_plans",
                keyColumn: "Id",
                keyValue: new Guid("4df8af7d-8514-45e7-ad42-f5d7b745fe71"),
                column: "ModulesIncluded",
                value: "Core,EmailIngestion,MonitoringIntegration");

            migrationBuilder.UpdateData(
                table: "license_plans",
                keyColumn: "Id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555551"),
                column: "ModulesIncluded",
                value: "Core,operations,EmailIngestion,MonitoringIntegration");

            migrationBuilder.UpdateData(
                table: "license_plans",
                keyColumn: "Id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555552"),
                column: "ModulesIncluded",
                value: "Core,finance");

            migrationBuilder.UpdateData(
                table: "license_plans",
                keyColumn: "Id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555553"),
                column: "ModulesIncluded",
                value: "Core,operations,finance,EmailIngestion,MonitoringIntegration");

            // Plans created from the platform (not seeded) may still list the retired features.
            migrationBuilder.Sql("""
                UPDATE license_plans
                SET "ModulesIncluded" = array_to_string(ARRAY(
                    SELECT token FROM unnest(string_to_array("ModulesIncluded", ',')) AS token
                    WHERE btrim(token) NOT IN ('AdvancedReports', 'ClientPortal')), ',')
                WHERE "ModulesIncluded" ~ '(AdvancedReports|ClientPortal)';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AdvancedReportsOverride",
                table: "tenant_licenses",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ClientPortalOverride",
                table: "tenant_licenses",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxAssetsOverride",
                table: "tenant_licenses",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MonthlyWorkOrdersOverride",
                table: "tenant_licenses",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AdvancedReports",
                table: "license_plans",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ClientPortal",
                table: "license_plans",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "MaxAssets",
                table: "license_plans",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MonthlyWorkOrders",
                table: "license_plans",
                type: "integer",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "license_plans",
                keyColumn: "Id",
                keyValue: new Guid("33fe16d8-5b55-45aa-89e2-1af0f2e7367d"),
                columns: new[] { "AdvancedReports", "ClientPortal", "MaxAssets", "ModulesIncluded", "MonthlyWorkOrders" },
                values: new object[] { true, true, null, "Core,EmailIngestion,MonitoringIntegration,AdvancedReports,ClientPortal", null });

            migrationBuilder.UpdateData(
                table: "license_plans",
                keyColumn: "Id",
                keyValue: new Guid("4df8af7d-8514-45e7-ad42-f5d7b745fe71"),
                columns: new[] { "AdvancedReports", "ClientPortal", "MaxAssets", "ModulesIncluded", "MonthlyWorkOrders" },
                values: new object[] { true, false, 2500, "Core,EmailIngestion,MonitoringIntegration,AdvancedReports", 5000 });

            migrationBuilder.UpdateData(
                table: "license_plans",
                keyColumn: "Id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555551"),
                columns: new[] { "AdvancedReports", "ClientPortal", "MaxAssets", "ModulesIncluded", "MonthlyWorkOrders" },
                values: new object[] { true, true, null, "Core,operations,EmailIngestion,MonitoringIntegration,AdvancedReports,ClientPortal", null });

            migrationBuilder.UpdateData(
                table: "license_plans",
                keyColumn: "Id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555552"),
                columns: new[] { "AdvancedReports", "ClientPortal", "MaxAssets", "ModulesIncluded", "MonthlyWorkOrders" },
                values: new object[] { true, false, null, "Core,finance,AdvancedReports", null });

            migrationBuilder.UpdateData(
                table: "license_plans",
                keyColumn: "Id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555553"),
                columns: new[] { "AdvancedReports", "ClientPortal", "MaxAssets", "ModulesIncluded", "MonthlyWorkOrders" },
                values: new object[] { true, true, null, "Core,operations,finance,EmailIngestion,MonitoringIntegration,AdvancedReports,ClientPortal", null });

            migrationBuilder.UpdateData(
                table: "license_plans",
                keyColumn: "Id",
                keyValue: new Guid("7e2bc113-bc77-4206-8c53-4696402f69c0"),
                columns: new[] { "AdvancedReports", "ClientPortal", "MaxAssets", "MonthlyWorkOrders" },
                values: new object[] { false, false, 250, 500 });

            migrationBuilder.UpdateData(
                table: "license_plans",
                keyColumn: "Id",
                keyValue: new Guid("d220965a-38b4-45d0-9870-a8e45627e21a"),
                columns: new[] { "AdvancedReports", "ClientPortal", "MaxAssets", "MonthlyWorkOrders" },
                values: new object[] { false, false, 25, 50 });
        }
    }
}
