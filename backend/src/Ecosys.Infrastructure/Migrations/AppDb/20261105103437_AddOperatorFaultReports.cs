using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class AddOperatorFaultReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OperatorReportKey",
                table: "factory_settings",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "OperatorReportingEnabled",
                table: "factory_settings",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateIndex(
                name: "IX_factory_settings_OperatorReportKey",
                table: "factory_settings",
                column: "OperatorReportKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_factory_settings_OperatorReportKey",
                table: "factory_settings");

            migrationBuilder.DropColumn(
                name: "OperatorReportKey",
                table: "factory_settings");

            migrationBuilder.DropColumn(
                name: "OperatorReportingEnabled",
                table: "factory_settings");
        }
    }
}
