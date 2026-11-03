using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class RetireLegacyPlansAndRequireLicensedUsers : Migration
    {
        /// <summary>
        /// Licensed users becomes one explicit, required value per license, and only the three ServiceOps
        /// plans remain. Order matters: limits are resolved while the old plan defaults still exist, licenses
        /// are moved off retired plans before those plans are deleted, and the NOT NULL/CHECK rules come last.
        /// </summary>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "MaxUsersOverride",
                table: "tenant_licenses",
                newName: "LicensedUsers");

            migrationBuilder.Sql("""
                -- 1. Freeze each tenant's current effective limit onto its license: its own positive value,
                --    else the old plan default, else (previously unlimited) 10 or its active users if higher.
                UPDATE tenant_licenses l
                SET "LicensedUsers" = COALESCE(
                    CASE WHEN l."LicensedUsers" > 0 THEN l."LicensedUsers" END,
                    CASE WHEN p."MaxUsers" > 0 THEN p."MaxUsers" END,
                    GREATEST(10, (SELECT COUNT(*) FROM users u WHERE u."TenantId" = l."TenantId" AND u."IsActive")))
                FROM license_plans p
                WHERE p."Id" = l."LicensePlanId";

                -- 2. Move licenses on any non-ServiceOps plan to Suite, which keeps both modules they had.
                UPDATE tenant_licenses
                SET "LicensePlanId" = '55555555-5555-5555-5555-555555555553'
                WHERE "LicensePlanId" IN (
                    SELECT "Id" FROM license_plans WHERE "PlanCode" NOT IN ('operations', 'finance', 'suite'));

                -- 3. Retire every other plan.
                DELETE FROM license_plans WHERE "PlanCode" NOT IN ('operations', 'finance', 'suite');

                -- 4. Keep the tenant row's mirror in step with the license (see TenantLifecycleState.Sync).
                UPDATE tenants t
                SET "MaxUsers" = l."LicensedUsers", "PlanName" = p."DisplayName"
                FROM tenant_licenses l
                JOIN license_plans p ON p."Id" = l."LicensePlanId"
                WHERE l."TenantId" = t."Id";
                """);

            migrationBuilder.AlterColumn<int>(
                name: "LicensedUsers",
                table: "tenant_licenses",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_tenant_licenses_LicensedUsers_Positive",
                table: "tenant_licenses",
                sql: "\"LicensedUsers\" > 0");

            migrationBuilder.DropColumn(
                name: "MaxUsers",
                table: "license_plans");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_tenant_licenses_LicensedUsers_Positive",
                table: "tenant_licenses");

            migrationBuilder.AlterColumn<int>(
                name: "LicensedUsers",
                table: "tenant_licenses",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.RenameColumn(
                name: "LicensedUsers",
                table: "tenant_licenses",
                newName: "MaxUsersOverride");

            // The retired plans come back empty: tenants moved to Suite stay there, with their limits intact.

            migrationBuilder.AddColumn<int>(
                name: "MaxUsers",
                table: "license_plans",
                type: "integer",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "license_plans",
                keyColumn: "Id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555551"),
                column: "MaxUsers",
                value: null);

            migrationBuilder.UpdateData(
                table: "license_plans",
                keyColumn: "Id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555552"),
                column: "MaxUsers",
                value: null);

            migrationBuilder.UpdateData(
                table: "license_plans",
                keyColumn: "Id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555553"),
                column: "MaxUsers",
                value: null);

            migrationBuilder.InsertData(
                table: "license_plans",
                columns: new[] { "Id", "AnnualPrice", "CreatedAt", "DisplayName", "EmailIngestion", "IsActive", "MaxBranches", "MaxUsers", "ModulesIncluded", "MonitoringIntegration", "MonthlyPrice", "PlanCode", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("33fe16d8-5b55-45aa-89e2-1af0f2e7367d"), 999000m, new DateTime(2026, 4, 30, 0, 0, 0, 0, DateTimeKind.Utc), "Enterprise", true, false, null, null, "Core,EmailIngestion,MonitoringIntegration", true, 99900m, "Enterprise", null },
                    { new Guid("4df8af7d-8514-45e7-ad42-f5d7b745fe71"), 399000m, new DateTime(2026, 4, 30, 0, 0, 0, 0, DateTimeKind.Utc), "Professional", true, false, 10, 50, "Core,EmailIngestion,MonitoringIntegration", true, 39900m, "Professional", null },
                    { new Guid("7e2bc113-bc77-4206-8c53-4696402f69c0"), 149000m, new DateTime(2026, 4, 30, 0, 0, 0, 0, DateTimeKind.Utc), "Starter", true, false, 3, 15, "Core,EmailIngestion", false, 14900m, "Starter", null },
                    { new Guid("d220965a-38b4-45d0-9870-a8e45627e21a"), 0m, new DateTime(2026, 4, 30, 0, 0, 0, 0, DateTimeKind.Utc), "Trial", false, false, 1, 5, "Core", false, 0m, "Trial", null }
                });
        }
    }
}
