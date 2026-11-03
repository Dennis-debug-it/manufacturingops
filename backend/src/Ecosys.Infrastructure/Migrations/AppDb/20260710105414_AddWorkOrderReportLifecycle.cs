using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class AddWorkOrderReportLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ReportApprovedAt",
                table: "work_orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReportGeneratedAt",
                table: "work_orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReportNumber",
                table: "work_orders",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReportSentAt",
                table: "work_orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReportStatus",
                table: "work_orders",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReportApprovedAt",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "ReportGeneratedAt",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "ReportNumber",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "ReportSentAt",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "ReportStatus",
                table: "work_orders");
        }
    }
}
