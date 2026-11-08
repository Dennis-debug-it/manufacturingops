using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class CloseJobsOnVerify : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CloseJobsOnVerify",
                table: "factory_settings",
                type: "boolean",
                nullable: false,
                // Existing factories get the shorter flow too; they can switch it off in factory settings.
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CloseJobsOnVerify",
                table: "factory_settings");
        }
    }
}
