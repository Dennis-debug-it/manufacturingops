using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class AddInvoiceDocumentType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DocumentType",
                table: "tenant_invoices",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "Invoice");

            migrationBuilder.CreateIndex(
                name: "IX_tenant_invoices_TenantId_DocumentType",
                table: "tenant_invoices",
                columns: new[] { "TenantId", "DocumentType" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_tenant_invoices_TenantId_DocumentType",
                table: "tenant_invoices");

            migrationBuilder.DropColumn(
                name: "DocumentType",
                table: "tenant_invoices");
        }
    }
}
