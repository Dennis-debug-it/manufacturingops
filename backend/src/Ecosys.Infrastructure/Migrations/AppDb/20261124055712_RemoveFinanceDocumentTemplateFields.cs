using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class RemoveFinanceDocumentTemplateFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The invoice, quotation, receipt and statement designs, and the bank, payment and
            // quote/invoice terms fields, are gone. Strip them from each workspace's saved templates.
            // A row whose saved JSON can't be read is left alone rather than failing the migration.
            migrationBuilder.Sql("""
                CREATE FUNCTION pg_temp.strip_template_keys(cfg jsonb) RETURNS jsonb AS $fn$
                    SELECT COALESCE(jsonb_object_agg(k, v), '{}'::jsonb)
                    FROM jsonb_each(cfg) AS e(k, v)
                    WHERE lower(k) <> ALL (ARRAY[
                        'showbankdetails', 'quoteterms', 'invoiceterms', 'warrantynote', 'latepaymentnote',
                        'bankname', 'accountname', 'accountnumber', 'branch', 'swift', 'paybill', 'paymentinstructions'])
                $fn$ LANGUAGE sql IMMUTABLE;

                DO $do$
                DECLARE r record;
                BEGIN
                    FOR r IN SELECT "Id", "ReportTemplateConfigJson" AS shared, "DocumentDesignsJson" AS designs
                             FROM tenants
                             WHERE "ReportTemplateConfigJson" IS NOT NULL OR "DocumentDesignsJson" IS NOT NULL
                    LOOP
                        BEGIN
                            IF r.shared IS NOT NULL AND jsonb_typeof(r.shared::jsonb) = 'object' THEN
                                UPDATE tenants SET "ReportTemplateConfigJson" = pg_temp.strip_template_keys(r.shared::jsonb)::text
                                WHERE "Id" = r."Id";
                            END IF;
                            IF r.designs IS NOT NULL AND jsonb_typeof(r.designs::jsonb) = 'object' THEN
                                UPDATE tenants SET "DocumentDesignsJson" = (
                                    SELECT NULLIF(COALESCE(jsonb_object_agg(k, CASE WHEN jsonb_typeof(v) = 'object' THEN pg_temp.strip_template_keys(v) ELSE v END), '{}'::jsonb), '{}'::jsonb)::text
                                    FROM jsonb_each(r.designs::jsonb) AS d(k, v)
                                    WHERE lower(k) NOT IN ('invoice', 'quotation', 'receipt', 'statement'))
                                WHERE "Id" = r."Id";
                            END IF;
                        EXCEPTION WHEN others THEN
                            NULL;
                        END;
                    END LOOP;
                END
                $do$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The removed settings are not restored.
        }
    }
}
