using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class PruneLeftoverModules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Grants and denies of capabilities whose modules are gone.
            migrationBuilder.Sql("DELETE FROM group_capabilities WHERE \"Capability\" IN ('can_view_knowledge', 'can_manage_knowledge', 'finance.view_billable_work', 'finance.manage_billable_work', 'projects.view', 'projects.manage', 'projects.variations.submit', 'projects.variations.approve', 'projects.close', 'projects.settings', 'projects.muster.mark', 'projects.financials.view', 'projects.financials.manage', 'crm.view', 'crm.manage', 'crm.view_all');");
            migrationBuilder.Sql("DELETE FROM user_permission_overrides WHERE \"Capability\" IN ('can_view_knowledge', 'can_manage_knowledge', 'finance.view_billable_work', 'finance.manage_billable_work', 'projects.view', 'projects.manage', 'projects.variations.submit', 'projects.variations.approve', 'projects.close', 'projects.settings', 'projects.muster.mark', 'projects.financials.view', 'projects.financials.manage', 'crm.view', 'crm.manage', 'crm.view_all');");
            migrationBuilder.Sql("DELETE FROM user_notification_preferences WHERE \"NotificationKey\" LIKE 'project.%';");
            migrationBuilder.Sql("DELETE FROM tenant_module_settings WHERE \"ModuleKey\" = 'payments';");
            migrationBuilder.Sql("DELETE FROM permission_thresholds WHERE \"Capability\" IN ('can_view_knowledge', 'can_manage_knowledge', 'finance.view_billable_work', 'finance.manage_billable_work', 'projects.view', 'projects.manage', 'projects.variations.submit', 'projects.variations.approve', 'projects.close', 'projects.settings', 'projects.muster.mark', 'projects.financials.view', 'projects.financials.manage', 'crm.view', 'crm.manage', 'crm.view_all');");

            migrationBuilder.DropTable(
                name: "tenant_finance_defaults");

            migrationBuilder.DropColumn(
                name: "CanManageBillableWork",
                table: "user_permissions");

            migrationBuilder.DropColumn(
                name: "CanManageCrm",
                table: "user_permissions");

            migrationBuilder.DropColumn(
                name: "CanManageKnowledge",
                table: "user_permissions");

            migrationBuilder.DropColumn(
                name: "CanViewBillableWork",
                table: "user_permissions");

            migrationBuilder.DropColumn(
                name: "CanViewCrm",
                table: "user_permissions");

            migrationBuilder.DropColumn(
                name: "CanViewKnowledge",
                table: "user_permissions");

            migrationBuilder.DropColumn(
                name: "CanManageBillableWork",
                table: "role_permissions");

            migrationBuilder.DropColumn(
                name: "CanManageCrm",
                table: "role_permissions");

            migrationBuilder.DropColumn(
                name: "CanManageKnowledge",
                table: "role_permissions");

            migrationBuilder.DropColumn(
                name: "CanViewBillableWork",
                table: "role_permissions");

            migrationBuilder.DropColumn(
                name: "CanViewCrm",
                table: "role_permissions");

            migrationBuilder.DropColumn(
                name: "CanViewKnowledge",
                table: "role_permissions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CanManageBillableWork",
                table: "user_permissions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CanManageCrm",
                table: "user_permissions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CanManageKnowledge",
                table: "user_permissions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CanViewBillableWork",
                table: "user_permissions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CanViewCrm",
                table: "user_permissions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CanViewKnowledge",
                table: "user_permissions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CanManageBillableWork",
                table: "role_permissions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CanManageCrm",
                table: "role_permissions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CanManageKnowledge",
                table: "role_permissions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CanViewBillableWork",
                table: "role_permissions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CanViewCrm",
                table: "role_permissions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CanViewKnowledge",
                table: "role_permissions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "tenant_finance_defaults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DefaultExpenseApprovalRequired = table.Column<bool>(type: "boolean", nullable: false),
                    DefaultInvoiceDueDays = table.Column<int>(type: "integer", nullable: false),
                    DefaultPaymentTerms = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DefaultQuotationValidityDays = table.Column<int>(type: "integer", nullable: false),
                    DefaultVatRate = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    EnableVat = table.Column<bool>(type: "boolean", nullable: false),
                    TaxName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_finance_defaults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tenant_finance_defaults_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tenant_finance_defaults_TenantId",
                table: "tenant_finance_defaults",
                column: "TenantId",
                unique: true);
        }
    }
}
