using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class AddNotificationInbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "PushEnabled",
                table: "user_notification_preferences",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            // Until now InAppEnabled was the push switch (there was no bell). Carry each saved choice
            // over to the new push flag, so nobody who turned push off starts getting it again.
            migrationBuilder.Sql("UPDATE user_notification_preferences SET \"PushEnabled\" = \"InAppEnabled\";");

            migrationBuilder.AddColumn<bool>(
                name: "WhatsAppEnabled",
                table: "user_notification_preferences",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    EntityType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: true),
                    Severity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ReadAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ArchivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PushPendingSince = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notifications", x => x.Id);
                    table.CheckConstraint("CK_notifications_severity", "\"Severity\" IN ('Info', 'Warning', 'Critical')");
                    table.ForeignKey(
                        name: "FK_notifications_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_notifications_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_notifications_CreatedAt",
                table: "notifications",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_notifications_PushPendingSince",
                table: "notifications",
                column: "PushPendingSince",
                filter: "\"PushPendingSince\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_notifications_TenantId_UserId_ArchivedAt_CreatedAt",
                table: "notifications",
                columns: new[] { "TenantId", "UserId", "ArchivedAt", "CreatedAt" },
                descending: new[] { false, false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_notifications_UserId",
                table: "notifications",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "notifications");

            migrationBuilder.DropColumn(
                name: "PushEnabled",
                table: "user_notification_preferences");

            migrationBuilder.DropColumn(
                name: "WhatsAppEnabled",
                table: "user_notification_preferences");
        }
    }
}
