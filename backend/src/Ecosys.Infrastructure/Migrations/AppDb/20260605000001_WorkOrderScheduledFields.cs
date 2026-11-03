using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class WorkOrderScheduledFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE work_orders ADD COLUMN IF NOT EXISTS "ScheduledStartAt" timestamp with time zone;
                ALTER TABLE work_orders ADD COLUMN IF NOT EXISTS "ScheduledEndAt"   timestamp with time zone;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE work_orders DROP COLUMN IF EXISTS "ScheduledEndAt";
                ALTER TABLE work_orders DROP COLUMN IF EXISTS "ScheduledStartAt";
                """);
        }
    }
}
