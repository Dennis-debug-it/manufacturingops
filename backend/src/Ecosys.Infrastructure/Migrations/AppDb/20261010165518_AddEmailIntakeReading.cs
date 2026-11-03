using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class AddEmailIntakeReading : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AutoReplyEnabled",
                table: "email_intake_settings",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<long>(
                name: "ImapLastUid",
                table: "email_intake_settings",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ImapUidValidity",
                table: "email_intake_settings",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "email_intake_messages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    MessageId = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    FromAddress = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    FromName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Subject = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Outcome = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Detail = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    WorkOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: true),
                    AutoReplySent = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_email_intake_messages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_email_intake_messages_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_email_intake_messages_work_orders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "work_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_email_intake_messages_TenantId_CreatedAt",
                table: "email_intake_messages",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_email_intake_messages_TenantId_MessageId",
                table: "email_intake_messages",
                columns: new[] { "TenantId", "MessageId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_email_intake_messages_WorkOrderId",
                table: "email_intake_messages",
                column: "WorkOrderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "email_intake_messages");

            migrationBuilder.DropColumn(
                name: "AutoReplyEnabled",
                table: "email_intake_settings");

            migrationBuilder.DropColumn(
                name: "ImapLastUid",
                table: "email_intake_settings");

            migrationBuilder.DropColumn(
                name: "ImapUidValidity",
                table: "email_intake_settings");
        }
    }
}
