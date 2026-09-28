using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class TenantLicenseSuspensionReason : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SuspensionReason",
                table: "tenant_licenses",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            // Make TenantLicense authoritative (audit TM-P1-01/02) without unblocking anyone an operator
            // blocked: a tenant-level Suspended/Inactive state that the license row did not reflect is
            // first pushed onto the license, then every tenant row is re-projected from its license.
            migrationBuilder.Sql("""
                UPDATE tenant_licenses l
                SET "Status" = 'Suspended', "SuspendedAt" = COALESCE(t."SuspendedAt", now() at time zone 'utc'), "SuspensionReason" = 'Manual'
                FROM tenants t
                WHERE l."TenantId" = t."Id" AND t."Status" = 'Suspended' AND l."Status" <> 'Suspended';

                UPDATE tenant_licenses l
                SET "Status" = 'Cancelled', "CancelledAt" = COALESCE(t."DeactivatedAt", now() at time zone 'utc'), "SuspendedAt" = NULL
                FROM tenants t
                WHERE l."TenantId" = t."Id" AND t."Status" = 'Inactive' AND l."Status" <> 'Cancelled';

                -- Suspensions the tenant row did not know about came from the non-payment worker.
                UPDATE tenant_licenses l
                SET "SuspensionReason" = CASE WHEN t."Status" = 'Suspended' THEN 'Manual' ELSE 'NonPayment' END
                FROM tenants t
                WHERE l."TenantId" = t."Id" AND l."Status" = 'Suspended' AND l."SuspensionReason" IS NULL;

                UPDATE tenants t
                SET "Status" = CASE
                        WHEN l."Status" = 'Suspended' THEN 'Suspended'
                        WHEN l."Status" = 'Cancelled' THEN 'Inactive'
                        WHEN l."Status" IN ('Trial', 'TrialExpiringSoon', 'TrialExpired') THEN 'Trial'
                        ELSE 'Active' END,
                    "IsActive" = l."Status" NOT IN ('Suspended', 'Cancelled'),
                    "LicenseStatus" = l."Status",
                    "SuspendedAt" = CASE WHEN l."Status" = 'Suspended' THEN COALESCE(t."SuspendedAt", l."SuspendedAt") ELSE NULL END,
                    "MaxUsers" = l."MaxUsersOverride",
                    "MaxBranches" = l."MaxBranchesOverride",
                    "SubscriptionStartsAt" = l."StartsAt",
                    "SubscriptionEndsAt" = l."ExpiresAt"
                FROM tenant_licenses l
                WHERE l."TenantId" = t."Id" AND t."Id" <> '00000000-0000-0000-0000-000000000001';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SuspensionReason",
                table: "tenant_licenses");
        }
    }
}
