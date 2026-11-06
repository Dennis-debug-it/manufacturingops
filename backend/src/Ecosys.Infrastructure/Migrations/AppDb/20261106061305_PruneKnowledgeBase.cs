using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class PruneKnowledgeBase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "knowledge_article_feedback");

            migrationBuilder.DropTable(
                name: "knowledge_article_tags");

            migrationBuilder.DropTable(
                name: "knowledge_article_versions");

            migrationBuilder.DropTable(
                name: "knowledge_document_chunks");

            migrationBuilder.DropTable(
                name: "knowledge_tags");

            migrationBuilder.DropTable(
                name: "knowledge_articles");

            migrationBuilder.DropTable(
                name: "knowledge_documents");

            migrationBuilder.DropTable(
                name: "knowledge_categories");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "knowledge_categories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    Icon = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsPublic = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_knowledge_categories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_knowledge_categories_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "knowledge_tags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Slug = table.Column<string>(type: "character varying(140)", maxLength: 140, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_knowledge_tags", x => x.Id);
                    table.ForeignKey(
                        name: "FK_knowledge_tags_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "knowledge_articles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceWorkOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Body = table.Column<string>(type: "character varying(64000)", maxLength: 64000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    HelpfulCount = table.Column<int>(type: "integer", nullable: false),
                    NotHelpfulCount = table.Column<int>(type: "integer", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Slug = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Summary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Title = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ViewCount = table.Column<int>(type: "integer", nullable: false),
                    Visibility = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_knowledge_articles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_knowledge_articles_knowledge_categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "knowledge_categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_knowledge_articles_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_knowledge_articles_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_knowledge_articles_users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_knowledge_articles_work_orders_SourceWorkOrderId",
                        column: x => x.SourceWorkOrderId,
                        principalTable: "work_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "knowledge_documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    LinkedAssetId = table.Column<Guid>(type: "uuid", nullable: true),
                    LinkedClientId = table.Column<Guid>(type: "uuid", nullable: true),
                    LinkedSiteId = table.Column<Guid>(type: "uuid", nullable: true),
                    LinkedSystemId = table.Column<Guid>(type: "uuid", nullable: true),
                    LinkedWorkOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UploadedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ExtractedText = table.Column<string>(type: "text", nullable: true),
                    FileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    FileType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    StoragePath = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    TagsJson = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false)
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
                name: "knowledge_article_feedback",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ArticleId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientPortalUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Comment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsHelpful = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_knowledge_article_feedback", x => x.Id);
                    table.ForeignKey(
                        name: "FK_knowledge_article_feedback_client_portal_users_ClientPortal~",
                        column: x => x.ClientPortalUserId,
                        principalTable: "client_portal_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_knowledge_article_feedback_knowledge_articles_ArticleId",
                        column: x => x.ArticleId,
                        principalTable: "knowledge_articles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_knowledge_article_feedback_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "knowledge_article_tags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ArticleId = table.Column<Guid>(type: "uuid", nullable: false),
                    TagId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_knowledge_article_tags", x => x.Id);
                    table.ForeignKey(
                        name: "FK_knowledge_article_tags_knowledge_articles_ArticleId",
                        column: x => x.ArticleId,
                        principalTable: "knowledge_articles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_knowledge_article_tags_knowledge_tags_TagId",
                        column: x => x.TagId,
                        principalTable: "knowledge_tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_knowledge_article_tags_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "knowledge_article_versions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ArticleId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Body = table.Column<string>(type: "character varying(64000)", maxLength: 64000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Title = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_knowledge_article_versions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_knowledge_article_versions_knowledge_articles_ArticleId",
                        column: x => x.ArticleId,
                        principalTable: "knowledge_articles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_knowledge_article_versions_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_knowledge_article_versions_users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "knowledge_document_chunks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ArticleId = table.Column<Guid>(type: "uuid", nullable: true),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChunkOrder = table.Column<int>(type: "integer", nullable: false),
                    ChunkText = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PageNumber = table.Column<int>(type: "integer", nullable: true),
                    SectionTitle = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
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
                name: "IX_knowledge_article_feedback_ArticleId",
                table: "knowledge_article_feedback",
                column: "ArticleId");

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_article_feedback_ClientPortalUserId",
                table: "knowledge_article_feedback",
                column: "ClientPortalUserId");

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_article_feedback_TenantId_ArticleId",
                table: "knowledge_article_feedback",
                columns: new[] { "TenantId", "ArticleId" });

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_article_tags_ArticleId_TagId",
                table: "knowledge_article_tags",
                columns: new[] { "ArticleId", "TagId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_article_tags_TagId",
                table: "knowledge_article_tags",
                column: "TagId");

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_article_tags_TenantId",
                table: "knowledge_article_tags",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_article_versions_ArticleId_VersionNumber",
                table: "knowledge_article_versions",
                columns: new[] { "ArticleId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_article_versions_TenantId",
                table: "knowledge_article_versions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_article_versions_UpdatedByUserId",
                table: "knowledge_article_versions",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_articles_CategoryId",
                table: "knowledge_articles",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_articles_CreatedByUserId",
                table: "knowledge_articles",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_articles_SourceWorkOrderId",
                table: "knowledge_articles",
                column: "SourceWorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_articles_TenantId_Slug",
                table: "knowledge_articles",
                columns: new[] { "TenantId", "Slug" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_articles_UpdatedByUserId",
                table: "knowledge_articles",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_categories_TenantId_Name",
                table: "knowledge_categories",
                columns: new[] { "TenantId", "Name" },
                unique: true);

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
                name: "IX_knowledge_tags_TenantId_Slug",
                table: "knowledge_tags",
                columns: new[] { "TenantId", "Slug" },
                unique: true);
        }
    }
}
