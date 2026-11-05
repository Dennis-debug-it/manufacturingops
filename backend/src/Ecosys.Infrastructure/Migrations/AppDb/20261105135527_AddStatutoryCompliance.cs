using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class AddStatutoryCompliance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "compliance_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgNodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Regulation = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IntervalMonths = table.Column<int>(type: "integer", nullable: false),
                    LastDoneAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Provider = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RaiseJobDaysBefore = table.Column<int>(type: "integer", nullable: true),
                    OpenWorkOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    TemplateKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LastDueAlertDays = table.Column<int>(type: "integer", nullable: true),
                    LastOverdueAlertAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_compliance_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_compliance_items_assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_compliance_items_org_nodes_OrgNodeId",
                        column: x => x.OrgNodeId,
                        principalTable: "org_nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_compliance_items_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_compliance_items_users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_compliance_items_work_orders_OpenWorkOrderId",
                        column: x => x.OpenWorkOrderId,
                        principalTable: "work_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "compliance_records",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    DoneAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Result = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ValidUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CertificateNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Examiner = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CertificateAttachmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    WorkOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    RecordedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_compliance_records", x => x.Id);
                    table.ForeignKey(
                        name: "FK_compliance_records_attachments_CertificateAttachmentId",
                        column: x => x.CertificateAttachmentId,
                        principalTable: "attachments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_compliance_records_compliance_items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "compliance_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_compliance_records_work_orders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "work_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_compliance_items_AssetId",
                table: "compliance_items",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_compliance_items_OpenWorkOrderId",
                table: "compliance_items",
                column: "OpenWorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_compliance_items_OrgNodeId",
                table: "compliance_items",
                column: "OrgNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_compliance_items_OwnerUserId",
                table: "compliance_items",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_compliance_items_TenantId_IsActive_DueDate",
                table: "compliance_items",
                columns: new[] { "TenantId", "IsActive", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_compliance_items_TenantId_OrgNodeId_DueDate",
                table: "compliance_items",
                columns: new[] { "TenantId", "OrgNodeId", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_compliance_records_CertificateAttachmentId",
                table: "compliance_records",
                column: "CertificateAttachmentId");

            migrationBuilder.CreateIndex(
                name: "IX_compliance_records_ItemId",
                table: "compliance_records",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_compliance_records_TenantId_ItemId_DoneAt",
                table: "compliance_records",
                columns: new[] { "TenantId", "ItemId", "DoneAt" });

            migrationBuilder.CreateIndex(
                name: "IX_compliance_records_WorkOrderId",
                table: "compliance_records",
                column: "WorkOrderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "compliance_records");

            migrationBuilder.DropTable(
                name: "compliance_items");
        }
    }
}
