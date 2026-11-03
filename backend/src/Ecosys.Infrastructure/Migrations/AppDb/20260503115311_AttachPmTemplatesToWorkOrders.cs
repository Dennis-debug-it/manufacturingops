using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class AttachPmTemplatesToWorkOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PmTemplateId",
                table: "work_orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PreventiveMaintenancePlanId",
                table: "work_orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "InviteAcceptedAt",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "InviteTokenExpiresAt",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InviteTokenHash",
                table: "users",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastCredentialSentAt",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "MustChangePassword",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SectionName",
                table: "pm_template_questions",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "work_order_checklist_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    PmTemplateQuestionId = table.Column<Guid>(type: "uuid", nullable: true),
                    SectionName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    QuestionText = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    InputType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    ResponseValue = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Remarks = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    IsCompleted = table.Column<bool>(type: "boolean", nullable: false),
                    CompletedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    OptionsJson = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_work_order_checklist_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_work_order_checklist_items_pm_template_questions_PmTemplate~",
                        column: x => x.PmTemplateQuestionId,
                        principalTable: "pm_template_questions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_work_order_checklist_items_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_work_order_checklist_items_users_CompletedByUserId",
                        column: x => x.CompletedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_work_order_checklist_items_work_orders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "work_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_work_orders_PmTemplateId",
                table: "work_orders",
                column: "PmTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_work_orders_PreventiveMaintenancePlanId",
                table: "work_orders",
                column: "PreventiveMaintenancePlanId");

            migrationBuilder.CreateIndex(
                name: "IX_work_order_checklist_items_CompletedByUserId",
                table: "work_order_checklist_items",
                column: "CompletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_work_order_checklist_items_PmTemplateQuestionId",
                table: "work_order_checklist_items",
                column: "PmTemplateQuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_work_order_checklist_items_TenantId_WorkOrderId_SortOrder",
                table: "work_order_checklist_items",
                columns: new[] { "TenantId", "WorkOrderId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_work_order_checklist_items_WorkOrderId",
                table: "work_order_checklist_items",
                column: "WorkOrderId");

            migrationBuilder.AddForeignKey(
                name: "FK_work_orders_pm_templates_PmTemplateId",
                table: "work_orders",
                column: "PmTemplateId",
                principalTable: "pm_templates",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_work_orders_preventive_maintenance_plans_PreventiveMaintena~",
                table: "work_orders",
                column: "PreventiveMaintenancePlanId",
                principalTable: "preventive_maintenance_plans",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_work_orders_pm_templates_PmTemplateId",
                table: "work_orders");

            migrationBuilder.DropForeignKey(
                name: "FK_work_orders_preventive_maintenance_plans_PreventiveMaintena~",
                table: "work_orders");

            migrationBuilder.DropTable(
                name: "work_order_checklist_items");

            migrationBuilder.DropIndex(
                name: "IX_work_orders_PmTemplateId",
                table: "work_orders");

            migrationBuilder.DropIndex(
                name: "IX_work_orders_PreventiveMaintenancePlanId",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "PmTemplateId",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "PreventiveMaintenancePlanId",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "InviteAcceptedAt",
                table: "users");

            migrationBuilder.DropColumn(
                name: "InviteTokenExpiresAt",
                table: "users");

            migrationBuilder.DropColumn(
                name: "InviteTokenHash",
                table: "users");

            migrationBuilder.DropColumn(
                name: "LastCredentialSentAt",
                table: "users");

            migrationBuilder.DropColumn(
                name: "MustChangePassword",
                table: "users");

            migrationBuilder.DropColumn(
                name: "SectionName",
                table: "pm_template_questions");
        }
    }
}
