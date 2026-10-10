using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class AddServiceReportEdits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ReportEditedAt",
                table: "work_orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReportEditedByUserId",
                table: "work_orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReportFindingsText",
                table: "work_orders",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReportProblemText",
                table: "work_orders",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReportWorkDoneText",
                table: "work_orders",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReportEditedAt",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "ReportEditedByUserId",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "ReportFindingsText",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "ReportProblemText",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "ReportWorkDoneText",
                table: "work_orders");
        }
    }
}
