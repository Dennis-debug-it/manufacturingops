using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class ConditionMonitoring : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AbnormalStreak",
                table: "meters",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "AlarmJobPlanId",
                table: "meters",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AlarmPriority",
                table: "meters",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "High");

            migrationBuilder.AddColumn<string>(
                name: "ConditionAction",
                table: "meters",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Alert");

            migrationBuilder.AddColumn<DateTime>(
                name: "ConditionSince",
                table: "meters",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConditionState",
                table: "meters",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Normal");

            migrationBuilder.AddColumn<int>(
                name: "TriggerAfterReadings",
                table: "meters",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<decimal>(
                name: "WarningMax",
                table: "meters",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "WarningMin",
                table: "meters",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Condition",
                table: "meter_readings",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Normal");

            migrationBuilder.CreateTable(
                name: "condition_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    MeterId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Level = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Direction = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastReadingAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ClearedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PeakValue = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    ReadingCount = table.Column<int>(type: "integer", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcknowledgedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AcknowledgedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcknowledgedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AcknowledgementNote = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_condition_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_condition_events_assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_condition_events_meters_MeterId",
                        column: x => x.MeterId,
                        principalTable: "meters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_condition_events_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_condition_events_work_orders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "work_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_meters_AlarmJobPlanId",
                table: "meters",
                column: "AlarmJobPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_meters_TenantId_ConditionState",
                table: "meters",
                columns: new[] { "TenantId", "ConditionState" });

            migrationBuilder.CreateIndex(
                name: "IX_condition_events_AssetId",
                table: "condition_events",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_condition_events_MeterId",
                table: "condition_events",
                column: "MeterId",
                unique: true,
                filter: "\"Status\" = 'Open'");

            migrationBuilder.CreateIndex(
                name: "IX_condition_events_TenantId_MeterId_StartedAt",
                table: "condition_events",
                columns: new[] { "TenantId", "MeterId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_condition_events_TenantId_Status_StartedAt",
                table: "condition_events",
                columns: new[] { "TenantId", "Status", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_condition_events_WorkOrderId",
                table: "condition_events",
                column: "WorkOrderId");

            migrationBuilder.AddForeignKey(
                name: "FK_meters_job_plans_AlarmJobPlanId",
                table: "meters",
                column: "AlarmJobPlanId",
                principalTable: "job_plans",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_meters_job_plans_AlarmJobPlanId",
                table: "meters");

            migrationBuilder.DropTable(
                name: "condition_events");

            migrationBuilder.DropIndex(
                name: "IX_meters_AlarmJobPlanId",
                table: "meters");

            migrationBuilder.DropIndex(
                name: "IX_meters_TenantId_ConditionState",
                table: "meters");

            migrationBuilder.DropColumn(
                name: "AbnormalStreak",
                table: "meters");

            migrationBuilder.DropColumn(
                name: "AlarmJobPlanId",
                table: "meters");

            migrationBuilder.DropColumn(
                name: "AlarmPriority",
                table: "meters");

            migrationBuilder.DropColumn(
                name: "ConditionAction",
                table: "meters");

            migrationBuilder.DropColumn(
                name: "ConditionSince",
                table: "meters");

            migrationBuilder.DropColumn(
                name: "ConditionState",
                table: "meters");

            migrationBuilder.DropColumn(
                name: "TriggerAfterReadings",
                table: "meters");

            migrationBuilder.DropColumn(
                name: "WarningMax",
                table: "meters");

            migrationBuilder.DropColumn(
                name: "WarningMin",
                table: "meters");

            migrationBuilder.DropColumn(
                name: "Condition",
                table: "meter_readings");
        }
    }
}
