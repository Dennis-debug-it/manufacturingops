using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class AddBillableItemSourceLinkage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE work_order_billable_items
                    ADD COLUMN IF NOT EXISTS "SourceMaterialUsageId" uuid NULL,
                    ADD COLUMN IF NOT EXISTS "SourceTimeLogId"       uuid NULL;

                CREATE INDEX IF NOT EXISTS ix_wo_billable_items_source_material
                    ON work_order_billable_items ("WorkOrderId", "SourceMaterialUsageId")
                    WHERE "SourceMaterialUsageId" IS NOT NULL;

                CREATE INDEX IF NOT EXISTS ix_wo_billable_items_source_timelog
                    ON work_order_billable_items ("WorkOrderId", "SourceTimeLogId")
                    WHERE "SourceTimeLogId" IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS ix_wo_billable_items_source_material;
                DROP INDEX IF EXISTS ix_wo_billable_items_source_timelog;

                ALTER TABLE work_order_billable_items
                    DROP COLUMN IF EXISTS "SourceMaterialUsageId",
                    DROP COLUMN IF EXISTS "SourceTimeLogId";
                """);
        }
    }
}
