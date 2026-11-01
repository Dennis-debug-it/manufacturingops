using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class AddHeldParts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "held_parts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    MaterialItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    Quantity = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    UnitCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    SourceReference = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    PartsReturnId = table.Column<Guid>(type: "uuid", nullable: true),
                    WorkOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResolutionReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ResolutionNote = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ResolvedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_held_parts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_held_parts_branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_held_parts_material_items_MaterialItemId",
                        column: x => x.MaterialItemId,
                        principalTable: "material_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_held_parts_suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_held_parts_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_held_parts_users_ResolvedByUserId",
                        column: x => x.ResolvedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_held_parts_work_orders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "work_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_held_parts_BranchId",
                table: "held_parts",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_held_parts_MaterialItemId",
                table: "held_parts",
                column: "MaterialItemId");

            migrationBuilder.CreateIndex(
                name: "IX_held_parts_ResolvedByUserId",
                table: "held_parts",
                column: "ResolvedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_held_parts_SupplierId",
                table: "held_parts",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_held_parts_TenantId_Status",
                table: "held_parts",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_held_parts_WorkOrderId",
                table: "held_parts",
                column: "WorkOrderId");

            // Faulty parts received before this table existed were simply dropped; list them as held so
            // stores can still return or write them off. Parts returns are tenant-scoped; the line takes
            // the job usage's cost where it still exists.
            migrationBuilder.Sql("""
                INSERT INTO held_parts ("Id", "TenantId", "MaterialItemId", "BranchId", "Quantity", "UnitCost", "Status",
                    "SourceReference", "PartsReturnId", "WorkOrderId", "Notes", "CreatedAt")
                SELECT gen_random_uuid(), r."TenantId", l."MaterialItemId", r."BranchId", l."Quantity",
                    COALESCE(u."UnitCost", m."UnitCost"), 'Held', r."ReturnNumber", r."Id", r."WorkOrderId", l."Notes",
                    COALESCE(r."ProcessedAt", r."CreatedAt")
                FROM parts_return_lines l
                JOIN parts_returns r ON r."Id" = l."PartsReturnId"
                JOIN material_items m ON m."Id" = l."MaterialItemId"
                LEFT JOIN work_order_material_usages u ON u."Id" = l."WorkOrderMaterialUsageId"
                WHERE l."Condition" = 'Faulty' AND r."Status" = 'Received';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "held_parts");
        }
    }
}
