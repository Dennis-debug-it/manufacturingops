using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class AddSalesPipelines : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PipelineId",
                table: "sales_opportunities",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "sales_pipelines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Module = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_pipelines", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "sales_pipeline_stages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PipelineId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Probability = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_pipeline_stages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sales_pipeline_stages_sales_pipelines_PipelineId",
                        column: x => x.PipelineId,
                        principalTable: "sales_pipelines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_sales_opportunities_TenantId_PipelineId",
                table: "sales_opportunities",
                columns: new[] { "TenantId", "PipelineId" });

            migrationBuilder.CreateIndex(
                name: "IX_sales_pipeline_stages_PipelineId_Key",
                table: "sales_pipeline_stages",
                columns: new[] { "PipelineId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sales_pipelines_TenantId_SortOrder",
                table: "sales_pipelines",
                columns: new[] { "TenantId", "SortOrder" });

            // Every workspace that already has deals gets its default pipeline with today's stages,
            // and its deals are put in it. Others get theirs on first use.
            migrationBuilder.Sql("""
                INSERT INTO sales_pipelines ("Id", "TenantId", "Name", "SortOrder", "Module", "IsDefault", "IsActive", "CreatedAt")
                SELECT gen_random_uuid(), t."TenantId", 'Sales', 10, NULL, true, true, now()
                FROM (SELECT DISTINCT "TenantId" FROM sales_opportunities) t;

                INSERT INTO sales_pipeline_stages ("Id", "TenantId", "PipelineId", "Key", "Name", "SortOrder", "Probability")
                SELECT gen_random_uuid(), p."TenantId", p."Id", s.key, s.name, s.sort, s.probability
                FROM sales_pipelines p
                CROSS JOIN (VALUES
                    ('Lead', 'Lead', 10, 10), ('Qualified', 'Qualified', 20, 25), ('SiteSurvey', 'Site Survey', 30, 40),
                    ('Proposal', 'Proposal', 40, 60), ('Negotiation', 'Negotiation', 50, 80), ('Won', 'Won', 60, 100), ('Lost', 'Lost', 70, 0)
                ) AS s(key, name, sort, probability);

                UPDATE sales_opportunities o
                SET "PipelineId" = p."Id"
                FROM sales_pipelines p
                WHERE p."TenantId" = o."TenantId" AND p."IsDefault";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sales_pipeline_stages");

            migrationBuilder.DropTable(
                name: "sales_pipelines");

            migrationBuilder.DropIndex(
                name: "IX_sales_opportunities_TenantId_PipelineId",
                table: "sales_opportunities");

            migrationBuilder.DropColumn(
                name: "PipelineId",
                table: "sales_opportunities");
        }
    }
}
