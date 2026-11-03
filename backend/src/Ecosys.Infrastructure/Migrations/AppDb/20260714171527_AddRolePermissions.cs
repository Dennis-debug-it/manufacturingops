using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class AddRolePermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "role_permissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CanViewWorkOrders = table.Column<bool>(type: "boolean", nullable: false),
                    CanCreateWorkOrders = table.Column<bool>(type: "boolean", nullable: false),
                    CanAssignWorkOrders = table.Column<bool>(type: "boolean", nullable: false),
                    CanCompleteWorkOrders = table.Column<bool>(type: "boolean", nullable: false),
                    CanViewKnowledge = table.Column<bool>(type: "boolean", nullable: false),
                    CanManageKnowledge = table.Column<bool>(type: "boolean", nullable: false),
                    CanViewMaterials = table.Column<bool>(type: "boolean", nullable: false),
                    CanRequestMaterials = table.Column<bool>(type: "boolean", nullable: false),
                    CanManageMaterials = table.Column<bool>(type: "boolean", nullable: false),
                    CanApproveMaterials = table.Column<bool>(type: "boolean", nullable: false),
                    CanIssueMaterials = table.Column<bool>(type: "boolean", nullable: false),
                    CanManageAssets = table.Column<bool>(type: "boolean", nullable: false),
                    CanManageSettings = table.Column<bool>(type: "boolean", nullable: false),
                    CanViewReports = table.Column<bool>(type: "boolean", nullable: false),
                    CanViewFinance = table.Column<bool>(type: "boolean", nullable: false),
                    CanManageFinance = table.Column<bool>(type: "boolean", nullable: false),
                    CanViewFinanceRates = table.Column<bool>(type: "boolean", nullable: false),
                    CanManageFinanceRates = table.Column<bool>(type: "boolean", nullable: false),
                    CanViewFinanceApprovals = table.Column<bool>(type: "boolean", nullable: false),
                    CanManageFinanceApprovals = table.Column<bool>(type: "boolean", nullable: false),
                    CanViewBillableWork = table.Column<bool>(type: "boolean", nullable: false),
                    CanManageBillableWork = table.Column<bool>(type: "boolean", nullable: false),
                    CanOverrideMaterialStock = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_role_permissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_role_permissions_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_role_permissions_TenantId_Role",
                table: "role_permissions",
                columns: new[] { "TenantId", "Role" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "role_permissions");
        }
    }
}
