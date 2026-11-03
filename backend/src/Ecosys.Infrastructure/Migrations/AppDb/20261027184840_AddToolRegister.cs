using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class AddToolRegister : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tool_assignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ToolId = table.Column<Guid>(type: "uuid", nullable: false),
                    TechnicianId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    CheckedOutAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CheckedOutByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpectedReturnAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ConditionOut = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    AcknowledgedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReturnedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReturnedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ConditionIn = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    ReturnNotes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tool_assignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tool_assignments_technicians_TechnicianId",
                        column: x => x.TechnicianId,
                        principalTable: "technicians",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tool_assignments_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_tool_assignments_users_CheckedOutByUserId",
                        column: x => x.CheckedOutByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tool_assignments_users_ReturnedByUserId",
                        column: x => x.ReturnedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_tool_assignments_work_orders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "work_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "tools",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ToolCode = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SerialNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Manufacturer = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PurchaseDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PurchaseCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    HomeBranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Condition = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CalibrationIntervalDays = table.Column<int>(type: "integer", nullable: true),
                    LastCalibratedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NextCalibrationDue = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CurrentAssignmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tools", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tools_branches_HomeBranchId",
                        column: x => x.HomeBranchId,
                        principalTable: "branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_tools_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_tools_tool_assignments_CurrentAssignmentId",
                        column: x => x.CurrentAssignmentId,
                        principalTable: "tool_assignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "tool_calibrations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ToolId = table.Column<Guid>(type: "uuid", nullable: false),
                    CalibratedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NextDueOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CertificateNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PerformedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Result = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    RecordedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tool_calibrations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tool_calibrations_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_tool_calibrations_tools_ToolId",
                        column: x => x.ToolId,
                        principalTable: "tools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tool_assignments_CheckedOutByUserId",
                table: "tool_assignments",
                column: "CheckedOutByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_tool_assignments_ReturnedByUserId",
                table: "tool_assignments",
                column: "ReturnedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_tool_assignments_TechnicianId",
                table: "tool_assignments",
                column: "TechnicianId");

            migrationBuilder.CreateIndex(
                name: "IX_tool_assignments_TenantId_TechnicianId_ReturnedAt",
                table: "tool_assignments",
                columns: new[] { "TenantId", "TechnicianId", "ReturnedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_tool_assignments_ToolId",
                table: "tool_assignments",
                column: "ToolId");

            migrationBuilder.CreateIndex(
                name: "IX_tool_assignments_WorkOrderId",
                table: "tool_assignments",
                column: "WorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_tool_calibrations_TenantId",
                table: "tool_calibrations",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_tool_calibrations_ToolId",
                table: "tool_calibrations",
                column: "ToolId");

            migrationBuilder.CreateIndex(
                name: "IX_tools_CurrentAssignmentId",
                table: "tools",
                column: "CurrentAssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_tools_HomeBranchId",
                table: "tools",
                column: "HomeBranchId");

            migrationBuilder.CreateIndex(
                name: "IX_tools_TenantId_Status",
                table: "tools",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_tools_TenantId_ToolCode",
                table: "tools",
                columns: new[] { "TenantId", "ToolCode" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_tool_assignments_tools_ToolId",
                table: "tool_assignments",
                column: "ToolId",
                principalTable: "tools",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_tool_assignments_tools_ToolId",
                table: "tool_assignments");

            migrationBuilder.DropTable(
                name: "tool_calibrations");

            migrationBuilder.DropTable(
                name: "tools");

            migrationBuilder.DropTable(
                name: "tool_assignments");
        }
    }
}
