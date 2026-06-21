using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EndOfDayCorrectiveSprint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SystemId",
                table: "work_orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SystemId",
                table: "material_issues",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SystemId",
                table: "assets",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "systems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    SiteId = table.Column<Guid>(type: "uuid", nullable: false),
                    SystemName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SystemType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Manufacturer = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Model = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    SerialNumber = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    InstallationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_systems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_systems_clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_systems_sites_SiteId",
                        column: x => x.SiteId,
                        principalTable: "sites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_systems_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "knowledge_documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    TagsJson = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    LinkedClientId = table.Column<Guid>(type: "uuid", nullable: true),
                    LinkedSiteId = table.Column<Guid>(type: "uuid", nullable: true),
                    LinkedSystemId = table.Column<Guid>(type: "uuid", nullable: true),
                    LinkedAssetId = table.Column<Guid>(type: "uuid", nullable: true),
                    LinkedWorkOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    UploadedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    FileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    FileType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    StoragePath = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ExtractedText = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_knowledge_documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_knowledge_documents_assets_LinkedAssetId",
                        column: x => x.LinkedAssetId,
                        principalTable: "assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_knowledge_documents_clients_LinkedClientId",
                        column: x => x.LinkedClientId,
                        principalTable: "clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_knowledge_documents_knowledge_categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "knowledge_categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_knowledge_documents_sites_LinkedSiteId",
                        column: x => x.LinkedSiteId,
                        principalTable: "sites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_knowledge_documents_systems_LinkedSystemId",
                        column: x => x.LinkedSystemId,
                        principalTable: "systems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_knowledge_documents_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_knowledge_documents_users_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_knowledge_documents_work_orders_LinkedWorkOrderId",
                        column: x => x.LinkedWorkOrderId,
                        principalTable: "work_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "knowledge_document_chunks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ArticleId = table.Column<Guid>(type: "uuid", nullable: true),
                    PageNumber = table.Column<int>(type: "integer", nullable: true),
                    SectionTitle = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ChunkText = table.Column<string>(type: "text", nullable: false),
                    ChunkOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_knowledge_document_chunks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_knowledge_document_chunks_knowledge_articles_ArticleId",
                        column: x => x.ArticleId,
                        principalTable: "knowledge_articles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_knowledge_document_chunks_knowledge_documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "knowledge_documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_knowledge_document_chunks_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_work_orders_SystemId",
                table: "work_orders",
                column: "SystemId");

            migrationBuilder.CreateIndex(
                name: "IX_material_issues_SystemId",
                table: "material_issues",
                column: "SystemId");

            migrationBuilder.CreateIndex(
                name: "IX_assets_SystemId",
                table: "assets",
                column: "SystemId");

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_document_chunks_ArticleId",
                table: "knowledge_document_chunks",
                column: "ArticleId");

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_document_chunks_DocumentId",
                table: "knowledge_document_chunks",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_document_chunks_TenantId_DocumentId_ChunkOrder",
                table: "knowledge_document_chunks",
                columns: new[] { "TenantId", "DocumentId", "ChunkOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_documents_CategoryId",
                table: "knowledge_documents",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_documents_LinkedAssetId",
                table: "knowledge_documents",
                column: "LinkedAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_documents_LinkedClientId",
                table: "knowledge_documents",
                column: "LinkedClientId");

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_documents_LinkedSiteId",
                table: "knowledge_documents",
                column: "LinkedSiteId");

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_documents_LinkedSystemId",
                table: "knowledge_documents",
                column: "LinkedSystemId");

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_documents_LinkedWorkOrderId",
                table: "knowledge_documents",
                column: "LinkedWorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_documents_TenantId_Title_Version",
                table: "knowledge_documents",
                columns: new[] { "TenantId", "Title", "Version" });

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_documents_UploadedByUserId",
                table: "knowledge_documents",
                column: "UploadedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_systems_ClientId",
                table: "systems",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_systems_SiteId",
                table: "systems",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "IX_systems_TenantId_SiteId_SystemName",
                table: "systems",
                columns: new[] { "TenantId", "SiteId", "SystemName" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_assets_systems_SystemId",
                table: "assets",
                column: "SystemId",
                principalTable: "systems",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_material_issues_systems_SystemId",
                table: "material_issues",
                column: "SystemId",
                principalTable: "systems",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_work_orders_systems_SystemId",
                table: "work_orders",
                column: "SystemId",
                principalTable: "systems",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_assets_systems_SystemId",
                table: "assets");

            migrationBuilder.DropForeignKey(
                name: "FK_material_issues_systems_SystemId",
                table: "material_issues");

            migrationBuilder.DropForeignKey(
                name: "FK_work_orders_systems_SystemId",
                table: "work_orders");

            migrationBuilder.DropTable(
                name: "knowledge_document_chunks");

            migrationBuilder.DropTable(
                name: "knowledge_documents");

            migrationBuilder.DropTable(
                name: "systems");

            migrationBuilder.DropIndex(
                name: "IX_work_orders_SystemId",
                table: "work_orders");

            migrationBuilder.DropIndex(
                name: "IX_material_issues_SystemId",
                table: "material_issues");

            migrationBuilder.DropIndex(
                name: "IX_assets_SystemId",
                table: "assets");

            migrationBuilder.DropColumn(
                name: "SystemId",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "SystemId",
                table: "material_issues");

            migrationBuilder.DropColumn(
                name: "SystemId",
                table: "assets");
        }
    }
}
