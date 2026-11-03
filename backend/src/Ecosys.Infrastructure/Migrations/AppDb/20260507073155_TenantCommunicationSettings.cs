using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class TenantCommunicationSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByUserId",
                table: "email_settings",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "OverrideSmtpSettings",
                table: "email_settings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedByUserId",
                table: "email_settings",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "UsePlatformDefaults",
                table: "email_settings",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "tenant_notification_recipients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipientGroup = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_notification_recipients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tenant_notification_recipients_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tenant_notification_settings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    NotificationKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    EmailEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    InAppEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    SmsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_notification_settings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tenant_notification_settings_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tenant_notification_recipients_TenantId_RecipientGroup_Email",
                table: "tenant_notification_recipients",
                columns: new[] { "TenantId", "RecipientGroup", "Email" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tenant_notification_settings_TenantId_NotificationKey",
                table: "tenant_notification_settings",
                columns: new[] { "TenantId", "NotificationKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tenant_notification_recipients");

            migrationBuilder.DropTable(
                name: "tenant_notification_settings");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "email_settings");

            migrationBuilder.DropColumn(
                name: "OverrideSmtpSettings",
                table: "email_settings");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "email_settings");

            migrationBuilder.DropColumn(
                name: "UsePlatformDefaults",
                table: "email_settings");
        }
    }
}
