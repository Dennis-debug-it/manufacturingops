using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class AddTenantLicenseIsPerpetual : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPerpetual",
                table: "tenant_licenses",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Formalizes today's existing (accidental) behavior explicitly rather than changing it:
            // BuildSnapshot's `ExpiresAt ?? TrialEndsAt` fallback already treats a null ExpiresAt as
            // never-expiring, and SubscriptionLifecycleWorker already excludes these rows from its
            // suspend sweep. Marking them IsPerpetual = true keeps every existing tenant's access
            // exactly as it is today; only NEW activations going forward are required to set an
            // explicit ExpiresAt unless a platform admin deliberately checks "perpetual/internal".
            migrationBuilder.Sql(
                """UPDATE "tenant_licenses" SET "IsPerpetual" = TRUE WHERE "ExpiresAt" IS NULL;""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPerpetual",
                table: "tenant_licenses");
        }
    }
}
