using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class RenamePlatformFinanceRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Platform finance staff were stored as "Finance", which differed from the tenant role
            // "finance" by case alone. Scoped to the root tenant so a customer's own finance users —
            // who are always stored lower-case — can never be caught by this.
            migrationBuilder.Sql("""
                UPDATE users
                SET "Role" = 'PlatformFinance'
                WHERE "Role" = 'Finance'
                  AND "TenantId" = '00000000-0000-0000-0000-000000000001';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE users
                SET "Role" = 'Finance'
                WHERE "Role" = 'PlatformFinance'
                  AND "TenantId" = '00000000-0000-0000-0000-000000000001';
                """);
        }
    }
}
