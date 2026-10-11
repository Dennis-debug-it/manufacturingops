using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class AddAssignmentLastArrival : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastArrivalAt",
                table: "work_order_technician_assignments",
                type: "timestamp with time zone",
                nullable: true);

            // Backfill: the latest arrival from the visit log, or the first arrival where no visit exists.
            if (ActiveProvider == "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                migrationBuilder.Sql("""
                    UPDATE work_order_technician_assignments a
                    SET "LastArrivalAt" = COALESCE(
                        (SELECT MAX(v."ArrivedAt") FROM work_order_visits v
                         WHERE v."WorkOrderId" = a."WorkOrderId" AND v."TechnicianId" = a."TechnicianId"),
                        a."ArrivalAt")
                    WHERE a."ArrivalAt" IS NOT NULL;
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastArrivalAt",
                table: "work_order_technician_assignments");
        }
    }
}
