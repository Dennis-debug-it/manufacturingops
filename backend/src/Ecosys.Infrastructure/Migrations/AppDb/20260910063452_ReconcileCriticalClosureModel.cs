using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class ReconcileCriticalClosureModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_email_outbox_messages_TenantId",
                table: "email_outbox_messages");

            migrationBuilder.DropIndex(
                name: "IX_email_delivery_logs_TenantId",
                table: "email_delivery_logs");

            // The prior handwritten migration already creates this column, but
            // omitted it from its snapshot. Preserve both upgrade lineages.
            migrationBuilder.Sql("ALTER TABLE email_delivery_logs ADD COLUMN IF NOT EXISTS \"TriggeredAutomatically\" boolean NOT NULL DEFAULT false;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_email_outbox_messages_TenantId",
                table: "email_outbox_messages",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_email_delivery_logs_TenantId",
                table: "email_delivery_logs",
                column: "TenantId");
        }
    }
}
