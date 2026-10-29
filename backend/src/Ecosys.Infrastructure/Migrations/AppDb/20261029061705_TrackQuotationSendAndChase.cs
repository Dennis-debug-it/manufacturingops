using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class TrackQuotationSendAndChase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ChaseReminderStage",
                table: "tenant_quotations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "SentAt",
                table: "tenant_quotations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SentByUserId",
                table: "tenant_quotations",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ChaseReminderStage",
                table: "tenant_quotations");

            migrationBuilder.DropColumn(
                name: "SentAt",
                table: "tenant_quotations");

            migrationBuilder.DropColumn(
                name: "SentByUserId",
                table: "tenant_quotations");
        }
    }
}
