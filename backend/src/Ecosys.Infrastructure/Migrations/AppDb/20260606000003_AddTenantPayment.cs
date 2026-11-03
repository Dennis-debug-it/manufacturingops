using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class AddTenantPayment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS tenant_payments (
                    "Id"            uuid          NOT NULL DEFAULT gen_random_uuid(),
                    "TenantId"      uuid          NOT NULL,
                    "InvoiceId"     uuid          NOT NULL,
                    "ClientId"      uuid          NOT NULL,
                    "PaymentNumber" varchar(60)   NOT NULL,
                    "Amount"        numeric(18,2) NOT NULL DEFAULT 0,
                    "PaymentDate"   timestamptz   NOT NULL,
                    "Method"        varchar(40)   NOT NULL DEFAULT 'Cash',
                    "Reference"     varchar(200)  NULL,
                    "Notes"         varchar(1000) NULL,
                    "Status"        varchar(40)   NOT NULL DEFAULT 'Confirmed',
                    "CreatedAt"     timestamptz   NOT NULL DEFAULT now(),
                    "UpdatedAt"     timestamptz   NULL,
                    CONSTRAINT pk_tenant_payments  PRIMARY KEY ("Id"),
                    CONSTRAINT fk_tpay_tenant      FOREIGN KEY ("TenantId")  REFERENCES tenants("Id")          ON DELETE CASCADE,
                    CONSTRAINT fk_tpay_invoice     FOREIGN KEY ("InvoiceId") REFERENCES tenant_invoices("Id")  ON DELETE CASCADE,
                    CONSTRAINT fk_tpay_client      FOREIGN KEY ("ClientId")  REFERENCES clients("Id")          ON DELETE RESTRICT,
                    CONSTRAINT uq_tpay_number      UNIQUE ("TenantId", "PaymentNumber")
                );
                CREATE INDEX IF NOT EXISTS ix_tpay_tenant_invoice ON tenant_payments ("TenantId", "InvoiceId");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS tenant_payments;");
        }
    }
}
