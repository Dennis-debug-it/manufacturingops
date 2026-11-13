using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class SecurityPatrol : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "incidents",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Safety");

            migrationBuilder.AddColumn<Guid>(
                name: "CheckpointId",
                table: "incidents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "incidents",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "incidents",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OwnerName",
                table: "incidents",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerUserId",
                table: "incidents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PatrolRunId",
                table: "incidents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PoliceObNumber",
                table: "incidents",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResponseStatus",
                table: "incidents",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "VisitId",
                table: "incidents",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "checkpoints",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    FactoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccessZoneId = table.Column<Guid>(type: "uuid", nullable: true),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Method = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    TagToken = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    NfcSerial = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Latitude = table.Column<double>(type: "double precision", nullable: true),
                    Longitude = table.Column<double>(type: "double precision", nullable: true),
                    RadiusM = table.Column<int>(type: "integer", nullable: false),
                    Instructions = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Questions = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_checkpoints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_checkpoints_access_zones_AccessZoneId",
                        column: x => x.AccessZoneId,
                        principalTable: "access_zones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_checkpoints_org_nodes_FactoryId",
                        column: x => x.FactoryId,
                        principalTable: "org_nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_checkpoints_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "patrol_routes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    FactoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Ordered = table.Column<bool>(type: "boolean", nullable: false),
                    MaxDurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    MinGapSeconds = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_patrol_routes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_patrol_routes_org_nodes_FactoryId",
                        column: x => x.FactoryId,
                        principalTable: "org_nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_patrol_routes_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "shift_handovers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    FactoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    ShiftName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    OutgoingUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    OutgoingName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IncomingUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    IncomingName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    HandedOverAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OpenIssues = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    KeysAndEquipment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    AcknowledgedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shift_handovers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_shift_handovers_org_nodes_FactoryId",
                        column: x => x.FactoryId,
                        principalTable: "org_nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_shift_handovers_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "patrol_route_checkpoints",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    RouteId = table.Column<Guid>(type: "uuid", nullable: false),
                    CheckpointId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    Required = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_patrol_route_checkpoints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_patrol_route_checkpoints_checkpoints_CheckpointId",
                        column: x => x.CheckpointId,
                        principalTable: "checkpoints",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_patrol_route_checkpoints_patrol_routes_RouteId",
                        column: x => x.RouteId,
                        principalTable: "patrol_routes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_patrol_route_checkpoints_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "patrol_schedules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    RouteId = table.Column<Guid>(type: "uuid", nullable: false),
                    EveryMinutes = table.Column<int>(type: "integer", nullable: true),
                    FixedTimes = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    WindowFrom = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    WindowTo = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    Days = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    GraceMinutes = table.Column<int>(type: "integer", nullable: false),
                    AssignedUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_patrol_schedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_patrol_schedules_patrol_routes_RouteId",
                        column: x => x.RouteId,
                        principalTable: "patrol_routes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_patrol_schedules_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_patrol_schedules_users_AssignedUserId",
                        column: x => x.AssignedUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "patrol_runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    FactoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    RouteId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScheduleId = table.Column<Guid>(type: "uuid", nullable: true),
                    DueFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DueBy = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CloseAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GuardUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    GuardName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FinishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RequiredCount = table.Column<int>(type: "integer", nullable: false),
                    ValidCount = table.Column<int>(type: "integer", nullable: false),
                    ScanCount = table.Column<int>(type: "integer", nullable: false),
                    FlaggedCount = table.Column<int>(type: "integer", nullable: false),
                    AlertSentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_patrol_runs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_patrol_runs_org_nodes_FactoryId",
                        column: x => x.FactoryId,
                        principalTable: "org_nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_patrol_runs_patrol_routes_RouteId",
                        column: x => x.RouteId,
                        principalTable: "patrol_routes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_patrol_runs_patrol_schedules_ScheduleId",
                        column: x => x.ScheduleId,
                        principalTable: "patrol_schedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_patrol_runs_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "checkpoint_scans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientScanId = table.Column<Guid>(type: "uuid", nullable: false),
                    RunId = table.Column<Guid>(type: "uuid", nullable: true),
                    CheckpointId = table.Column<Guid>(type: "uuid", nullable: false),
                    GuardUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    GuardName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DeviceId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Method = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    NfcSerial = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ScannedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Latitude = table.Column<double>(type: "double precision", nullable: true),
                    Longitude = table.Column<double>(type: "double precision", nullable: true),
                    AccuracyM = table.Column<double>(type: "double precision", nullable: true),
                    DistanceM = table.Column<double>(type: "double precision", nullable: true),
                    Validation = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ValidationNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AnswersJson = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_checkpoint_scans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_checkpoint_scans_checkpoints_CheckpointId",
                        column: x => x.CheckpointId,
                        principalTable: "checkpoints",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_checkpoint_scans_patrol_runs_RunId",
                        column: x => x.RunId,
                        principalTable: "patrol_runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_checkpoint_scans_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_incidents_CheckpointId",
                table: "incidents",
                column: "CheckpointId");

            migrationBuilder.CreateIndex(
                name: "IX_incidents_PatrolRunId",
                table: "incidents",
                column: "PatrolRunId");

            migrationBuilder.CreateIndex(
                name: "IX_incidents_TenantId_Category_Status",
                table: "incidents",
                columns: new[] { "TenantId", "Category", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_incidents_VisitId",
                table: "incidents",
                column: "VisitId");

            migrationBuilder.CreateIndex(
                name: "IX_checkpoint_scans_CheckpointId",
                table: "checkpoint_scans",
                column: "CheckpointId");

            migrationBuilder.CreateIndex(
                name: "IX_checkpoint_scans_RunId_ScannedAt",
                table: "checkpoint_scans",
                columns: new[] { "RunId", "ScannedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_checkpoint_scans_TenantId_CheckpointId_ScannedAt",
                table: "checkpoint_scans",
                columns: new[] { "TenantId", "CheckpointId", "ScannedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_checkpoint_scans_TenantId_ClientScanId",
                table: "checkpoint_scans",
                columns: new[] { "TenantId", "ClientScanId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_checkpoints_AccessZoneId",
                table: "checkpoints",
                column: "AccessZoneId");

            migrationBuilder.CreateIndex(
                name: "IX_checkpoints_FactoryId",
                table: "checkpoints",
                column: "FactoryId");

            migrationBuilder.CreateIndex(
                name: "IX_checkpoints_TagToken",
                table: "checkpoints",
                column: "TagToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_checkpoints_TenantId_Code",
                table: "checkpoints",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_checkpoints_TenantId_NfcSerial",
                table: "checkpoints",
                columns: new[] { "TenantId", "NfcSerial" },
                unique: true,
                filter: "\"NfcSerial\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_patrol_route_checkpoints_CheckpointId",
                table: "patrol_route_checkpoints",
                column: "CheckpointId");

            migrationBuilder.CreateIndex(
                name: "IX_patrol_route_checkpoints_RouteId_CheckpointId",
                table: "patrol_route_checkpoints",
                columns: new[] { "RouteId", "CheckpointId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_patrol_route_checkpoints_TenantId",
                table: "patrol_route_checkpoints",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_patrol_routes_FactoryId",
                table: "patrol_routes",
                column: "FactoryId");

            migrationBuilder.CreateIndex(
                name: "IX_patrol_routes_TenantId_FactoryId_Name",
                table: "patrol_routes",
                columns: new[] { "TenantId", "FactoryId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_patrol_runs_FactoryId",
                table: "patrol_runs",
                column: "FactoryId");

            migrationBuilder.CreateIndex(
                name: "IX_patrol_runs_RouteId",
                table: "patrol_runs",
                column: "RouteId");

            migrationBuilder.CreateIndex(
                name: "IX_patrol_runs_ScheduleId_DueFrom",
                table: "patrol_runs",
                columns: new[] { "ScheduleId", "DueFrom" },
                unique: true,
                filter: "\"ScheduleId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_patrol_runs_TenantId_FactoryId_DueFrom",
                table: "patrol_runs",
                columns: new[] { "TenantId", "FactoryId", "DueFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_patrol_runs_TenantId_Status_CloseAt",
                table: "patrol_runs",
                columns: new[] { "TenantId", "Status", "CloseAt" });

            migrationBuilder.CreateIndex(
                name: "IX_patrol_schedules_AssignedUserId",
                table: "patrol_schedules",
                column: "AssignedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_patrol_schedules_RouteId",
                table: "patrol_schedules",
                column: "RouteId");

            migrationBuilder.CreateIndex(
                name: "IX_patrol_schedules_TenantId",
                table: "patrol_schedules",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_shift_handovers_FactoryId",
                table: "shift_handovers",
                column: "FactoryId");

            migrationBuilder.CreateIndex(
                name: "IX_shift_handovers_TenantId_FactoryId_HandedOverAt",
                table: "shift_handovers",
                columns: new[] { "TenantId", "FactoryId", "HandedOverAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_incidents_checkpoints_CheckpointId",
                table: "incidents",
                column: "CheckpointId",
                principalTable: "checkpoints",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_incidents_patrol_runs_PatrolRunId",
                table: "incidents",
                column: "PatrolRunId",
                principalTable: "patrol_runs",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_incidents_visits_VisitId",
                table: "incidents",
                column: "VisitId",
                principalTable: "visits",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_incidents_checkpoints_CheckpointId",
                table: "incidents");

            migrationBuilder.DropForeignKey(
                name: "FK_incidents_patrol_runs_PatrolRunId",
                table: "incidents");

            migrationBuilder.DropForeignKey(
                name: "FK_incidents_visits_VisitId",
                table: "incidents");

            migrationBuilder.DropTable(
                name: "checkpoint_scans");

            migrationBuilder.DropTable(
                name: "patrol_route_checkpoints");

            migrationBuilder.DropTable(
                name: "shift_handovers");

            migrationBuilder.DropTable(
                name: "patrol_runs");

            migrationBuilder.DropTable(
                name: "checkpoints");

            migrationBuilder.DropTable(
                name: "patrol_schedules");

            migrationBuilder.DropTable(
                name: "patrol_routes");

            migrationBuilder.DropIndex(
                name: "IX_incidents_CheckpointId",
                table: "incidents");

            migrationBuilder.DropIndex(
                name: "IX_incidents_PatrolRunId",
                table: "incidents");

            migrationBuilder.DropIndex(
                name: "IX_incidents_TenantId_Category_Status",
                table: "incidents");

            migrationBuilder.DropIndex(
                name: "IX_incidents_VisitId",
                table: "incidents");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "incidents");

            migrationBuilder.DropColumn(
                name: "CheckpointId",
                table: "incidents");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "incidents");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "incidents");

            migrationBuilder.DropColumn(
                name: "OwnerName",
                table: "incidents");

            migrationBuilder.DropColumn(
                name: "OwnerUserId",
                table: "incidents");

            migrationBuilder.DropColumn(
                name: "PatrolRunId",
                table: "incidents");

            migrationBuilder.DropColumn(
                name: "PoliceObNumber",
                table: "incidents");

            migrationBuilder.DropColumn(
                name: "ResponseStatus",
                table: "incidents");

            migrationBuilder.DropColumn(
                name: "VisitId",
                table: "incidents");
        }
    }
}
