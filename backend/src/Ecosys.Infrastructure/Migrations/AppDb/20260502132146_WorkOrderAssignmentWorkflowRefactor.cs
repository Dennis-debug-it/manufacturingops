using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class WorkOrderAssignmentWorkflowRefactor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_assignment_group_technicians_assignment_groups_AssignmentGr~",
                table: "assignment_group_technicians");

            migrationBuilder.DropForeignKey(
                name: "FK_assignment_group_technicians_technicians_TechnicianId",
                table: "assignment_group_technicians");

            migrationBuilder.DropForeignKey(
                name: "FK_assignment_group_technicians_tenants_TenantId",
                table: "assignment_group_technicians");

            migrationBuilder.DropPrimaryKey(
                name: "PK_assignment_group_technicians",
                table: "assignment_group_technicians");

            migrationBuilder.RenameTable(
                name: "assignment_group_technicians",
                newName: "assignment_group_members");

            migrationBuilder.RenameIndex(
                name: "IX_assignment_group_technicians_TenantId_AssignmentGroupId_Tec~",
                table: "assignment_group_members",
                newName: "IX_assignment_group_members_TenantId_AssignmentGroupId_Technic~");

            migrationBuilder.RenameIndex(
                name: "IX_assignment_group_technicians_TechnicianId",
                table: "assignment_group_members",
                newName: "IX_assignment_group_members_TechnicianId");

            migrationBuilder.RenameIndex(
                name: "IX_assignment_group_technicians_AssignmentGroupId",
                table: "assignment_group_members",
                newName: "IX_assignment_group_members_AssignmentGroupId");

            migrationBuilder.AddColumn<string>(
                name: "SkillArea",
                table: "assignment_groups",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AddedAt",
                table: "assignment_group_members",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "assignment_group_members",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsLead",
                table: "assignment_group_members",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddPrimaryKey(
                name: "PK_assignment_group_members",
                table: "assignment_group_members",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "work_order_assignment_history",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FromGroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    ToGroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    FromTechnicianId = table.Column<Guid>(type: "uuid", nullable: true),
                    ToTechnicianId = table.Column<Guid>(type: "uuid", nullable: true),
                    PerformedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    PerformedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_work_order_assignment_history", x => x.Id);
                    table.ForeignKey(
                        name: "FK_work_order_assignment_history_assignment_groups_FromGroupId",
                        column: x => x.FromGroupId,
                        principalTable: "assignment_groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_work_order_assignment_history_assignment_groups_ToGroupId",
                        column: x => x.ToGroupId,
                        principalTable: "assignment_groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_work_order_assignment_history_technicians_FromTechnicianId",
                        column: x => x.FromTechnicianId,
                        principalTable: "technicians",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_work_order_assignment_history_technicians_ToTechnicianId",
                        column: x => x.ToTechnicianId,
                        principalTable: "technicians",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_work_order_assignment_history_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_work_order_assignment_history_users_PerformedByUserId",
                        column: x => x.PerformedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_work_order_assignment_history_work_orders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "work_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "work_order_assignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignmentGroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssignmentStatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    AssignedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_work_order_assignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_work_order_assignments_assignment_groups_AssignmentGroupId",
                        column: x => x.AssignmentGroupId,
                        principalTable: "assignment_groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_work_order_assignments_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_work_order_assignments_users_AssignedByUserId",
                        column: x => x.AssignedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_work_order_assignments_work_orders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "work_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "work_order_technician_assignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    TechnicianId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsLead = table.Column<bool>(type: "boolean", nullable: false),
                    AssignedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    AcceptedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeclinedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ArrivalAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DepartureAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_work_order_technician_assignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_work_order_technician_assignments_technicians_TechnicianId",
                        column: x => x.TechnicianId,
                        principalTable: "technicians",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_work_order_technician_assignments_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_work_order_technician_assignments_users_AssignedByUserId",
                        column: x => x.AssignedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_work_order_technician_assignments_work_orders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "work_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_work_order_assignment_history_FromGroupId",
                table: "work_order_assignment_history",
                column: "FromGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_work_order_assignment_history_FromTechnicianId",
                table: "work_order_assignment_history",
                column: "FromTechnicianId");

            migrationBuilder.CreateIndex(
                name: "IX_work_order_assignment_history_PerformedByUserId",
                table: "work_order_assignment_history",
                column: "PerformedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_work_order_assignment_history_TenantId_WorkOrderId_Performe~",
                table: "work_order_assignment_history",
                columns: new[] { "TenantId", "WorkOrderId", "PerformedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_work_order_assignment_history_ToGroupId",
                table: "work_order_assignment_history",
                column: "ToGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_work_order_assignment_history_ToTechnicianId",
                table: "work_order_assignment_history",
                column: "ToTechnicianId");

            migrationBuilder.CreateIndex(
                name: "IX_work_order_assignment_history_WorkOrderId",
                table: "work_order_assignment_history",
                column: "WorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_work_order_assignments_AssignedByUserId",
                table: "work_order_assignments",
                column: "AssignedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_work_order_assignments_AssignmentGroupId",
                table: "work_order_assignments",
                column: "AssignmentGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_work_order_assignments_TenantId_AssignmentGroupId_Assignmen~",
                table: "work_order_assignments",
                columns: new[] { "TenantId", "AssignmentGroupId", "AssignmentStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_work_order_assignments_WorkOrderId",
                table: "work_order_assignments",
                column: "WorkOrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_work_order_technician_assignments_AssignedByUserId",
                table: "work_order_technician_assignments",
                column: "AssignedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_work_order_technician_assignments_TechnicianId",
                table: "work_order_technician_assignments",
                column: "TechnicianId");

            migrationBuilder.CreateIndex(
                name: "IX_work_order_technician_assignments_TenantId_TechnicianId_Sta~",
                table: "work_order_technician_assignments",
                columns: new[] { "TenantId", "TechnicianId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_work_order_technician_assignments_WorkOrderId_TechnicianId",
                table: "work_order_technician_assignments",
                columns: new[] { "WorkOrderId", "TechnicianId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_assignment_group_members_assignment_groups_AssignmentGroupId",
                table: "assignment_group_members",
                column: "AssignmentGroupId",
                principalTable: "assignment_groups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_assignment_group_members_technicians_TechnicianId",
                table: "assignment_group_members",
                column: "TechnicianId",
                principalTable: "technicians",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_assignment_group_members_tenants_TenantId",
                table: "assignment_group_members",
                column: "TenantId",
                principalTable: "tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.Sql("""
                UPDATE assignment_group_members
                SET "IsActive" = TRUE,
                    "AddedAt" = COALESCE("CreatedAt", NOW())
                WHERE "IsActive" = FALSE;
                """);

            migrationBuilder.Sql("""
                INSERT INTO work_order_technician_assignments
                ("Id", "TenantId", "WorkOrderId", "TechnicianId", "IsLead", "AssignedByUserId", "AssignedAt", "Status", "Notes", "AcceptedAt", "DeclinedAt", "ArrivalAt", "DepartureAt", "CreatedAt", "UpdatedAt")
                SELECT DISTINCT
                    gen_random_uuid(),
                    source."TenantId",
                    source."Id",
                    source."TechnicianId",
                    CASE
                        WHEN source."LeadTechnicianId" IS NOT NULL THEN source."TechnicianId" = source."LeadTechnicianId"
                        ELSE source."TechnicianId" = source."AssignedTechnicianId"
                    END,
                    NULL::uuid,
                    source."CreatedAt",
                    CASE
                        WHEN source."Status" IN ('Completed', 'Acknowledged', 'Closed') THEN 'Completed'
                        WHEN source."ArrivalAt" IS NOT NULL THEN 'Arrived'
                        WHEN source."Status" IN ('In Progress', 'Pending Materials') THEN 'InProgress'
                        ELSE 'Pending'
                    END,
                    NULL::text,
                    NULL::timestamp with time zone,
                    NULL::timestamp with time zone,
                    source."ArrivalAt",
                    source."DepartureAt",
                    source."CreatedAt",
                    source."UpdatedAt"
                FROM (
                    SELECT
                        work_orders."Id",
                        work_orders."TenantId",
                        work_orders."AssignedTechnicianId",
                        work_orders."LeadTechnicianId",
                        work_orders."Status",
                        work_orders."ArrivalAt",
                        work_orders."DepartureAt",
                        work_orders."CreatedAt",
                        work_orders."UpdatedAt",
                        COALESCE(assigned_from_json."TechnicianId", work_orders."AssignedTechnicianId") AS "TechnicianId"
                    FROM work_orders
                    LEFT JOIN LATERAL (
                        SELECT CAST(value AS uuid) AS "TechnicianId"
                        FROM jsonb_array_elements_text(COALESCE(work_orders."AssignedTechnicianIdsJson", '[]')::jsonb)
                    ) AS assigned_from_json ON TRUE
                    WHERE work_orders."AssignedTechnicianId" IS NOT NULL
                       OR work_orders."AssignedTechnicianIdsJson" IS NOT NULL
                ) AS source
                WHERE source."TechnicianId" IS NOT NULL;
                """);

            migrationBuilder.Sql("""
                INSERT INTO work_order_assignments
                ("Id", "TenantId", "WorkOrderId", "AssignmentGroupId", "AssignmentStatus", "AssignedByUserId", "AssignedAt", "Notes", "CreatedAt", "UpdatedAt")
                SELECT
                    gen_random_uuid(),
                    work_orders."TenantId",
                    work_orders."Id",
                    work_orders."AssignmentGroupId",
                    CASE
                        WHEN work_orders."Status" IN ('Completed', 'Acknowledged', 'Closed') THEN 'Completed'
                        WHEN work_orders."Status" = 'Cancelled' THEN 'Cancelled'
                        WHEN work_orders."Status" = 'Pending Materials' THEN 'AwaitingParts'
                        WHEN work_orders."Status" = 'Awaiting User' THEN 'AwaitingClient'
                        WHEN work_orders."Status" = 'In Progress' THEN 'InProgress'
                        WHEN EXISTS (
                            SELECT 1
                            FROM work_order_technician_assignments technician_assignments
                            WHERE technician_assignments."WorkOrderId" = work_orders."Id"
                        ) THEN 'AssignedToTechnician'
                        WHEN work_orders."AssignmentGroupId" IS NOT NULL THEN 'AssignedToGroup'
                        ELSE 'Unassigned'
                    END,
                    NULL::uuid,
                    work_orders."CreatedAt",
                    NULL::text,
                    work_orders."CreatedAt",
                    work_orders."UpdatedAt"
                FROM work_orders
                WHERE work_orders."AssignmentGroupId" IS NOT NULL
                   OR work_orders."AssignedTechnicianId" IS NOT NULL
                   OR work_orders."AssignedTechnicianIdsJson" IS NOT NULL;
                """);

            migrationBuilder.Sql("""
                INSERT INTO work_order_assignment_history
                ("Id", "TenantId", "WorkOrderId", "Action", "FromGroupId", "ToGroupId", "FromTechnicianId", "ToTechnicianId", "PerformedByUserId", "PerformedAt", "Notes", "CreatedAt", "UpdatedAt")
                SELECT
                    gen_random_uuid(),
                    work_orders."TenantId",
                    work_orders."Id",
                    CASE
                        WHEN work_orders."AssignmentGroupId" IS NOT NULL AND (work_orders."AssignedTechnicianId" IS NOT NULL OR work_orders."AssignedTechnicianIdsJson" IS NOT NULL) THEN 'MigratedGroupAndTechnicians'
                        WHEN work_orders."AssignmentGroupId" IS NOT NULL THEN 'MigratedGroupAssignment'
                        ELSE 'MigratedTechnicianAssignment'
                    END,
                    NULL::uuid,
                    work_orders."AssignmentGroupId",
                    NULL::uuid,
                    work_orders."AssignedTechnicianId",
                    NULL::uuid,
                    work_orders."CreatedAt",
                    'Migrated from legacy work order assignment fields.',
                    work_orders."CreatedAt",
                    work_orders."UpdatedAt"
                FROM work_orders
                WHERE work_orders."AssignmentGroupId" IS NOT NULL
                   OR work_orders."AssignedTechnicianId" IS NOT NULL
                   OR work_orders."AssignedTechnicianIdsJson" IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_assignment_group_members_assignment_groups_AssignmentGroupId",
                table: "assignment_group_members");

            migrationBuilder.DropForeignKey(
                name: "FK_assignment_group_members_technicians_TechnicianId",
                table: "assignment_group_members");

            migrationBuilder.DropForeignKey(
                name: "FK_assignment_group_members_tenants_TenantId",
                table: "assignment_group_members");

            migrationBuilder.DropTable(
                name: "work_order_assignment_history");

            migrationBuilder.DropTable(
                name: "work_order_assignments");

            migrationBuilder.DropTable(
                name: "work_order_technician_assignments");

            migrationBuilder.DropPrimaryKey(
                name: "PK_assignment_group_members",
                table: "assignment_group_members");

            migrationBuilder.DropColumn(
                name: "SkillArea",
                table: "assignment_groups");

            migrationBuilder.DropColumn(
                name: "AddedAt",
                table: "assignment_group_members");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "assignment_group_members");

            migrationBuilder.DropColumn(
                name: "IsLead",
                table: "assignment_group_members");

            migrationBuilder.RenameTable(
                name: "assignment_group_members",
                newName: "assignment_group_technicians");

            migrationBuilder.RenameIndex(
                name: "IX_assignment_group_members_TenantId_AssignmentGroupId_Technic~",
                table: "assignment_group_technicians",
                newName: "IX_assignment_group_technicians_TenantId_AssignmentGroupId_Tec~");

            migrationBuilder.RenameIndex(
                name: "IX_assignment_group_members_TechnicianId",
                table: "assignment_group_technicians",
                newName: "IX_assignment_group_technicians_TechnicianId");

            migrationBuilder.RenameIndex(
                name: "IX_assignment_group_members_AssignmentGroupId",
                table: "assignment_group_technicians",
                newName: "IX_assignment_group_technicians_AssignmentGroupId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_assignment_group_technicians",
                table: "assignment_group_technicians",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_assignment_group_technicians_assignment_groups_AssignmentGr~",
                table: "assignment_group_technicians",
                column: "AssignmentGroupId",
                principalTable: "assignment_groups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_assignment_group_technicians_technicians_TechnicianId",
                table: "assignment_group_technicians",
                column: "TechnicianId",
                principalTable: "technicians",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_assignment_group_technicians_tenants_TenantId",
                table: "assignment_group_technicians",
                column: "TenantId",
                principalTable: "tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
