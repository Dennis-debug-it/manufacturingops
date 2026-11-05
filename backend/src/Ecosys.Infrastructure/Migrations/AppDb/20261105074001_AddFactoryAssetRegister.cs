using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class AddFactoryAssetRegister : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "ClientId",
                table: "assets",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "Criticality",
                table: "assets",
                type: "character varying(1)",
                maxLength: 1,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DisposalDate",
                table: "assets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DisposalMethod",
                table: "assets",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalId",
                table: "assets",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalSystem",
                table: "assets",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LostProductionPerHour",
                table: "assets",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OperationalStatus",
                table: "assets",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Operational");

            migrationBuilder.AddColumn<Guid>(
                name: "ParentAssetId",
                table: "assets",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "asset_lifecycle_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FromOrgNodeId = table.Column<Guid>(type: "uuid", nullable: true),
                    ToOrgNodeId = table.Column<Guid>(type: "uuid", nullable: true),
                    FromParentAssetId = table.Column<Guid>(type: "uuid", nullable: true),
                    ToParentAssetId = table.Column<Guid>(type: "uuid", nullable: true),
                    FromValue = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ToValue = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    PerformedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    PerformedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_asset_lifecycle_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_asset_lifecycle_events_assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_asset_lifecycle_events_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_assets_ParentAssetId",
                table: "assets",
                column: "ParentAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_assets_TenantId_ExternalSystem_ExternalId",
                table: "assets",
                columns: new[] { "TenantId", "ExternalSystem", "ExternalId" });

            migrationBuilder.CreateIndex(
                name: "IX_assets_TenantId_UpdatedAt",
                table: "assets",
                columns: new[] { "TenantId", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_asset_lifecycle_events_AssetId",
                table: "asset_lifecycle_events",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_asset_lifecycle_events_TenantId_AssetId_OccurredAt",
                table: "asset_lifecycle_events",
                columns: new[] { "TenantId", "AssetId", "OccurredAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_assets_assets_ParentAssetId",
                table: "assets",
                column: "ParentAssetId",
                principalTable: "assets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_assets_assets_ParentAssetId",
                table: "assets");

            migrationBuilder.DropTable(
                name: "asset_lifecycle_events");

            migrationBuilder.DropIndex(
                name: "IX_assets_ParentAssetId",
                table: "assets");

            migrationBuilder.DropIndex(
                name: "IX_assets_TenantId_ExternalSystem_ExternalId",
                table: "assets");

            migrationBuilder.DropIndex(
                name: "IX_assets_TenantId_UpdatedAt",
                table: "assets");

            migrationBuilder.DropColumn(
                name: "Criticality",
                table: "assets");

            migrationBuilder.DropColumn(
                name: "DisposalDate",
                table: "assets");

            migrationBuilder.DropColumn(
                name: "DisposalMethod",
                table: "assets");

            migrationBuilder.DropColumn(
                name: "ExternalId",
                table: "assets");

            migrationBuilder.DropColumn(
                name: "ExternalSystem",
                table: "assets");

            migrationBuilder.DropColumn(
                name: "LostProductionPerHour",
                table: "assets");

            migrationBuilder.DropColumn(
                name: "OperationalStatus",
                table: "assets");

            migrationBuilder.DropColumn(
                name: "ParentAssetId",
                table: "assets");

            migrationBuilder.AlterColumn<Guid>(
                name: "ClientId",
                table: "assets",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
