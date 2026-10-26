using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class LinkPurchaseOrderLinesToParts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BranchId",
                table: "purchase_order_receipt_events",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "MaterialItemId",
                table: "purchase_order_lines",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_purchase_order_receipt_events_BranchId",
                table: "purchase_order_receipt_events",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_order_lines_MaterialItemId",
                table: "purchase_order_lines",
                column: "MaterialItemId");

            migrationBuilder.AddForeignKey(
                name: "FK_purchase_order_lines_material_items_MaterialItemId",
                table: "purchase_order_lines",
                column: "MaterialItemId",
                principalTable: "material_items",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_purchase_order_receipt_events_branches_BranchId",
                table: "purchase_order_receipt_events",
                column: "BranchId",
                principalTable: "branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // Existing lines were free text. Link each to the tenant's active part of the same name when
            // exactly one matches, so receiving what is still outstanding on open POs adds stock.
            // Ambiguous or unmatched names stay unlinked (non-stock), as they were.
            migrationBuilder.Sql("""
                UPDATE purchase_order_lines AS l
                SET "MaterialItemId" = m."Id"
                FROM material_items AS m
                WHERE m."TenantId" = l."TenantId"
                  AND m."IsActive"
                  AND lower(trim(m."ItemName")) = lower(trim(l."ItemName"))
                  AND (SELECT count(*) FROM material_items AS d
                       WHERE d."TenantId" = l."TenantId" AND d."IsActive"
                         AND lower(trim(d."ItemName")) = lower(trim(l."ItemName"))) = 1;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_purchase_order_lines_material_items_MaterialItemId",
                table: "purchase_order_lines");

            migrationBuilder.DropForeignKey(
                name: "FK_purchase_order_receipt_events_branches_BranchId",
                table: "purchase_order_receipt_events");

            migrationBuilder.DropIndex(
                name: "IX_purchase_order_receipt_events_BranchId",
                table: "purchase_order_receipt_events");

            migrationBuilder.DropIndex(
                name: "IX_purchase_order_lines_MaterialItemId",
                table: "purchase_order_lines");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "purchase_order_receipt_events");

            migrationBuilder.DropColumn(
                name: "MaterialItemId",
                table: "purchase_order_lines");
        }
    }
}
