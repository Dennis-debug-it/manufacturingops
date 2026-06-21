using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class AddPhase2CSlaEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "SlaResolutionBreached",
                table: "work_orders",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "SlaResolutionBreachedAt",
                table: "work_orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SlaResolutionDeadline",
                table: "work_orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SlaResponseBreached",
                table: "work_orders",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "SlaResponseBreachedAt",
                table: "work_orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SlaResponseDeadline",
                table: "work_orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SlaDefinitionId",
                table: "clients",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "sla_definitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sla_definitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sla_definitions_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sla_rules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    SlaDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Priority = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ResponseTargetHours = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    ResolutionTargetHours = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    BusinessHoursOnly = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sla_rules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sla_rules_sla_definitions_SlaDefinitionId",
                        column: x => x.SlaDefinitionId,
                        principalTable: "sla_definitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_sla_rules_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_clients_SlaDefinitionId",
                table: "clients",
                column: "SlaDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_sla_definitions_TenantId_PlanName",
                table: "sla_definitions",
                columns: new[] { "TenantId", "PlanName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sla_rules_SlaDefinitionId_Priority",
                table: "sla_rules",
                columns: new[] { "SlaDefinitionId", "Priority" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sla_rules_TenantId",
                table: "sla_rules",
                column: "TenantId");

            migrationBuilder.AddForeignKey(
                name: "FK_clients_sla_definitions_SlaDefinitionId",
                table: "clients",
                column: "SlaDefinitionId",
                principalTable: "sla_definitions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_clients_sla_definitions_SlaDefinitionId",
                table: "clients");

            migrationBuilder.DropTable(
                name: "sla_rules");

            migrationBuilder.DropTable(
                name: "sla_definitions");

            migrationBuilder.DropIndex(
                name: "IX_clients_SlaDefinitionId",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "SlaResolutionBreached",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "SlaResolutionBreachedAt",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "SlaResolutionDeadline",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "SlaResponseBreached",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "SlaResponseBreachedAt",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "SlaResponseDeadline",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "SlaDefinitionId",
                table: "clients");
        }
    }
}
