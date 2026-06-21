using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class PlatformSettingsTemplatesExpansion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Type",
                table: "platform_document_templates",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30);

            migrationBuilder.AddColumn<string>(
                name: "BodyHtml",
                table: "platform_document_templates",
                type: "character varying(64000)",
                maxLength: 64000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByUserId",
                table: "platform_document_templates",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FooterHtml",
                table: "platform_document_templates",
                type: "character varying(12000)",
                maxLength: 12000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HeaderHtml",
                table: "platform_document_templates",
                type: "character varying(12000)",
                maxLength: 12000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Orientation",
                table: "platform_document_templates",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Portrait");

            migrationBuilder.AddColumn<string>(
                name: "PageSize",
                table: "platform_document_templates",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "A4");

            migrationBuilder.AddColumn<bool>(
                name: "ShowLogo",
                table: "platform_document_templates",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "ShowPoweredByEcosys",
                table: "platform_document_templates",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "ShowTenantBranding",
                table: "platform_document_templates",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "SignatureHtml",
                table: "platform_document_templates",
                type: "character varying(8000)",
                maxLength: 8000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Subject",
                table: "platform_document_templates",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TermsHtml",
                table: "platform_document_templates",
                type: "character varying(24000)",
                maxLength: 24000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedByUserId",
                table: "platform_document_templates",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BodyHtml",
                table: "platform_document_templates");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "platform_document_templates");

            migrationBuilder.DropColumn(
                name: "FooterHtml",
                table: "platform_document_templates");

            migrationBuilder.DropColumn(
                name: "HeaderHtml",
                table: "platform_document_templates");

            migrationBuilder.DropColumn(
                name: "Orientation",
                table: "platform_document_templates");

            migrationBuilder.DropColumn(
                name: "PageSize",
                table: "platform_document_templates");

            migrationBuilder.DropColumn(
                name: "ShowLogo",
                table: "platform_document_templates");

            migrationBuilder.DropColumn(
                name: "ShowPoweredByEcosys",
                table: "platform_document_templates");

            migrationBuilder.DropColumn(
                name: "ShowTenantBranding",
                table: "platform_document_templates");

            migrationBuilder.DropColumn(
                name: "SignatureHtml",
                table: "platform_document_templates");

            migrationBuilder.DropColumn(
                name: "Subject",
                table: "platform_document_templates");

            migrationBuilder.DropColumn(
                name: "TermsHtml",
                table: "platform_document_templates");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "platform_document_templates");

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                table: "platform_document_templates",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(80)",
                oldMaxLength: 80);
        }
    }
}
