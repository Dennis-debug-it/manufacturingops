using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class AddUsernameLogin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_users_TenantId",
                table: "users");

            migrationBuilder.AddColumn<bool>(
                name: "HasEmail",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "Username",
                table: "users",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            // Backfill every existing user with a username derived from their email's local part
            // (falling back to "user" when that part is empty, e.g. an address starting with a
            // symbol) so username login works for pre-existing accounts too, not just new ones.
            // Duplicates within a tenant — two people whose email local part normalises the same
            // way — get a numeric suffix; row_number()'s ordering (CreatedAt, Id) just needs to be
            // deterministic, not meaningful. left(..., 90) caps the base before the ".N" suffix is
            // appended — Username is character varying(100), and an email local part can run well
            // past that (RFC 5321 allows up to 64 octets there, and this app doesn't cap it), so an
            // uncapped value here would abort the whole migration with a length-constraint error.
            migrationBuilder.Sql(
                """
                WITH ranked AS (
                    SELECT
                        "Id",
                        "TenantId",
                        COALESCE(
                            NULLIF(left(btrim(regexp_replace(lower(split_part("Email", '@', 1)), '[^a-z0-9]+', '.', 'g'), '.'), 90), ''),
                            'user'
                        ) AS base_username,
                        ROW_NUMBER() OVER (
                            PARTITION BY "TenantId", COALESCE(
                                NULLIF(left(btrim(regexp_replace(lower(split_part("Email", '@', 1)), '[^a-z0-9]+', '.', 'g'), '.'), 90), ''),
                                'user'
                            )
                            ORDER BY "CreatedAt", "Id"
                        ) AS rn
                    FROM users
                )
                UPDATE users u
                SET "Username" = CASE WHEN r.rn = 1 THEN r.base_username ELSE r.base_username || '.' || (r.rn - 1)::text END
                FROM ranked r
                WHERE u."Id" = r."Id";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_users_TenantId_Username",
                table: "users",
                columns: new[] { "TenantId", "Username" },
                unique: true,
                filter: "\"Username\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_users_TenantId_Username",
                table: "users");

            migrationBuilder.DropColumn(
                name: "HasEmail",
                table: "users");

            migrationBuilder.DropColumn(
                name: "Username",
                table: "users");

            migrationBuilder.CreateIndex(
                name: "IX_users_TenantId",
                table: "users",
                column: "TenantId");
        }
    }
}
