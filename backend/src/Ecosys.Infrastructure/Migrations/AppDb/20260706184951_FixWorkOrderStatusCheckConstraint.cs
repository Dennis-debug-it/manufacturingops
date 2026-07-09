using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class FixWorkOrderStatusCheckConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_work_orders_status",
                table: "work_orders");

            migrationBuilder.AddCheckConstraint(
                name: "CK_work_orders_status",
                table: "work_orders",
                sql: "\"Status\" IN ('New', 'Open', 'Assigned', 'Scheduled', 'In Progress', 'On Hold', 'Completed', 'Client Accepted', 'Closed', 'Cancelled', 'Rejected', 'Reopened')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_work_orders_status",
                table: "work_orders");

            migrationBuilder.AddCheckConstraint(
                name: "CK_work_orders_status",
                table: "work_orders",
                sql: "\"Status\" IN ('Open', 'Assigned', 'In Progress', 'On Hold', 'Completed', 'Closed', 'Cancelled')");
        }
    }
}
