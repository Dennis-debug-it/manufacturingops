using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class AddRateCardAndBillingEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                -- rate_cards
                CREATE TABLE IF NOT EXISTS rate_cards (
                    "Id"          uuid          NOT NULL DEFAULT gen_random_uuid(),
                    "TenantId"    uuid          NOT NULL,
                    "Name"        varchar(160)  NOT NULL,
                    "Category"    varchar(40)   NOT NULL DEFAULT 'Labour',
                    "Description" varchar(500)  NULL,
                    "UnitRate"    numeric(18,4) NOT NULL DEFAULT 0,
                    "Unit"        varchar(40)   NOT NULL DEFAULT 'hour',
                    "SkillLevel"  varchar(40)   NULL,
                    "TimeType"    varchar(40)   NOT NULL DEFAULT 'Standard',
                    "IsDefault"   boolean       NOT NULL DEFAULT false,
                    "TaxRate"     numeric(8,4)  NULL,
                    "IsActive"    boolean       NOT NULL DEFAULT true,
                    "CreatedAt"   timestamptz   NOT NULL DEFAULT now(),
                    "UpdatedAt"   timestamptz   NULL,
                    CONSTRAINT pk_rate_cards PRIMARY KEY ("Id"),
                    CONSTRAINT fk_rate_cards_tenant FOREIGN KEY ("TenantId") REFERENCES tenants("Id") ON DELETE CASCADE
                );
                CREATE UNIQUE INDEX IF NOT EXISTS ix_rate_cards_tenant_name
                    ON rate_cards ("TenantId", "Name");
                CREATE INDEX IF NOT EXISTS ix_rate_cards_tenant_category_timetype
                    ON rate_cards ("TenantId", "Category", "TimeType");

                -- work_order_billable_items
                CREATE TABLE IF NOT EXISTS work_order_billable_items (
                    "Id"            uuid          NOT NULL DEFAULT gen_random_uuid(),
                    "WorkOrderId"   uuid          NOT NULL,
                    "TenantId"      uuid          NOT NULL,
                    "ItemType"      varchar(40)   NOT NULL DEFAULT 'Material',
                    "Description"   varchar(500)  NOT NULL DEFAULT '',
                    "Quantity"      numeric(18,4) NOT NULL DEFAULT 0,
                    "Unit"          varchar(40)   NOT NULL DEFAULT 'unit',
                    "UnitRate"      numeric(18,4) NOT NULL DEFAULT 0,
                    "RateCardId"    uuid          NULL,
                    "LineTotal"     numeric(18,2) NOT NULL DEFAULT 0,
                    "TaxRate"       numeric(8,4)  NOT NULL DEFAULT 0,
                    "TaxAmount"     numeric(18,2) NOT NULL DEFAULT 0,
                    "GrandTotal"    numeric(18,2) NOT NULL DEFAULT 0,
                    "IsManualEntry" boolean       NOT NULL DEFAULT false,
                    "IsBilled"      boolean       NOT NULL DEFAULT false,
                    "InvoiceId"     uuid          NULL,
                    "Notes"         varchar(1000) NULL,
                    "CreatedAt"     timestamptz   NOT NULL DEFAULT now(),
                    "UpdatedAt"     timestamptz   NULL,
                    CONSTRAINT pk_work_order_billable_items PRIMARY KEY ("Id"),
                    CONSTRAINT fk_wobi_tenant     FOREIGN KEY ("TenantId")    REFERENCES tenants("Id")      ON DELETE CASCADE,
                    CONSTRAINT fk_wobi_workorder  FOREIGN KEY ("WorkOrderId") REFERENCES work_orders("Id")  ON DELETE CASCADE,
                    CONSTRAINT fk_wobi_ratecard   FOREIGN KEY ("RateCardId")  REFERENCES rate_cards("Id")   ON DELETE SET NULL
                );
                CREATE INDEX IF NOT EXISTS ix_wobi_tenant_workorder
                    ON work_order_billable_items ("TenantId", "WorkOrderId");
                CREATE INDEX IF NOT EXISTS ix_wobi_tenant_billed
                    ON work_order_billable_items ("TenantId", "IsBilled");

                -- work_order_time_logs
                CREATE TABLE IF NOT EXISTS work_order_time_logs (
                    "Id"               uuid          NOT NULL DEFAULT gen_random_uuid(),
                    "WorkOrderId"      uuid          NOT NULL,
                    "TenantId"         uuid          NOT NULL,
                    "TechnicianId"     uuid          NULL,
                    "TechnicianName"   varchar(200)  NULL,
                    "StartedAt"        timestamptz   NOT NULL,
                    "EndedAt"          timestamptz   NULL,
                    "DurationMinutes"  int           NOT NULL DEFAULT 0,
                    "Notes"            varchar(1000) NULL,
                    "IsBilled"         boolean       NOT NULL DEFAULT false,
                    "CreatedAt"        timestamptz   NOT NULL DEFAULT now(),
                    "UpdatedAt"        timestamptz   NULL,
                    CONSTRAINT pk_work_order_time_logs PRIMARY KEY ("Id"),
                    CONSTRAINT fk_wotl_tenant     FOREIGN KEY ("TenantId")    REFERENCES tenants("Id")      ON DELETE CASCADE,
                    CONSTRAINT fk_wotl_workorder  FOREIGN KEY ("WorkOrderId") REFERENCES work_orders("Id")  ON DELETE CASCADE,
                    CONSTRAINT fk_wotl_technician FOREIGN KEY ("TechnicianId") REFERENCES technicians("Id") ON DELETE SET NULL
                );
                CREATE INDEX IF NOT EXISTS ix_wotl_tenant_workorder
                    ON work_order_time_logs ("TenantId", "WorkOrderId");

                -- billing columns on work_orders
                ALTER TABLE work_orders ADD COLUMN IF NOT EXISTS "BillingStatus" varchar(40) NOT NULL DEFAULT 'Unbilled';
                ALTER TABLE work_orders ADD COLUMN IF NOT EXISTS "IsBillable"    boolean     NOT NULL DEFAULT true;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE work_orders DROP COLUMN IF EXISTS "IsBillable";
                ALTER TABLE work_orders DROP COLUMN IF EXISTS "BillingStatus";
                DROP TABLE IF EXISTS work_order_time_logs;
                DROP TABLE IF EXISTS work_order_billable_items;
                DROP TABLE IF EXISTS rate_cards;
                """);
        }
    }
}
