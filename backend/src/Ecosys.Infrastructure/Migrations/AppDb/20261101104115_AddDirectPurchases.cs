using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class AddDirectPurchases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "direct_purchases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    MaterialItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    Quantity = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    UnitCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    WorkOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    WorkOrderMaterialUsageId = table.Column<Guid>(type: "uuid", nullable: true),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: true),
                    SupplierName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SupplierReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PurchasedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    InvoiceReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ReconciledByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReconciledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_direct_purchases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_direct_purchases_branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_direct_purchases_material_items_MaterialItemId",
                        column: x => x.MaterialItemId,
                        principalTable: "material_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_direct_purchases_suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_direct_purchases_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_direct_purchases_users_PurchasedByUserId",
                        column: x => x.PurchasedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_direct_purchases_users_ReconciledByUserId",
                        column: x => x.ReconciledByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_direct_purchases_work_orders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "work_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_direct_purchases_BranchId",
                table: "direct_purchases",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_direct_purchases_MaterialItemId",
                table: "direct_purchases",
                column: "MaterialItemId");

            migrationBuilder.CreateIndex(
                name: "IX_direct_purchases_PurchasedByUserId",
                table: "direct_purchases",
                column: "PurchasedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_direct_purchases_ReconciledByUserId",
                table: "direct_purchases",
                column: "ReconciledByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_direct_purchases_SupplierId",
                table: "direct_purchases",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_direct_purchases_TenantId_Status",
                table: "direct_purchases",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_direct_purchases_WorkOrderId",
                table: "direct_purchases",
                column: "WorkOrderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "direct_purchases");
        }
    }
}
