using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class AddTimeAttendance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "attendance_entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    TechnicianId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ClockInAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ClockOutAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    BreakMinutes = table.Column<int>(type: "integer", nullable: false),
                    BreakStartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false, defaultValue: "ClockedIn"),
                    LastLocation = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CurrentWorkOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_attendance_entries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_attendance_entries_technicians_TechnicianId",
                        column: x => x.TechnicianId,
                        principalTable: "technicians",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_attendance_entries_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_attendance_entries_work_orders_CurrentWorkOrderId",
                        column: x => x.CurrentWorkOrderId,
                        principalTable: "work_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "leave_requests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    TechnicianId = table.Column<Guid>(type: "uuid", nullable: false),
                    LeaveTypeCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    TotalDays = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "Pending"),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DecisionNote = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DecidedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_leave_requests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_leave_requests_technicians_TechnicianId",
                        column: x => x.TechnicianId,
                        principalTable: "technicians",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_leave_requests_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_leave_requests_users_DecidedByUserId",
                        column: x => x.DecidedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "leave_types",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    IsPaid = table.Column<bool>(type: "boolean", nullable: false),
                    AnnualAllowanceDays = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_leave_types", x => x.Id);
                    table.ForeignKey(
                        name: "FK_leave_types_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "time_attendance_settings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoundingMinutes = table.Column<int>(type: "integer", nullable: false),
                    OvertimeThresholdHoursPerDay = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    GraceMinutes = table.Column<int>(type: "integer", nullable: false),
                    AutoClockOutAfterHours = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_time_attendance_settings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_time_attendance_settings_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_attendance_entries_CurrentWorkOrderId",
                table: "attendance_entries",
                column: "CurrentWorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_attendance_entries_TechnicianId",
                table: "attendance_entries",
                column: "TechnicianId");

            migrationBuilder.CreateIndex(
                name: "IX_attendance_entries_TenantId_Status",
                table: "attendance_entries",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_attendance_entries_TenantId_TechnicianId_WorkDate",
                table: "attendance_entries",
                columns: new[] { "TenantId", "TechnicianId", "WorkDate" });

            migrationBuilder.CreateIndex(
                name: "IX_leave_requests_DecidedByUserId",
                table: "leave_requests",
                column: "DecidedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_leave_requests_TechnicianId",
                table: "leave_requests",
                column: "TechnicianId");

            migrationBuilder.CreateIndex(
                name: "IX_leave_requests_TenantId_Status",
                table: "leave_requests",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_leave_requests_TenantId_TechnicianId",
                table: "leave_requests",
                columns: new[] { "TenantId", "TechnicianId" });

            migrationBuilder.CreateIndex(
                name: "IX_leave_types_TenantId_Code",
                table: "leave_types",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_time_attendance_settings_TenantId",
                table: "time_attendance_settings",
                column: "TenantId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "attendance_entries");

            migrationBuilder.DropTable(
                name: "leave_requests");

            migrationBuilder.DropTable(
                name: "leave_types");

            migrationBuilder.DropTable(
                name: "time_attendance_settings");
        }
    }
}
