using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class AddStockCounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "stock_counts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CountNumber = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    Category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    StartedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PostedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    PostedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_counts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_stock_counts_branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_counts_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_stock_counts_users_PostedByUserId",
                        column: x => x.PostedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_stock_counts_users_StartedByUserId",
                        column: x => x.StartedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "stock_count_lines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    StockCountId = table.Column<Guid>(type: "uuid", nullable: false),
                    MaterialItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpectedQuantity = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CountedQuantity = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    UnitCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CountedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CountedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_count_lines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_stock_count_lines_material_items_MaterialItemId",
                        column: x => x.MaterialItemId,
                        principalTable: "material_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_count_lines_stock_counts_StockCountId",
                        column: x => x.StockCountId,
                        principalTable: "stock_counts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_stock_count_lines_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_stock_count_lines_MaterialItemId",
                table: "stock_count_lines",
                column: "MaterialItemId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_count_lines_StockCountId_MaterialItemId",
                table: "stock_count_lines",
                columns: new[] { "StockCountId", "MaterialItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_stock_count_lines_TenantId",
                table: "stock_count_lines",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_counts_BranchId",
                table: "stock_counts",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_counts_PostedByUserId",
                table: "stock_counts",
                column: "PostedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_counts_StartedByUserId",
                table: "stock_counts",
                column: "StartedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_counts_TenantId_CountNumber",
                table: "stock_counts",
                columns: new[] { "TenantId", "CountNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_stock_counts_TenantId_Status",
                table: "stock_counts",
                columns: new[] { "TenantId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "stock_count_lines");

            migrationBuilder.DropTable(
                name: "stock_counts");
        }
    }
}
