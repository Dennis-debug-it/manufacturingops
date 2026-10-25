using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class AddStockMovementQuantityChange : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "QuantityChange",
                table: "stock_movements",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            // Quantity was always stored unsigned. Most types say which way stock went; for older
            // adjustments, the step from the location's previous balance does. Where neither tells,
            // the change stays null and the ledger shows it as unknown.
            migrationBuilder.Sql("""
                UPDATE stock_movements
                SET "QuantityChange" = CASE
                    WHEN "MovementType" IN ('Receipt', 'Return', 'TransferIn', 'VanLoad') THEN "Quantity"
                    WHEN "MovementType" IN ('Issue', 'TransferOut', 'VanReturn', 'VanConsume') THEN -"Quantity"
                END
                WHERE "MovementType" <> 'Adjustment';

                WITH ordered AS (
                    SELECT "Id", "Quantity", "BalanceAfter",
                           LAG("BalanceAfter") OVER (
                               PARTITION BY "TenantId", "MaterialId", "BranchId", "VanId"
                               ORDER BY "CreatedAt", "Id") AS previous
                    FROM stock_movements)
                UPDATE stock_movements AS m
                SET "QuantityChange" = CASE
                    WHEN o."BalanceAfter" - o.previous = o."Quantity" THEN o."Quantity"
                    WHEN o."BalanceAfter" - o.previous = -o."Quantity" THEN -o."Quantity"
                END
                FROM ordered AS o
                WHERE m."Id" = o."Id" AND m."MovementType" = 'Adjustment' AND o.previous IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "QuantityChange",
                table: "stock_movements");
        }
    }
}
