using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class AddSalesCustomFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RequiredFields",
                table: "sales_pipeline_stages",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "sales_custom_fields",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Label = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FieldType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Options = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    HelpText = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    PipelineId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_custom_fields", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sales_custom_fields_sales_pipelines_PipelineId",
                        column: x => x.PipelineId,
                        principalTable: "sales_pipelines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "sales_custom_field_values",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OpportunityId = table.Column<Guid>(type: "uuid", nullable: false),
                    FieldId = table.Column<Guid>(type: "uuid", nullable: false),
                    Value = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_custom_field_values", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sales_custom_field_values_sales_custom_fields_FieldId",
                        column: x => x.FieldId,
                        principalTable: "sales_custom_fields",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_sales_custom_field_values_sales_opportunities_OpportunityId",
                        column: x => x.OpportunityId,
                        principalTable: "sales_opportunities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_sales_custom_field_values_FieldId",
                table: "sales_custom_field_values",
                column: "FieldId");

            migrationBuilder.CreateIndex(
                name: "IX_sales_custom_field_values_OpportunityId_FieldId",
                table: "sales_custom_field_values",
                columns: new[] { "OpportunityId", "FieldId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sales_custom_field_values_TenantId_FieldId_Value",
                table: "sales_custom_field_values",
                columns: new[] { "TenantId", "FieldId", "Value" });

            migrationBuilder.CreateIndex(
                name: "IX_sales_custom_fields_PipelineId",
                table: "sales_custom_fields",
                column: "PipelineId");

            migrationBuilder.CreateIndex(
                name: "IX_sales_custom_fields_TenantId_Key",
                table: "sales_custom_fields",
                columns: new[] { "TenantId", "Key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sales_custom_field_values");

            migrationBuilder.DropTable(
                name: "sales_custom_fields");

            migrationBuilder.DropColumn(
                name: "RequiredFields",
                table: "sales_pipeline_stages");
        }
    }
}
