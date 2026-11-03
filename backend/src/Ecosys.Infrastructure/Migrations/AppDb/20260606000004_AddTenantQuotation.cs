using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class AddTenantQuotation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                -- tenant_quotations
                CREATE TABLE IF NOT EXISTS tenant_quotations (
                    "Id"                     uuid          NOT NULL DEFAULT gen_random_uuid(),
                    "TenantId"               uuid          NOT NULL,
                    "ClientId"               uuid          NOT NULL,
                    "SiteId"                 uuid          NULL,
                    "QuoteNumber"            varchar(60)   NOT NULL,
                    "Status"                 varchar(40)   NOT NULL DEFAULT 'Draft',
                    "QuoteDate"              timestamptz   NOT NULL,
                    "ValidUntil"             timestamptz   NOT NULL,
                    "Subject"                varchar(300)  NULL,
                    "Notes"                  varchar(2000) NULL,
                    "Subtotal"               numeric(18,2) NOT NULL DEFAULT 0,
                    "TaxAmount"              numeric(18,2) NOT NULL DEFAULT 0,
                    "TotalAmount"            numeric(18,2) NOT NULL DEFAULT 0,
                    "Currency"               varchar(10)   NOT NULL DEFAULT 'KES',
                    "ConvertedToInvoiceId"   uuid          NULL,
                    "ConvertedToWorkOrderId" uuid          NULL,
                    "RejectionReason"        varchar(500)  NULL,
                    "CreatedAt"              timestamptz   NOT NULL DEFAULT now(),
                    "UpdatedAt"              timestamptz   NULL,
                    CONSTRAINT pk_tenant_quotations PRIMARY KEY ("Id"),
                    CONSTRAINT fk_tquo_tenant   FOREIGN KEY ("TenantId")   REFERENCES tenants("Id")         ON DELETE CASCADE,
                    CONSTRAINT fk_tquo_client   FOREIGN KEY ("ClientId")   REFERENCES clients("Id")         ON DELETE RESTRICT,
                    CONSTRAINT fk_tquo_site     FOREIGN KEY ("SiteId")     REFERENCES sites("Id")           ON DELETE SET NULL,
                    CONSTRAINT fk_tquo_invoice  FOREIGN KEY ("ConvertedToInvoiceId")   REFERENCES tenant_invoices("Id") ON DELETE SET NULL,
                    CONSTRAINT fk_tquo_wo       FOREIGN KEY ("ConvertedToWorkOrderId") REFERENCES work_orders("Id")     ON DELETE SET NULL,
                    CONSTRAINT uq_tquo_number   UNIQUE ("TenantId", "QuoteNumber")
                );
                CREATE INDEX IF NOT EXISTS ix_tquo_tenant_status ON tenant_quotations ("TenantId", "Status");
                CREATE INDEX IF NOT EXISTS ix_tquo_tenant_client ON tenant_quotations ("TenantId", "ClientId");

                -- tenant_quotation_lines
                CREATE TABLE IF NOT EXISTS tenant_quotation_lines (
                    "Id"           uuid          NOT NULL DEFAULT gen_random_uuid(),
                    "QuotationId"  uuid          NOT NULL,
                    "RateCardId"   uuid          NULL,
                    "Description"  varchar(500)  NOT NULL DEFAULT '',
                    "ItemType"     varchar(40)   NOT NULL DEFAULT 'Service',
                    "Quantity"     numeric(18,4) NOT NULL DEFAULT 0,
                    "Unit"         varchar(40)   NOT NULL DEFAULT 'unit',
                    "UnitRate"     numeric(18,4) NOT NULL DEFAULT 0,
                    "LineTotal"    numeric(18,2) NOT NULL DEFAULT 0,
                    "TaxRate"      numeric(8,4)  NOT NULL DEFAULT 0,
                    "GrandTotal"   numeric(18,2) NOT NULL DEFAULT 0,
                    "CreatedAt"    timestamptz   NOT NULL DEFAULT now(),
                    "UpdatedAt"    timestamptz   NULL,
                    CONSTRAINT pk_tenant_quotation_lines PRIMARY KEY ("Id"),
                    CONSTRAINT fk_tquol_quotation FOREIGN KEY ("QuotationId") REFERENCES tenant_quotations("Id") ON DELETE CASCADE,
                    CONSTRAINT fk_tquol_ratecard  FOREIGN KEY ("RateCardId")  REFERENCES rate_cards("Id")        ON DELETE SET NULL
                );
                CREATE INDEX IF NOT EXISTS ix_tquol_quotation ON tenant_quotation_lines ("QuotationId");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TABLE IF EXISTS tenant_quotation_lines;
                DROP TABLE IF EXISTS tenant_quotations;
                """);
        }
    }
}
