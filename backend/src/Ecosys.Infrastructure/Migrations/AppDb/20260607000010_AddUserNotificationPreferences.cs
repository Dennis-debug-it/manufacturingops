using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class AddUserNotificationPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS user_notification_preferences (
                    "Id"              uuid         NOT NULL DEFAULT gen_random_uuid(),
                    "TenantId"        uuid         NOT NULL,
                    "UserId"          uuid         NOT NULL,
                    "NotificationKey" varchar(120) NOT NULL,
                    "EmailEnabled"    boolean      NOT NULL DEFAULT true,
                    "InAppEnabled"    boolean      NOT NULL DEFAULT true,
                    "CreatedAt"       timestamptz  NOT NULL DEFAULT now(),
                    "UpdatedAt"       timestamptz  NULL,
                    CONSTRAINT pk_user_notification_preferences PRIMARY KEY ("Id"),
                    CONSTRAINT fk_unp_user FOREIGN KEY ("UserId") REFERENCES users("Id") ON DELETE CASCADE
                );
                CREATE UNIQUE INDEX IF NOT EXISTS ix_unp_tenant_user_key
                    ON user_notification_preferences ("TenantId", "UserId", "NotificationKey");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TABLE IF EXISTS user_notification_preferences;
                """);
        }
    }
}
