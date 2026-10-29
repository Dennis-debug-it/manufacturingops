using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <summary>
    /// New work orders used to copy their site's access notes into JobCardNotes, which is also the
    /// technician's findings, so the service report printed gate and parking notes as "Findings"
    /// and the technician's finish screen started with them. Clear the copies that nobody edited.
    /// </summary>
    public partial class ClearSiteAccessNotesFromFindings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE work_orders AS w
                SET "JobCardNotes" = NULL
                FROM sites AS s
                WHERE s."Id" = w."SiteId"
                  AND s."TenantId" = w."TenantId"
                  AND w."JobCardNotes" IS NOT NULL
                  AND btrim(w."JobCardNotes") = btrim(s."AccessNotes");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Data clean-up only; the cleared text is still on the site.
        }
    }
}
