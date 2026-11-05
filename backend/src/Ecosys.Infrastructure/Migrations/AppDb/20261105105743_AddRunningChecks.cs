using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class AddRunningChecks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FailAction",
                table: "pm_template_questions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FeedsMeterType",
                table: "pm_template_questions",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShiftsJson",
                table: "factory_settings",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "check_schedules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PmTemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Frequency = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    OrgNodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsRoute = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_check_schedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_check_schedules_org_nodes_OrgNodeId",
                        column: x => x.OrgNodeId,
                        principalTable: "org_nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_check_schedules_pm_templates_PmTemplateId",
                        column: x => x.PmTemplateId,
                        principalTable: "pm_templates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_check_schedules_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "check_runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CheckScheduleId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgNodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    PmTemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    TemplateVersion = table.Column<int>(type: "integer", nullable: false),
                    PeriodKey = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ShiftName = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    DueFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DueBy = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompletedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    FailedCount = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    SignatureDataUrl = table.Column<string>(type: "text", nullable: true),
                    MissedNotifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_check_runs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_check_runs_assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_check_runs_check_schedules_CheckScheduleId",
                        column: x => x.CheckScheduleId,
                        principalTable: "check_schedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_check_runs_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "check_schedule_assets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CheckScheduleId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_check_schedule_assets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_check_schedule_assets_assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_check_schedule_assets_check_schedules_CheckScheduleId",
                        column: x => x.CheckScheduleId,
                        principalTable: "check_schedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "check_run_answers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CheckRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestionId = table.Column<Guid>(type: "uuid", nullable: true),
                    SectionName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Prompt = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ResponseType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Unit = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    MinValue = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    MaxValue = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    Value = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    NumberValue = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    Passed = table.Column<bool>(type: "boolean", nullable: true),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    AttachmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    FailAction = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RaisedWorkOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_check_run_answers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_check_run_answers_check_runs_CheckRunId",
                        column: x => x.CheckRunId,
                        principalTable: "check_runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_check_run_answers_CheckRunId",
                table: "check_run_answers",
                column: "CheckRunId");

            migrationBuilder.CreateIndex(
                name: "IX_check_runs_AssetId",
                table: "check_runs",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_check_runs_CheckScheduleId_AssetId_PeriodKey",
                table: "check_runs",
                columns: new[] { "CheckScheduleId", "AssetId", "PeriodKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_check_runs_TenantId_OrgNodeId_DueFrom",
                table: "check_runs",
                columns: new[] { "TenantId", "OrgNodeId", "DueFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_check_runs_TenantId_Status_DueBy",
                table: "check_runs",
                columns: new[] { "TenantId", "Status", "DueBy" });

            migrationBuilder.CreateIndex(
                name: "IX_check_schedule_assets_AssetId",
                table: "check_schedule_assets",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_check_schedule_assets_CheckScheduleId_AssetId",
                table: "check_schedule_assets",
                columns: new[] { "CheckScheduleId", "AssetId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_check_schedules_OrgNodeId",
                table: "check_schedules",
                column: "OrgNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_check_schedules_PmTemplateId",
                table: "check_schedules",
                column: "PmTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_check_schedules_TenantId_OrgNodeId",
                table: "check_schedules",
                columns: new[] { "TenantId", "OrgNodeId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "check_run_answers");

            migrationBuilder.DropTable(
                name: "check_schedule_assets");

            migrationBuilder.DropTable(
                name: "check_runs");

            migrationBuilder.DropTable(
                name: "check_schedules");

            migrationBuilder.DropColumn(
                name: "FailAction",
                table: "pm_template_questions");

            migrationBuilder.DropColumn(
                name: "FeedsMeterType",
                table: "pm_template_questions");

            migrationBuilder.DropColumn(
                name: "ShiftsJson",
                table: "factory_settings");
        }
    }
}
