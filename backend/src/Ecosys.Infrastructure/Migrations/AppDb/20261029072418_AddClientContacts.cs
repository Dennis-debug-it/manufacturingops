using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class AddClientContacts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "client_contacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    FullName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    JobTitle = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Role = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_client_contacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_client_contacts_clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sales_opportunity_contacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OpportunityId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContactId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_opportunity_contacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sales_opportunity_contacts_client_contacts_ContactId",
                        column: x => x.ContactId,
                        principalTable: "client_contacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_sales_opportunity_contacts_sales_opportunities_OpportunityId",
                        column: x => x.OpportunityId,
                        principalTable: "sales_opportunities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_client_contacts_ClientId",
                table: "client_contacts",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_client_contacts_TenantId_ClientId",
                table: "client_contacts",
                columns: new[] { "TenantId", "ClientId" });

            migrationBuilder.CreateIndex(
                name: "IX_client_contacts_TenantId_Email",
                table: "client_contacts",
                columns: new[] { "TenantId", "Email" });

            migrationBuilder.CreateIndex(
                name: "IX_sales_opportunity_contacts_ContactId",
                table: "sales_opportunity_contacts",
                column: "ContactId");

            migrationBuilder.CreateIndex(
                name: "IX_sales_opportunity_contacts_OpportunityId_ContactId",
                table: "sales_opportunity_contacts",
                columns: new[] { "OpportunityId", "ContactId" },
                unique: true);

            // Each deal's contact person becomes a contact at the deal's client (one per name), linked to the deal.
            migrationBuilder.Sql("""
                INSERT INTO client_contacts ("Id", "TenantId", "ClientId", "FullName", "Email", "Phone", "Role", "IsPrimary", "IsActive", "CreatedAt")
                SELECT gen_random_uuid(), d."TenantId", d."ClientId", left(d.name, 150), d."ContactEmail", d."ContactPhone", 'Other', false, true, now()
                FROM (
                    SELECT DISTINCT ON (o."ClientId", lower(btrim(o."ContactName")))
                           o."TenantId", o."ClientId", btrim(o."ContactName") AS name, o."ContactEmail", o."ContactPhone"
                    FROM sales_opportunities o
                    WHERE o."ClientId" IS NOT NULL AND nullif(btrim(o."ContactName"), '') IS NOT NULL
                    ORDER BY o."ClientId", lower(btrim(o."ContactName")), o."CreatedAt" DESC
                ) d;

                INSERT INTO sales_opportunity_contacts ("Id", "TenantId", "OpportunityId", "ContactId", "CreatedAt")
                SELECT gen_random_uuid(), o."TenantId", o."Id", c."Id", now()
                FROM sales_opportunities o
                JOIN client_contacts c ON c."ClientId" = o."ClientId" AND lower(c."FullName") = lower(btrim(o."ContactName"))
                WHERE nullif(btrim(o."ContactName"), '') IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sales_opportunity_contacts");

            migrationBuilder.DropTable(
                name: "client_contacts");
        }
    }
}
