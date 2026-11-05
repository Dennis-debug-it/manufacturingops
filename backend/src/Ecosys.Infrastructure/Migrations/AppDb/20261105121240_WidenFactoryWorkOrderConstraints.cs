using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class WidenFactoryWorkOrderConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_work_orders_hold_reason",
                table: "work_orders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_work_orders_status",
                table: "work_orders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_work_orders_work_type",
                table: "work_orders");

            migrationBuilder.AddCheckConstraint(
                name: "CK_work_orders_hold_reason",
                table: "work_orders",
                sql: "\"HoldReason\" IS NULL OR \"HoldReason\" IN ('Awaiting Materials', 'Awaiting Client', 'Awaiting Vendor', 'Awaiting Approval', 'Awaiting Site Access', 'Awaiting Spares', 'Other', 'Waiting for Window', 'Waiting for Shutdown', 'Waiting for Contractor')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_work_orders_status",
                table: "work_orders",
                sql: "\"Status\" IN ('New', 'Open', 'Assigned', 'Scheduled', 'In Progress', 'On Hold', 'Waiting on Parts', 'Waiting on Client', 'Completed', 'Client Accepted', 'Closed', 'Cancelled', 'Rejected', 'Reopened', 'Requested', 'Approved', 'Planned', 'Verified')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_work_orders_work_type",
                table: "work_orders",
                sql: "\"WorkType\" IN ('NewInstallation', 'PreventiveMaintenance', 'CorrectiveMaintenance', 'InspectionAssessment', 'Corrective', 'Preventive', 'Inspection', 'Statutory', 'Modification', 'Safety')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_work_orders_hold_reason",
                table: "work_orders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_work_orders_status",
                table: "work_orders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_work_orders_work_type",
                table: "work_orders");

            migrationBuilder.AddCheckConstraint(
                name: "CK_work_orders_hold_reason",
                table: "work_orders",
                sql: "\"HoldReason\" IS NULL OR \"HoldReason\" IN ('Awaiting Materials', 'Awaiting Client', 'Awaiting Vendor', 'Awaiting Approval', 'Awaiting Site Access', 'Awaiting Spares', 'Other')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_work_orders_status",
                table: "work_orders",
                sql: "\"Status\" IN ('New', 'Open', 'Assigned', 'Scheduled', 'In Progress', 'On Hold', 'Waiting on Parts', 'Waiting on Client', 'Completed', 'Client Accepted', 'Closed', 'Cancelled', 'Rejected', 'Reopened')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_work_orders_work_type",
                table: "work_orders",
                sql: "\"WorkType\" IN ('NewInstallation', 'PreventiveMaintenance', 'CorrectiveMaintenance', 'InspectionAssessment')");
        }
    }
}
