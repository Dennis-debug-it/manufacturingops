using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class AddFactoryWorkOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "ClientId",
                table: "work_orders",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAt",
                table: "work_orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ApprovedByUserId",
                table: "work_orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DowntimeEndedAt",
                table: "work_orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DowntimeStartedAt",
                table: "work_orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FailureCauseCodeId",
                table: "work_orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FailureProblemCodeId",
                table: "work_orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FailureRemedyCodeId",
                table: "work_orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsBreakdown",
                table: "work_orders",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "NeedsLineStop",
                table: "work_orders",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "OrgNodeId",
                table: "work_orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PlannedAt",
                table: "work_orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestSource",
                table: "work_orders",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestedByName",
                table: "work_orders",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReworkOfWorkOrderId",
                table: "work_orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VerificationNotes",
                table: "work_orders",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "VerifiedAt",
                table: "work_orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "VerifiedByUserId",
                table: "work_orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AutoApproveBreakdowns",
                table: "factory_settings",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "RequireWorkOrderApproval",
                table: "factory_settings",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "ReworkWindowDays",
                table: "factory_settings",
                type: "integer",
                nullable: false,
                defaultValue: 30);

            migrationBuilder.CreateTable(
                name: "failure_codes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AssetCategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_failure_codes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_failure_codes_asset_categories_AssetCategoryId",
                        column: x => x.AssetCategoryId,
                        principalTable: "asset_categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_failure_codes_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_work_orders_FailureCauseCodeId",
                table: "work_orders",
                column: "FailureCauseCodeId");

            migrationBuilder.CreateIndex(
                name: "IX_work_orders_FailureProblemCodeId",
                table: "work_orders",
                column: "FailureProblemCodeId");

            migrationBuilder.CreateIndex(
                name: "IX_work_orders_FailureRemedyCodeId",
                table: "work_orders",
                column: "FailureRemedyCodeId");

            migrationBuilder.CreateIndex(
                name: "IX_work_orders_OrgNodeId",
                table: "work_orders",
                column: "OrgNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_work_orders_ReworkOfWorkOrderId",
                table: "work_orders",
                column: "ReworkOfWorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_work_orders_TenantId_AssetId_FailureProblemCodeId_Completed~",
                table: "work_orders",
                columns: new[] { "TenantId", "AssetId", "FailureProblemCodeId", "CompletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_work_orders_TenantId_OrgNodeId_Status",
                table: "work_orders",
                columns: new[] { "TenantId", "OrgNodeId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_failure_codes_AssetCategoryId",
                table: "failure_codes",
                column: "AssetCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_failure_codes_TenantId_Kind_Code",
                table: "failure_codes",
                columns: new[] { "TenantId", "Kind", "Code" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_work_orders_failure_codes_FailureCauseCodeId",
                table: "work_orders",
                column: "FailureCauseCodeId",
                principalTable: "failure_codes",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_work_orders_failure_codes_FailureProblemCodeId",
                table: "work_orders",
                column: "FailureProblemCodeId",
                principalTable: "failure_codes",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_work_orders_failure_codes_FailureRemedyCodeId",
                table: "work_orders",
                column: "FailureRemedyCodeId",
                principalTable: "failure_codes",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_work_orders_org_nodes_OrgNodeId",
                table: "work_orders",
                column: "OrgNodeId",
                principalTable: "org_nodes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_work_orders_work_orders_ReworkOfWorkOrderId",
                table: "work_orders",
                column: "ReworkOfWorkOrderId",
                principalTable: "work_orders",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_work_orders_failure_codes_FailureCauseCodeId",
                table: "work_orders");

            migrationBuilder.DropForeignKey(
                name: "FK_work_orders_failure_codes_FailureProblemCodeId",
                table: "work_orders");

            migrationBuilder.DropForeignKey(
                name: "FK_work_orders_failure_codes_FailureRemedyCodeId",
                table: "work_orders");

            migrationBuilder.DropForeignKey(
                name: "FK_work_orders_org_nodes_OrgNodeId",
                table: "work_orders");

            migrationBuilder.DropForeignKey(
                name: "FK_work_orders_work_orders_ReworkOfWorkOrderId",
                table: "work_orders");

            migrationBuilder.DropTable(
                name: "failure_codes");

            migrationBuilder.DropIndex(
                name: "IX_work_orders_FailureCauseCodeId",
                table: "work_orders");

            migrationBuilder.DropIndex(
                name: "IX_work_orders_FailureProblemCodeId",
                table: "work_orders");

            migrationBuilder.DropIndex(
                name: "IX_work_orders_FailureRemedyCodeId",
                table: "work_orders");

            migrationBuilder.DropIndex(
                name: "IX_work_orders_OrgNodeId",
                table: "work_orders");

            migrationBuilder.DropIndex(
                name: "IX_work_orders_ReworkOfWorkOrderId",
                table: "work_orders");

            migrationBuilder.DropIndex(
                name: "IX_work_orders_TenantId_AssetId_FailureProblemCodeId_Completed~",
                table: "work_orders");

            migrationBuilder.DropIndex(
                name: "IX_work_orders_TenantId_OrgNodeId_Status",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "ApprovedByUserId",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "DowntimeEndedAt",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "DowntimeStartedAt",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "FailureCauseCodeId",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "FailureProblemCodeId",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "FailureRemedyCodeId",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "IsBreakdown",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "NeedsLineStop",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "OrgNodeId",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "PlannedAt",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "RequestSource",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "RequestedByName",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "ReworkOfWorkOrderId",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "VerificationNotes",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "VerifiedAt",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "VerifiedByUserId",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "AutoApproveBreakdowns",
                table: "factory_settings");

            migrationBuilder.DropColumn(
                name: "RequireWorkOrderApproval",
                table: "factory_settings");

            migrationBuilder.DropColumn(
                name: "ReworkWindowDays",
                table: "factory_settings");

            migrationBuilder.AlterColumn<Guid>(
                name: "ClientId",
                table: "work_orders",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
