using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class AddFactoryPm : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_preventive_maintenance_plans_TenantId",
                table: "preventive_maintenance_plans");

            migrationBuilder.AddColumn<string>(
                name: "Basis",
                table: "preventive_maintenance_plans",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeferReason",
                table: "preventive_maintenance_plans",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "DeferRisk",
                table: "preventive_maintenance_plans",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeferredAt",
                table: "preventive_maintenance_plans",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "EstimatedHours",
                table: "preventive_maintenance_plans",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsRoute",
                table: "preventive_maintenance_plans",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "LastMeterValue",
                table: "preventive_maintenance_plans",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "MeterId",
                table: "preventive_maintenance_plans",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "preventive_maintenance_plans",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "NeedsLineStop",
                table: "preventive_maintenance_plans",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "NextMeterDue",
                table: "preventive_maintenance_plans",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OrgNodeId",
                table: "preventive_maintenance_plans",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Priority",
                table: "preventive_maintenance_plans",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Medium");

            migrationBuilder.AddColumn<string>(
                name: "ScheduleMode",
                table: "preventive_maintenance_plans",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Floating");

            migrationBuilder.AddColumn<int>(
                name: "TolerancePercent",
                table: "preventive_maintenance_plans",
                type: "integer",
                nullable: false,
                defaultValue: 10);

            migrationBuilder.AddColumn<decimal>(
                name: "PmHoursPerTechnicianWeek",
                table: "factory_settings",
                type: "numeric(6,1)",
                precision: 6,
                scale: 1,
                nullable: false,
                defaultValue: 30m);

            migrationBuilder.CreateTable(
                name: "pm_plan_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DueMeterValue = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OnTime = table.Column<bool>(type: "boolean", nullable: true),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ActorName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pm_plan_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_pm_plan_events_preventive_maintenance_plans_PlanId",
                        column: x => x.PlanId,
                        principalTable: "preventive_maintenance_plans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_pm_plan_events_work_orders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "work_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "pm_plan_route_assets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pm_plan_route_assets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_pm_plan_route_assets_assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_pm_plan_route_assets_preventive_maintenance_plans_PlanId",
                        column: x => x.PlanId,
                        principalTable: "preventive_maintenance_plans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_preventive_maintenance_plans_MeterId",
                table: "preventive_maintenance_plans",
                column: "MeterId");

            migrationBuilder.CreateIndex(
                name: "IX_preventive_maintenance_plans_OrgNodeId",
                table: "preventive_maintenance_plans",
                column: "OrgNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_preventive_maintenance_plans_TenantId_OrgNodeId_Status",
                table: "preventive_maintenance_plans",
                columns: new[] { "TenantId", "OrgNodeId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_pm_plan_events_PlanId",
                table: "pm_plan_events",
                column: "PlanId");

            migrationBuilder.CreateIndex(
                name: "IX_pm_plan_events_TenantId_Kind_OccurredAt",
                table: "pm_plan_events",
                columns: new[] { "TenantId", "Kind", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_pm_plan_events_TenantId_PlanId_OccurredAt",
                table: "pm_plan_events",
                columns: new[] { "TenantId", "PlanId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_pm_plan_events_WorkOrderId",
                table: "pm_plan_events",
                column: "WorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_pm_plan_route_assets_AssetId",
                table: "pm_plan_route_assets",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_pm_plan_route_assets_PlanId_AssetId",
                table: "pm_plan_route_assets",
                columns: new[] { "PlanId", "AssetId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_preventive_maintenance_plans_meters_MeterId",
                table: "preventive_maintenance_plans",
                column: "MeterId",
                principalTable: "meters",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_preventive_maintenance_plans_org_nodes_OrgNodeId",
                table: "preventive_maintenance_plans",
                column: "OrgNodeId",
                principalTable: "org_nodes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_preventive_maintenance_plans_meters_MeterId",
                table: "preventive_maintenance_plans");

            migrationBuilder.DropForeignKey(
                name: "FK_preventive_maintenance_plans_org_nodes_OrgNodeId",
                table: "preventive_maintenance_plans");

            migrationBuilder.DropTable(
                name: "pm_plan_events");

            migrationBuilder.DropTable(
                name: "pm_plan_route_assets");

            migrationBuilder.DropIndex(
                name: "IX_preventive_maintenance_plans_MeterId",
                table: "preventive_maintenance_plans");

            migrationBuilder.DropIndex(
                name: "IX_preventive_maintenance_plans_OrgNodeId",
                table: "preventive_maintenance_plans");

            migrationBuilder.DropIndex(
                name: "IX_preventive_maintenance_plans_TenantId_OrgNodeId_Status",
                table: "preventive_maintenance_plans");

            migrationBuilder.DropColumn(
                name: "Basis",
                table: "preventive_maintenance_plans");

            migrationBuilder.DropColumn(
                name: "DeferReason",
                table: "preventive_maintenance_plans");

            migrationBuilder.DropColumn(
                name: "DeferRisk",
                table: "preventive_maintenance_plans");

            migrationBuilder.DropColumn(
                name: "DeferredAt",
                table: "preventive_maintenance_plans");

            migrationBuilder.DropColumn(
                name: "EstimatedHours",
                table: "preventive_maintenance_plans");

            migrationBuilder.DropColumn(
                name: "IsRoute",
                table: "preventive_maintenance_plans");

            migrationBuilder.DropColumn(
                name: "LastMeterValue",
                table: "preventive_maintenance_plans");

            migrationBuilder.DropColumn(
                name: "MeterId",
                table: "preventive_maintenance_plans");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "preventive_maintenance_plans");

            migrationBuilder.DropColumn(
                name: "NeedsLineStop",
                table: "preventive_maintenance_plans");

            migrationBuilder.DropColumn(
                name: "NextMeterDue",
                table: "preventive_maintenance_plans");

            migrationBuilder.DropColumn(
                name: "OrgNodeId",
                table: "preventive_maintenance_plans");

            migrationBuilder.DropColumn(
                name: "Priority",
                table: "preventive_maintenance_plans");

            migrationBuilder.DropColumn(
                name: "ScheduleMode",
                table: "preventive_maintenance_plans");

            migrationBuilder.DropColumn(
                name: "TolerancePercent",
                table: "preventive_maintenance_plans");

            migrationBuilder.DropColumn(
                name: "PmHoursPerTechnicianWeek",
                table: "factory_settings");

            migrationBuilder.CreateIndex(
                name: "IX_preventive_maintenance_plans_TenantId",
                table: "preventive_maintenance_plans",
                column: "TenantId");
        }
    }
}
