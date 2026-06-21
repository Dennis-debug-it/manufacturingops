using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class AddKnowledgeAndMaterialsPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CanManageKnowledge",
                table: "user_permissions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CanManageMaterials",
                table: "user_permissions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CanRequestMaterials",
                table: "user_permissions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CanViewKnowledge",
                table: "user_permissions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CanViewMaterials",
                table: "user_permissions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("""
                UPDATE user_permissions
                SET "CanViewKnowledge" = TRUE,
                    "CanManageKnowledge" = TRUE,
                    "CanViewMaterials" = TRUE,
                    "CanRequestMaterials" = TRUE,
                    "CanManageMaterials" = TRUE;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CanManageKnowledge",
                table: "user_permissions");

            migrationBuilder.DropColumn(
                name: "CanManageMaterials",
                table: "user_permissions");

            migrationBuilder.DropColumn(
                name: "CanRequestMaterials",
                table: "user_permissions");

            migrationBuilder.DropColumn(
                name: "CanViewKnowledge",
                table: "user_permissions");

            migrationBuilder.DropColumn(
                name: "CanViewMaterials",
                table: "user_permissions");
        }
    }
}
