using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class AddVanStockTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "VanId",
                table: "stock_movements",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "vans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    RegistrationNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    TechnicianId = table.Column<Guid>(type: "uuid", nullable: true),
                    HomeBranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_vans_branches_HomeBranchId",
                        column: x => x.HomeBranchId,
                        principalTable: "branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_vans_technicians_TechnicianId",
                        column: x => x.TechnicianId,
                        principalTable: "technicians",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_vans_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "van_material_stocks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    VanId = table.Column<Guid>(type: "uuid", nullable: false),
                    MaterialId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuantityOnHand = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ReorderLevel = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    UnitCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_van_material_stocks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_van_material_stocks_material_items_MaterialId",
                        column: x => x.MaterialId,
                        principalTable: "material_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_van_material_stocks_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_van_material_stocks_vans_VanId",
                        column: x => x.VanId,
                        principalTable: "vans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_stock_movements_VanId",
                table: "stock_movements",
                column: "VanId");

            migrationBuilder.CreateIndex(
                name: "IX_van_material_stocks_MaterialId",
                table: "van_material_stocks",
                column: "MaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_van_material_stocks_TenantId_VanId_MaterialId",
                table: "van_material_stocks",
                columns: new[] { "TenantId", "VanId", "MaterialId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_van_material_stocks_VanId",
                table: "van_material_stocks",
                column: "VanId");

            migrationBuilder.CreateIndex(
                name: "IX_vans_HomeBranchId",
                table: "vans",
                column: "HomeBranchId");

            migrationBuilder.CreateIndex(
                name: "IX_vans_TechnicianId",
                table: "vans",
                column: "TechnicianId");

            migrationBuilder.CreateIndex(
                name: "IX_vans_TenantId_Name",
                table: "vans",
                columns: new[] { "TenantId", "Name" });

            migrationBuilder.AddForeignKey(
                name: "FK_stock_movements_vans_VanId",
                table: "stock_movements",
                column: "VanId",
                principalTable: "vans",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_stock_movements_vans_VanId",
                table: "stock_movements");

            migrationBuilder.DropTable(
                name: "van_material_stocks");

            migrationBuilder.DropTable(
                name: "vans");

            migrationBuilder.DropIndex(
                name: "IX_stock_movements_VanId",
                table: "stock_movements");

            migrationBuilder.DropColumn(
                name: "VanId",
                table: "stock_movements");
        }
    }
}
