using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class PruneClientFinance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_clients_credit_terms_DefaultCreditTermId",
                table: "clients");

            migrationBuilder.DropForeignKey(
                name: "FK_work_orders_tenant_quotations_QuoteId",
                table: "work_orders");

            migrationBuilder.DropTable(
                name: "credit_note_lines");

            migrationBuilder.DropTable(
                name: "expenses");

            migrationBuilder.DropTable(
                name: "payment_allocations");

            migrationBuilder.DropTable(
                name: "supplier_bill_payments");

            migrationBuilder.DropTable(
                name: "tenant_invoice_lines");

            migrationBuilder.DropTable(
                name: "tenant_payments");

            migrationBuilder.DropTable(
                name: "tenant_quotation_lines");

            migrationBuilder.DropTable(
                name: "withholding_certificates");

            migrationBuilder.DropTable(
                name: "credit_notes");

            migrationBuilder.DropTable(
                name: "supplier_bills");

            migrationBuilder.DropTable(
                name: "work_order_billable_items");

            migrationBuilder.DropTable(
                name: "tenant_quotations");

            migrationBuilder.DropTable(
                name: "tenant_invoices");

            migrationBuilder.DropTable(
                name: "credit_terms");

            migrationBuilder.DropIndex(
                name: "IX_work_orders_QuoteId",
                table: "work_orders");

            migrationBuilder.DropIndex(
                name: "IX_clients_DefaultCreditTermId",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "BillingStatus",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "IsBillable",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "QuoteId",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "CreditLimit",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "CurrentBalance",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "DefaultCreditTermId",
                table: "clients");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BillingStatus",
                table: "work_orders",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsBillable",
                table: "work_orders",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "QuoteId",
                table: "work_orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CreditLimit",
                table: "clients",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CurrentBalance",
                table: "clients",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "DefaultCreditTermId",
                table: "clients",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "credit_terms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreditLimit = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    DepositPercent = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    IsDepositRequired = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NetDays = table.Column<int>(type: "integer", nullable: false, defaultValue: 30),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_credit_terms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_credit_terms_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "expenses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ApprovedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ApprovedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Category = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ClaimNumber = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: "KES"),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    IncurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReceiptUrl = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false, defaultValue: "Submitted"),
                    SubmittedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SubmittedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_expenses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_expenses_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "supplier_bills",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    AmountPaid = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    BillNumber = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: "KES"),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false, defaultValue: "Open"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_supplier_bills", x => x.Id);
                    table.ForeignKey(
                        name: "FK_supplier_bills_suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_supplier_bills_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "work_order_billable_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RateCardId = table.Column<Guid>(type: "uuid", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    GrandTotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsBilled = table.Column<bool>(type: "boolean", nullable: false),
                    IsManualEntry = table.Column<bool>(type: "boolean", nullable: false),
                    ItemType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    LineTotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    SourceMaterialUsageId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceTimeLogId = table.Column<Guid>(type: "uuid", nullable: true),
                    TaxAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxRate = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: false),
                    Unit = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    UnitRate = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_work_order_billable_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_work_order_billable_items_rate_cards_RateCardId",
                        column: x => x.RateCardId,
                        principalTable: "rate_cards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_work_order_billable_items_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_work_order_billable_items_work_orders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "work_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tenant_invoices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreditTermId = table.Column<Guid>(type: "uuid", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    AmountPaid = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Balance = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    DepositAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    DepositDueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DepositPaid = table.Column<bool>(type: "boolean", nullable: false),
                    DocumentType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    InvoiceDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    InvoiceNumber = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Subtotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TaxAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TermsAndConditions = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_invoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tenant_invoices_clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tenant_invoices_credit_terms_CreditTermId",
                        column: x => x.CreditTermId,
                        principalTable: "credit_terms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_tenant_invoices_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "supplier_bill_payments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierBillId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Method = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    PaidAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Reference = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_supplier_bill_payments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_supplier_bill_payments_supplier_bills_SupplierBillId",
                        column: x => x.SupplierBillId,
                        principalTable: "supplier_bills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_supplier_bill_payments_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "credit_notes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    AmountApplied = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Balance = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreditNoteNumber = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    IssueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Reason = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Subtotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TaxAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
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
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    AllocatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
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
                name: "tenant_invoice_lines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BillableItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    GrandTotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ItemType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    LineTotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    TaxAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TaxRate = table.Column<decimal>(type: "numeric(8,4)", nullable: false),
                    Unit = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    UnitRate = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_invoice_lines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tenant_invoice_lines_tenant_invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "tenant_invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_tenant_invoice_lines_work_order_billable_items_BillableItem~",
                        column: x => x.BillableItemId,
                        principalTable: "work_order_billable_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_tenant_invoice_lines_work_orders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "work_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "tenant_payments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Method = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    PaymentDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PaymentNumber = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_payments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tenant_payments_clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tenant_payments_tenant_invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "tenant_invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_tenant_payments_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tenant_quotations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConvertedToInvoiceId = table.Column<Guid>(type: "uuid", nullable: true),
                    ConvertedToWorkOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    SiteId = table.Column<Guid>(type: "uuid", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    AcceptedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AcceptedByName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    AcceptedByPortalUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcceptedByTitle = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    AcceptedFromIp = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ChaseReminderStage = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    QuoteDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    QuoteNumber = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    RejectionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SentByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Subject = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Subtotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TaxAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ValidUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_quotations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tenant_quotations_clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tenant_quotations_sites_SiteId",
                        column: x => x.SiteId,
                        principalTable: "sites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_tenant_quotations_tenant_invoices_ConvertedToInvoiceId",
                        column: x => x.ConvertedToInvoiceId,
                        principalTable: "tenant_invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_tenant_quotations_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_tenant_quotations_work_orders_ConvertedToWorkOrderId",
                        column: x => x.ConvertedToWorkOrderId,
                        principalTable: "work_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "withholding_certificates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CertificateDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CertificateNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    RatePercent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    RecordedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TaxType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_withholding_certificates", x => x.Id);
                    table.CheckConstraint("CK_withholding_certificates_amount", "\"Amount\" > 0");
                    table.CheckConstraint("CK_withholding_certificates_tax_type", "\"TaxType\" IN ('VAT', 'IncomeTax')");
                    table.ForeignKey(
                        name: "FK_withholding_certificates_clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_withholding_certificates_tenant_invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "tenant_invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_withholding_certificates_tenants_TenantId",
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
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    GrandTotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ItemType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    LineTotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    TaxAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TaxRate = table.Column<decimal>(type: "numeric(8,4)", nullable: false),
                    Unit = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    UnitRate = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
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

            migrationBuilder.CreateTable(
                name: "tenant_quotation_lines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    QuotationId = table.Column<Guid>(type: "uuid", nullable: false),
                    RateCardId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    GrandTotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ItemType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    LineTotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    TaxRate = table.Column<decimal>(type: "numeric(8,4)", nullable: false),
                    Unit = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    UnitRate = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_quotation_lines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tenant_quotation_lines_rate_cards_RateCardId",
                        column: x => x.RateCardId,
                        principalTable: "rate_cards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_tenant_quotation_lines_tenant_quotations_QuotationId",
                        column: x => x.QuotationId,
                        principalTable: "tenant_quotations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_work_orders_QuoteId",
                table: "work_orders",
                column: "QuoteId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_clients_DefaultCreditTermId",
                table: "clients",
                column: "DefaultCreditTermId");

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
                name: "IX_credit_terms_TenantId_Code",
                table: "credit_terms",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_expenses_TenantId_Status",
                table: "expenses",
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

            migrationBuilder.CreateIndex(
                name: "IX_supplier_bill_payments_SupplierBillId",
                table: "supplier_bill_payments",
                column: "SupplierBillId");

            migrationBuilder.CreateIndex(
                name: "IX_supplier_bill_payments_TenantId",
                table: "supplier_bill_payments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_supplier_bills_SupplierId",
                table: "supplier_bills",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_supplier_bills_TenantId_Status",
                table: "supplier_bills",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_tenant_invoice_lines_BillableItemId",
                table: "tenant_invoice_lines",
                column: "BillableItemId");

            migrationBuilder.CreateIndex(
                name: "IX_tenant_invoice_lines_InvoiceId",
                table: "tenant_invoice_lines",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_tenant_invoice_lines_WorkOrderId",
                table: "tenant_invoice_lines",
                column: "WorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_tenant_invoices_ClientId",
                table: "tenant_invoices",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_tenant_invoices_CreditTermId",
                table: "tenant_invoices",
                column: "CreditTermId");

            migrationBuilder.CreateIndex(
                name: "IX_tenant_invoices_TenantId_ClientId",
                table: "tenant_invoices",
                columns: new[] { "TenantId", "ClientId" });

            migrationBuilder.CreateIndex(
                name: "IX_tenant_invoices_TenantId_DocumentType",
                table: "tenant_invoices",
                columns: new[] { "TenantId", "DocumentType" });

            migrationBuilder.CreateIndex(
                name: "IX_tenant_invoices_TenantId_InvoiceNumber",
                table: "tenant_invoices",
                columns: new[] { "TenantId", "InvoiceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tenant_invoices_TenantId_Status",
                table: "tenant_invoices",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_tenant_payments_ClientId",
                table: "tenant_payments",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_tenant_payments_InvoiceId",
                table: "tenant_payments",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_tenant_payments_TenantId_InvoiceId",
                table: "tenant_payments",
                columns: new[] { "TenantId", "InvoiceId" });

            migrationBuilder.CreateIndex(
                name: "IX_tenant_payments_TenantId_PaymentNumber",
                table: "tenant_payments",
                columns: new[] { "TenantId", "PaymentNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tenant_quotation_lines_QuotationId",
                table: "tenant_quotation_lines",
                column: "QuotationId");

            migrationBuilder.CreateIndex(
                name: "IX_tenant_quotation_lines_RateCardId",
                table: "tenant_quotation_lines",
                column: "RateCardId");

            migrationBuilder.CreateIndex(
                name: "IX_tenant_quotations_ClientId",
                table: "tenant_quotations",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_tenant_quotations_ConvertedToInvoiceId",
                table: "tenant_quotations",
                column: "ConvertedToInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_tenant_quotations_ConvertedToWorkOrderId",
                table: "tenant_quotations",
                column: "ConvertedToWorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_tenant_quotations_SiteId",
                table: "tenant_quotations",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "IX_tenant_quotations_TenantId_QuoteNumber",
                table: "tenant_quotations",
                columns: new[] { "TenantId", "QuoteNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tenant_quotations_TenantId_Status",
                table: "tenant_quotations",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_withholding_certificates_ClientId",
                table: "withholding_certificates",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_withholding_certificates_InvoiceId",
                table: "withholding_certificates",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_withholding_certificates_TenantId_ClientId_CertificateNumber",
                table: "withholding_certificates",
                columns: new[] { "TenantId", "ClientId", "CertificateNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_withholding_certificates_TenantId_InvoiceId",
                table: "withholding_certificates",
                columns: new[] { "TenantId", "InvoiceId" });

            migrationBuilder.CreateIndex(
                name: "IX_work_order_billable_items_RateCardId",
                table: "work_order_billable_items",
                column: "RateCardId");

            migrationBuilder.CreateIndex(
                name: "IX_work_order_billable_items_TenantId_IsBilled",
                table: "work_order_billable_items",
                columns: new[] { "TenantId", "IsBilled" });

            migrationBuilder.CreateIndex(
                name: "IX_work_order_billable_items_TenantId_WorkOrderId",
                table: "work_order_billable_items",
                columns: new[] { "TenantId", "WorkOrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_work_order_billable_items_WorkOrderId",
                table: "work_order_billable_items",
                column: "WorkOrderId");

            migrationBuilder.AddForeignKey(
                name: "FK_clients_credit_terms_DefaultCreditTermId",
                table: "clients",
                column: "DefaultCreditTermId",
                principalTable: "credit_terms",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_work_orders_tenant_quotations_QuoteId",
                table: "work_orders",
                column: "QuoteId",
                principalTable: "tenant_quotations",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
