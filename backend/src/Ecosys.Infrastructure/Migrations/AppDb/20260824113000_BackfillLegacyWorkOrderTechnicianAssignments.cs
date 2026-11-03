using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <summary>
    /// EC-019 — data-only backfill. <c>work_order_technician_assignments</c> is the source of truth
    /// for work order assignment, but rows written straight to the legacy
    /// <c>work_orders."AssignedTechnicianId"</c> column (the demo seeder, the dispatch board's
    /// schedule endpoint, anything created after the 20260502 assignment refactor by a writer that
    /// bypasses the assignment workflow) never got an assignment row, so the API reported a named
    /// technician and "Unassigned" for the same work order.
    ///
    /// This migration promotes every one of those stand-ins into a real assignment row, adds the
    /// matching work order assignment row, and (EC-026) repairs assignment rows whose status still
    /// contradicts a terminal work order status.
    /// </summary>
    public partial class BackfillLegacyWorkOrderTechnicianAssignments : Migration
    {
        private const string BackfillNote = "Backfilled from legacy work order assigned technician column.";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Legacy assigned technician -> technician assignment row, for work orders that have
            //    no assignment row at all. Work orders that already have rows are left untouched:
            //    the table wins.
            migrationBuilder.Sql($"""
                INSERT INTO work_order_technician_assignments
                ("Id", "TenantId", "WorkOrderId", "TechnicianId", "IsLead", "AssignedByUserId", "AssignedAt", "Status", "Notes", "AcceptedAt", "DeclinedAt", "ArrivalAt", "DepartureAt", "CreatedAt", "UpdatedAt")
                SELECT
                    gen_random_uuid(),
                    work_orders."TenantId",
                    work_orders."Id",
                    COALESCE(work_orders."LeadTechnicianId", work_orders."AssignedTechnicianId"),
                    TRUE,
                    NULL::uuid,
                    work_orders."CreatedAt",
                    CASE
                        WHEN work_orders."Status" IN ('Completed', 'Client Accepted', 'Acknowledged', 'Closed') THEN 'Completed'
                        WHEN work_orders."Status" IN ('Cancelled', 'Rejected') THEN 'Cancelled'
                        WHEN work_orders."ArrivalAt" IS NOT NULL THEN 'Arrived'
                        WHEN work_orders."Status" = 'In Progress' THEN 'InProgress'
                        ELSE 'Pending'
                    END,
                    '{BackfillNote}',
                    NULL::timestamp with time zone,
                    NULL::timestamp with time zone,
                    work_orders."ArrivalAt",
                    work_orders."DepartureAt",
                    work_orders."CreatedAt",
                    NULL::timestamp with time zone
                FROM work_orders
                WHERE work_orders."AssignedTechnicianId" IS NOT NULL
                  AND NOT EXISTS (
                      SELECT 1
                      FROM work_order_technician_assignments existing
                      WHERE existing."WorkOrderId" = work_orders."Id"
                  )
                  AND EXISTS (
                      SELECT 1
                      FROM technicians
                      WHERE technicians."Id" = COALESCE(work_orders."LeadTechnicianId", work_orders."AssignedTechnicianId")
                  );
                """);

            // 2. Keep the legacy column derived from the table (the lead row is the primary
            //    technician), exactly as WorkOrderAssignmentWorkflowService.SyncLegacyFieldsAsync
            //    does for every write that goes through the assignment workflow.
            migrationBuilder.Sql("""
                UPDATE work_orders
                SET "AssignedTechnicianId" = primary_assignment."TechnicianId",
                    "AssignedTechnicianIdsJson" = NULL
                FROM (
                    SELECT DISTINCT ON (assignments."WorkOrderId")
                        assignments."WorkOrderId",
                        assignments."TechnicianId"
                    FROM work_order_technician_assignments assignments
                    ORDER BY assignments."WorkOrderId", assignments."IsLead" DESC, assignments."AssignedAt"
                ) AS primary_assignment
                WHERE primary_assignment."WorkOrderId" = work_orders."Id"
                  AND work_orders."AssignedTechnicianId" IS DISTINCT FROM primary_assignment."TechnicianId";
                """);

            // 3. Work orders with an assignment but no work order assignment row get one, so the
            //    assignment status has somewhere to live.
            migrationBuilder.Sql($"""
                INSERT INTO work_order_assignments
                ("Id", "TenantId", "WorkOrderId", "AssignmentGroupId", "AssignmentStatus", "AssignedByUserId", "AssignedAt", "Notes", "CreatedAt", "UpdatedAt")
                SELECT
                    gen_random_uuid(),
                    work_orders."TenantId",
                    work_orders."Id",
                    work_orders."AssignmentGroupId",
                    CASE
                        WHEN work_orders."Status" IN ('Completed', 'Client Accepted', 'Acknowledged') THEN 'Completed'
                        WHEN work_orders."Status" = 'Closed' THEN 'Closed'
                        WHEN work_orders."Status" IN ('Cancelled', 'Rejected') THEN 'Cancelled'
                        WHEN work_orders."Status" IN ('On Hold', 'Waiting on Parts', 'Waiting on Client', 'Paused') THEN 'OnHold'
                        WHEN work_orders."Status" = 'In Progress' THEN 'InProgress'
                        WHEN EXISTS (
                            SELECT 1
                            FROM work_order_technician_assignments assignments
                            WHERE assignments."WorkOrderId" = work_orders."Id"
                        ) THEN 'AssignedToTechnician'
                        WHEN work_orders."AssignmentGroupId" IS NOT NULL THEN 'AssignedToGroup'
                        ELSE 'Unassigned'
                    END,
                    NULL::uuid,
                    work_orders."CreatedAt",
                    '{BackfillNote}',
                    work_orders."CreatedAt",
                    NULL::timestamp with time zone
                FROM work_orders
                WHERE NOT EXISTS (
                        SELECT 1
                        FROM work_order_assignments existing
                        WHERE existing."WorkOrderId" = work_orders."Id"
                    )
                  AND (
                        work_orders."AssignmentGroupId" IS NOT NULL
                        OR EXISTS (
                            SELECT 1
                            FROM work_order_technician_assignments assignments
                            WHERE assignments."WorkOrderId" = work_orders."Id"
                        )
                    );
                """);

            // 4. EC-026 — historical rows whose assignment lifecycle was never finalised: a closed,
            //    completed or cancelled work order cannot still be "Arrived"/"InProgress".
            migrationBuilder.Sql("""
                UPDATE work_order_technician_assignments
                SET "Status" = CASE
                        WHEN work_orders."Status" IN ('Cancelled', 'Rejected') THEN 'Cancelled'
                        ELSE 'Completed'
                    END,
                    "DepartureAt" = CASE
                        WHEN work_order_technician_assignments."DepartureAt" IS NULL
                             AND work_order_technician_assignments."ArrivalAt" IS NOT NULL
                             AND work_orders."Status" NOT IN ('Cancelled', 'Rejected')
                        THEN COALESCE(work_orders."CompletedAt", work_orders."ClosedAt", work_order_technician_assignments."ArrivalAt")
                        ELSE work_order_technician_assignments."DepartureAt"
                    END
                FROM work_orders
                WHERE work_orders."Id" = work_order_technician_assignments."WorkOrderId"
                  AND work_orders."Status" IN ('Completed', 'Client Accepted', 'Acknowledged', 'Closed', 'Cancelled', 'Rejected')
                  AND work_order_technician_assignments."Status" NOT IN ('Completed', 'Cancelled', 'Declined');
                """);

            migrationBuilder.Sql("""
                UPDATE work_order_assignments
                SET "AssignmentStatus" = CASE
                        WHEN work_orders."Status" = 'Closed' THEN 'Closed'
                        WHEN work_orders."Status" IN ('Cancelled', 'Rejected') THEN 'Cancelled'
                        ELSE 'Completed'
                    END
                FROM work_orders
                WHERE work_orders."Id" = work_order_assignments."WorkOrderId"
                  AND work_orders."Status" IN ('Completed', 'Client Accepted', 'Acknowledged', 'Closed', 'Cancelled', 'Rejected')
                  AND work_order_assignments."AssignmentStatus" NOT IN ('Completed', 'Closed', 'Cancelled');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Only the rows this migration created can be undone; the status repairs above are not
            // reversible (the pre-repair values are not recorded anywhere).
            migrationBuilder.Sql($"""
                DELETE FROM work_order_assignments
                WHERE "Notes" = '{BackfillNote}';
                """);

            migrationBuilder.Sql($"""
                DELETE FROM work_order_technician_assignments
                WHERE "Notes" = '{BackfillNote}';
                """);
        }
    }
}
