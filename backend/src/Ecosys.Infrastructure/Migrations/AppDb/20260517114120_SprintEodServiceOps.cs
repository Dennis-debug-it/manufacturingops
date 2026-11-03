using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SprintEodServiceOps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CanManageBillableWork",
                table: "user_permissions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CanManageFinance",
                table: "user_permissions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CanManageFinanceApprovals",
                table: "user_permissions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CanManageFinanceRates",
                table: "user_permissions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CanOverrideMaterialStock",
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
                name: "CanViewFinance",
                table: "user_permissions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CanViewFinanceApprovals",
                table: "user_permissions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CanViewFinanceRates",
                table: "user_permissions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ActiveDurationUnit",
                table: "clients",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ActiveDurationValue",
                table: "clients",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ActiveEndDate",
                table: "clients",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ActiveStartDate",
                table: "clients",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PmFrequencyUnit",
                table: "assets",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PmFrequencyValue",
                table: "assets",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "client_preferential_rates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    RateName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    RateType = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    ItemType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Unit = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    DefaultRate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PreferentialRate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    RequiresApproval = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_client_preferential_rates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_client_preferential_rates_clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_client_preferential_rates_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_client_preferential_rates_users_ApprovedByUserId",
                        column: x => x.ApprovedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_client_preferential_rates_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "finance_approval_rules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovalArea = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ThresholdAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    RequiredRole = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RequiredApproverUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    RequiredAssignmentGroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_finance_approval_rules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_finance_approval_rules_assignment_groups_RequiredAssignment~",
                        column: x => x.RequiredAssignmentGroupId,
                        principalTable: "assignment_groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_finance_approval_rules_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_finance_approval_rules_users_RequiredApproverUserId",
                        column: x => x.RequiredApproverUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "material_issues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    WorkOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssetId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: true),
                    SiteId = table.Column<Guid>(type: "uuid", nullable: true),
                    IssuedToUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    IssueNumber = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    IssueStatus = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    RequestedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    IssuedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    IssuedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ApprovedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RejectedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    RejectedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    TotalEstimatedCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalIssuedCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_material_issues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_material_issues_assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_material_issues_branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_material_issues_clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_material_issues_sites_SiteId",
                        column: x => x.SiteId,
                        principalTable: "sites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_material_issues_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_material_issues_users_ApprovedByUserId",
                        column: x => x.ApprovedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_material_issues_users_IssuedByUserId",
                        column: x => x.IssuedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_material_issues_users_IssuedToUserId",
                        column: x => x.IssuedToUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_material_issues_users_RejectedByUserId",
                        column: x => x.RejectedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_material_issues_users_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_material_issues_work_orders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "work_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "material_issue_lines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    MaterialIssueId = table.Column<Guid>(type: "uuid", nullable: false),
                    MaterialItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuantityRequested = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    QuantityIssued = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    QuantityReturned = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Unit = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    UnitCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_material_issue_lines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_material_issue_lines_material_issues_MaterialIssueId",
                        column: x => x.MaterialIssueId,
                        principalTable: "material_issues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_material_issue_lines_material_items_MaterialItemId",
                        column: x => x.MaterialItemId,
                        principalTable: "material_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_client_preferential_rates_ApprovedByUserId",
                table: "client_preferential_rates",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_client_preferential_rates_ClientId",
                table: "client_preferential_rates",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_client_preferential_rates_CreatedByUserId",
                table: "client_preferential_rates",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_client_preferential_rates_TenantId_ClientId_RateName_ItemTy~",
                table: "client_preferential_rates",
                columns: new[] { "TenantId", "ClientId", "RateName", "ItemType", "EffectiveFrom" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_finance_approval_rules_RequiredApproverUserId",
                table: "finance_approval_rules",
                column: "RequiredApproverUserId");

            migrationBuilder.CreateIndex(
                name: "IX_finance_approval_rules_RequiredAssignmentGroupId",
                table: "finance_approval_rules",
                column: "RequiredAssignmentGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_finance_approval_rules_TenantId_ApprovalArea_Sequence",
                table: "finance_approval_rules",
                columns: new[] { "TenantId", "ApprovalArea", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_material_issue_lines_MaterialIssueId",
                table: "material_issue_lines",
                column: "MaterialIssueId");

            migrationBuilder.CreateIndex(
                name: "IX_material_issue_lines_MaterialItemId",
                table: "material_issue_lines",
                column: "MaterialItemId");

            migrationBuilder.CreateIndex(
                name: "IX_material_issues_ApprovedByUserId",
                table: "material_issues",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_material_issues_AssetId",
                table: "material_issues",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_material_issues_BranchId",
                table: "material_issues",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_material_issues_ClientId",
                table: "material_issues",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_material_issues_IssuedByUserId",
                table: "material_issues",
                column: "IssuedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_material_issues_IssuedToUserId",
                table: "material_issues",
                column: "IssuedToUserId");

            migrationBuilder.CreateIndex(
                name: "IX_material_issues_RejectedByUserId",
                table: "material_issues",
                column: "RejectedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_material_issues_RequestedByUserId",
                table: "material_issues",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_material_issues_SiteId",
                table: "material_issues",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "IX_material_issues_TenantId_IssueNumber",
                table: "material_issues",
                columns: new[] { "TenantId", "IssueNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_material_issues_WorkOrderId",
                table: "material_issues",
                column: "WorkOrderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "client_preferential_rates");

            migrationBuilder.DropTable(
                name: "finance_approval_rules");

            migrationBuilder.DropTable(
                name: "material_issue_lines");

            migrationBuilder.DropTable(
                name: "material_issues");

            migrationBuilder.DropColumn(
                name: "CanManageBillableWork",
                table: "user_permissions");

            migrationBuilder.DropColumn(
                name: "CanManageFinance",
                table: "user_permissions");

            migrationBuilder.DropColumn(
                name: "CanManageFinanceApprovals",
                table: "user_permissions");

            migrationBuilder.DropColumn(
                name: "CanManageFinanceRates",
                table: "user_permissions");

            migrationBuilder.DropColumn(
                name: "CanOverrideMaterialStock",
                table: "user_permissions");

            migrationBuilder.DropColumn(
                name: "CanViewBillableWork",
                table: "user_permissions");

            migrationBuilder.DropColumn(
                name: "CanViewFinance",
                table: "user_permissions");

            migrationBuilder.DropColumn(
                name: "CanViewFinanceApprovals",
                table: "user_permissions");

            migrationBuilder.DropColumn(
                name: "CanViewFinanceRates",
                table: "user_permissions");

            migrationBuilder.DropColumn(
                name: "ActiveDurationUnit",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "ActiveDurationValue",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "ActiveEndDate",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "ActiveStartDate",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "PmFrequencyUnit",
                table: "assets");

            migrationBuilder.DropColumn(
                name: "PmFrequencyValue",
                table: "assets");
        }
    }
}
