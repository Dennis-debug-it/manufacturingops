using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class MoveToEditionsAndSeatTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_tenant_licenses_LicensedUsers_Positive",
                table: "tenant_licenses");

            // Plan columns the editions no longer use; nothing below reads them.
            migrationBuilder.DropColumn(
                name: "EmailIngestion",
                table: "license_plans");

            migrationBuilder.DropColumn(
                name: "MaxBranches",
                table: "license_plans");

            migrationBuilder.DropColumn(
                name: "ModulesIncluded",
                table: "license_plans");

            migrationBuilder.DropColumn(
                name: "MonitoringIntegration",
                table: "license_plans");

            migrationBuilder.RenameColumn(
                name: "LicensedUsers",
                table: "tenant_licenses",
                newName: "OfficeSeats");

            migrationBuilder.RenameColumn(
                name: "MonthlyPrice",
                table: "license_plans",
                newName: "OfficeSeatMonthlyPrice");

            migrationBuilder.RenameColumn(
                name: "AnnualPrice",
                table: "license_plans",
                newName: "OfficeSeatAnnualPrice");

            migrationBuilder.AddColumn<int>(
                name: "FieldSeats",
                table: "tenant_licenses",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "WhatsAppAddOn",
                table: "tenant_licenses",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FieldSeatAnnualPrice",
                table: "license_plans",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "FieldSeatMonthlyPrice",
                table: "license_plans",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "MinOfficeSeats",
                table: "license_plans",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "license_add_ons",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    MonthlyPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    AnnualPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    IncludedMessages = table.Column<int>(type: "integer", nullable: false),
                    ExtraMessagePrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ExtraNumberPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_license_add_ons", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "license_add_ons",
                columns: new[] { "Id", "AnnualPrice", "Code", "CreatedAt", "DisplayName", "ExtraMessagePrice", "ExtraNumberPrice", "IncludedMessages", "IsActive", "MonthlyPrice", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("55555555-5555-5555-5555-555555555571"), 15000m, "whatsapp_notifications", new DateTime(2026, 10, 24, 0, 0, 0, 0, DateTimeKind.Utc), "WhatsApp Notifications", 3m, 0m, 500, true, 1500m, null },
                    { new Guid("55555555-5555-5555-5555-555555555572"), 45000m, "whatsapp_business", new DateTime(2026, 10, 24, 0, 0, 0, 0, DateTimeKind.Utc), "WhatsApp Business", 3m, 2500m, 1500, true, 4500m, null }
                });

            migrationBuilder.InsertData(
                table: "license_plans",
                columns: new[] { "Id", "CreatedAt", "DisplayName", "FieldSeatAnnualPrice", "FieldSeatMonthlyPrice", "IsActive", "MinOfficeSeats", "OfficeSeatAnnualPrice", "OfficeSeatMonthlyPrice", "PlanCode", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("55555555-5555-5555-5555-555555555561"), new DateTime(2026, 10, 24, 0, 0, 0, 0, DateTimeKind.Utc), "Ecosys ServiceOps Essentials", 0m, 0m, true, 2, 0m, 0m, "essentials", null },
                    { new Guid("55555555-5555-5555-5555-555555555562"), new DateTime(2026, 10, 24, 0, 0, 0, 0, DateTimeKind.Utc), "Ecosys ServiceOps Professional", 0m, 0m, true, 3, 0m, 0m, "professional", null },
                    { new Guid("55555555-5555-5555-5555-555555555563"), new DateTime(2026, 10, 24, 0, 0, 0, 0, DateTimeKind.Utc), "Ecosys ServiceOps Enterprise", 0m, 0m, true, 10, 0m, 0m, "enterprise", null }
                });

            // Existing tenants move onto the editions without losing anything they use today (see
            // docs/design/LICENSING_REMODEL_PLAN.md §1.5). Order matters: the WhatsApp and edition choices
            // read the old columns, seats are split once the edition (and its minimum) is known, and the old
            // plans and columns go last.
            migrationBuilder.Sql("""
                -- 1. WhatsApp: a tenant using the older two-way inbox gets Business; one with reminders on gets Notifications.
                UPDATE tenant_licenses l
                SET "WhatsAppAddOn" = CASE
                    WHEN EXISTS (SELECT 1 FROM whatsapp_settings w WHERE w."TenantId" = l."TenantId" AND w."IsEnabled") THEN 'whatsapp_business'
                    WHEN l."WhatsAppEnabled" THEN 'whatsapp_notifications'
                    ELSE NULL END;

                -- 2. Edition: Enterprise for tenants already using several branches, API keys, webhooks or
                --    monitoring (Enterprise features); Professional, which has everything else, for the rest.
                UPDATE tenant_licenses l
                SET "LicensePlanId" = CASE
                    WHEN (SELECT COUNT(*) FROM branches b WHERE b."TenantId" = l."TenantId" AND b."IsActive") > 1
                      OR COALESCE(l."MaxBranchesOverride", 1) > 1
                      OR COALESCE(l."MonitoringIntegrationOverride", FALSE)
                      OR EXISTS (SELECT 1 FROM tenant_api_keys k WHERE k."TenantId" = l."TenantId" AND k."RevokedAt" IS NULL)
                      OR EXISTS (SELECT 1 FROM tenant_webhooks h WHERE h."TenantId" = l."TenantId" AND h."IsActive")
                      OR EXISTS (SELECT 1 FROM monitoring_webhook_integrations m WHERE m."TenantId" = l."TenantId" AND m."IsActive")
                    THEN '55555555-5555-5555-5555-555555555563'::uuid
                    ELSE '55555555-5555-5555-5555-555555555562'::uuid END;

                -- 3. Seats: active technicians become field seats; the rest of the old licensed users become
                --    office seats, never fewer than the office users already active or the edition minimum.
                UPDATE tenant_licenses l
                SET "FieldSeats" = (SELECT COUNT(*) FROM users u
                                    WHERE u."TenantId" = l."TenantId" AND u."IsActive" AND LOWER(u."Role") = 'technician');

                UPDATE tenant_licenses l
                SET "OfficeSeats" = GREATEST(
                        p."MinOfficeSeats",
                        l."OfficeSeats" - l."FieldSeats",
                        (SELECT COUNT(*) FROM users u
                         WHERE u."TenantId" = l."TenantId" AND u."IsActive" AND LOWER(u."Role") NOT IN ('technician', 'viewer', 'user')))
                FROM license_plans p
                WHERE p."Id" = l."LicensePlanId";

                -- 4. The tenant row mirrors its license.
                UPDATE tenants t
                SET "PlanName" = p."DisplayName", "MaxUsers" = l."OfficeSeats" + l."FieldSeats"
                FROM tenant_licenses l
                JOIN license_plans p ON p."Id" = l."LicensePlanId"
                WHERE t."Id" = l."TenantId";

                -- 5. Retire every plan that is not an edition (all licenses were moved above).
                DELETE FROM license_plans WHERE "PlanCode" NOT IN ('essentials', 'professional', 'enterprise');
                """);

            migrationBuilder.DropColumn(
                name: "WhatsAppEnabled",
                table: "tenant_licenses");

            migrationBuilder.AddCheckConstraint(
                name: "CK_tenant_licenses_FieldSeats_NonNegative",
                table: "tenant_licenses",
                sql: "\"FieldSeats\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_tenant_licenses_OfficeSeats_Positive",
                table: "tenant_licenses",
                sql: "\"OfficeSeats\" > 0");

            migrationBuilder.CreateIndex(
                name: "IX_license_add_ons_Code",
                table: "license_add_ons",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "license_add_ons");

            migrationBuilder.DropCheckConstraint(
                name: "CK_tenant_licenses_FieldSeats_NonNegative",
                table: "tenant_licenses");

            migrationBuilder.DropCheckConstraint(
                name: "CK_tenant_licenses_OfficeSeats_Positive",
                table: "tenant_licenses");

            migrationBuilder.RenameColumn(
                name: "OfficeSeats",
                table: "tenant_licenses",
                newName: "LicensedUsers");

            migrationBuilder.RenameColumn(
                name: "OfficeSeatMonthlyPrice",
                table: "license_plans",
                newName: "MonthlyPrice");

            migrationBuilder.RenameColumn(
                name: "OfficeSeatAnnualPrice",
                table: "license_plans",
                newName: "AnnualPrice");

            migrationBuilder.AddColumn<bool>(
                name: "WhatsAppEnabled",
                table: "tenant_licenses",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "EmailIngestion",
                table: "license_plans",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "MaxBranches",
                table: "license_plans",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModulesIncluded",
                table: "license_plans",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "MonitoringIntegration",
                table: "license_plans",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.InsertData(
                table: "license_plans",
                columns: new[] { "Id", "AnnualPrice", "CreatedAt", "DisplayName", "EmailIngestion", "IsActive", "MaxBranches", "ModulesIncluded", "MonitoringIntegration", "MonthlyPrice", "PlanCode", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("55555555-5555-5555-5555-555555555551"), 0m, new DateTime(2026, 9, 5, 0, 0, 0, 0, DateTimeKind.Utc), "Ecosys ServiceOps Operations", true, true, null, "Core,operations,EmailIngestion,MonitoringIntegration", true, 0m, "operations", null },
                    { new Guid("55555555-5555-5555-5555-555555555552"), 0m, new DateTime(2026, 9, 5, 0, 0, 0, 0, DateTimeKind.Utc), "Ecosys ServiceOps Finance", false, true, null, "Core,finance", false, 0m, "finance", null },
                    { new Guid("55555555-5555-5555-5555-555555555553"), 0m, new DateTime(2026, 9, 5, 0, 0, 0, 0, DateTimeKind.Utc), "Ecosys ServiceOps Suite", true, true, null, "Core,operations,finance,EmailIngestion,MonitoringIntegration", true, 0m, "suite", null }
                });

            // Every tenant goes back to Suite (both old modules) with all its seats as licensed users.
            migrationBuilder.Sql("""
                UPDATE tenant_licenses
                SET "LicensePlanId" = '55555555-5555-5555-5555-555555555553'::uuid,
                    "LicensedUsers" = "LicensedUsers" + "FieldSeats",
                    "WhatsAppEnabled" = "WhatsAppAddOn" IS NOT NULL;

                UPDATE tenants t
                SET "PlanName" = 'Ecosys ServiceOps Suite', "MaxUsers" = l."LicensedUsers"
                FROM tenant_licenses l
                WHERE t."Id" = l."TenantId";

                DELETE FROM license_plans WHERE "PlanCode" IN ('essentials', 'professional', 'enterprise');
                """);

            migrationBuilder.DropColumn(
                name: "FieldSeats",
                table: "tenant_licenses");

            migrationBuilder.DropColumn(
                name: "WhatsAppAddOn",
                table: "tenant_licenses");

            migrationBuilder.DropColumn(
                name: "FieldSeatAnnualPrice",
                table: "license_plans");

            migrationBuilder.DropColumn(
                name: "FieldSeatMonthlyPrice",
                table: "license_plans");

            migrationBuilder.DropColumn(
                name: "MinOfficeSeats",
                table: "license_plans");

            migrationBuilder.AddCheckConstraint(
                name: "CK_tenant_licenses_LicensedUsers_Positive",
                table: "tenant_licenses",
                sql: "\"LicensedUsers\" > 0");
        }
    }
}
