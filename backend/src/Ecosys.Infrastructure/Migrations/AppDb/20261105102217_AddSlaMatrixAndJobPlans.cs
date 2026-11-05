using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class AddSlaMatrixAndJobPlans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "JobPlanId",
                table: "work_orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "job_plans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    WorkType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    AssetCategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    EstimatedHours = table.Column<decimal>(type: "numeric(9,2)", precision: 9, scale: 2, nullable: true),
                    NeedsLineStop = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_plans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_job_plans_asset_categories_AssetCategoryId",
                        column: x => x.AssetCategoryId,
                        principalTable: "asset_categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_job_plans_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sla_matrix_cells",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Priority = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Criticality = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: false),
                    ResponseHours = table.Column<decimal>(type: "numeric(9,2)", precision: 9, scale: 2, nullable: false),
                    RepairHours = table.Column<decimal>(type: "numeric(9,2)", precision: 9, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sla_matrix_cells", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sla_matrix_cells_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "job_plan_resources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    JobPlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    MaterialItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_plan_resources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_job_plan_resources_job_plans_JobPlanId",
                        column: x => x.JobPlanId,
                        principalTable: "job_plans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_job_plan_resources_material_items_MaterialItemId",
                        column: x => x.MaterialItemId,
                        principalTable: "material_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "job_plan_steps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    JobPlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    Instruction = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    EstimatedMinutes = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_plan_steps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_job_plan_steps_job_plans_JobPlanId",
                        column: x => x.JobPlanId,
                        principalTable: "job_plans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_work_orders_JobPlanId",
                table: "work_orders",
                column: "JobPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_job_plan_resources_JobPlanId",
                table: "job_plan_resources",
                column: "JobPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_job_plan_resources_MaterialItemId",
                table: "job_plan_resources",
                column: "MaterialItemId");

            migrationBuilder.CreateIndex(
                name: "IX_job_plan_steps_JobPlanId",
                table: "job_plan_steps",
                column: "JobPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_job_plans_AssetCategoryId",
                table: "job_plans",
                column: "AssetCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_job_plans_TenantId_Name",
                table: "job_plans",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sla_matrix_cells_TenantId_Priority_Criticality",
                table: "sla_matrix_cells",
                columns: new[] { "TenantId", "Priority", "Criticality" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_work_orders_job_plans_JobPlanId",
                table: "work_orders",
                column: "JobPlanId",
                principalTable: "job_plans",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_work_orders_job_plans_JobPlanId",
                table: "work_orders");

            migrationBuilder.DropTable(
                name: "job_plan_resources");

            migrationBuilder.DropTable(
                name: "job_plan_steps");

            migrationBuilder.DropTable(
                name: "sla_matrix_cells");

            migrationBuilder.DropTable(
                name: "job_plans");

            migrationBuilder.DropIndex(
                name: "IX_work_orders_JobPlanId",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "JobPlanId",
                table: "work_orders");
        }
    }
}
