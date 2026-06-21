using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class Phase3WorkOrderControlledValues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE work_orders
                SET "Priority" = CASE TRIM(COALESCE("Priority", ''))
                    WHEN 'Low' THEN 'Low'
                    WHEN 'Medium' THEN 'Medium'
                    WHEN 'High' THEN 'High'
                    WHEN 'Critical' THEN 'Critical'
                    ELSE 'Medium'
                END;
                """);

            migrationBuilder.Sql(
                """
                UPDATE work_orders
                SET "WorkType" = CASE TRIM(COALESCE("WorkType", ''))
                    WHEN 'NewInstallation' THEN 'NewInstallation'
                    WHEN 'New Installation' THEN 'NewInstallation'
                    WHEN 'PreventiveMaintenance' THEN 'PreventiveMaintenance'
                    WHEN 'Preventive Maintenance' THEN 'PreventiveMaintenance'
                    WHEN 'CorrectiveMaintenance' THEN 'CorrectiveMaintenance'
                    WHEN 'Corrective Maintenance' THEN 'CorrectiveMaintenance'
                    WHEN 'BreakdownCorrective' THEN 'CorrectiveMaintenance'
                    WHEN 'Breakdown / Corrective Maintenance' THEN 'CorrectiveMaintenance'
                    WHEN 'InspectionAssessment' THEN 'InspectionAssessment'
                    WHEN 'Inspection / Assessment' THEN 'InspectionAssessment'
                    ELSE 'CorrectiveMaintenance'
                END;
                """);

            migrationBuilder.Sql(
                """
                UPDATE work_orders
                SET "Status" = CASE TRIM(COALESCE("Status", ''))
                    WHEN 'Open' THEN 'Open'
                    WHEN 'Assigned' THEN 'Assigned'
                    WHEN 'In Progress' THEN 'In Progress'
                    WHEN 'Completed' THEN 'Completed'
                    WHEN 'Closed' THEN 'Closed'
                    WHEN 'Cancelled' THEN 'Cancelled'
                    WHEN 'Pending Materials' THEN 'On Hold'
                    WHEN 'Awaiting Materials' THEN 'On Hold'
                    WHEN 'Awaiting Parts' THEN 'On Hold'
                    WHEN 'Waiting for Spares' THEN 'On Hold'
                    WHEN 'Awaiting Spares' THEN 'On Hold'
                    WHEN 'Pending Client' THEN 'On Hold'
                    WHEN 'Awaiting User' THEN 'On Hold'
                    WHEN 'Awaiting Client' THEN 'On Hold'
                    WHEN 'Awaiting Approval' THEN 'On Hold'
                    WHEN 'Awaiting Vendor' THEN 'On Hold'
                    WHEN 'Waiting for Access' THEN 'On Hold'
                    WHEN 'Awaiting Site Access' THEN 'On Hold'
                    WHEN 'Paused' THEN 'On Hold'
                    WHEN 'On Hold' THEN 'On Hold'
                    WHEN 'Acknowledged' THEN 'Closed'
                    ELSE 'Open'
                END;
                """);

            migrationBuilder.Sql(
                """
                UPDATE work_orders
                SET "HoldReason" = CASE
                    WHEN "HoldReason" IS NULL OR BTRIM("HoldReason") = '' THEN NULL
                    WHEN BTRIM("HoldReason") IN ('Awaiting Materials', 'Pending Materials', 'Awaiting Parts') THEN 'Awaiting Materials'
                    WHEN BTRIM("HoldReason") IN ('Awaiting Client', 'Pending Client', 'Awaiting User') THEN 'Awaiting Client'
                    WHEN BTRIM("HoldReason") = 'Awaiting Vendor' THEN 'Awaiting Vendor'
                    WHEN BTRIM("HoldReason") = 'Awaiting Approval' THEN 'Awaiting Approval'
                    WHEN BTRIM("HoldReason") IN ('Awaiting Site Access', 'Waiting for Access') THEN 'Awaiting Site Access'
                    WHEN BTRIM("HoldReason") IN ('Awaiting Spares', 'Waiting for Spares') THEN 'Awaiting Spares'
                    WHEN BTRIM("HoldReason") = 'Other' THEN 'Other'
                    ELSE 'Other'
                END;
                """);

            migrationBuilder.Sql(
                """
                UPDATE work_orders
                SET "AssignmentType" = CASE
                    WHEN BTRIM(COALESCE("AssignmentType", '')) IN ('Unassigned', 'IndividualTechnician', 'MultipleTechnicians', 'AssignmentGroup')
                        THEN BTRIM("AssignmentType")
                    WHEN "AssignedTechnicianIdsJson" IS NOT NULL AND BTRIM("AssignedTechnicianIdsJson") NOT IN ('', '[]')
                        THEN 'MultipleTechnicians'
                    WHEN "AssignedTechnicianId" IS NOT NULL
                        THEN 'IndividualTechnician'
                    WHEN "AssignmentGroupId" IS NOT NULL
                        THEN 'AssignmentGroup'
                    ELSE 'Unassigned'
                END;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_work_orders_assignment_type",
                table: "work_orders",
                sql: "\"AssignmentType\" IN ('Unassigned', 'IndividualTechnician', 'MultipleTechnicians', 'AssignmentGroup')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_work_orders_hold_reason",
                table: "work_orders",
                sql: "\"HoldReason\" IS NULL OR \"HoldReason\" IN ('Awaiting Materials', 'Awaiting Client', 'Awaiting Vendor', 'Awaiting Approval', 'Awaiting Site Access', 'Awaiting Spares', 'Other')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_work_orders_priority",
                table: "work_orders",
                sql: "\"Priority\" IN ('Low', 'Medium', 'High', 'Critical')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_work_orders_status",
                table: "work_orders",
                sql: "\"Status\" IN ('Open', 'Assigned', 'In Progress', 'On Hold', 'Completed', 'Closed', 'Cancelled')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_work_orders_work_type",
                table: "work_orders",
                sql: "\"WorkType\" IN ('NewInstallation', 'PreventiveMaintenance', 'CorrectiveMaintenance', 'InspectionAssessment')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_work_orders_assignment_type",
                table: "work_orders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_work_orders_hold_reason",
                table: "work_orders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_work_orders_priority",
                table: "work_orders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_work_orders_status",
                table: "work_orders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_work_orders_work_type",
                table: "work_orders");
        }
    }
}
