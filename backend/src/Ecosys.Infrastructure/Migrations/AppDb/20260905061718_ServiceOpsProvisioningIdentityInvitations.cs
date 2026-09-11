using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class ServiceOpsProvisioningIdentityInvitations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "tenants",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TimeZoneId",
                table: "tenants",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Purpose",
                table: "password_reset_tokens",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "PasswordReset");

            migrationBuilder.AddColumn<DateTime>(
                name: "RevokedAt",
                table: "password_reset_tokens",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SupersededAt",
                table: "password_reset_tokens",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmailSignature",
                table: "email_settings",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "InboundEnabled",
                table: "email_settings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ManagedEmailAddress",
                table: "email_settings",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "license_plans",
                keyColumn: "Id",
                keyValue: new Guid("33fe16d8-5b55-45aa-89e2-1af0f2e7367d"),
                column: "IsActive",
                value: false);

            migrationBuilder.UpdateData(
                table: "license_plans",
                keyColumn: "Id",
                keyValue: new Guid("4df8af7d-8514-45e7-ad42-f5d7b745fe71"),
                column: "IsActive",
                value: false);

            migrationBuilder.UpdateData(
                table: "license_plans",
                keyColumn: "Id",
                keyValue: new Guid("7e2bc113-bc77-4206-8c53-4696402f69c0"),
                column: "IsActive",
                value: false);

            migrationBuilder.UpdateData(
                table: "license_plans",
                keyColumn: "Id",
                keyValue: new Guid("d220965a-38b4-45d0-9870-a8e45627e21a"),
                column: "IsActive",
                value: false);

            migrationBuilder.InsertData(
                table: "license_plans",
                columns: new[] { "Id", "AdvancedReports", "AnnualPrice", "ClientPortal", "CreatedAt", "DisplayName", "EmailIngestion", "IsActive", "MaxAssets", "MaxBranches", "MaxUsers", "ModulesIncluded", "MonitoringIntegration", "MonthlyPrice", "MonthlyWorkOrders", "PlanCode", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("55555555-5555-5555-5555-555555555551"), true, 0m, true, new DateTime(2026, 9, 5, 0, 0, 0, 0, DateTimeKind.Utc), "Ecosys ServiceOps Operations", true, true, null, null, null, "Core,operations,EmailIngestion,MonitoringIntegration,AdvancedReports,ClientPortal", true, 0m, null, "operations", null },
                    { new Guid("55555555-5555-5555-5555-555555555552"), true, 0m, false, new DateTime(2026, 9, 5, 0, 0, 0, 0, DateTimeKind.Utc), "Ecosys ServiceOps Finance", false, true, null, null, null, "Core,finance,AdvancedReports", false, 0m, null, "finance", null },
                    { new Guid("55555555-5555-5555-5555-555555555553"), true, 0m, true, new DateTime(2026, 9, 5, 0, 0, 0, 0, DateTimeKind.Utc), "Ecosys ServiceOps Suite", true, true, null, null, null, "Core,operations,finance,EmailIngestion,MonitoringIntegration,AdvancedReports,ClientPortal", true, 0m, null, "suite", null }
                });

            // Preserve existing contacts, custom SMTP credentials, and intake addresses. Only the
            // new managed identity is populated; existing valid slugs are retained whenever safe.
            migrationBuilder.Sql("""
                UPDATE tenants SET "TimeZoneId" = CASE lower(trim("Country"))
                    WHEN 'kenya' THEN 'Africa/Nairobi' WHEN 'ke' THEN 'Africa/Nairobi'
                    WHEN 'uganda' THEN 'Africa/Kampala' WHEN 'ug' THEN 'Africa/Kampala'
                    WHEN 'tanzania' THEN 'Africa/Dar_es_Salaam' WHEN 'tz' THEN 'Africa/Dar_es_Salaam'
                    WHEN 'rwanda' THEN 'Africa/Kigali' WHEN 'rw' THEN 'Africa/Kigali'
                    WHEN 'nigeria' THEN 'Africa/Lagos' WHEN 'ng' THEN 'Africa/Lagos'
                    WHEN 'south africa' THEN 'Africa/Johannesburg' WHEN 'za' THEN 'Africa/Johannesburg'
                    WHEN 'united kingdom' THEN 'Europe/London' WHEN 'gb' THEN 'Europe/London'
                    WHEN 'uk' THEN 'Europe/London' ELSE 'Etc/UTC' END
                WHERE "TimeZoneId" = '';
                UPDATE tenants SET "Currency" = CASE lower(trim("Country"))
                    WHEN 'kenya' THEN 'KES' WHEN 'ke' THEN 'KES' WHEN 'uganda' THEN 'UGX' WHEN 'ug' THEN 'UGX'
                    WHEN 'tanzania' THEN 'TZS' WHEN 'tz' THEN 'TZS' WHEN 'rwanda' THEN 'RWF' WHEN 'rw' THEN 'RWF'
                    WHEN 'nigeria' THEN 'NGN' WHEN 'ng' THEN 'NGN' WHEN 'south africa' THEN 'ZAR' WHEN 'za' THEN 'ZAR'
                    WHEN 'united kingdom' THEN 'GBP' WHEN 'gb' THEN 'GBP' WHEN 'uk' THEN 'GBP' ELSE 'USD' END
                WHERE "Currency" = '';

                DO $backfill$
                DECLARE tenant_row record; candidate text; base_slug text; address text; suffix integer;
                BEGIN
                    FOR tenant_row IN SELECT * FROM tenants
                        WHERE "Id" <> '00000000-0000-0000-0000-000000000001'::uuid
                        ORDER BY CASE WHEN "Slug" ~ '^[a-z0-9]+(-[a-z0-9]+)*$' AND length("Slug") BETWEEN 3 AND 63 THEN 0 ELSE 1 END,
                            "CreatedAt", "Id"
                    LOOP
                        base_slug := lower(trim(coalesce(nullif(tenant_row."Slug", ''), tenant_row."CompanyName")));
                        base_slug := trim(both '-' from regexp_replace(base_slug, '[^a-z0-9]+', '-', 'g'));
                        base_slug := trim(trailing '-' from left(base_slug, 63));
                        IF length(base_slug) < 3 THEN base_slug := 'tenant-' || left(replace(tenant_row."Id"::text, '-', ''), 12); END IF;
                        IF base_slug = ANY(ARRAY['admin','api','app','mail','support','help','serviceops','ecosys','system','root','postmaster'])
                            THEN base_slug := base_slug || '-tenant'; END IF;
                        candidate := base_slug; suffix := 1;
                        LOOP
                            address := candidate || '@serviceops.ecosysdigital.co.ke';
                            EXIT WHEN NOT EXISTS (SELECT 1 FROM tenants WHERE "Id" <> tenant_row."Id" AND lower("Slug") = candidate)
                                AND NOT EXISTS (SELECT 1 FROM email_intake_settings WHERE "TenantId" <> tenant_row."Id" AND lower("IntakeEmailAddress") = address)
                                AND NOT EXISTS (SELECT 1 FROM email_settings WHERE "TenantId" <> tenant_row."Id" AND (lower("ManagedEmailAddress") = address OR lower("SenderAddress") = address));
                            suffix := suffix + 1;
                            candidate := trim(trailing '-' from left(base_slug, 63 - length(suffix::text) - 1)) || '-' || suffix;
                        END LOOP;
                        UPDATE tenants SET "Slug" = candidate WHERE "Id" = tenant_row."Id";
                        INSERT INTO email_settings ("Id", "TenantId", "CreatedAt", "UsePlatformDefaults", "OverrideSmtpSettings",
                            "IsEnabled", "Provider", "Host", "Port", "UseSsl", "SenderName", "SenderAddress", "IsConfigured", "InboundEnabled", "ManagedEmailAddress")
                        VALUES (md5(tenant_row."Id"::text || ':managed-email')::uuid, tenant_row."Id", now(), true, false,
                            true, 'Smtp', '', 25, false, left(tenant_row."CompanyName", 150), address, false, true, address)
                        ON CONFLICT ("TenantId") DO UPDATE SET "ManagedEmailAddress" = excluded."ManagedEmailAddress", "InboundEnabled" = true;
                    END LOOP;
                END $backfill$;

                -- Existing setup links keep their original expiration and become identifiable as invitations.
                UPDATE password_reset_tokens p SET "Purpose" = 'Invitation'
                FROM users u WHERE u."Id" = p."UserId" AND u."MustChangePassword" = true AND p."UsedAt" IS NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_email_settings_ManagedEmailAddress",
                table: "email_settings",
                column: "ManagedEmailAddress",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_email_settings_ManagedEmailAddress",
                table: "email_settings");

            migrationBuilder.DeleteData(
                table: "license_plans",
                keyColumn: "Id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555551"));

            migrationBuilder.DeleteData(
                table: "license_plans",
                keyColumn: "Id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555552"));

            migrationBuilder.DeleteData(
                table: "license_plans",
                keyColumn: "Id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555553"));

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "TimeZoneId",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "Purpose",
                table: "password_reset_tokens");

            migrationBuilder.DropColumn(
                name: "RevokedAt",
                table: "password_reset_tokens");

            migrationBuilder.DropColumn(
                name: "SupersededAt",
                table: "password_reset_tokens");

            migrationBuilder.DropColumn(
                name: "EmailSignature",
                table: "email_settings");

            migrationBuilder.DropColumn(
                name: "InboundEnabled",
                table: "email_settings");

            migrationBuilder.DropColumn(
                name: "ManagedEmailAddress",
                table: "email_settings");

            migrationBuilder.UpdateData(
                table: "license_plans",
                keyColumn: "Id",
                keyValue: new Guid("33fe16d8-5b55-45aa-89e2-1af0f2e7367d"),
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "license_plans",
                keyColumn: "Id",
                keyValue: new Guid("4df8af7d-8514-45e7-ad42-f5d7b745fe71"),
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "license_plans",
                keyColumn: "Id",
                keyValue: new Guid("7e2bc113-bc77-4206-8c53-4696402f69c0"),
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "license_plans",
                keyColumn: "Id",
                keyValue: new Guid("d220965a-38b4-45d0-9870-a8e45627e21a"),
                column: "IsActive",
                value: true);
        }
    }
}
