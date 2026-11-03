using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class AddStarterTemplatePacks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                table: "pm_templates",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "pm_templates",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DefaultIntervalMonths",
                table: "pm_templates",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsSystemTemplate",
                table: "pm_templates",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "OptionalReportSectionsJson",
                table: "pm_templates",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceTemplateId",
                table: "pm_templates",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TemplatePackId",
                table: "pm_templates",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WorkType",
                table: "pm_templates",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "PreventiveMaintenance");

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "pm_template_sections",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "pm_template_sections",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "pm_template_questions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "HelpText",
                table: "asset_category_fields",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "asset_category_fields",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                table: "asset_categories",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "asset_categories",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContractorType",
                table: "asset_categories",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DefaultServiceIntervalMonths",
                table: "asset_categories",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "asset_categories",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsSystemTemplate",
                table: "asset_categories",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceAssetCategoryId",
                table: "asset_categories",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TemplatePackId",
                table: "asset_categories",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "system_template_packs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Code = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ContractorType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    IsSystem = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_system_template_packs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tenant_template_pack_installations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceTemplatePackId = table.Column<Guid>(type: "uuid", nullable: false),
                    InstalledByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    InstalledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_template_pack_installations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tenant_template_pack_installations_system_template_packs_So~",
                        column: x => x.SourceTemplatePackId,
                        principalTable: "system_template_packs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_tenant_template_pack_installations_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_pm_templates_TemplatePackId_Code",
                table: "pm_templates",
                columns: new[] { "TemplatePackId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_asset_categories_TemplatePackId_Code",
                table: "asset_categories",
                columns: new[] { "TemplatePackId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_system_template_packs_Code",
                table: "system_template_packs",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tenant_template_pack_installations_SourceTemplatePackId",
                table: "tenant_template_pack_installations",
                column: "SourceTemplatePackId");

            migrationBuilder.CreateIndex(
                name: "IX_tenant_template_pack_installations_TenantId_SourceTemplateP~",
                table: "tenant_template_pack_installations",
                columns: new[] { "TenantId", "SourceTemplatePackId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_asset_categories_system_template_packs_TemplatePackId",
                table: "asset_categories",
                column: "TemplatePackId",
                principalTable: "system_template_packs",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_pm_templates_system_template_packs_TemplatePackId",
                table: "pm_templates",
                column: "TemplatePackId",
                principalTable: "system_template_packs",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_asset_categories_system_template_packs_TemplatePackId",
                table: "asset_categories");

            migrationBuilder.DropForeignKey(
                name: "FK_pm_templates_system_template_packs_TemplatePackId",
                table: "pm_templates");

            migrationBuilder.DropTable(
                name: "tenant_template_pack_installations");

            migrationBuilder.DropTable(
                name: "system_template_packs");

            migrationBuilder.DropIndex(
                name: "IX_pm_templates_TemplatePackId_Code",
                table: "pm_templates");

            migrationBuilder.DropIndex(
                name: "IX_asset_categories_TemplatePackId_Code",
                table: "asset_categories");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "pm_templates");

            migrationBuilder.DropColumn(
                name: "DefaultIntervalMonths",
                table: "pm_templates");

            migrationBuilder.DropColumn(
                name: "IsSystemTemplate",
                table: "pm_templates");

            migrationBuilder.DropColumn(
                name: "OptionalReportSectionsJson",
                table: "pm_templates");

            migrationBuilder.DropColumn(
                name: "SourceTemplateId",
                table: "pm_templates");

            migrationBuilder.DropColumn(
                name: "TemplatePackId",
                table: "pm_templates");

            migrationBuilder.DropColumn(
                name: "WorkType",
                table: "pm_templates");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "pm_template_sections");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "pm_template_sections");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "pm_template_questions");

            migrationBuilder.DropColumn(
                name: "HelpText",
                table: "asset_category_fields");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "asset_category_fields");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "asset_categories");

            migrationBuilder.DropColumn(
                name: "ContractorType",
                table: "asset_categories");

            migrationBuilder.DropColumn(
                name: "DefaultServiceIntervalMonths",
                table: "asset_categories");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "asset_categories");

            migrationBuilder.DropColumn(
                name: "IsSystemTemplate",
                table: "asset_categories");

            migrationBuilder.DropColumn(
                name: "SourceAssetCategoryId",
                table: "asset_categories");

            migrationBuilder.DropColumn(
                name: "TemplatePackId",
                table: "asset_categories");

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                table: "pm_templates",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                table: "asset_categories",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
