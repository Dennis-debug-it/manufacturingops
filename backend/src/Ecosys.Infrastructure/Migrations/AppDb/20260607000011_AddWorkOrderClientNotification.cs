using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class AddWorkOrderClientNotification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE work_orders ADD COLUMN IF NOT EXISTS "NotifyClient" boolean NOT NULL DEFAULT true;
                ALTER TABLE work_orders ADD COLUMN IF NOT EXISTS "ClientContactEmail" text;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE work_orders DROP COLUMN IF EXISTS "ClientContactEmail";
                ALTER TABLE work_orders DROP COLUMN IF EXISTS "NotifyClient";
                """);
        }
    }
}
