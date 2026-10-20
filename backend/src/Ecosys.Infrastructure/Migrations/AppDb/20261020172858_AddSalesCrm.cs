using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class AddSalesCrm : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SalesOpportunityId",
                table: "work_orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SalesOpportunityId",
                table: "tenant_quotations",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "sales_opportunities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Stage = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: true),
                    SiteId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ContactName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    ContactEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ContactPhone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Location = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    Source = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    ServiceType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    EstimatedValue = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Probability = table.Column<int>(type: "integer", nullable: false),
                    ExpectedCloseDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    OwnerName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    LostReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    WonAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LostAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastActivityAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_opportunities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sales_opportunities_clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_sales_opportunities_sites_SiteId",
                        column: x => x.SiteId,
                        principalTable: "sites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_sales_opportunities_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sales_activities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OpportunityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Summary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DueAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LoggedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    LoggedByName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_activities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sales_activities_sales_opportunities_OpportunityId",
                        column: x => x.OpportunityId,
                        principalTable: "sales_opportunities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_work_orders_SalesOpportunityId",
                table: "work_orders",
                column: "SalesOpportunityId");

            migrationBuilder.CreateIndex(
                name: "IX_tenant_quotations_SalesOpportunityId",
                table: "tenant_quotations",
                column: "SalesOpportunityId");

            migrationBuilder.CreateIndex(
                name: "IX_sales_activities_OpportunityId",
                table: "sales_activities",
                column: "OpportunityId");

            migrationBuilder.CreateIndex(
                name: "IX_sales_activities_TenantId_DueAt",
                table: "sales_activities",
                columns: new[] { "TenantId", "DueAt" });

            migrationBuilder.CreateIndex(
                name: "IX_sales_activities_TenantId_OpportunityId_OccurredAt",
                table: "sales_activities",
                columns: new[] { "TenantId", "OpportunityId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_sales_opportunities_ClientId",
                table: "sales_opportunities",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_sales_opportunities_SiteId",
                table: "sales_opportunities",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "IX_sales_opportunities_TenantId_ClientId",
                table: "sales_opportunities",
                columns: new[] { "TenantId", "ClientId" });

            migrationBuilder.CreateIndex(
                name: "IX_sales_opportunities_TenantId_OwnerUserId",
                table: "sales_opportunities",
                columns: new[] { "TenantId", "OwnerUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_sales_opportunities_TenantId_Stage",
                table: "sales_opportunities",
                columns: new[] { "TenantId", "Stage" });

            migrationBuilder.AddForeignKey(
                name: "FK_tenant_quotations_sales_opportunities_SalesOpportunityId",
                table: "tenant_quotations",
                column: "SalesOpportunityId",
                principalTable: "sales_opportunities",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_work_orders_sales_opportunities_SalesOpportunityId",
                table: "work_orders",
                column: "SalesOpportunityId",
                principalTable: "sales_opportunities",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_tenant_quotations_sales_opportunities_SalesOpportunityId",
                table: "tenant_quotations");

            migrationBuilder.DropForeignKey(
                name: "FK_work_orders_sales_opportunities_SalesOpportunityId",
                table: "work_orders");

            migrationBuilder.DropTable(
                name: "sales_activities");

            migrationBuilder.DropTable(
                name: "sales_opportunities");

            migrationBuilder.DropIndex(
                name: "IX_work_orders_SalesOpportunityId",
                table: "work_orders");

            migrationBuilder.DropIndex(
                name: "IX_tenant_quotations_SalesOpportunityId",
                table: "tenant_quotations");

            migrationBuilder.DropColumn(
                name: "SalesOpportunityId",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "SalesOpportunityId",
                table: "tenant_quotations");
        }
    }
}
