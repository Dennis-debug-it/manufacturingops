using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class AddSalesTenders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sales_tenders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Reference = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: true),
                    IssuerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ContactName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    ContactEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ContactPhone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ClarificationDeadline = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SubmissionDeadline = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SubmissionMethod = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    BidBondRequired = table.Column<bool>(type: "boolean", nullable: false),
                    BidBondAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    EstimatedValue = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    SubmittedValue = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    OutcomeNotes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    OwnerName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    OpportunityId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_tenders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sales_tenders_clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_sales_tenders_sales_opportunities_OpportunityId",
                        column: x => x.OpportunityId,
                        principalTable: "sales_opportunities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_sales_tenders_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_sales_tenders_ClientId",
                table: "sales_tenders",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_sales_tenders_OpportunityId",
                table: "sales_tenders",
                column: "OpportunityId");

            migrationBuilder.CreateIndex(
                name: "IX_sales_tenders_TenantId_Status",
                table: "sales_tenders",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_sales_tenders_TenantId_SubmissionDeadline",
                table: "sales_tenders",
                columns: new[] { "TenantId", "SubmissionDeadline" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sales_tenders");
        }
    }
}
