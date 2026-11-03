using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class AddTenantFinanceDefaults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tenant_finance_defaults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    EnableVat = table.Column<bool>(type: "boolean", nullable: false),
                    TaxName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DefaultVatRate = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    DefaultPaymentTerms = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DefaultInvoiceDueDays = table.Column<int>(type: "integer", nullable: false),
                    DefaultQuotationValidityDays = table.Column<int>(type: "integer", nullable: false),
                    DefaultExpenseApprovalRequired = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_finance_defaults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tenant_finance_defaults_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tenant_finance_defaults_TenantId",
                table: "tenant_finance_defaults",
                column: "TenantId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tenant_finance_defaults");
        }
    }
}
