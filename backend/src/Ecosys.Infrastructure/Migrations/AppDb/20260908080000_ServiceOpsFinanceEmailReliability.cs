using System;
using Ecosys.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb;

[DbContext(typeof(AppDbContext))]
[Migration("20260908080000_ServiceOpsFinanceEmailReliability")]
public partial class ServiceOpsFinanceEmailReliability : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "CcAddressesJson", table: "email_outbox_messages", type: "text", nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "AttachmentsJson", table: "email_outbox_messages", type: "text", nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "DocumentType", table: "email_outbox_messages", type: "character varying(80)", maxLength: 80, nullable: true);
        migrationBuilder.AddColumn<Guid>(
            name: "DocumentId", table: "email_outbox_messages", type: "uuid", nullable: true);
        migrationBuilder.AddColumn<bool>(
            name: "TriggeredAutomatically", table: "email_outbox_messages", type: "boolean", nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<string>(
            name: "IdempotencyKey", table: "email_outbox_messages", type: "character varying(200)", maxLength: 200, nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "CcAddressesJson", table: "email_delivery_logs", type: "text", nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "DocumentType", table: "email_delivery_logs", type: "character varying(80)", maxLength: 80, nullable: true);
        migrationBuilder.AddColumn<Guid>(
            name: "DocumentId", table: "email_delivery_logs", type: "uuid", nullable: true);
        migrationBuilder.AddColumn<bool>(
            name: "TriggeredAutomatically", table: "email_delivery_logs", type: "boolean", nullable: false, defaultValue: false);

        migrationBuilder.CreateIndex(
            name: "IX_email_outbox_messages_IdempotencyKey",
            table: "email_outbox_messages",
            column: "IdempotencyKey",
            unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_email_outbox_messages_TenantId_DocumentType_DocumentId",
            table: "email_outbox_messages",
            columns: new[] { "TenantId", "DocumentType", "DocumentId" });
        migrationBuilder.CreateIndex(
            name: "IX_email_delivery_logs_TenantId_DocumentType_DocumentId",
            table: "email_delivery_logs",
            columns: new[] { "TenantId", "DocumentType", "DocumentId" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_email_outbox_messages_IdempotencyKey", table: "email_outbox_messages");
        migrationBuilder.DropIndex(name: "IX_email_outbox_messages_TenantId_DocumentType_DocumentId", table: "email_outbox_messages");
        migrationBuilder.DropIndex(name: "IX_email_delivery_logs_TenantId_DocumentType_DocumentId", table: "email_delivery_logs");

        migrationBuilder.DropColumn(name: "CcAddressesJson", table: "email_outbox_messages");
        migrationBuilder.DropColumn(name: "AttachmentsJson", table: "email_outbox_messages");
        migrationBuilder.DropColumn(name: "DocumentType", table: "email_outbox_messages");
        migrationBuilder.DropColumn(name: "DocumentId", table: "email_outbox_messages");
        migrationBuilder.DropColumn(name: "TriggeredAutomatically", table: "email_outbox_messages");
        migrationBuilder.DropColumn(name: "IdempotencyKey", table: "email_outbox_messages");
        migrationBuilder.DropColumn(name: "CcAddressesJson", table: "email_delivery_logs");
        migrationBuilder.DropColumn(name: "DocumentType", table: "email_delivery_logs");
        migrationBuilder.DropColumn(name: "DocumentId", table: "email_delivery_logs");
        migrationBuilder.DropColumn(name: "TriggeredAutomatically", table: "email_delivery_logs");
    }
}
