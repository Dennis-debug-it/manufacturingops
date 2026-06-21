using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class PlatformTenantManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Address",
                table: "tenants",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "tenants",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactEmail",
                table: "tenants",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactName",
                table: "tenants",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactPhone",
                table: "tenants",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "County",
                table: "tenants",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByUserId",
                table: "tenants",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LicenseStatus",
                table: "tenants",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxBranches",
                table: "tenants",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxUsers",
                table: "tenants",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "tenants",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PlanName",
                table: "tenants",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Slug",
                table: "tenants",
                type: "character varying(160)",
                maxLength: 160,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "tenants",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Active");

            migrationBuilder.AddColumn<DateTime>(
                name: "SubscriptionEndsAt",
                table: "tenants",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TaxPin",
                table: "tenants",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TrialEndsAt",
                table: "tenants",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedByUserId",
                table: "tenants",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE tenants
                SET "Name" = COALESCE(NULLIF("Name", ''), "CompanyName"),
                    "Slug" = CONCAT(
                        TRIM(BOTH '-' FROM REGEXP_REPLACE(LOWER(COALESCE(NULLIF("CompanyName", ''), 'tenant')), '[^a-z0-9]+', '-', 'g')),
                        '-',
                        SUBSTRING(REPLACE("Id"::text, '-', '') FROM 1 FOR 8)
                    ),
                    "ContactEmail" = NULLIF("Email", ''),
                    "ContactPhone" = NULLIF("Phone", ''),
                    "Status" = CASE WHEN "IsActive" THEN 'Active' ELSE 'Inactive' END
                WHERE "Slug" = '' OR "Name" = '';
                """);

            migrationBuilder.Sql("""
                UPDATE tenants AS tenant
                SET "PlanName" = COALESCE(plan."DisplayName", tenant."PlanName"),
                    "LicenseStatus" = COALESCE(license."Status", tenant."LicenseStatus"),
                    "MaxUsers" = COALESCE(license."MaxUsersOverride", plan."MaxUsers", tenant."MaxUsers"),
                    "MaxBranches" = COALESCE(license."MaxBranchesOverride", plan."MaxBranches", tenant."MaxBranches"),
                    "TrialEndsAt" = COALESCE(license."TrialEndsAt", tenant."TrialEndsAt"),
                    "SubscriptionEndsAt" = COALESCE(license."ExpiresAt", tenant."SubscriptionEndsAt")
                FROM tenant_licenses AS license
                LEFT JOIN license_plans AS plan ON plan."Id" = license."LicensePlanId"
                WHERE tenant."Id" = license."TenantId";
                """);

            migrationBuilder.Sql("""
                UPDATE tenants
                SET "LicenseStatus" = COALESCE(NULLIF("LicenseStatus", ''), CASE WHEN "Status" = 'Trial' THEN 'Trial' ELSE 'Active' END);
                """);

            migrationBuilder.CreateIndex(
                name: "IX_tenants_Slug",
                table: "tenants",
                column: "Slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_tenants_Slug",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "Address",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "City",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "ContactEmail",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "ContactName",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "ContactPhone",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "County",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "LicenseStatus",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "MaxBranches",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "MaxUsers",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "PlanName",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "Slug",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "SubscriptionEndsAt",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "TaxPin",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "TrialEndsAt",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "tenants");
        }
    }
}
