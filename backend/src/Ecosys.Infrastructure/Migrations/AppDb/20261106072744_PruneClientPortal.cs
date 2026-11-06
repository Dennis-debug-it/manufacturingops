using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class PruneClientPortal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "client_portal_login_tokens");

            migrationBuilder.DropColumn(
                name: "AccessLinkCreatedAt",
                table: "client_portal_users");

            migrationBuilder.DropColumn(
                name: "AccessLinkNonce",
                table: "client_portal_users");

            migrationBuilder.DropColumn(
                name: "IsPendingApproval",
                table: "client_portal_users");

            migrationBuilder.DropColumn(
                name: "LastLoginAt",
                table: "client_portal_users");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AccessLinkCreatedAt",
                table: "client_portal_users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AccessLinkNonce",
                table: "client_portal_users",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPendingApproval",
                table: "client_portal_users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastLoginAt",
                table: "client_portal_users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "client_portal_login_tokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientPortalUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConsumedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RequestedIp = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TokenHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_client_portal_login_tokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_client_portal_login_tokens_client_portal_users_ClientPortal~",
                        column: x => x.ClientPortalUserId,
                        principalTable: "client_portal_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_client_portal_login_tokens_ClientPortalUserId_ExpiresAt",
                table: "client_portal_login_tokens",
                columns: new[] { "ClientPortalUserId", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_client_portal_login_tokens_TokenHash",
                table: "client_portal_login_tokens",
                column: "TokenHash",
                unique: true);
        }
    }
}
