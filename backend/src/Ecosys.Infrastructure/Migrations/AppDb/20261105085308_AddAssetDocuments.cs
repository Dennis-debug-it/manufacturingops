using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class AddAssetDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "asset_documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DocumentType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CurrentVersion = table.Column<int>(type: "integer", nullable: false),
                    ExpiryDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsArchived = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_asset_documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_asset_documents_assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_asset_documents_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "asset_document_versions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    AttachmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    UploadedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UploadedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_asset_document_versions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_asset_document_versions_asset_documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "asset_documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_asset_document_versions_attachments_AttachmentId",
                        column: x => x.AttachmentId,
                        principalTable: "attachments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_asset_document_versions_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_asset_document_versions_AttachmentId",
                table: "asset_document_versions",
                column: "AttachmentId");

            migrationBuilder.CreateIndex(
                name: "IX_asset_document_versions_DocumentId",
                table: "asset_document_versions",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_asset_document_versions_TenantId_DocumentId_VersionNumber",
                table: "asset_document_versions",
                columns: new[] { "TenantId", "DocumentId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_asset_documents_AssetId",
                table: "asset_documents",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_asset_documents_TenantId_AssetId",
                table: "asset_documents",
                columns: new[] { "TenantId", "AssetId" });

            migrationBuilder.CreateIndex(
                name: "IX_asset_documents_TenantId_UpdatedAt",
                table: "asset_documents",
                columns: new[] { "TenantId", "UpdatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "asset_document_versions");

            migrationBuilder.DropTable(
                name: "asset_documents");
        }
    }
}
