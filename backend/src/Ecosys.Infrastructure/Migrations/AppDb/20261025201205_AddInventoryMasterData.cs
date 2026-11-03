using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class AddInventoryMasterData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LeadTimeDays",
                table: "suppliers",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BatchOrSerial",
                table: "stock_movements",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Barcode",
                table: "material_items",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PreferredSupplierId",
                table: "material_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ReorderQuantity",
                table: "material_items",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_material_items_PreferredSupplierId",
                table: "material_items",
                column: "PreferredSupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_material_items_TenantId_Barcode",
                table: "material_items",
                columns: new[] { "TenantId", "Barcode" });

            migrationBuilder.AddForeignKey(
                name: "FK_material_items_suppliers_PreferredSupplierId",
                table: "material_items",
                column: "PreferredSupplierId",
                principalTable: "suppliers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // A part already bought through a linked PO line gets that supplier as its preferred one.
            migrationBuilder.Sql("""
                UPDATE material_items AS m
                SET "PreferredSupplierId" = latest."SupplierId"
                FROM (
                    SELECT DISTINCT ON (l."MaterialItemId") l."MaterialItemId", o."SupplierId"
                    FROM purchase_order_lines AS l
                    JOIN purchase_orders AS o ON o."Id" = l."PurchaseOrderId"
                    WHERE l."MaterialItemId" IS NOT NULL AND o."Status" <> 'Cancelled'
                    ORDER BY l."MaterialItemId", o."OrderedAt" DESC) AS latest
                WHERE m."Id" = latest."MaterialItemId";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_material_items_suppliers_PreferredSupplierId",
                table: "material_items");

            migrationBuilder.DropIndex(
                name: "IX_material_items_PreferredSupplierId",
                table: "material_items");

            migrationBuilder.DropIndex(
                name: "IX_material_items_TenantId_Barcode",
                table: "material_items");

            migrationBuilder.DropColumn(
                name: "LeadTimeDays",
                table: "suppliers");

            migrationBuilder.DropColumn(
                name: "BatchOrSerial",
                table: "stock_movements");

            migrationBuilder.DropColumn(
                name: "Barcode",
                table: "material_items");

            migrationBuilder.DropColumn(
                name: "PreferredSupplierId",
                table: "material_items");

            migrationBuilder.DropColumn(
                name: "ReorderQuantity",
                table: "material_items");
        }
    }
}
