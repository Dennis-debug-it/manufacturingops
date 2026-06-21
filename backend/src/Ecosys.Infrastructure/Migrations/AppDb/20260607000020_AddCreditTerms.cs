using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class AddCreditTerms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── credit_terms table ───────────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "credit_terms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    NetDays = table.Column<int>(type: "integer", nullable: false, defaultValue: 30),
                    IsDepositRequired = table.Column<bool>(type: "boolean", nullable: false),
                    DepositPercent = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    CreditLimit = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
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

            migrationBuilder.CreateIndex(
                name: "IX_credit_terms_TenantId_Code",
                table: "credit_terms",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            // ── Add DefaultCreditTermId, CreditLimit, CurrentBalance to clients ──
            migrationBuilder.AddColumn<Guid>(
                name: "DefaultCreditTermId",
                table: "clients",
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

            migrationBuilder.CreateIndex(
                name: "IX_clients_DefaultCreditTermId",
                table: "clients",
                column: "DefaultCreditTermId");

            migrationBuilder.AddForeignKey(
                name: "FK_clients_credit_terms_DefaultCreditTermId",
                table: "clients",
                column: "DefaultCreditTermId",
                principalTable: "credit_terms",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // ── Add CreditTermId, DepositAmount, DepositPaid, DepositDueDate to tenant_invoices ──
            migrationBuilder.AddColumn<Guid>(
                name: "CreditTermId",
                table: "tenant_invoices",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DepositAmount",
                table: "tenant_invoices",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "DepositPaid",
                table: "tenant_invoices",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DepositDueDate",
                table: "tenant_invoices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_tenant_invoices_CreditTermId",
                table: "tenant_invoices",
                column: "CreditTermId");

            migrationBuilder.AddForeignKey(
                name: "FK_tenant_invoices_credit_terms_CreditTermId",
                table: "tenant_invoices",
                column: "CreditTermId",
                principalTable: "credit_terms",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_tenant_invoices_credit_terms_CreditTermId",
                table: "tenant_invoices");

            migrationBuilder.DropIndex(
                name: "IX_tenant_invoices_CreditTermId",
                table: "tenant_invoices");

            migrationBuilder.DropColumn(name: "CreditTermId", table: "tenant_invoices");
            migrationBuilder.DropColumn(name: "DepositAmount", table: "tenant_invoices");
            migrationBuilder.DropColumn(name: "DepositPaid", table: "tenant_invoices");
            migrationBuilder.DropColumn(name: "DepositDueDate", table: "tenant_invoices");

            migrationBuilder.DropForeignKey(
                name: "FK_clients_credit_terms_DefaultCreditTermId",
                table: "clients");

            migrationBuilder.DropIndex(
                name: "IX_clients_DefaultCreditTermId",
                table: "clients");

            migrationBuilder.DropColumn(name: "DefaultCreditTermId", table: "clients");
            migrationBuilder.DropColumn(name: "CreditLimit", table: "clients");
            migrationBuilder.DropColumn(name: "CurrentBalance", table: "clients");

            migrationBuilder.DropTable(name: "credit_terms");
        }
    }
}
