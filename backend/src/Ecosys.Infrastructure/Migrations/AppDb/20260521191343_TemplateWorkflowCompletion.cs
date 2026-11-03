using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class TemplateWorkflowCompletion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PmTemplateNameSnapshot",
                table: "work_orders",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PmTemplateVersionSnapshot",
                table: "work_orders",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DefaultValue",
                table: "work_order_checklist_items",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "EvidenceRequired",
                table: "work_order_checklist_items",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "HelperText",
                table: "work_order_checklist_items",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MaxValue",
                table: "work_order_checklist_items",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MinValue",
                table: "work_order_checklist_items",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SectionOrder",
                table: "work_order_checklist_items",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "TechnicianRemarksAllowed",
                table: "work_order_checklist_items",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPlatformSeed",
                table: "pm_templates",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "pm_templates",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "DefaultValue",
                table: "pm_template_questions",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "EvidenceRequired",
                table: "pm_template_questions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "HelperText",
                table: "pm_template_questions",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MaxValue",
                table: "pm_template_questions",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MinValue",
                table: "pm_template_questions",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "TechnicianRemarksAllowed",
                table: "pm_template_questions",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PmTemplateNameSnapshot",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "PmTemplateVersionSnapshot",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "DefaultValue",
                table: "work_order_checklist_items");

            migrationBuilder.DropColumn(
                name: "EvidenceRequired",
                table: "work_order_checklist_items");

            migrationBuilder.DropColumn(
                name: "HelperText",
                table: "work_order_checklist_items");

            migrationBuilder.DropColumn(
                name: "MaxValue",
                table: "work_order_checklist_items");

            migrationBuilder.DropColumn(
                name: "MinValue",
                table: "work_order_checklist_items");

            migrationBuilder.DropColumn(
                name: "SectionOrder",
                table: "work_order_checklist_items");

            migrationBuilder.DropColumn(
                name: "TechnicianRemarksAllowed",
                table: "work_order_checklist_items");

            migrationBuilder.DropColumn(
                name: "IsPlatformSeed",
                table: "pm_templates");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "pm_templates");

            migrationBuilder.DropColumn(
                name: "DefaultValue",
                table: "pm_template_questions");

            migrationBuilder.DropColumn(
                name: "EvidenceRequired",
                table: "pm_template_questions");

            migrationBuilder.DropColumn(
                name: "HelperText",
                table: "pm_template_questions");

            migrationBuilder.DropColumn(
                name: "MaxValue",
                table: "pm_template_questions");

            migrationBuilder.DropColumn(
                name: "MinValue",
                table: "pm_template_questions");

            migrationBuilder.DropColumn(
                name: "TechnicianRemarksAllowed",
                table: "pm_template_questions");
        }
    }
}
