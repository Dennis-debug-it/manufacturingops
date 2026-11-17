using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class PlatformInvoicePeriods : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_platform_invoices_TenantId",
                table: "platform_invoices");

            migrationBuilder.AddColumn<Guid>(
                name: "InvoiceId",
                table: "price_quotes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PilotInvoiceId",
                table: "price_quotes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CoversFrom",
                table: "platform_invoices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CoversTo",
                table: "platform_invoices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PriceQuoteId",
                table: "platform_invoices",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Purpose",
                table: "platform_invoices",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_platform_invoices_TenantId_Purpose_CoversFrom",
                table: "platform_invoices",
                columns: new[] { "TenantId", "Purpose", "CoversFrom" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_platform_invoices_TenantId_Purpose_CoversFrom",
                table: "platform_invoices");

            migrationBuilder.DropColumn(
                name: "InvoiceId",
                table: "price_quotes");

            migrationBuilder.DropColumn(
                name: "PilotInvoiceId",
                table: "price_quotes");

            migrationBuilder.DropColumn(
                name: "CoversFrom",
                table: "platform_invoices");

            migrationBuilder.DropColumn(
                name: "CoversTo",
                table: "platform_invoices");

            migrationBuilder.DropColumn(
                name: "PriceQuoteId",
                table: "platform_invoices");

            migrationBuilder.DropColumn(
                name: "Purpose",
                table: "platform_invoices");

            migrationBuilder.CreateIndex(
                name: "IX_platform_invoices_TenantId",
                table: "platform_invoices",
                column: "TenantId");
        }
    }
}
