using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class TenantLifecycleSchemaRepair : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE tenants ADD COLUMN IF NOT EXISTS "DeactivatedAt" timestamp with time zone;
                ALTER TABLE tenants ADD COLUMN IF NOT EXISTS "SubscriptionStartsAt" timestamp with time zone;
                ALTER TABLE tenants ADD COLUMN IF NOT EXISTS "SuspendedAt" timestamp with time zone;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE tenants DROP COLUMN IF EXISTS "SuspendedAt";
                ALTER TABLE tenants DROP COLUMN IF EXISTS "SubscriptionStartsAt";
                ALTER TABLE tenants DROP COLUMN IF EXISTS "DeactivatedAt";
                """);
        }
    }
}
