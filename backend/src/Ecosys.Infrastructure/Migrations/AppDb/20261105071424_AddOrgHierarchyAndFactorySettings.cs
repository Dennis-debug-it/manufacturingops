using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class AddOrgHierarchyAndFactorySettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OrgNodeId",
                table: "assets",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "factory_settings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    IndustryTemplate = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ProductionUnit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ProductionUnitShort = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    UseGroupLevel = table.Column<bool>(type: "boolean", nullable: false),
                    UseSubSectionLevel = table.Column<bool>(type: "boolean", nullable: false),
                    WeeklyWindowEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    WeeklyWindowDay = table.Column<int>(type: "integer", nullable: false),
                    WeeklyWindowStart = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    WeeklyWindowEnd = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    LowCropStartMonth = table.Column<int>(type: "integer", nullable: true),
                    LowCropEndMonth = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_factory_settings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_factory_settings_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "org_nodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Level = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Location = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    TemplateKey = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ExternalId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ExternalSystem = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_org_nodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_org_nodes_org_nodes_ParentId",
                        column: x => x.ParentId,
                        principalTable: "org_nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_org_nodes_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_org_node_assignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgNodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_org_node_assignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_org_node_assignments_org_nodes_OrgNodeId",
                        column: x => x.OrgNodeId,
                        principalTable: "org_nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_org_node_assignments_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_org_node_assignments_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_assets_OrgNodeId",
                table: "assets",
                column: "OrgNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_assets_TenantId_OrgNodeId",
                table: "assets",
                columns: new[] { "TenantId", "OrgNodeId" });

            migrationBuilder.CreateIndex(
                name: "IX_factory_settings_TenantId",
                table: "factory_settings",
                column: "TenantId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_org_nodes_ParentId",
                table: "org_nodes",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_org_nodes_TenantId_Code",
                table: "org_nodes",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_org_nodes_TenantId_Level",
                table: "org_nodes",
                columns: new[] { "TenantId", "Level" });

            migrationBuilder.CreateIndex(
                name: "IX_org_nodes_TenantId_ParentId_Name",
                table: "org_nodes",
                columns: new[] { "TenantId", "ParentId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_org_nodes_TenantId_UpdatedAt",
                table: "org_nodes",
                columns: new[] { "TenantId", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_user_org_node_assignments_OrgNodeId",
                table: "user_org_node_assignments",
                column: "OrgNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_user_org_node_assignments_TenantId_UserId_OrgNodeId",
                table: "user_org_node_assignments",
                columns: new[] { "TenantId", "UserId", "OrgNodeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_org_node_assignments_UserId",
                table: "user_org_node_assignments",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_assets_org_nodes_OrgNodeId",
                table: "assets",
                column: "OrgNodeId",
                principalTable: "org_nodes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_assets_org_nodes_OrgNodeId",
                table: "assets");

            migrationBuilder.DropTable(
                name: "factory_settings");

            migrationBuilder.DropTable(
                name: "user_org_node_assignments");

            migrationBuilder.DropTable(
                name: "org_nodes");

            migrationBuilder.DropIndex(
                name: "IX_assets_OrgNodeId",
                table: "assets");

            migrationBuilder.DropIndex(
                name: "IX_assets_TenantId_OrgNodeId",
                table: "assets");

            migrationBuilder.DropColumn(
                name: "OrgNodeId",
                table: "assets");
        }
    }
}
