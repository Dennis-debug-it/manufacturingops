using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class AddPeopleAndSafety : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "RequiredCertificationId",
                table: "work_orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequiredPermitTypes",
                table: "work_orders",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsOvertime",
                table: "work_order_time_logs",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "LabourCost",
                table: "work_order_time_logs",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "HomeOrgNodeId",
                table: "technicians",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "HourlyRate",
                table: "technicians",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OvertimeMultiplier",
                table: "technicians",
                type: "numeric(4,2)",
                precision: 4,
                scale: 2,
                nullable: false,
                defaultValue: 1.5m);

            migrationBuilder.AddColumn<string>(
                name: "Trade",
                table: "technicians",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RequiredCertificationId",
                table: "job_plans",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequiredPermitTypes",
                table: "job_plans",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "incidents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    IncidentNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Severity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OrgNodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetId = table.Column<Guid>(type: "uuid", nullable: true),
                    WorkOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    PersonsInvolved = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ImmediateAction = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    RootCause = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Lessons = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    DaysLost = table.Column<int>(type: "integer", nullable: true),
                    ReportedToAuthority = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ReportedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CorrectiveWorkOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClosedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_incidents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_incidents_assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_incidents_org_nodes_OrgNodeId",
                        column: x => x.OrgNodeId,
                        principalTable: "org_nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_incidents_work_orders_CorrectiveWorkOrderId",
                        column: x => x.CorrectiveWorkOrderId,
                        principalTable: "work_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_incidents_work_orders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "work_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "isolation_points",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    EnergyType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Location = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Method = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_isolation_points", x => x.Id);
                    table.ForeignKey(
                        name: "FK_isolation_points_assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ppe_issues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    TechnicianId = table.Column<Guid>(type: "uuid", nullable: true),
                    PersonName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Item = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Size = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    IssuedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IssuedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ReplaceBy = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ppe_issues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ppe_issues_technicians_TechnicianId",
                        column: x => x.TechnicianId,
                        principalTable: "technicians",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "roster_entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    TechnicianId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ShiftName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_roster_entries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_roster_entries_technicians_TechnicianId",
                        column: x => x.TechnicianId,
                        principalTable: "technicians",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "technician_certificates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    TechnicianId = table.Column<Guid>(type: "uuid", nullable: false),
                    CertificationDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CertificateNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IssuedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpiresOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AttachmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    LastAlertDays = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_technician_certificates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_technician_certificates_attachments_AttachmentId",
                        column: x => x.AttachmentId,
                        principalTable: "attachments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_technician_certificates_certification_definitions_Certifica~",
                        column: x => x.CertificationDefinitionId,
                        principalTable: "certification_definitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_technician_certificates_technicians_TechnicianId",
                        column: x => x.TechnicianId,
                        principalTable: "technicians",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "technician_skills",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    TechnicianId = table.Column<Guid>(type: "uuid", nullable: false),
                    Skill = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_technician_skills", x => x.Id);
                    table.ForeignKey(
                        name: "FK_technician_skills_technicians_TechnicianId",
                        column: x => x.TechnicianId,
                        principalTable: "technicians",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "training_records",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    TechnicianId = table.Column<Guid>(type: "uuid", nullable: true),
                    PersonName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Company = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IsContractor = table.Column<bool>(type: "boolean", nullable: false),
                    Course = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CompletedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Trainer = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_training_records", x => x.Id);
                    table.ForeignKey(
                        name: "FK_training_records_technicians_TechnicianId",
                        column: x => x.TechnicianId,
                        principalTable: "technicians",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "work_permits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PermitNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    PermitType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Hazards = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    WorkDescription = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ValidFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ValidTo = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IssuedToName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RequestedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    RequestedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ApprovedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DecisionNote = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    GasTest = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClosedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CloseOutNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_work_permits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_work_permits_work_orders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "work_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "loto_locks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    PermitId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsolationPointId = table.Column<Guid>(type: "uuid", nullable: false),
                    LockNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    AppliedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AppliedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ZeroEnergyVerified = table.Column<bool>(type: "boolean", nullable: false),
                    RemovedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RemovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_loto_locks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_loto_locks_isolation_points_IsolationPointId",
                        column: x => x.IsolationPointId,
                        principalTable: "isolation_points",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_loto_locks_work_orders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "work_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_loto_locks_work_permits_PermitId",
                        column: x => x.PermitId,
                        principalTable: "work_permits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "work_permit_checks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PermitId = table.Column<Guid>(type: "uuid", nullable: false),
                    Prompt = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    Done = table.Column<bool>(type: "boolean", nullable: false),
                    DoneByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_work_permit_checks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_work_permit_checks_work_permits_PermitId",
                        column: x => x.PermitId,
                        principalTable: "work_permits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_work_orders_RequiredCertificationId",
                table: "work_orders",
                column: "RequiredCertificationId");

            migrationBuilder.CreateIndex(
                name: "IX_technicians_HomeOrgNodeId",
                table: "technicians",
                column: "HomeOrgNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_job_plans_RequiredCertificationId",
                table: "job_plans",
                column: "RequiredCertificationId");

            migrationBuilder.CreateIndex(
                name: "IX_incidents_AssetId",
                table: "incidents",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_incidents_CorrectiveWorkOrderId",
                table: "incidents",
                column: "CorrectiveWorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_incidents_OrgNodeId",
                table: "incidents",
                column: "OrgNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_incidents_TenantId_IncidentNumber",
                table: "incidents",
                columns: new[] { "TenantId", "IncidentNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_incidents_TenantId_OccurredAt",
                table: "incidents",
                columns: new[] { "TenantId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_incidents_WorkOrderId",
                table: "incidents",
                column: "WorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_isolation_points_AssetId_Code",
                table: "isolation_points",
                columns: new[] { "AssetId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_loto_locks_IsolationPointId",
                table: "loto_locks",
                column: "IsolationPointId");

            migrationBuilder.CreateIndex(
                name: "IX_loto_locks_PermitId",
                table: "loto_locks",
                column: "PermitId");

            migrationBuilder.CreateIndex(
                name: "IX_loto_locks_TenantId_WorkOrderId",
                table: "loto_locks",
                columns: new[] { "TenantId", "WorkOrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_loto_locks_WorkOrderId",
                table: "loto_locks",
                column: "WorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ppe_issues_TechnicianId",
                table: "ppe_issues",
                column: "TechnicianId");

            migrationBuilder.CreateIndex(
                name: "IX_ppe_issues_TenantId_IssuedOn",
                table: "ppe_issues",
                columns: new[] { "TenantId", "IssuedOn" });

            migrationBuilder.CreateIndex(
                name: "IX_roster_entries_TechnicianId_WorkDate",
                table: "roster_entries",
                columns: new[] { "TechnicianId", "WorkDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_roster_entries_TenantId_WorkDate",
                table: "roster_entries",
                columns: new[] { "TenantId", "WorkDate" });

            migrationBuilder.CreateIndex(
                name: "IX_technician_certificates_AttachmentId",
                table: "technician_certificates",
                column: "AttachmentId");

            migrationBuilder.CreateIndex(
                name: "IX_technician_certificates_CertificationDefinitionId",
                table: "technician_certificates",
                column: "CertificationDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_technician_certificates_TechnicianId",
                table: "technician_certificates",
                column: "TechnicianId");

            migrationBuilder.CreateIndex(
                name: "IX_technician_certificates_TenantId_ExpiresOn",
                table: "technician_certificates",
                columns: new[] { "TenantId", "ExpiresOn" });

            migrationBuilder.CreateIndex(
                name: "IX_technician_skills_TechnicianId_Skill",
                table: "technician_skills",
                columns: new[] { "TechnicianId", "Skill" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_training_records_TechnicianId",
                table: "training_records",
                column: "TechnicianId");

            migrationBuilder.CreateIndex(
                name: "IX_training_records_TenantId_CompletedOn",
                table: "training_records",
                columns: new[] { "TenantId", "CompletedOn" });

            migrationBuilder.CreateIndex(
                name: "IX_work_permit_checks_PermitId",
                table: "work_permit_checks",
                column: "PermitId");

            migrationBuilder.CreateIndex(
                name: "IX_work_permits_TenantId_PermitNumber",
                table: "work_permits",
                columns: new[] { "TenantId", "PermitNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_work_permits_TenantId_WorkOrderId_Status",
                table: "work_permits",
                columns: new[] { "TenantId", "WorkOrderId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_work_permits_WorkOrderId",
                table: "work_permits",
                column: "WorkOrderId");

            migrationBuilder.AddForeignKey(
                name: "FK_job_plans_certification_definitions_RequiredCertificationId",
                table: "job_plans",
                column: "RequiredCertificationId",
                principalTable: "certification_definitions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_technicians_org_nodes_HomeOrgNodeId",
                table: "technicians",
                column: "HomeOrgNodeId",
                principalTable: "org_nodes",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_work_orders_certification_definitions_RequiredCertification~",
                table: "work_orders",
                column: "RequiredCertificationId",
                principalTable: "certification_definitions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_job_plans_certification_definitions_RequiredCertificationId",
                table: "job_plans");

            migrationBuilder.DropForeignKey(
                name: "FK_technicians_org_nodes_HomeOrgNodeId",
                table: "technicians");

            migrationBuilder.DropForeignKey(
                name: "FK_work_orders_certification_definitions_RequiredCertification~",
                table: "work_orders");

            migrationBuilder.DropTable(
                name: "incidents");

            migrationBuilder.DropTable(
                name: "loto_locks");

            migrationBuilder.DropTable(
                name: "ppe_issues");

            migrationBuilder.DropTable(
                name: "roster_entries");

            migrationBuilder.DropTable(
                name: "technician_certificates");

            migrationBuilder.DropTable(
                name: "technician_skills");

            migrationBuilder.DropTable(
                name: "training_records");

            migrationBuilder.DropTable(
                name: "work_permit_checks");

            migrationBuilder.DropTable(
                name: "isolation_points");

            migrationBuilder.DropTable(
                name: "work_permits");

            migrationBuilder.DropIndex(
                name: "IX_work_orders_RequiredCertificationId",
                table: "work_orders");

            migrationBuilder.DropIndex(
                name: "IX_technicians_HomeOrgNodeId",
                table: "technicians");

            migrationBuilder.DropIndex(
                name: "IX_job_plans_RequiredCertificationId",
                table: "job_plans");

            migrationBuilder.DropColumn(
                name: "RequiredCertificationId",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "RequiredPermitTypes",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "IsOvertime",
                table: "work_order_time_logs");

            migrationBuilder.DropColumn(
                name: "LabourCost",
                table: "work_order_time_logs");

            migrationBuilder.DropColumn(
                name: "HomeOrgNodeId",
                table: "technicians");

            migrationBuilder.DropColumn(
                name: "HourlyRate",
                table: "technicians");

            migrationBuilder.DropColumn(
                name: "OvertimeMultiplier",
                table: "technicians");

            migrationBuilder.DropColumn(
                name: "Trade",
                table: "technicians");

            migrationBuilder.DropColumn(
                name: "RequiredCertificationId",
                table: "job_plans");

            migrationBuilder.DropColumn(
                name: "RequiredPermitTypes",
                table: "job_plans");
        }
    }
}
