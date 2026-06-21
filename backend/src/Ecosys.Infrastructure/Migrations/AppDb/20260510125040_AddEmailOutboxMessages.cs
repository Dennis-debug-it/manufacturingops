using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class AddEmailOutboxMessages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AttemptCount",
                table: "email_delivery_logs",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastAttemptAt",
                table: "email_delivery_logs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NextAttemptAt",
                table: "email_delivery_logs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OutboxMessageId",
                table: "email_delivery_logs",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "email_outbox_messages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    EventKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    TemplateKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    RecipientEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    RecipientName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SenderName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SenderEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ReplyToEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Subject = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    HtmlBody = table.Column<string>(type: "character varying(64000)", maxLength: 64000, nullable: true),
                    TextBody = table.Column<string>(type: "character varying(32000)", maxLength: 32000, nullable: true),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    MaxAttempts = table.Column<int>(type: "integer", nullable: false),
                    NextAttemptAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastAttemptAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ErrorCategory = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    TriggeredByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_email_outbox_messages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_email_outbox_messages_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_email_delivery_logs_OutboxMessageId",
                table: "email_delivery_logs",
                column: "OutboxMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_email_outbox_messages_CreatedAt",
                table: "email_outbox_messages",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_email_outbox_messages_Status_NextAttemptAt",
                table: "email_outbox_messages",
                columns: new[] { "Status", "NextAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "IX_email_outbox_messages_TenantId",
                table: "email_outbox_messages",
                column: "TenantId");

            migrationBuilder.AddForeignKey(
                name: "FK_email_delivery_logs_email_outbox_messages_OutboxMessageId",
                table: "email_delivery_logs",
                column: "OutboxMessageId",
                principalTable: "email_outbox_messages",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_email_delivery_logs_email_outbox_messages_OutboxMessageId",
                table: "email_delivery_logs");

            migrationBuilder.DropTable(
                name: "email_outbox_messages");

            migrationBuilder.DropIndex(
                name: "IX_email_delivery_logs_OutboxMessageId",
                table: "email_delivery_logs");

            migrationBuilder.DropColumn(
                name: "AttemptCount",
                table: "email_delivery_logs");

            migrationBuilder.DropColumn(
                name: "LastAttemptAt",
                table: "email_delivery_logs");

            migrationBuilder.DropColumn(
                name: "NextAttemptAt",
                table: "email_delivery_logs");

            migrationBuilder.DropColumn(
                name: "OutboxMessageId",
                table: "email_delivery_logs");
        }
    }
}
