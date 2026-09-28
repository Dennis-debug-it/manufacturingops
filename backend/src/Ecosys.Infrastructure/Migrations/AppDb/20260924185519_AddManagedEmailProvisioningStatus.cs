using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class AddManagedEmailProvisioningStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ManagedEmailStatus",
                table: "email_settings",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Pending");

            migrationBuilder.AddColumn<DateTime>(
                name: "ManagedEmailStatusCheckedAt",
                table: "email_settings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ManagedEmailStatusDetail",
                table: "email_settings",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ManagedEmailStatus",
                table: "email_settings");

            migrationBuilder.DropColumn(
                name: "ManagedEmailStatusCheckedAt",
                table: "email_settings");

            migrationBuilder.DropColumn(
                name: "ManagedEmailStatusDetail",
                table: "email_settings");
        }
    }
}
