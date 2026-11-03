using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class AddScheduledReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "scheduled_reports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ReportType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Frequency = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DayOfMonth = table.Column<int>(type: "integer", nullable: true),
                    DayOfWeek = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    TimeOfDay = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false, defaultValue: "08:00"),
                    DateRangePreset = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "LastMonth"),
                    GroupBy = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    ClientId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Recipients = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    LastRunAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NextRunAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastRunStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_scheduled_reports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_scheduled_reports_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_scheduled_reports_TenantId",
                table: "scheduled_reports",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_scheduled_reports_TenantId_IsActive",
                table: "scheduled_reports",
                columns: new[] { "TenantId", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "scheduled_reports");
        }
    }
}
