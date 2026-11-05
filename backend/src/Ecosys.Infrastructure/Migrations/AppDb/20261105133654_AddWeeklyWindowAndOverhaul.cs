using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class AddWeeklyWindowAndOverhaul : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "WindowCarryoverCount",
                table: "work_orders",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "WindowDay",
                table: "org_nodes",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "WindowEnabled",
                table: "org_nodes",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WindowCarryoverEscalateAt",
                table: "factory_settings",
                type: "integer",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.AddColumn<int>(
                name: "WindowCarryoverToOverhaulAt",
                table: "factory_settings",
                type: "integer",
                nullable: false,
                defaultValue: 3);

            migrationBuilder.CreateTable(
                name: "maintenance_windows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    FactoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    WindowDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StartsAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndsAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CapacityHours = table.Column<decimal>(type: "numeric(10,1)", precision: 10, scale: 1, nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    MovedFromDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SkipReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    PackedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PackedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    HandedBackAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    HandedBackByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    HandbackNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    HandbackSignatureDataUrl = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_maintenance_windows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_maintenance_windows_org_nodes_FactoryId",
                        column: x => x.FactoryId,
                        principalTable: "org_nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_maintenance_windows_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "overhaul_plans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    FactoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PlannedStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PlannedEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Budget = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ActualStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ActualEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_overhaul_plans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_overhaul_plans_org_nodes_FactoryId",
                        column: x => x.FactoryId,
                        principalTable: "org_nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_overhaul_plans_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "maintenance_window_handback_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    WindowId = table.Column<Guid>(type: "uuid", nullable: false),
                    Prompt = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    Done = table.Column<bool>(type: "boolean", nullable: false),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DoneByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DoneAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_maintenance_window_handback_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_maintenance_window_handback_items_maintenance_windows_Windo~",
                        column: x => x.WindowId,
                        principalTable: "maintenance_windows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "maintenance_window_jobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    WindowId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    AddedAutomatically = table.Column<bool>(type: "boolean", nullable: false),
                    Outcome = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_maintenance_window_jobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_maintenance_window_jobs_maintenance_windows_WindowId",
                        column: x => x.WindowId,
                        principalTable: "maintenance_windows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_maintenance_window_jobs_work_orders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "work_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "overhaul_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    FactoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    OverhaulPlanId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssetId = table.Column<Guid>(type: "uuid", nullable: true),
                    WorkOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Priority = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    EstimatedHours = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    EstimatedCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    ActualCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    SparesNote = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ContractorName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ProgressPercent = table.Column<int>(type: "integer", nullable: false),
                    NeedsRecommissioning = table.Column<bool>(type: "boolean", nullable: false),
                    RecommissionedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RecommissionedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RecommissioningNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    AddedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_overhaul_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_overhaul_items_assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_overhaul_items_org_nodes_FactoryId",
                        column: x => x.FactoryId,
                        principalTable: "org_nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_overhaul_items_overhaul_plans_OverhaulPlanId",
                        column: x => x.OverhaulPlanId,
                        principalTable: "overhaul_plans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_overhaul_items_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_overhaul_items_work_orders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "work_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_maintenance_window_handback_items_WindowId",
                table: "maintenance_window_handback_items",
                column: "WindowId");

            migrationBuilder.CreateIndex(
                name: "IX_maintenance_window_jobs_TenantId_WorkOrderId",
                table: "maintenance_window_jobs",
                columns: new[] { "TenantId", "WorkOrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_maintenance_window_jobs_WindowId_WorkOrderId",
                table: "maintenance_window_jobs",
                columns: new[] { "WindowId", "WorkOrderId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_maintenance_window_jobs_WorkOrderId",
                table: "maintenance_window_jobs",
                column: "WorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_maintenance_windows_FactoryId",
                table: "maintenance_windows",
                column: "FactoryId");

            migrationBuilder.CreateIndex(
                name: "IX_maintenance_windows_TenantId_FactoryId_WindowDate",
                table: "maintenance_windows",
                columns: new[] { "TenantId", "FactoryId", "WindowDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_maintenance_windows_TenantId_Status",
                table: "maintenance_windows",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_overhaul_items_AssetId",
                table: "overhaul_items",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_overhaul_items_FactoryId",
                table: "overhaul_items",
                column: "FactoryId");

            migrationBuilder.CreateIndex(
                name: "IX_overhaul_items_OverhaulPlanId",
                table: "overhaul_items",
                column: "OverhaulPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_overhaul_items_TenantId_FactoryId_Status",
                table: "overhaul_items",
                columns: new[] { "TenantId", "FactoryId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_overhaul_items_TenantId_WorkOrderId",
                table: "overhaul_items",
                columns: new[] { "TenantId", "WorkOrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_overhaul_items_WorkOrderId",
                table: "overhaul_items",
                column: "WorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_overhaul_plans_FactoryId",
                table: "overhaul_plans",
                column: "FactoryId");

            migrationBuilder.CreateIndex(
                name: "IX_overhaul_plans_TenantId_FactoryId_PlannedStart",
                table: "overhaul_plans",
                columns: new[] { "TenantId", "FactoryId", "PlannedStart" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "maintenance_window_handback_items");

            migrationBuilder.DropTable(
                name: "maintenance_window_jobs");

            migrationBuilder.DropTable(
                name: "overhaul_items");

            migrationBuilder.DropTable(
                name: "maintenance_windows");

            migrationBuilder.DropTable(
                name: "overhaul_plans");

            migrationBuilder.DropColumn(
                name: "WindowCarryoverCount",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "WindowDay",
                table: "org_nodes");

            migrationBuilder.DropColumn(
                name: "WindowEnabled",
                table: "org_nodes");

            migrationBuilder.DropColumn(
                name: "WindowCarryoverEscalateAt",
                table: "factory_settings");

            migrationBuilder.DropColumn(
                name: "WindowCarryoverToOverhaulAt",
                table: "factory_settings");
        }
    }
}
