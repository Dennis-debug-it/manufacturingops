using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <summary>
    /// Adds credit notes (credit_notes, credit_note_lines) and the payment_allocations table that
    /// decouples payments from a single invoice, and makes tenant_payments.InvoiceId nullable.
    /// Existing confirmed payments are backfilled with one allocation each so invoice balances stay
    /// correct under the new allocation model.
    /// </summary>
    public partial class AddCreditNotesAndPaymentAllocations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // tenant_payments is created by 20260606000003_AddTenantPayment through raw SQL, so its
            // invoice foreign key is named "fk_tpay_invoice" (ON DELETE CASCADE) — not the EF-convention
            // name "FK_tenant_payments_tenant_invoices_InvoiceId" that the scaffolder assumed here. A
            // plain DropForeignKey on that convention name raises 42704 ("constraint ... does not exist")
            // on every database. Drop whichever name is actually present; IF EXISTS keeps it a no-op when
            // absent. The AddForeignKey below then recreates it under the EF-convention name with
            // ON DELETE SET NULL, so the schema converges to the model snapshot afterwards.
            migrationBuilder.Sql("""
                ALTER TABLE tenant_payments DROP CONSTRAINT IF EXISTS "fk_tpay_invoice";
                ALTER TABLE tenant_payments DROP CONSTRAINT IF EXISTS "FK_tenant_payments_tenant_invoices_InvoiceId";
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "InvoiceId",
                table: "tenant_payments",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.CreateTable(
                name: "credit_notes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreditNoteNumber = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: true),
                    Reason = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    IssueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Subtotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TaxAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    AmountApplied = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Balance = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_credit_notes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_credit_notes_clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_credit_notes_tenant_invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "tenant_invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_credit_notes_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "payment_allocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    AllocatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_allocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_payment_allocations_tenant_invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "tenant_invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_payment_allocations_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "credit_note_lines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreditNoteId = table.Column<Guid>(type: "uuid", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ItemType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Unit = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    UnitRate = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    LineTotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TaxRate = table.Column<decimal>(type: "numeric(8,4)", nullable: false),
                    TaxAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    GrandTotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_credit_note_lines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_credit_note_lines_credit_notes_CreditNoteId",
                        column: x => x.CreditNoteId,
                        principalTable: "credit_notes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_credit_note_lines_CreditNoteId",
                table: "credit_note_lines",
                column: "CreditNoteId");

            migrationBuilder.CreateIndex(
                name: "IX_credit_notes_ClientId",
                table: "credit_notes",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_credit_notes_InvoiceId",
                table: "credit_notes",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_credit_notes_TenantId_ClientId",
                table: "credit_notes",
                columns: new[] { "TenantId", "ClientId" });

            migrationBuilder.CreateIndex(
                name: "IX_credit_notes_TenantId_CreditNoteNumber",
                table: "credit_notes",
                columns: new[] { "TenantId", "CreditNoteNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_credit_notes_TenantId_Status",
                table: "credit_notes",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_payment_allocations_InvoiceId",
                table: "payment_allocations",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_payment_allocations_TenantId_InvoiceId_Status",
                table: "payment_allocations",
                columns: new[] { "TenantId", "InvoiceId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_payment_allocations_TenantId_SourceType_SourceId",
                table: "payment_allocations",
                columns: new[] { "TenantId", "SourceType", "SourceId" });

            migrationBuilder.AddForeignKey(
                name: "FK_tenant_payments_tenant_invoices_InvoiceId",
                table: "tenant_payments",
                column: "InvoiceId",
                principalTable: "tenant_invoices",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // Backfill: every existing confirmed payment becomes one active allocation against the
            // invoice it was recorded for, so invoice balances recompute identically under the new
            // allocation model. Reversed payments and any null-invoice payments are skipped.
            migrationBuilder.Sql(@"
                INSERT INTO payment_allocations
                    (""Id"", ""TenantId"", ""SourceType"", ""SourceId"", ""InvoiceId"", ""Amount"", ""AllocatedAt"", ""Status"", ""CreatedAt"", ""UpdatedAt"")
                SELECT gen_random_uuid(), p.""TenantId"", 'Payment', p.""Id"", p.""InvoiceId"", p.""Amount"", p.""PaymentDate"", 'Active', now(), NULL
                FROM tenant_payments p
                WHERE p.""Status"" = 'Confirmed' AND p.""InvoiceId"" IS NOT NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // After Up() ran, the constraint carries the EF-convention name; drop whichever name is
            // present so this reversal is safe regardless of how tenant_payments was originally created.
            migrationBuilder.Sql("""
                ALTER TABLE tenant_payments DROP CONSTRAINT IF EXISTS "FK_tenant_payments_tenant_invoices_InvoiceId";
                ALTER TABLE tenant_payments DROP CONSTRAINT IF EXISTS "fk_tpay_invoice";
                """);

            migrationBuilder.DropTable(
                name: "credit_note_lines");

            migrationBuilder.DropTable(
                name: "payment_allocations");

            migrationBuilder.DropTable(
                name: "credit_notes");

            migrationBuilder.AlterColumn<Guid>(
                name: "InvoiceId",
                table: "tenant_payments",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_tenant_payments_tenant_invoices_InvoiceId",
                table: "tenant_payments",
                column: "InvoiceId",
                principalTable: "tenant_invoices",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
