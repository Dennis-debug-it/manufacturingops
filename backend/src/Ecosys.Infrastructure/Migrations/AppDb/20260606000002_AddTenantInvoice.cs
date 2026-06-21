using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class AddTenantInvoice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                -- tenant_invoices
                CREATE TABLE IF NOT EXISTS tenant_invoices (
                    "Id"                 uuid          NOT NULL DEFAULT gen_random_uuid(),
                    "TenantId"           uuid          NOT NULL,
                    "ClientId"           uuid          NOT NULL,
                    "InvoiceNumber"      varchar(60)   NOT NULL,
                    "Status"             varchar(40)   NOT NULL DEFAULT 'Draft',
                    "InvoiceDate"        timestamptz   NOT NULL,
                    "DueDate"            timestamptz   NOT NULL,
                    "Notes"              varchar(2000) NULL,
                    "TermsAndConditions" varchar(4000) NULL,
                    "Subtotal"           numeric(18,2) NOT NULL DEFAULT 0,
                    "TaxAmount"          numeric(18,2) NOT NULL DEFAULT 0,
                    "TotalAmount"        numeric(18,2) NOT NULL DEFAULT 0,
                    "AmountPaid"         numeric(18,2) NOT NULL DEFAULT 0,
                    "Balance"            numeric(18,2) NOT NULL DEFAULT 0,
                    "Currency"           varchar(10)   NOT NULL DEFAULT 'KES',
                    "CreatedAt"          timestamptz   NOT NULL DEFAULT now(),
                    "UpdatedAt"          timestamptz   NULL,
                    CONSTRAINT pk_tenant_invoices PRIMARY KEY ("Id"),
                    CONSTRAINT fk_tinv_tenant  FOREIGN KEY ("TenantId")  REFERENCES tenants("Id") ON DELETE CASCADE,
                    CONSTRAINT fk_tinv_client  FOREIGN KEY ("ClientId")  REFERENCES clients("Id") ON DELETE RESTRICT,
                    CONSTRAINT uq_tinv_number  UNIQUE ("TenantId", "InvoiceNumber")
                );
                CREATE INDEX IF NOT EXISTS ix_tinv_tenant_status   ON tenant_invoices ("TenantId", "Status");
                CREATE INDEX IF NOT EXISTS ix_tinv_tenant_client   ON tenant_invoices ("TenantId", "ClientId");

                -- tenant_invoice_lines
                CREATE TABLE IF NOT EXISTS tenant_invoice_lines (
                    "Id"             uuid          NOT NULL DEFAULT gen_random_uuid(),
                    "InvoiceId"      uuid          NOT NULL,
                    "BillableItemId" uuid          NULL,
                    "WorkOrderId"    uuid          NULL,
                    "Description"    varchar(500)  NOT NULL DEFAULT '',
                    "ItemType"       varchar(40)   NOT NULL DEFAULT 'Service',
                    "Quantity"       numeric(18,4) NOT NULL DEFAULT 0,
                    "Unit"           varchar(40)   NOT NULL DEFAULT 'unit',
                    "UnitRate"       numeric(18,4) NOT NULL DEFAULT 0,
                    "LineTotal"      numeric(18,2) NOT NULL DEFAULT 0,
                    "TaxRate"        numeric(8,4)  NOT NULL DEFAULT 0,
                    "TaxAmount"      numeric(18,2) NOT NULL DEFAULT 0,
                    "GrandTotal"     numeric(18,2) NOT NULL DEFAULT 0,
                    "CreatedAt"      timestamptz   NOT NULL DEFAULT now(),
                    "UpdatedAt"      timestamptz   NULL,
                    CONSTRAINT pk_tenant_invoice_lines PRIMARY KEY ("Id"),
                    CONSTRAINT fk_tinvl_invoice      FOREIGN KEY ("InvoiceId")      REFERENCES tenant_invoices("Id") ON DELETE CASCADE,
                    CONSTRAINT fk_tinvl_billable     FOREIGN KEY ("BillableItemId") REFERENCES work_order_billable_items("Id") ON DELETE SET NULL,
                    CONSTRAINT fk_tinvl_workorder    FOREIGN KEY ("WorkOrderId")    REFERENCES work_orders("Id") ON DELETE SET NULL
                );
                CREATE INDEX IF NOT EXISTS ix_tinvl_invoice ON tenant_invoice_lines ("InvoiceId");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TABLE IF EXISTS tenant_invoice_lines;
                DROP TABLE IF EXISTS tenant_invoices;
                """);
        }
    }
}
