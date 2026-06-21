using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class PlatformCommandCentreCompleteness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_audit_logs_tenants_TenantId",
                table: "audit_logs");

            migrationBuilder.AddColumn<string>(
                name: "Phone",
                table: "users",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeactivatedByUserId",
                table: "tenants",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeactivationReason",
                table: "tenants",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BillingCycle",
                table: "tenant_licenses",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "NextBillingDate",
                table: "tenant_licenses",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountAmount",
                table: "platform_quotations",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountRate",
                table: "platform_quotations",
                type: "numeric(9,4)",
                precision: 9,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountAmount",
                table: "platform_invoices",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountRate",
                table: "platform_invoices",
                type: "numeric(9,4)",
                precision: 9,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAt",
                table: "platform_expenses",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "platform_expenses",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "AnnualPrice",
                table: "license_plans",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "ModulesIncluded",
                table: "license_plans",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MonthlyPrice",
                table: "license_plans",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                table: "audit_logs",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "ActorName",
                table: "audit_logs",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IpAddress",
                table: "audit_logs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Severity",
                table: "audit_logs",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "UserAgent",
                table: "audit_logs",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "license_plans",
                keyColumn: "Id",
                keyValue: new Guid("33fe16d8-5b55-45aa-89e2-1af0f2e7367d"),
                columns: new[] { "AnnualPrice", "ModulesIncluded", "MonthlyPrice" },
                values: new object[] { 999000m, "Core,EmailIngestion,MonitoringIntegration,AdvancedReports,ClientPortal", 99900m });

            migrationBuilder.UpdateData(
                table: "license_plans",
                keyColumn: "Id",
                keyValue: new Guid("4df8af7d-8514-45e7-ad42-f5d7b745fe71"),
                columns: new[] { "AnnualPrice", "ModulesIncluded", "MonthlyPrice" },
                values: new object[] { 399000m, "Core,EmailIngestion,MonitoringIntegration,AdvancedReports", 39900m });

            migrationBuilder.UpdateData(
                table: "license_plans",
                keyColumn: "Id",
                keyValue: new Guid("7e2bc113-bc77-4206-8c53-4696402f69c0"),
                columns: new[] { "AnnualPrice", "ModulesIncluded", "MonthlyPrice" },
                values: new object[] { 149000m, "Core,EmailIngestion", 14900m });

            migrationBuilder.UpdateData(
                table: "license_plans",
                keyColumn: "Id",
                keyValue: new Guid("d220965a-38b4-45d0-9870-a8e45627e21a"),
                columns: new[] { "AnnualPrice", "ModulesIncluded", "MonthlyPrice" },
                values: new object[] { 0m, "Core", 0m });

            migrationBuilder.AddForeignKey(
                name: "FK_audit_logs_tenants_TenantId",
                table: "audit_logs",
                column: "TenantId",
                principalTable: "tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_audit_logs_tenants_TenantId",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "Phone",
                table: "users");

            migrationBuilder.DropColumn(
                name: "DeactivatedByUserId",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "DeactivationReason",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "BillingCycle",
                table: "tenant_licenses");

            migrationBuilder.DropColumn(
                name: "NextBillingDate",
                table: "tenant_licenses");

            migrationBuilder.DropColumn(
                name: "DiscountAmount",
                table: "platform_quotations");

            migrationBuilder.DropColumn(
                name: "DiscountRate",
                table: "platform_quotations");

            migrationBuilder.DropColumn(
                name: "DiscountAmount",
                table: "platform_invoices");

            migrationBuilder.DropColumn(
                name: "DiscountRate",
                table: "platform_invoices");

            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                table: "platform_expenses");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "platform_expenses");

            migrationBuilder.DropColumn(
                name: "AnnualPrice",
                table: "license_plans");

            migrationBuilder.DropColumn(
                name: "ModulesIncluded",
                table: "license_plans");

            migrationBuilder.DropColumn(
                name: "MonthlyPrice",
                table: "license_plans");

            migrationBuilder.DropColumn(
                name: "ActorName",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "IpAddress",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "Severity",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "UserAgent",
                table: "audit_logs");

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                table: "audit_logs",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_audit_logs_tenants_TenantId",
                table: "audit_logs",
                column: "TenantId",
                principalTable: "tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
