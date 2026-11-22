using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class DocumentDesigner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AccentColor",
                table: "platform_document_templates",
                type: "character varying(9)",
                maxLength: 9,
                nullable: false,
                defaultValue: "#0f3d3e");

            migrationBuilder.AddColumn<string>(
                name: "Layout",
                table: "platform_document_templates",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Classic");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccentColor",
                table: "platform_document_templates");

            migrationBuilder.DropColumn(
                name: "Layout",
                table: "platform_document_templates");
        }
    }
}
