using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class TemplateDomainCleanup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── pm_template_questions: drop redundant QuestionType and SortOrder ────
            // Backfill DisplayOrder from SortOrder where DisplayOrder is 0 before dropping.
            migrationBuilder.Sql(
                """
                UPDATE pm_template_questions
                SET "DisplayOrder" = "SortOrder"
                WHERE "DisplayOrder" = 0 AND "SortOrder" > 0;
                """);

            migrationBuilder.DropColumn(
                name: "QuestionType",
                table: "pm_template_questions");

            migrationBuilder.DropColumn(
                name: "SortOrder",
                table: "pm_template_questions");

            // ── pm_templates: add soft-delete audit columns ───────────────────────
            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "pm_templates",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeletedByUserId",
                table: "pm_templates",
                type: "uuid",
                nullable: true);

            // ── pm_template_versions: version snapshot table ──────────────────────
            migrationBuilder.CreateTable(
                name: "pm_template_versions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PmTemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    SnapshotJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pm_template_versions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_pm_template_versions_pm_templates_PmTemplateId",
                        column: x => x.PmTemplateId,
                        principalTable: "pm_templates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_pm_template_versions_PmTemplateId_Version",
                table: "pm_template_versions",
                columns: new[] { "PmTemplateId", "Version" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pm_template_versions");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "pm_templates");

            migrationBuilder.DropColumn(
                name: "DeletedByUserId",
                table: "pm_templates");

            migrationBuilder.AddColumn<string>(
                name: "QuestionType",
                table: "pm_template_questions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                table: "pm_template_questions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Restore SortOrder from DisplayOrder on rollback
            migrationBuilder.Sql(
                """
                UPDATE pm_template_questions SET "SortOrder" = "DisplayOrder";
                UPDATE pm_template_questions SET "QuestionType" = "ResponseType";
                """);
        }
    }
}
