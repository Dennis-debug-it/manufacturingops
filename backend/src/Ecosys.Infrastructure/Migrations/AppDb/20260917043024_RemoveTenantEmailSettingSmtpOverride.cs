using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class RemoveTenantEmailSettingSmtpOverride : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OverrideSmtpSettings",
                table: "email_settings");

            migrationBuilder.DropColumn(
                name: "UsePlatformDefaults",
                table: "email_settings");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "OverrideSmtpSettings",
                table: "email_settings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "UsePlatformDefaults",
                table: "email_settings",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }
    }
}
