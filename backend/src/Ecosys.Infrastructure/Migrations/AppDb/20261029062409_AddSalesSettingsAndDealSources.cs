using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class AddSalesSettingsAndDealSources : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "RenewalOfContractId",
                table: "sales_opportunities",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceWorkOrderId",
                table: "sales_opportunities",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "tenant_sales_settings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    DefaultOwnerUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DealVisibility = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreateRenewalDeals = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_sales_settings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_sales_opportunities_TenantId_RenewalOfContractId",
                table: "sales_opportunities",
                columns: new[] { "TenantId", "RenewalOfContractId" });

            migrationBuilder.CreateIndex(
                name: "IX_sales_opportunities_TenantId_SourceWorkOrderId",
                table: "sales_opportunities",
                columns: new[] { "TenantId", "SourceWorkOrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_tenant_sales_settings_TenantId",
                table: "tenant_sales_settings",
                column: "TenantId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tenant_sales_settings");

            migrationBuilder.DropIndex(
                name: "IX_sales_opportunities_TenantId_RenewalOfContractId",
                table: "sales_opportunities");

            migrationBuilder.DropIndex(
                name: "IX_sales_opportunities_TenantId_SourceWorkOrderId",
                table: "sales_opportunities");

            migrationBuilder.DropColumn(
                name: "RenewalOfContractId",
                table: "sales_opportunities");

            migrationBuilder.DropColumn(
                name: "SourceWorkOrderId",
                table: "sales_opportunities");
        }
    }
}
