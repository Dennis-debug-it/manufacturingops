using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class AddTenantSmtpVisitsAndPartsReturns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AutoSendServiceReportOnApproval",
                table: "tenant_operations_policies",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "CustomFromAddress",
                table: "email_settings",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "UseCustomSmtp",
                table: "email_settings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "parts_returns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    WorkOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReturnNumber = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcessedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_parts_returns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_parts_returns_branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_parts_returns_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_parts_returns_users_ProcessedByUserId",
                        column: x => x.ProcessedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_parts_returns_users_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_parts_returns_work_orders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "work_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "work_order_visits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    TechnicianId = table.Column<Guid>(type: "uuid", nullable: false),
                    TravelStartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ArrivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DepartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TravelMinutes = table.Column<int>(type: "integer", nullable: false),
                    OnSiteMinutes = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_work_order_visits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_work_order_visits_technicians_TechnicianId",
                        column: x => x.TechnicianId,
                        principalTable: "technicians",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_work_order_visits_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_work_order_visits_work_orders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "work_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "parts_return_lines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PartsReturnId = table.Column<Guid>(type: "uuid", nullable: false),
                    MaterialItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkOrderMaterialUsageId = table.Column<Guid>(type: "uuid", nullable: true),
                    Quantity = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Condition = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_parts_return_lines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_parts_return_lines_material_items_MaterialItemId",
                        column: x => x.MaterialItemId,
                        principalTable: "material_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_parts_return_lines_parts_returns_PartsReturnId",
                        column: x => x.PartsReturnId,
                        principalTable: "parts_returns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_parts_return_lines_MaterialItemId",
                table: "parts_return_lines",
                column: "MaterialItemId");

            migrationBuilder.CreateIndex(
                name: "IX_parts_return_lines_PartsReturnId",
                table: "parts_return_lines",
                column: "PartsReturnId");

            migrationBuilder.CreateIndex(
                name: "IX_parts_returns_BranchId",
                table: "parts_returns",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_parts_returns_ProcessedByUserId",
                table: "parts_returns",
                column: "ProcessedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_parts_returns_RequestedByUserId",
                table: "parts_returns",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_parts_returns_TenantId_ReturnNumber",
                table: "parts_returns",
                columns: new[] { "TenantId", "ReturnNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_parts_returns_TenantId_Status",
                table: "parts_returns",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_parts_returns_TenantId_WorkOrderId",
                table: "parts_returns",
                columns: new[] { "TenantId", "WorkOrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_parts_returns_WorkOrderId",
                table: "parts_returns",
                column: "WorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_work_order_visits_TechnicianId",
                table: "work_order_visits",
                column: "TechnicianId");

            migrationBuilder.CreateIndex(
                name: "IX_work_order_visits_TenantId_WorkOrderId_TechnicianId",
                table: "work_order_visits",
                columns: new[] { "TenantId", "WorkOrderId", "TechnicianId" });

            migrationBuilder.CreateIndex(
                name: "IX_work_order_visits_WorkOrderId",
                table: "work_order_visits",
                column: "WorkOrderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "parts_return_lines");

            migrationBuilder.DropTable(
                name: "work_order_visits");

            migrationBuilder.DropTable(
                name: "parts_returns");

            migrationBuilder.DropColumn(
                name: "AutoSendServiceReportOnApproval",
                table: "tenant_operations_policies");

            migrationBuilder.DropColumn(
                name: "CustomFromAddress",
                table: "email_settings");

            migrationBuilder.DropColumn(
                name: "UseCustomSmtp",
                table: "email_settings");
        }
    }
}
