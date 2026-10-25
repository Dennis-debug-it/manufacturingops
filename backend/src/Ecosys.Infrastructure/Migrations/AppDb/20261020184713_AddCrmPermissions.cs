using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class AddCrmPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CanManageCrm",
                table: "user_permissions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CanViewCrm",
                table: "user_permissions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CanManageCrm",
                table: "role_permissions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CanViewCrm",
                table: "role_permissions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Keep everyone's CRM access exactly as it was under the role rule this replaces:
            // Manager and Finance managed the pipeline, Viewer could read it. Admin has no row and
            // holds every capability. Tenants can now change this per role in the matrix.
            migrationBuilder.Sql("""
                UPDATE role_permissions SET "CanViewCrm" = TRUE, "CanManageCrm" = TRUE
                WHERE lower("Role") IN ('manager', 'finance');
                UPDATE role_permissions SET "CanViewCrm" = TRUE
                WHERE lower("Role") = 'viewer';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CanManageCrm",
                table: "user_permissions");

            migrationBuilder.DropColumn(
                name: "CanViewCrm",
                table: "user_permissions");

            migrationBuilder.DropColumn(
                name: "CanManageCrm",
                table: "role_permissions");

            migrationBuilder.DropColumn(
                name: "CanViewCrm",
                table: "role_permissions");
        }
    }
}
