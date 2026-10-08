using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <summary>
    /// Technician-role users created without a phone number or assignment group never got a
    /// technician profile, so they could not be picked when creating or assigning a work order.
    /// Create the missing profiles; the user API now creates one for every Technician-role user.
    /// </summary>
    public partial class BackfillTechnicianProfilesForTechnicianUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                INSERT INTO technicians (""Id"", ""TenantId"", ""UserId"", ""BranchId"", ""FullName"", ""Email"", ""Phone"", ""IsActive"", ""CreatedAt"", ""IsTrackingActive"", ""AvailabilityStatus"")
                SELECT
                    gen_random_uuid(),
                    u.""TenantId"",
                    u.""Id"",
                    COALESCE(u.""DefaultBranchId"", (
                        SELECT b.""BranchId"" FROM user_branch_assignments b
                        WHERE b.""UserId"" = u.""Id""
                        ORDER BY b.""IsDefault"" DESC
                        LIMIT 1)),
                    u.""FullName"",
                    u.""Email"",
                    u.""Phone"",
                    u.""IsActive"",
                    now(),
                    false,
                    'Available'
                FROM users u
                WHERE u.""Role"" = 'Technician'
                  AND u.""TenantId"" IS NOT NULL
                  AND NOT EXISTS (
                      SELECT 1 FROM technicians t
                      WHERE t.""TenantId"" = u.""TenantId""
                        AND (t.""UserId"" = u.""Id"" OR lower(t.""Email"") = lower(u.""Email"")));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Data backfill only; the created profiles may already be assigned to work orders.
        }
    }
}
