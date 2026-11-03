using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class ReclassifyVanBranchMovements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Loading a van wrote its branch side as an "Issue" and a van return wrote its branch side
            // as a "Receipt", so usage reports counted van loads as parts used and returns as stock
            // bought. Each branch row was written in the same transaction as its van row: same part,
            // quantity, user and reason, moments apart. Those pairs take the van movement's type.
            migrationBuilder.Sql("""
                UPDATE stock_movements AS b
                SET "MovementType" = v."MovementType"
                FROM stock_movements AS v
                WHERE v."TenantId" = b."TenantId"
                  AND v."VanId" IS NOT NULL
                  AND b."VanId" IS NULL
                  AND b."BranchId" IS NOT NULL
                  AND b."WorkOrderId" IS NULL
                  AND b."MaterialRequestId" IS NULL
                  AND v."MaterialId" = b."MaterialId"
                  AND v."Quantity" = b."Quantity"
                  AND v."CreatedByUserId" IS NOT DISTINCT FROM b."CreatedByUserId"
                  AND v."Reason" IS NOT DISTINCT FROM b."Reason"
                  AND abs(extract(epoch FROM v."CreatedAt" - b."CreatedAt")) < 5
                  AND ((v."MovementType" = 'VanLoad' AND b."MovementType" = 'Issue')
                    OR (v."MovementType" = 'VanReturn' AND b."MovementType" = 'Receipt'));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE stock_movements
                SET "MovementType" = CASE "MovementType" WHEN 'VanLoad' THEN 'Issue' ELSE 'Receipt' END
                WHERE "VanId" IS NULL AND "BranchId" IS NOT NULL AND "MovementType" IN ('VanLoad', 'VanReturn');
                """);
        }
    }
}
