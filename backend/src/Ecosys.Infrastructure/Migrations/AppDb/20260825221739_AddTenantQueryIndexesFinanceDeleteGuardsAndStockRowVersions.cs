using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class AddTenantQueryIndexesFinanceDeleteGuardsAndStockRowVersions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── Foreign-key drops ────────────────────────────────────────────────────────────────
            // work_order_billable_items and tenant_payments are created by 20260606000001 and
            // 20260606000003 through raw SQL, so their foreign keys carry the hand-written names
            // fk_wobi_workorder / fk_tpay_invoice rather than the EF-convention names the scaffolder
            // assumes. A plain DropForeignKey on the convention name raises 42704 — the exact failure
            // f462b26 had to repair. Drop whichever name is actually present; the AddForeignKey calls
            // below then recreate both under the convention name, so the schema converges.
            // (20260719110500 already recreated tenant_payments' key under the convention name on
            // databases that have run it, hence covering both spellings here.)
            migrationBuilder.Sql("""
                ALTER TABLE work_order_billable_items DROP CONSTRAINT IF EXISTS "fk_wobi_workorder";
                ALTER TABLE work_order_billable_items DROP CONSTRAINT IF EXISTS "FK_work_order_billable_items_work_orders_WorkOrderId";
                ALTER TABLE tenant_payments DROP CONSTRAINT IF EXISTS "fk_tpay_invoice";
                ALTER TABLE tenant_payments DROP CONSTRAINT IF EXISTS "FK_tenant_payments_tenant_invoices_InvoiceId";
                """);

            // credit_notes, payment_allocations and knowledge_articles were all scaffolded by EF
            // (20260719110500 and 20260516183854), so their constraint names follow the convention.
            migrationBuilder.DropForeignKey(
                name: "FK_credit_notes_tenant_invoices_InvoiceId",
                table: "credit_notes");

            migrationBuilder.DropForeignKey(
                name: "FK_knowledge_articles_users_UpdatedByUserId",
                table: "knowledge_articles");

            migrationBuilder.DropForeignKey(
                name: "FK_payment_allocations_tenant_invoices_InvoiceId",
                table: "payment_allocations");

            // ── Per-tenant uniqueness ────────────────────────────────────────────────────────────
            // DEPLOYMENT NOTE: the three unique indexes below (sites.SiteCode, expenses.ClaimNumber,
            // supplier_bills.BillNumber) will FAIL TO BUILD on any database that already contains two
            // rows sharing (TenantId, <column>). All three columns are NOT NULL, so PostgreSQL's
            // "NULLs are distinct" rule offers no escape. De-duplicate before applying this migration:
            //   SELECT "TenantId", "SiteCode",    count(*) FROM sites          GROUP BY 1, 2 HAVING count(*) > 1;
            //   SELECT "TenantId", "ClaimNumber", count(*) FROM expenses       GROUP BY 1, 2 HAVING count(*) > 1;
            //   SELECT "TenantId", "BillNumber",  count(*) FROM supplier_bills GROUP BY 1, 2 HAVING count(*) > 1;
            migrationBuilder.DropIndex(
                name: "IX_sites_TenantId_SiteCode",
                table: "sites");

            migrationBuilder.CreateIndex(
                name: "IX_sites_TenantId_SiteCode",
                table: "sites",
                columns: new[] { "TenantId", "SiteCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_expenses_TenantId_ClaimNumber",
                table: "expenses",
                columns: new[] { "TenantId", "ClaimNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_supplier_bills_TenantId_BillNumber",
                table: "supplier_bills",
                columns: new[] { "TenantId", "BillNumber" },
                unique: true);

            // ── work_orders tenant-leading query indexes ─────────────────────────────────────────
            migrationBuilder.CreateIndex(
                name: "IX_work_orders_TenantId_Status_CreatedAt",
                table: "work_orders",
                columns: new[] { "TenantId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_work_orders_TenantId_CreatedAt",
                table: "work_orders",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_work_orders_TenantId_DueDate",
                table: "work_orders",
                columns: new[] { "TenantId", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_work_orders_TenantId_ScheduledStartAt",
                table: "work_orders",
                columns: new[] { "TenantId", "ScheduledStartAt" },
                filter: "\"ScheduledStartAt\" IS NOT NULL");

            // ── Foreign keys recreated with the corrected delete behaviours (EC-P2-11/12) ────────
            migrationBuilder.AddForeignKey(
                name: "FK_work_order_billable_items_work_orders_WorkOrderId",
                table: "work_order_billable_items",
                column: "WorkOrderId",
                principalTable: "work_orders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_tenant_payments_tenant_invoices_InvoiceId",
                table: "tenant_payments",
                column: "InvoiceId",
                principalTable: "tenant_invoices",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_payment_allocations_tenant_invoices_InvoiceId",
                table: "payment_allocations",
                column: "InvoiceId",
                principalTable: "tenant_invoices",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_credit_notes_tenant_invoices_InvoiceId",
                table: "credit_notes",
                column: "InvoiceId",
                principalTable: "tenant_invoices",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_knowledge_articles_users_UpdatedByUserId",
                table: "knowledge_articles",
                column: "UpdatedByUserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // ── Missing foreign-key indexes on the raw-SQL finance tables (EC-P2-14) ─────────────
            // The six migrations 20260606000001..4 / 20260607000001 / 20260607000010 build their
            // tables with raw SQL, which created only the composite indexes spelled out in that SQL.
            // Every single-column FK index EF's convention declares in the model was therefore never
            // created, so each referential-integrity check and each join on those columns falls back
            // to a sequential scan. They are already present in the model snapshot, so creating them
            // here is a pure catch-up: no model change and no drift. IF NOT EXISTS keeps it a no-op
            // where an operator has already added one by hand.
            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS "IX_work_order_billable_items_WorkOrderId"    ON work_order_billable_items ("WorkOrderId");
                CREATE INDEX IF NOT EXISTS "IX_work_order_billable_items_RateCardId"     ON work_order_billable_items ("RateCardId");
                CREATE INDEX IF NOT EXISTS "IX_work_order_time_logs_WorkOrderId"         ON work_order_time_logs ("WorkOrderId");
                CREATE INDEX IF NOT EXISTS "IX_work_order_time_logs_TechnicianId"        ON work_order_time_logs ("TechnicianId");
                CREATE INDEX IF NOT EXISTS "IX_tenant_invoices_ClientId"                 ON tenant_invoices ("ClientId");
                CREATE INDEX IF NOT EXISTS "IX_tenant_invoice_lines_BillableItemId"      ON tenant_invoice_lines ("BillableItemId");
                CREATE INDEX IF NOT EXISTS "IX_tenant_invoice_lines_WorkOrderId"         ON tenant_invoice_lines ("WorkOrderId");
                CREATE INDEX IF NOT EXISTS "IX_tenant_payments_ClientId"                 ON tenant_payments ("ClientId");
                CREATE INDEX IF NOT EXISTS "IX_tenant_payments_InvoiceId"                ON tenant_payments ("InvoiceId");
                CREATE INDEX IF NOT EXISTS "IX_tenant_quotations_ClientId"               ON tenant_quotations ("ClientId");
                CREATE INDEX IF NOT EXISTS "IX_tenant_quotations_SiteId"                 ON tenant_quotations ("SiteId");
                CREATE INDEX IF NOT EXISTS "IX_tenant_quotations_ConvertedToInvoiceId"   ON tenant_quotations ("ConvertedToInvoiceId");
                CREATE INDEX IF NOT EXISTS "IX_tenant_quotations_ConvertedToWorkOrderId" ON tenant_quotations ("ConvertedToWorkOrderId");
                CREATE INDEX IF NOT EXISTS "IX_tenant_quotation_lines_RateCardId"        ON tenant_quotation_lines ("RateCardId");
                CREATE INDEX IF NOT EXISTS "IX_user_notification_preferences_UserId"     ON user_notification_preferences ("UserId");
                """);

            // REMAINING TRAP — deliberately not repaired here. Those tables still carry their
            // hand-written primary-key, unique-constraint and remaining foreign-key names
            // (pk_tenant_invoices, uq_tinv_number, uq_tpay_number, uq_tquo_number, fk_tinv_*,
            // fk_wobi_tenant, fk_wobi_ratecard, fk_wotl_*, fk_tinvl_*, fk_tpay_tenant,
            // fk_tpay_client, fk_tquo_*, fk_tquol_*, fk_rate_cards_tenant, fk_top_tenant,
            // fk_unp_user), and their model-declared composite indexes exist only under the ix_*
            // names. Renaming them to EF conventions was assessed and rejected: which name a given
            // database holds depends on which migrations that deployment has already run, so every
            // rename would have to be conditional per name, and a rename that does not reproduce
            // EF's convention exactly only moves the 42704 somewhere new. Standing rule for future
            // migration authors: any scaffolded DropForeignKey, DropPrimaryKey, DropUniqueConstraint
            // or DropIndex against rate_cards, work_order_billable_items, work_order_time_logs,
            // tenant_invoices, tenant_invoice_lines, tenant_payments, tenant_quotations,
            // tenant_quotation_lines, tenant_operations_policies or user_notification_preferences
            // must be rewritten as DROP ... IF EXISTS covering both spellings, exactly as the block
            // at the top of this Up() and f462b26 do.

            // NOTE ON xmin: MaterialItem, BranchMaterialStock and VanMaterialStock gain a uint "xmin"
            // concurrency token in this change, matching WorkOrder. xmin is a PostgreSQL system
            // column present on every table, so there is deliberately no AddColumn here — the
            // scaffolder emitted three and they were removed. Issuing one would raise 42701 ("column
            // name xmin conflicts with a system column name"). The token exists in the model snapshot
            // only; the physical schema is unchanged.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS "IX_work_order_billable_items_WorkOrderId";
                DROP INDEX IF EXISTS "IX_work_order_billable_items_RateCardId";
                DROP INDEX IF EXISTS "IX_work_order_time_logs_WorkOrderId";
                DROP INDEX IF EXISTS "IX_work_order_time_logs_TechnicianId";
                DROP INDEX IF EXISTS "IX_tenant_invoices_ClientId";
                DROP INDEX IF EXISTS "IX_tenant_invoice_lines_BillableItemId";
                DROP INDEX IF EXISTS "IX_tenant_invoice_lines_WorkOrderId";
                DROP INDEX IF EXISTS "IX_tenant_payments_ClientId";
                DROP INDEX IF EXISTS "IX_tenant_payments_InvoiceId";
                DROP INDEX IF EXISTS "IX_tenant_quotations_ClientId";
                DROP INDEX IF EXISTS "IX_tenant_quotations_SiteId";
                DROP INDEX IF EXISTS "IX_tenant_quotations_ConvertedToInvoiceId";
                DROP INDEX IF EXISTS "IX_tenant_quotations_ConvertedToWorkOrderId";
                DROP INDEX IF EXISTS "IX_tenant_quotation_lines_RateCardId";
                DROP INDEX IF EXISTS "IX_user_notification_preferences_UserId";
                """);

            // Up() left both of these under the EF-convention name, so a plain drop would work;
            // IF EXISTS keeps the reversal safe if Up() was only partially applied.
            migrationBuilder.Sql("""
                ALTER TABLE work_order_billable_items DROP CONSTRAINT IF EXISTS "FK_work_order_billable_items_work_orders_WorkOrderId";
                ALTER TABLE work_order_billable_items DROP CONSTRAINT IF EXISTS "fk_wobi_workorder";
                ALTER TABLE tenant_payments DROP CONSTRAINT IF EXISTS "FK_tenant_payments_tenant_invoices_InvoiceId";
                ALTER TABLE tenant_payments DROP CONSTRAINT IF EXISTS "fk_tpay_invoice";
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_credit_notes_tenant_invoices_InvoiceId",
                table: "credit_notes");

            migrationBuilder.DropForeignKey(
                name: "FK_knowledge_articles_users_UpdatedByUserId",
                table: "knowledge_articles");

            migrationBuilder.DropForeignKey(
                name: "FK_payment_allocations_tenant_invoices_InvoiceId",
                table: "payment_allocations");

            migrationBuilder.DropIndex(
                name: "IX_work_orders_TenantId_Status_CreatedAt",
                table: "work_orders");

            migrationBuilder.DropIndex(
                name: "IX_work_orders_TenantId_CreatedAt",
                table: "work_orders");

            migrationBuilder.DropIndex(
                name: "IX_work_orders_TenantId_DueDate",
                table: "work_orders");

            migrationBuilder.DropIndex(
                name: "IX_work_orders_TenantId_ScheduledStartAt",
                table: "work_orders");

            migrationBuilder.DropIndex(
                name: "IX_supplier_bills_TenantId_BillNumber",
                table: "supplier_bills");

            migrationBuilder.DropIndex(
                name: "IX_expenses_TenantId_ClaimNumber",
                table: "expenses");

            migrationBuilder.DropIndex(
                name: "IX_sites_TenantId_SiteCode",
                table: "sites");

            migrationBuilder.CreateIndex(
                name: "IX_sites_TenantId_SiteCode",
                table: "sites",
                columns: new[] { "TenantId", "SiteCode" });

            migrationBuilder.AddForeignKey(
                name: "FK_work_order_billable_items_work_orders_WorkOrderId",
                table: "work_order_billable_items",
                column: "WorkOrderId",
                principalTable: "work_orders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_tenant_payments_tenant_invoices_InvoiceId",
                table: "tenant_payments",
                column: "InvoiceId",
                principalTable: "tenant_invoices",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_payment_allocations_tenant_invoices_InvoiceId",
                table: "payment_allocations",
                column: "InvoiceId",
                principalTable: "tenant_invoices",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_credit_notes_tenant_invoices_InvoiceId",
                table: "credit_notes",
                column: "InvoiceId",
                principalTable: "tenant_invoices",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_knowledge_articles_users_UpdatedByUserId",
                table: "knowledge_articles",
                column: "UpdatedByUserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
