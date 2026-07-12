using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class ApprovalMatrix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "approval_matrices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_approval_matrices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_approval_matrices_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "approval_requests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovalMatrixId = table.Column<Guid>(type: "uuid", nullable: true),
                    DocumentType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CurrentLevelOrder = table.Column<int>(type: "integer", nullable: false),
                    InitiatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_approval_requests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "approval_levels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovalMatrixId = table.Column<Guid>(type: "uuid", nullable: false),
                    LevelOrder = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    MinAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    MaxAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    ApproverType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    RequiredPermission = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ApproverUserIdsCsv = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ReminderAfterHours = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_approval_levels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_approval_levels_approval_matrices_ApprovalMatrixId",
                        column: x => x.ApprovalMatrixId,
                        principalTable: "approval_matrices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "approval_request_steps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovalRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    LevelOrder = table.Column<int>(type: "integer", nullable: false),
                    LevelName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ApproverType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    RequiredPermission = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ApproverUserIdsCsv = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DecidedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DecidedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    DueAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReminderSentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_approval_request_steps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_approval_request_steps_approval_requests_ApprovalRequestId",
                        column: x => x.ApprovalRequestId,
                        principalTable: "approval_requests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_approval_levels_ApprovalMatrixId_LevelOrder",
                table: "approval_levels",
                columns: new[] { "ApprovalMatrixId", "LevelOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_approval_matrices_TenantId_DocumentType",
                table: "approval_matrices",
                columns: new[] { "TenantId", "DocumentType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_approval_request_steps_ApprovalRequestId",
                table: "approval_request_steps",
                column: "ApprovalRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_approval_request_steps_TenantId_Status",
                table: "approval_request_steps",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_approval_requests_TenantId_DocumentType_DocumentId",
                table: "approval_requests",
                columns: new[] { "TenantId", "DocumentType", "DocumentId" });

            migrationBuilder.CreateIndex(
                name: "IX_approval_requests_TenantId_Status",
                table: "approval_requests",
                columns: new[] { "TenantId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "approval_levels");

            migrationBuilder.DropTable(
                name: "approval_request_steps");

            migrationBuilder.DropTable(
                name: "approval_matrices");

            migrationBuilder.DropTable(
                name: "approval_requests");
        }
    }
}
