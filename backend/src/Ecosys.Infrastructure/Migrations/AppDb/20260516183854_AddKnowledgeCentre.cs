using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class AddKnowledgeCentre : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "knowledge_categories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
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
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Slug = table.Column<string>(type: "character varying(140)", maxLength: 140, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
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
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    Slug = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    Summary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Body = table.Column<string>(type: "character varying(64000)", maxLength: 64000, nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Visibility = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SourceWorkOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
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
                name: "knowledge_article_tags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ArticleId = table.Column<Guid>(type: "uuid", nullable: false),
                    TagId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ArticleId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    Body = table.Column<string>(type: "character varying(64000)", maxLength: 64000, nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
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
                name: "IX_knowledge_tags_TenantId_Slug",
                table: "knowledge_tags",
                columns: new[] { "TenantId", "Slug" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "knowledge_article_tags");

            migrationBuilder.DropTable(
                name: "knowledge_article_versions");

            migrationBuilder.DropTable(
                name: "knowledge_tags");

            migrationBuilder.DropTable(
                name: "knowledge_articles");

            migrationBuilder.DropTable(
                name: "knowledge_categories");
        }
    }
}
