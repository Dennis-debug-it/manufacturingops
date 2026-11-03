using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class BranchesAndSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_numbering_settings_TenantId",
                table: "numbering_settings");

            migrationBuilder.DropIndex(
                name: "IX_material_requests_TenantId",
                table: "material_requests");

            migrationBuilder.DropColumn(
                name: "LastSequenceYear",
                table: "numbering_settings");

            migrationBuilder.RenameColumn(
                name: "WorkOrderPrefix",
                table: "numbering_settings",
                newName: "ResetFrequency");

            migrationBuilder.RenameColumn(
                name: "NextSequence",
                table: "numbering_settings",
                newName: "PaddingLength");

            migrationBuilder.AddColumn<Guid>(
                name: "BranchId",
                table: "work_orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DefaultBranchId",
                table: "users",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasAllBranchAccess",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "BranchId",
                table: "technicians",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BranchId",
                table: "stock_movements",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BranchId",
                table: "preventive_maintenance_plans",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BranchId",
                table: "numbering_settings",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DocumentType",
                table: "numbering_settings",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IncludeMonth",
                table: "numbering_settings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IncludeYear",
                table: "numbering_settings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "numbering_settings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastResetAt",
                table: "numbering_settings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "NextNumber",
                table: "numbering_settings",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "Prefix",
                table: "numbering_settings",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "BranchId",
                table: "material_requests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestNumber",
                table: "material_requests",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "BranchId",
                table: "assets",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "branches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Location = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    Address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ContactPerson = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_branches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_branches_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_sessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    JwtId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    LoginAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LogoutAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsRevoked = table.Column<bool>(type: "boolean", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_sessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_sessions_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_sessions_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "branch_material_stocks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    MaterialId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuantityOnHand = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ReorderLevel = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    UnitCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_branch_material_stocks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_branch_material_stocks_branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_branch_material_stocks_material_items_MaterialId",
                        column: x => x.MaterialId,
                        principalTable: "material_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_branch_material_stocks_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "stock_transfers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromBranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    ToBranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    MaterialId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompletedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ReferenceNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_transfers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_stock_transfers_branches_FromBranchId",
                        column: x => x.FromBranchId,
                        principalTable: "branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_transfers_branches_ToBranchId",
                        column: x => x.ToBranchId,
                        principalTable: "branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_transfers_material_items_MaterialId",
                        column: x => x.MaterialId,
                        principalTable: "material_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_transfers_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_stock_transfers_users_ApprovedByUserId",
                        column: x => x.ApprovedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_stock_transfers_users_CompletedByUserId",
                        column: x => x.CompletedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_stock_transfers_users_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "user_branch_assignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_branch_assignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_branch_assignments_branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_branch_assignments_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_branch_assignments_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql("""
                UPDATE numbering_settings
                SET "DocumentType" = 'WorkOrder',
                    "Prefix" = "ResetFrequency",
                    "NextNumber" = "PaddingLength",
                    "PaddingLength" = 6,
                    "ResetFrequency" = 'Yearly',
                    "IncludeYear" = TRUE,
                    "IncludeMonth" = FALSE,
                    "IsActive" = TRUE
                WHERE "DocumentType" = '';
                """);

            migrationBuilder.Sql("""
                UPDATE material_requests
                SET "RequestNumber" = 'MR-' || UPPER(LEFT(REPLACE(CAST("Id" AS text), '-', ''), 8))
                WHERE "RequestNumber" = '';
                """);

            migrationBuilder.Sql("""
                UPDATE users
                SET "HasAllBranchAccess" = TRUE
                WHERE "Role" IN ('Admin', 'SuperAdmin');
                """);

            migrationBuilder.CreateIndex(
                name: "IX_work_orders_BranchId",
                table: "work_orders",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_users_DefaultBranchId",
                table: "users",
                column: "DefaultBranchId");

            migrationBuilder.CreateIndex(
                name: "IX_technicians_BranchId",
                table: "technicians",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_movements_BranchId",
                table: "stock_movements",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_preventive_maintenance_plans_BranchId",
                table: "preventive_maintenance_plans",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_numbering_settings_BranchId",
                table: "numbering_settings",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_numbering_settings_TenantId_BranchId_DocumentType",
                table: "numbering_settings",
                columns: new[] { "TenantId", "BranchId", "DocumentType" },
                unique: true,
                filter: "\"BranchId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_numbering_settings_TenantId_DocumentType",
                table: "numbering_settings",
                columns: new[] { "TenantId", "DocumentType" },
                unique: true,
                filter: "\"BranchId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_material_requests_BranchId",
                table: "material_requests",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_material_requests_TenantId_RequestNumber",
                table: "material_requests",
                columns: new[] { "TenantId", "RequestNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_assets_BranchId",
                table: "assets",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_branch_material_stocks_BranchId",
                table: "branch_material_stocks",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_branch_material_stocks_MaterialId",
                table: "branch_material_stocks",
                column: "MaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_branch_material_stocks_TenantId_BranchId_MaterialId",
                table: "branch_material_stocks",
                columns: new[] { "TenantId", "BranchId", "MaterialId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_branches_TenantId_Code",
                table: "branches",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_branches_TenantId_Name",
                table: "branches",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_stock_transfers_ApprovedByUserId",
                table: "stock_transfers",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_transfers_CompletedByUserId",
                table: "stock_transfers",
                column: "CompletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_transfers_FromBranchId",
                table: "stock_transfers",
                column: "FromBranchId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_transfers_MaterialId",
                table: "stock_transfers",
                column: "MaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_transfers_RequestedByUserId",
                table: "stock_transfers",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_transfers_TenantId_ReferenceNumber",
                table: "stock_transfers",
                columns: new[] { "TenantId", "ReferenceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_stock_transfers_ToBranchId",
                table: "stock_transfers",
                column: "ToBranchId");

            migrationBuilder.CreateIndex(
                name: "IX_user_branch_assignments_BranchId",
                table: "user_branch_assignments",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_user_branch_assignments_TenantId_UserId_BranchId",
                table: "user_branch_assignments",
                columns: new[] { "TenantId", "UserId", "BranchId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_branch_assignments_UserId",
                table: "user_branch_assignments",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_user_sessions_JwtId",
                table: "user_sessions",
                column: "JwtId");

            migrationBuilder.CreateIndex(
                name: "IX_user_sessions_TenantId_UserId_LoginAt",
                table: "user_sessions",
                columns: new[] { "TenantId", "UserId", "LoginAt" });

            migrationBuilder.CreateIndex(
                name: "IX_user_sessions_UserId",
                table: "user_sessions",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_assets_branches_BranchId",
                table: "assets",
                column: "BranchId",
                principalTable: "branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_material_requests_branches_BranchId",
                table: "material_requests",
                column: "BranchId",
                principalTable: "branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_numbering_settings_branches_BranchId",
                table: "numbering_settings",
                column: "BranchId",
                principalTable: "branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_preventive_maintenance_plans_branches_BranchId",
                table: "preventive_maintenance_plans",
                column: "BranchId",
                principalTable: "branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_stock_movements_branches_BranchId",
                table: "stock_movements",
                column: "BranchId",
                principalTable: "branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_technicians_branches_BranchId",
                table: "technicians",
                column: "BranchId",
                principalTable: "branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_users_branches_DefaultBranchId",
                table: "users",
                column: "DefaultBranchId",
                principalTable: "branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_work_orders_branches_BranchId",
                table: "work_orders",
                column: "BranchId",
                principalTable: "branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_assets_branches_BranchId",
                table: "assets");

            migrationBuilder.DropForeignKey(
                name: "FK_material_requests_branches_BranchId",
                table: "material_requests");

            migrationBuilder.DropForeignKey(
                name: "FK_numbering_settings_branches_BranchId",
                table: "numbering_settings");

            migrationBuilder.DropForeignKey(
                name: "FK_preventive_maintenance_plans_branches_BranchId",
                table: "preventive_maintenance_plans");

            migrationBuilder.DropForeignKey(
                name: "FK_stock_movements_branches_BranchId",
                table: "stock_movements");

            migrationBuilder.DropForeignKey(
                name: "FK_technicians_branches_BranchId",
                table: "technicians");

            migrationBuilder.DropForeignKey(
                name: "FK_users_branches_DefaultBranchId",
                table: "users");

            migrationBuilder.DropForeignKey(
                name: "FK_work_orders_branches_BranchId",
                table: "work_orders");

            migrationBuilder.DropTable(
                name: "branch_material_stocks");

            migrationBuilder.DropTable(
                name: "stock_transfers");

            migrationBuilder.DropTable(
                name: "user_branch_assignments");

            migrationBuilder.DropTable(
                name: "user_sessions");

            migrationBuilder.DropTable(
                name: "branches");

            migrationBuilder.DropIndex(
                name: "IX_work_orders_BranchId",
                table: "work_orders");

            migrationBuilder.DropIndex(
                name: "IX_users_DefaultBranchId",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_technicians_BranchId",
                table: "technicians");

            migrationBuilder.DropIndex(
                name: "IX_stock_movements_BranchId",
                table: "stock_movements");

            migrationBuilder.DropIndex(
                name: "IX_preventive_maintenance_plans_BranchId",
                table: "preventive_maintenance_plans");

            migrationBuilder.DropIndex(
                name: "IX_numbering_settings_BranchId",
                table: "numbering_settings");

            migrationBuilder.DropIndex(
                name: "IX_numbering_settings_TenantId_BranchId_DocumentType",
                table: "numbering_settings");

            migrationBuilder.DropIndex(
                name: "IX_numbering_settings_TenantId_DocumentType",
                table: "numbering_settings");

            migrationBuilder.DropIndex(
                name: "IX_material_requests_BranchId",
                table: "material_requests");

            migrationBuilder.DropIndex(
                name: "IX_material_requests_TenantId_RequestNumber",
                table: "material_requests");

            migrationBuilder.DropIndex(
                name: "IX_assets_BranchId",
                table: "assets");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "DefaultBranchId",
                table: "users");

            migrationBuilder.DropColumn(
                name: "HasAllBranchAccess",
                table: "users");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "technicians");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "stock_movements");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "preventive_maintenance_plans");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "numbering_settings");

            migrationBuilder.DropColumn(
                name: "DocumentType",
                table: "numbering_settings");

            migrationBuilder.DropColumn(
                name: "IncludeMonth",
                table: "numbering_settings");

            migrationBuilder.DropColumn(
                name: "IncludeYear",
                table: "numbering_settings");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "numbering_settings");

            migrationBuilder.DropColumn(
                name: "LastResetAt",
                table: "numbering_settings");

            migrationBuilder.DropColumn(
                name: "NextNumber",
                table: "numbering_settings");

            migrationBuilder.DropColumn(
                name: "Prefix",
                table: "numbering_settings");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "material_requests");

            migrationBuilder.DropColumn(
                name: "RequestNumber",
                table: "material_requests");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "assets");

            migrationBuilder.RenameColumn(
                name: "ResetFrequency",
                table: "numbering_settings",
                newName: "WorkOrderPrefix");

            migrationBuilder.RenameColumn(
                name: "PaddingLength",
                table: "numbering_settings",
                newName: "NextSequence");

            migrationBuilder.AddColumn<int>(
                name: "LastSequenceYear",
                table: "numbering_settings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_numbering_settings_TenantId",
                table: "numbering_settings",
                column: "TenantId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_material_requests_TenantId",
                table: "material_requests",
                column: "TenantId");
        }
    }
}
