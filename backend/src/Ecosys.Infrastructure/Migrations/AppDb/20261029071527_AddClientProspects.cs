using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class AddClientProspects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsProspect",
                table: "clients",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_clients_TenantId_IsProspect",
                table: "clients",
                columns: new[] { "TenantId", "IsProspect" });

            // Deals entered for a prospect (a company or contact name, no client) get a client record:
            // a client or prospect of the same name if there is one, else a new prospect, one per name.
            migrationBuilder.Sql("""
                CREATE TEMP TABLE prospect_names ON COMMIT DROP AS
                SELECT DISTINCT ON (o."TenantId", lower(btrim(coalesce(nullif(btrim(o."CompanyName"), ''), o."ContactName"))))
                       o."TenantId",
                       btrim(coalesce(nullif(btrim(o."CompanyName"), ''), o."ContactName")) AS name,
                       lower(btrim(coalesce(nullif(btrim(o."CompanyName"), ''), o."ContactName"))) AS key,
                       o."ContactName", o."ContactEmail", o."ContactPhone", o."Location", o."Title"
                FROM sales_opportunities o
                WHERE o."ClientId" IS NULL
                  AND coalesce(nullif(btrim(o."CompanyName"), ''), nullif(btrim(o."ContactName"), '')) IS NOT NULL
                ORDER BY o."TenantId", lower(btrim(coalesce(nullif(btrim(o."CompanyName"), ''), o."ContactName"))), o."CreatedAt";

                INSERT INTO clients ("Id", "TenantId", "ClientName", "ContactPerson", "Email", "Phone", "ContactPhone", "Location", "Notes", "IsActive", "IsProspect", "CreatedAt")
                SELECT gen_random_uuid(), n."TenantId", left(n.name, 200), n."ContactName", n."ContactEmail", n."ContactPhone", n."ContactPhone", n."Location",
                       'Prospect from sales opportunity "' || n."Title" || '".', true, true, now()
                FROM prospect_names n
                WHERE NOT EXISTS (SELECT 1 FROM clients c WHERE c."TenantId" = n."TenantId" AND lower(btrim(c."ClientName")) = n.key);

                UPDATE sales_opportunities o
                SET "ClientId" = (
                    SELECT c."Id" FROM clients c
                    WHERE c."TenantId" = o."TenantId"
                      AND lower(btrim(c."ClientName")) = lower(btrim(coalesce(nullif(btrim(o."CompanyName"), ''), o."ContactName")))
                    ORDER BY c."IsProspect", c."CreatedAt"
                    LIMIT 1)
                WHERE o."ClientId" IS NULL
                  AND coalesce(nullif(btrim(o."CompanyName"), ''), nullif(btrim(o."ContactName"), '')) IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_clients_TenantId_IsProspect",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "IsProspect",
                table: "clients");
        }
    }
}
