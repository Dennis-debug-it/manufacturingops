using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class WorkOrderExecutionEvidenceReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "work_order_material_usages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    MaterialItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetId = table.Column<Guid>(type: "uuid", nullable: true),
                    QuantityUsed = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    UnitCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Chargeable = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    UsedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    UsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_work_order_material_usages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_work_order_material_usages_assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_work_order_material_usages_material_items_MaterialItemId",
                        column: x => x.MaterialItemId,
                        principalTable: "material_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_work_order_material_usages_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_work_order_material_usages_users_UsedByUserId",
                        column: x => x.UsedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_work_order_material_usages_work_orders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "work_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "work_order_photo_evidence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttachmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Caption = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IncludeInReport = table.Column<bool>(type: "boolean", nullable: false),
                    UploadedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_work_order_photo_evidence", x => x.Id);
                    table.ForeignKey(
                        name: "FK_work_order_photo_evidence_attachments_AttachmentId",
                        column: x => x.AttachmentId,
                        principalTable: "attachments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_work_order_photo_evidence_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_work_order_photo_evidence_users_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_work_order_photo_evidence_work_orders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "work_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "work_order_signatures",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    SignatureType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SignerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SignerRole = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    SignatureDataUrl = table.Column<string>(type: "character varying(32000)", maxLength: 32000, nullable: false),
                    Comment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CapturedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CapturedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_work_order_signatures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_work_order_signatures_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_work_order_signatures_users_CapturedByUserId",
                        column: x => x.CapturedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_work_order_signatures_work_orders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "work_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_work_order_material_usages_AssetId",
                table: "work_order_material_usages",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_work_order_material_usages_MaterialItemId",
                table: "work_order_material_usages",
                column: "MaterialItemId");

            migrationBuilder.CreateIndex(
                name: "IX_work_order_material_usages_TenantId_WorkOrderId_MaterialIte~",
                table: "work_order_material_usages",
                columns: new[] { "TenantId", "WorkOrderId", "MaterialItemId", "UsedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_work_order_material_usages_UsedByUserId",
                table: "work_order_material_usages",
                column: "UsedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_work_order_material_usages_WorkOrderId",
                table: "work_order_material_usages",
                column: "WorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_work_order_photo_evidence_AttachmentId",
                table: "work_order_photo_evidence",
                column: "AttachmentId");

            migrationBuilder.CreateIndex(
                name: "IX_work_order_photo_evidence_TenantId_WorkOrderId_UploadedAt",
                table: "work_order_photo_evidence",
                columns: new[] { "TenantId", "WorkOrderId", "UploadedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_work_order_photo_evidence_UploadedByUserId",
                table: "work_order_photo_evidence",
                column: "UploadedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_work_order_photo_evidence_WorkOrderId",
                table: "work_order_photo_evidence",
                column: "WorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_work_order_signatures_CapturedByUserId",
                table: "work_order_signatures",
                column: "CapturedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_work_order_signatures_TenantId_WorkOrderId_SignatureType",
                table: "work_order_signatures",
                columns: new[] { "TenantId", "WorkOrderId", "SignatureType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_work_order_signatures_WorkOrderId",
                table: "work_order_signatures",
                column: "WorkOrderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "work_order_material_usages");

            migrationBuilder.DropTable(
                name: "work_order_photo_evidence");

            migrationBuilder.DropTable(
                name: "work_order_signatures");
        }
    }
}
