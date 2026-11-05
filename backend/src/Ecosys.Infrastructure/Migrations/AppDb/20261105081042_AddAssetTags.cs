using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class AddAssetTags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "NextTagNumber",
                table: "factory_settings",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "TagPrefix",
                table: "factory_settings",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "TAG");

            migrationBuilder.CreateTable(
                name: "asset_tags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    TagNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Symbology = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IssuedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RetiredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RetiredReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ReplacedByTagId = table.Column<Guid>(type: "uuid", nullable: true),
                    PrintCount = table.Column<int>(type: "integer", nullable: false),
                    LastPrintedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_asset_tags", x => x.Id);
                    table.ForeignKey(
                        name: "FK_asset_tags_assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_asset_tags_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_asset_tags_AssetId",
                table: "asset_tags",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_asset_tags_TenantId_AssetId_Active",
                table: "asset_tags",
                columns: new[] { "TenantId", "AssetId" },
                unique: true,
                filter: "\"Status\" = 'Active'");

            migrationBuilder.CreateIndex(
                name: "IX_asset_tags_TenantId_TagNumber",
                table: "asset_tags",
                columns: new[] { "TenantId", "TagNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_asset_tags_TenantId_UpdatedAt",
                table: "asset_tags",
                columns: new[] { "TenantId", "UpdatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "asset_tags");

            migrationBuilder.DropColumn(
                name: "NextTagNumber",
                table: "factory_settings");

            migrationBuilder.DropColumn(
                name: "TagPrefix",
                table: "factory_settings");
        }
    }
}
