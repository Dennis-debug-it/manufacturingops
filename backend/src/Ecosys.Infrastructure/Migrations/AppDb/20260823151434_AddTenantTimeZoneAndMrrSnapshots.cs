using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class AddTenantTimeZoneAndMrrSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TimeZoneId",
                table: "tenants",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "mrr_snapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SnapshotDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Mrr = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ActiveTenants = table.Column<int>(type: "integer", nullable: false),
                    TrialTenants = table.Column<int>(type: "integer", nullable: false),
                    PaidTenants = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mrr_snapshots", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_mrr_snapshots_SnapshotDate",
                table: "mrr_snapshots",
                column: "SnapshotDate",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "mrr_snapshots");

            migrationBuilder.DropColumn(
                name: "TimeZoneId",
                table: "tenants");
        }
    }
}
