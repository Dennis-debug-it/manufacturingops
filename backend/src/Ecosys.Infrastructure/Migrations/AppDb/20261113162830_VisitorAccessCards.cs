using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class VisitorAccessCards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CardId",
                table: "visit_attendees",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CardIssuedAt",
                table: "visit_attendees",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CardIssuedByName",
                table: "visit_attendees",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CardNotReturned",
                table: "visit_attendees",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "CardNumber",
                table: "visit_attendees",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CardReturnedAt",
                table: "visit_attendees",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "visitor_cards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    FactoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    CardNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Colour = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Token = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ChipSerial = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    HolderAttendeeId = table.Column<Guid>(type: "uuid", nullable: true),
                    IssuedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastReturnedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_visitor_cards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_visitor_cards_org_nodes_FactoryId",
                        column: x => x.FactoryId,
                        principalTable: "org_nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_visitor_cards_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_visit_attendees_CardId",
                table: "visit_attendees",
                column: "CardId");

            migrationBuilder.CreateIndex(
                name: "IX_visitor_cards_FactoryId",
                table: "visitor_cards",
                column: "FactoryId");

            migrationBuilder.CreateIndex(
                name: "IX_visitor_cards_TenantId_CardNumber",
                table: "visitor_cards",
                columns: new[] { "TenantId", "CardNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_visitor_cards_TenantId_ChipSerial",
                table: "visitor_cards",
                columns: new[] { "TenantId", "ChipSerial" },
                unique: true,
                filter: "\"ChipSerial\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_visitor_cards_TenantId_FactoryId_Status",
                table: "visitor_cards",
                columns: new[] { "TenantId", "FactoryId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_visitor_cards_Token",
                table: "visitor_cards",
                column: "Token",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_visit_attendees_visitor_cards_CardId",
                table: "visit_attendees",
                column: "CardId",
                principalTable: "visitor_cards",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_visit_attendees_visitor_cards_CardId",
                table: "visit_attendees");

            migrationBuilder.DropTable(
                name: "visitor_cards");

            migrationBuilder.DropIndex(
                name: "IX_visit_attendees_CardId",
                table: "visit_attendees");

            migrationBuilder.DropColumn(
                name: "CardId",
                table: "visit_attendees");

            migrationBuilder.DropColumn(
                name: "CardIssuedAt",
                table: "visit_attendees");

            migrationBuilder.DropColumn(
                name: "CardIssuedByName",
                table: "visit_attendees");

            migrationBuilder.DropColumn(
                name: "CardNotReturned",
                table: "visit_attendees");

            migrationBuilder.DropColumn(
                name: "CardNumber",
                table: "visit_attendees");

            migrationBuilder.DropColumn(
                name: "CardReturnedAt",
                table: "visit_attendees");
        }
    }
}
