using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class AddDynamicAccessControlTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "permission_groups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_permission_groups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "access_scopes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    PermissionGroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    Capability = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    ScopeType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ScopeEntityId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_access_scopes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_access_scopes_permission_groups_PermissionGroupId",
                        column: x => x.PermissionGroupId,
                        principalTable: "permission_groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_access_scopes_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "group_capabilities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PermissionGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    Capability = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_group_capabilities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_group_capabilities_permission_groups_PermissionGroupId",
                        column: x => x.PermissionGroupId,
                        principalTable: "permission_groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "permission_thresholds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    PermissionGroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    Capability = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    MaximumAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_permission_thresholds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_permission_thresholds_permission_groups_PermissionGroupId",
                        column: x => x.PermissionGroupId,
                        principalTable: "permission_groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_permission_thresholds_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_permission_groups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PermissionGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_permission_groups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_permission_groups_permission_groups_PermissionGroupId",
                        column: x => x.PermissionGroupId,
                        principalTable: "permission_groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_permission_groups_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_permission_overrides",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Capability = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    IsAllowed = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_permission_overrides", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_permission_overrides_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_access_scopes_PermissionGroupId",
                table: "access_scopes",
                column: "PermissionGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_access_scopes_UserId",
                table: "access_scopes",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_group_capabilities_PermissionGroupId",
                table: "group_capabilities",
                column: "PermissionGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_group_capabilities_TenantId_PermissionGroupId_Capability",
                table: "group_capabilities",
                columns: new[] { "TenantId", "PermissionGroupId", "Capability" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_permission_groups_TenantId_Name",
                table: "permission_groups",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_permission_thresholds_PermissionGroupId",
                table: "permission_thresholds",
                column: "PermissionGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_permission_thresholds_UserId",
                table: "permission_thresholds",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_user_permission_groups_PermissionGroupId",
                table: "user_permission_groups",
                column: "PermissionGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_user_permission_groups_TenantId_UserId_PermissionGroupId",
                table: "user_permission_groups",
                columns: new[] { "TenantId", "UserId", "PermissionGroupId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_permission_groups_UserId",
                table: "user_permission_groups",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_user_permission_overrides_TenantId_UserId_Capability",
                table: "user_permission_overrides",
                columns: new[] { "TenantId", "UserId", "Capability" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_permission_overrides_UserId",
                table: "user_permission_overrides",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "access_scopes");

            migrationBuilder.DropTable(
                name: "group_capabilities");

            migrationBuilder.DropTable(
                name: "permission_thresholds");

            migrationBuilder.DropTable(
                name: "user_permission_groups");

            migrationBuilder.DropTable(
                name: "user_permission_overrides");

            migrationBuilder.DropTable(
                name: "permission_groups");
        }
    }
}
