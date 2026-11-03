using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class AddSalesStageHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "StageChangedAt",
                table: "sales_opportunities",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "sales_stage_changes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OpportunityId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromStage = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    ToStage = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ChangedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ChangedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_stage_changes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sales_stage_changes_sales_opportunities_OpportunityId",
                        column: x => x.OpportunityId,
                        principalTable: "sales_opportunities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_sales_stage_changes_OpportunityId_ChangedAt",
                table: "sales_stage_changes",
                columns: new[] { "OpportunityId", "ChangedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_sales_stage_changes_TenantId_ChangedAt",
                table: "sales_stage_changes",
                columns: new[] { "TenantId", "ChangedAt" });

            // History for existing deals, rebuilt from the "Stage changed from A to B" entries the
            // deal timeline already holds: the deal's first stage at creation, then each logged move,
            // then a win or loss that was set another way (a tender, a quote) at its own date.
            migrationBuilder.Sql("""
                INSERT INTO sales_stage_changes ("Id", "TenantId", "OpportunityId", "FromStage", "ToStage", "ChangedAt")
                SELECT gen_random_uuid(), o."TenantId", o."Id", NULL,
                       coalesce((SELECT (regexp_match(a."Summary", '^Stage changed from (\w+) to'))[1]
                                 FROM sales_activities a
                                 WHERE a."OpportunityId" = o."Id" AND a."Type" = 'System' AND a."Summary" LIKE 'Stage changed from %'
                                 ORDER BY a."OccurredAt" LIMIT 1),
                                CASE WHEN o."Stage" IN ('Won', 'Lost') THEN 'Lead' ELSE o."Stage" END),
                       o."CreatedAt"
                FROM sales_opportunities o;

                INSERT INTO sales_stage_changes ("Id", "TenantId", "OpportunityId", "FromStage", "ToStage", "ChangedAt", "ChangedByUserId")
                SELECT gen_random_uuid(), a."TenantId", a."OpportunityId", m[1], m[2], a."OccurredAt", a."LoggedByUserId"
                FROM sales_activities a
                CROSS JOIN LATERAL regexp_match(a."Summary", '^Stage changed from (\w+) to (\w+)') AS m
                WHERE a."Type" = 'System' AND m IS NOT NULL;

                INSERT INTO sales_stage_changes ("Id", "TenantId", "OpportunityId", "FromStage", "ToStage", "ChangedAt")
                SELECT gen_random_uuid(), o."TenantId", o."Id", NULL, o."Stage",
                       coalesce(CASE WHEN o."Stage" = 'Won' THEN o."WonAt" ELSE o."LostAt" END, o."UpdatedAt", o."CreatedAt")
                FROM sales_opportunities o
                WHERE o."Stage" IN ('Won', 'Lost')
                  AND NOT EXISTS (SELECT 1 FROM sales_stage_changes c WHERE c."OpportunityId" = o."Id" AND c."ToStage" = o."Stage");

                UPDATE sales_opportunities o
                SET "StageChangedAt" = (SELECT max(c."ChangedAt") FROM sales_stage_changes c WHERE c."OpportunityId" = o."Id");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sales_stage_changes");

            migrationBuilder.DropColumn(
                name: "StageChangedAt",
                table: "sales_opportunities");
        }
    }
}
