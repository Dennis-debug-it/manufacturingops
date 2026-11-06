using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class PruneProjects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_activity_attendees_consultants_ConsultantId",
                table: "activity_attendees");

            migrationBuilder.DropForeignKey(
                name: "FK_attendance_entries_projects_ProjectId",
                table: "attendance_entries");

            migrationBuilder.DropForeignKey(
                name: "FK_contracts_projects_SourceProjectId",
                table: "contracts");

            migrationBuilder.DropForeignKey(
                name: "FK_expenses_projects_ProjectId",
                table: "expenses");

            migrationBuilder.DropForeignKey(
                name: "FK_material_issues_projects_ProjectId",
                table: "material_issues");

            migrationBuilder.DropForeignKey(
                name: "FK_purchase_orders_projects_ProjectId",
                table: "purchase_orders");

            migrationBuilder.DropForeignKey(
                name: "FK_supplier_bills_projects_ProjectId",
                table: "supplier_bills");

            migrationBuilder.DropForeignKey(
                name: "FK_tenant_invoices_project_valuations_ProjectValuationId",
                table: "tenant_invoices");

            migrationBuilder.DropForeignKey(
                name: "FK_tenant_invoices_projects_ProjectId",
                table: "tenant_invoices");

            migrationBuilder.DropForeignKey(
                name: "FK_tenant_quotations_projects_ConvertedToProjectId",
                table: "tenant_quotations");

            migrationBuilder.DropForeignKey(
                name: "FK_work_orders_projects_ProjectId",
                table: "work_orders");

            migrationBuilder.DropTable(
                name: "project_budget_lines");

            migrationBuilder.DropTable(
                name: "project_consultants");

            migrationBuilder.DropTable(
                name: "project_cost_entries");

            migrationBuilder.DropTable(
                name: "project_diary_entries");

            migrationBuilder.DropTable(
                name: "project_members");

            migrationBuilder.DropTable(
                name: "project_muster_entries");

            migrationBuilder.DropTable(
                name: "project_reminder_logs");

            migrationBuilder.DropTable(
                name: "project_retention_releases");

            migrationBuilder.DropTable(
                name: "project_tasks");

            migrationBuilder.DropTable(
                name: "project_valuations");

            migrationBuilder.DropTable(
                name: "project_variations");

            migrationBuilder.DropTable(
                name: "project_worker_rates");

            migrationBuilder.DropTable(
                name: "project_milestones");

            migrationBuilder.DropTable(
                name: "consultants");

            migrationBuilder.DropTable(
                name: "project_workers");

            migrationBuilder.DropTable(
                name: "projects");

            migrationBuilder.DropTable(
                name: "project_stages");

            migrationBuilder.DropTable(
                name: "project_types");

            migrationBuilder.DropTable(
                name: "project_templates");

            migrationBuilder.DropIndex(
                name: "IX_work_orders_ProjectId",
                table: "work_orders");

            migrationBuilder.DropIndex(
                name: "IX_work_orders_TenantId_ProjectId",
                table: "work_orders");

            migrationBuilder.DropIndex(
                name: "IX_tenant_quotations_ConvertedToProjectId",
                table: "tenant_quotations");

            migrationBuilder.DropIndex(
                name: "IX_tenant_quotations_TenantId_ConvertedToProjectId",
                table: "tenant_quotations");

            migrationBuilder.DropIndex(
                name: "IX_tenant_invoices_ProjectId",
                table: "tenant_invoices");

            migrationBuilder.DropIndex(
                name: "IX_tenant_invoices_ProjectValuationId",
                table: "tenant_invoices");

            migrationBuilder.DropIndex(
                name: "IX_tenant_invoices_TenantId_ProjectId",
                table: "tenant_invoices");

            migrationBuilder.DropIndex(
                name: "IX_supplier_bills_ProjectId",
                table: "supplier_bills");

            migrationBuilder.DropIndex(
                name: "IX_supplier_bills_TenantId_ProjectId",
                table: "supplier_bills");

            migrationBuilder.DropIndex(
                name: "IX_purchase_orders_ProjectId",
                table: "purchase_orders");

            migrationBuilder.DropIndex(
                name: "IX_purchase_orders_TenantId_ProjectId",
                table: "purchase_orders");

            migrationBuilder.DropIndex(
                name: "IX_material_issues_ProjectId",
                table: "material_issues");

            migrationBuilder.DropIndex(
                name: "IX_material_issues_TenantId_ProjectId",
                table: "material_issues");

            migrationBuilder.DropIndex(
                name: "IX_expenses_ProjectId",
                table: "expenses");

            migrationBuilder.DropIndex(
                name: "IX_expenses_TenantId_ProjectId",
                table: "expenses");

            migrationBuilder.DropIndex(
                name: "IX_contracts_SourceProjectId",
                table: "contracts");

            migrationBuilder.DropIndex(
                name: "IX_contracts_TenantId_SourceProjectId",
                table: "contracts");

            migrationBuilder.DropIndex(
                name: "IX_attendance_entries_ProjectId",
                table: "attendance_entries");

            migrationBuilder.DropIndex(
                name: "IX_attendance_entries_TenantId_ProjectId",
                table: "attendance_entries");

            migrationBuilder.DropIndex(
                name: "IX_activity_attendees_ConsultantId",
                table: "activity_attendees");

            migrationBuilder.DropCheckConstraint(
                name: "CK_activity_attendees_one_kind",
                table: "activity_attendees");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "ConvertedToProjectId",
                table: "tenant_quotations");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                table: "tenant_invoices");

            migrationBuilder.DropColumn(
                name: "ProjectValuationId",
                table: "tenant_invoices");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                table: "supplier_bills");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                table: "purchase_orders");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                table: "material_issues");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                table: "expenses");

            migrationBuilder.DropColumn(
                name: "SourceProjectId",
                table: "contracts");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                table: "attendance_entries");

            migrationBuilder.DropColumn(
                name: "ConsultantId",
                table: "activity_attendees");

            migrationBuilder.AddCheckConstraint(
                name: "CK_activity_attendees_one_kind",
                table: "activity_attendees",
                sql: "(CASE WHEN \"UserId\" IS NULL THEN 0 ELSE 1 END) + (CASE WHEN \"ClientContactId\" IS NULL THEN 0 ELSE 1 END) + (CASE WHEN \"ExternalEmail\" IS NULL AND \"ExternalName\" IS NULL THEN 0 ELSE 1 END) = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_activity_attendees_one_kind",
                table: "activity_attendees");

            migrationBuilder.AddColumn<Guid>(
                name: "ProjectId",
                table: "work_orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ConvertedToProjectId",
                table: "tenant_quotations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProjectId",
                table: "tenant_invoices",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProjectValuationId",
                table: "tenant_invoices",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProjectId",
                table: "supplier_bills",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProjectId",
                table: "purchase_orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProjectId",
                table: "material_issues",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProjectId",
                table: "expenses",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceProjectId",
                table: "contracts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProjectId",
                table: "attendance_entries",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ConsultantId",
                table: "activity_attendees",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "consultants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContactPerson = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Discipline = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    FirmName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    PhysicalAddress = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_consultants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_consultants_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "project_reminder_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    ForDate = table.Column<DateOnly>(type: "date", nullable: false),
                    NotificationKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_reminder_logs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "project_stages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Category = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_stages", x => x.Id);
                    table.CheckConstraint("CK_project_stages_category", "\"Category\" IN ('Planning', 'Active', 'OnHold', 'Handover', 'Completed', 'Closed', 'Cancelled')");
                    table.ForeignKey(
                        name: "FK_project_stages_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "project_templates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    ItemsJson = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_templates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_project_templates_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "project_types",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DefaultTemplateId = table.Column<Guid>(type: "uuid", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_types", x => x.Id);
                    table.ForeignKey(
                        name: "FK_project_types_project_templates_DefaultTemplateId",
                        column: x => x.DefaultTemplateId,
                        principalTable: "project_templates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_project_types_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "projects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectManagerUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProjectTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    SiteId = table.Column<Guid>(type: "uuid", nullable: true),
                    StageId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActualEndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ActualStartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    AdvanceInvoiceId = table.Column<Guid>(type: "uuid", nullable: true),
                    AdvancePaymentAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    AdvanceRecoveryPercent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    ApprovedAdditions = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ApprovedOmissions = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClosedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClosureNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: "KES"),
                    DefectsLiabilityMonths = table.Column<int>(type: "integer", nullable: true),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    HandedOverAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    MaintenanceContractId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    OriginalValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PlannedEndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    PlannedStartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ProgressOverrideByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProgressOverridePercent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    ProgressOverrideReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ProgressPercent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    ProjectNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    RetentionCapAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    RetentionPercent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    RetentionReleaseAtCompletionPercent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false, defaultValue: 50m),
                    SourceQuotationId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ValueVersion = table.Column<int>(type: "integer", nullable: false),
                    VisibleInClientPortal = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_projects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_projects_branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_projects_clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_projects_project_stages_StageId",
                        column: x => x.StageId,
                        principalTable: "project_stages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_projects_project_types_ProjectTypeId",
                        column: x => x.ProjectTypeId,
                        principalTable: "project_types",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_projects_sites_SiteId",
                        column: x => x.SiteId,
                        principalTable: "sites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_projects_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_projects_users_ProjectManagerUserId",
                        column: x => x.ProjectManagerUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "project_budget_lines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Category = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_budget_lines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_project_budget_lines_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_project_budget_lines_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "project_consultants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConsultantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RoleOnProject = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_consultants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_project_consultants_consultants_ConsultantId",
                        column: x => x.ConsultantId,
                        principalTable: "consultants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_project_consultants_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_project_consultants_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "project_cost_entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Category = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    IncurredOn = table.Column<DateOnly>(type: "date", nullable: false),
                    RecordedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_cost_entries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_project_cost_entries_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_project_cost_entries_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "project_diary_entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EntryDate = table.Column<DateOnly>(type: "date", nullable: false),
                    InstructionsReceived = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    IssuesAndDelays = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Weather = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    WorkCompleted = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_diary_entries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_project_diary_entries_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_project_diary_entries_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_project_diary_entries_users_AuthorUserId",
                        column: x => x.AuthorUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "project_members",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    TechnicianId = table.Column<Guid>(type: "uuid", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FromDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Role = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ToDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Trade = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_members", x => x.Id);
                    table.CheckConstraint("CK_project_members_person", "\"UserId\" IS NOT NULL OR \"TechnicianId\" IS NOT NULL");
                    table.ForeignKey(
                        name: "FK_project_members_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_project_members_technicians_TechnicianId",
                        column: x => x.TechnicianId,
                        principalTable: "technicians",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_project_members_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_project_members_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "project_milestones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ForecastDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PlannedDate = table.Column<DateOnly>(type: "date", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    VerifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    VerifiedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Weight = table.Column<decimal>(type: "numeric(9,2)", precision: 9, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_milestones", x => x.Id);
                    table.CheckConstraint("CK_project_milestones_weight", "\"Weight\" >= 0");
                    table.ForeignKey(
                        name: "FK_project_milestones_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_project_milestones_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "project_retention_releases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReleasedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReleasedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Stage = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_retention_releases", x => x.Id);
                    table.CheckConstraint("CK_project_retention_releases_amount", "\"Amount\" > 0");
                    table.ForeignKey(
                        name: "FK_project_retention_releases_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_project_retention_releases_tenant_invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "tenant_invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_project_retention_releases_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "project_valuations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CertifiedByConsultantId = table.Column<Guid>(type: "uuid", nullable: true),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    AdvanceRecoveredToDate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    AmountDue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CertificateReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CertifiedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    CertifiedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GrossValueToDate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    MaterialsOnSite = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    PeriodEnd = table.Column<DateOnly>(type: "date", nullable: false),
                    PreviouslyCertified = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    RetentionToDate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ValuationNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    WorkDoneToDate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_valuations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_project_valuations_consultants_CertifiedByConsultantId",
                        column: x => x.CertifiedByConsultantId,
                        principalTable: "consultants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_project_valuations_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_project_valuations_tenant_invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "tenant_invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_project_valuations_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "project_variations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ClientReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DecidedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DecidedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DecisionNote = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SubmittedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    VariationNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_variations", x => x.Id);
                    table.CheckConstraint("CK_project_variations_amount", "\"Amount\" > 0");
                    table.CheckConstraint("CK_project_variations_kind", "\"Kind\" IN ('Addition', 'Omission')");
                    table.ForeignKey(
                        name: "FK_project_variations_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_project_variations_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "project_workers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    TechnicianId = table.Column<Guid>(type: "uuid", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EngagementType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    FullName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Trade = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_workers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_project_workers_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_project_workers_technicians_TechnicianId",
                        column: x => x.TechnicianId,
                        principalTable: "technicians",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_project_workers_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "project_tasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssigneeUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    MilestoneId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Priority = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_tasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_project_tasks_project_milestones_MilestoneId",
                        column: x => x.MilestoneId,
                        principalTable: "project_milestones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_project_tasks_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_project_tasks_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_project_tasks_users_AssigneeUserId",
                        column: x => x.AssigneeUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "project_muster_entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectWorkerId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DayFraction = table.Column<decimal>(type: "numeric(3,2)", precision: 3, scale: 2, nullable: false),
                    MarkedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    WorkDate = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_muster_entries", x => x.Id);
                    table.CheckConstraint("CK_project_muster_entries_status", "\"Status\" IN ('Present', 'HalfDay', 'Absent')");
                    table.ForeignKey(
                        name: "FK_project_muster_entries_project_workers_ProjectWorkerId",
                        column: x => x.ProjectWorkerId,
                        principalTable: "project_workers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_project_muster_entries_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_project_muster_entries_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "project_worker_rates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectWorkerId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DailyRate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SetByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_worker_rates", x => x.Id);
                    table.CheckConstraint("CK_project_worker_rates_rate", "\"DailyRate\" >= 0");
                    table.ForeignKey(
                        name: "FK_project_worker_rates_project_workers_ProjectWorkerId",
                        column: x => x.ProjectWorkerId,
                        principalTable: "project_workers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_project_worker_rates_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_work_orders_ProjectId",
                table: "work_orders",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_work_orders_TenantId_ProjectId",
                table: "work_orders",
                columns: new[] { "TenantId", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_tenant_quotations_ConvertedToProjectId",
                table: "tenant_quotations",
                column: "ConvertedToProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_tenant_quotations_TenantId_ConvertedToProjectId",
                table: "tenant_quotations",
                columns: new[] { "TenantId", "ConvertedToProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_tenant_invoices_ProjectId",
                table: "tenant_invoices",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_tenant_invoices_ProjectValuationId",
                table: "tenant_invoices",
                column: "ProjectValuationId");

            migrationBuilder.CreateIndex(
                name: "IX_tenant_invoices_TenantId_ProjectId",
                table: "tenant_invoices",
                columns: new[] { "TenantId", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_supplier_bills_ProjectId",
                table: "supplier_bills",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_supplier_bills_TenantId_ProjectId",
                table: "supplier_bills",
                columns: new[] { "TenantId", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_orders_ProjectId",
                table: "purchase_orders",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_orders_TenantId_ProjectId",
                table: "purchase_orders",
                columns: new[] { "TenantId", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_material_issues_ProjectId",
                table: "material_issues",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_material_issues_TenantId_ProjectId",
                table: "material_issues",
                columns: new[] { "TenantId", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_expenses_ProjectId",
                table: "expenses",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_expenses_TenantId_ProjectId",
                table: "expenses",
                columns: new[] { "TenantId", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_contracts_SourceProjectId",
                table: "contracts",
                column: "SourceProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_contracts_TenantId_SourceProjectId",
                table: "contracts",
                columns: new[] { "TenantId", "SourceProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_attendance_entries_ProjectId",
                table: "attendance_entries",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_attendance_entries_TenantId_ProjectId",
                table: "attendance_entries",
                columns: new[] { "TenantId", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_activity_attendees_ConsultantId",
                table: "activity_attendees",
                column: "ConsultantId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_activity_attendees_one_kind",
                table: "activity_attendees",
                sql: "(CASE WHEN \"UserId\" IS NULL THEN 0 ELSE 1 END) + (CASE WHEN \"ConsultantId\" IS NULL THEN 0 ELSE 1 END) + (CASE WHEN \"ClientContactId\" IS NULL THEN 0 ELSE 1 END) + (CASE WHEN \"ExternalEmail\" IS NULL AND \"ExternalName\" IS NULL THEN 0 ELSE 1 END) = 1");

            migrationBuilder.CreateIndex(
                name: "IX_consultants_TenantId_FirmName",
                table: "consultants",
                columns: new[] { "TenantId", "FirmName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_project_budget_lines_ProjectId",
                table: "project_budget_lines",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_project_budget_lines_TenantId_ProjectId",
                table: "project_budget_lines",
                columns: new[] { "TenantId", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_project_consultants_ConsultantId",
                table: "project_consultants",
                column: "ConsultantId");

            migrationBuilder.CreateIndex(
                name: "IX_project_consultants_ProjectId",
                table: "project_consultants",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_project_consultants_TenantId_ConsultantId",
                table: "project_consultants",
                columns: new[] { "TenantId", "ConsultantId" });

            migrationBuilder.CreateIndex(
                name: "IX_project_consultants_TenantId_ProjectId_ConsultantId",
                table: "project_consultants",
                columns: new[] { "TenantId", "ProjectId", "ConsultantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_project_cost_entries_ProjectId",
                table: "project_cost_entries",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_project_cost_entries_TenantId_ProjectId",
                table: "project_cost_entries",
                columns: new[] { "TenantId", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_project_diary_entries_AuthorUserId",
                table: "project_diary_entries",
                column: "AuthorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_project_diary_entries_ProjectId",
                table: "project_diary_entries",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_project_diary_entries_TenantId_ProjectId_EntryDate",
                table: "project_diary_entries",
                columns: new[] { "TenantId", "ProjectId", "EntryDate" });

            migrationBuilder.CreateIndex(
                name: "IX_project_members_ProjectId",
                table: "project_members",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_project_members_TechnicianId",
                table: "project_members",
                column: "TechnicianId");

            migrationBuilder.CreateIndex(
                name: "IX_project_members_TenantId_ProjectId",
                table: "project_members",
                columns: new[] { "TenantId", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_project_members_TenantId_UserId",
                table: "project_members",
                columns: new[] { "TenantId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_project_members_UserId",
                table: "project_members",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_project_milestones_ProjectId",
                table: "project_milestones",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_project_milestones_TenantId_ProjectId",
                table: "project_milestones",
                columns: new[] { "TenantId", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_project_muster_entries_ProjectId",
                table: "project_muster_entries",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_project_muster_entries_ProjectWorkerId",
                table: "project_muster_entries",
                column: "ProjectWorkerId");

            migrationBuilder.CreateIndex(
                name: "IX_project_muster_entries_TenantId_ProjectId_WorkDate",
                table: "project_muster_entries",
                columns: new[] { "TenantId", "ProjectId", "WorkDate" });

            migrationBuilder.CreateIndex(
                name: "IX_project_muster_entries_TenantId_ProjectWorkerId_WorkDate",
                table: "project_muster_entries",
                columns: new[] { "TenantId", "ProjectWorkerId", "WorkDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_project_reminder_logs_TenantId_EntityId_NotificationKey_For~",
                table: "project_reminder_logs",
                columns: new[] { "TenantId", "EntityId", "NotificationKey", "ForDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_project_retention_releases_InvoiceId",
                table: "project_retention_releases",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_project_retention_releases_ProjectId",
                table: "project_retention_releases",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_project_retention_releases_TenantId_ProjectId_Stage",
                table: "project_retention_releases",
                columns: new[] { "TenantId", "ProjectId", "Stage" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_project_stages_TenantId_Name",
                table: "project_stages",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_project_tasks_AssigneeUserId",
                table: "project_tasks",
                column: "AssigneeUserId");

            migrationBuilder.CreateIndex(
                name: "IX_project_tasks_MilestoneId",
                table: "project_tasks",
                column: "MilestoneId");

            migrationBuilder.CreateIndex(
                name: "IX_project_tasks_ProjectId",
                table: "project_tasks",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_project_tasks_TenantId_AssigneeUserId",
                table: "project_tasks",
                columns: new[] { "TenantId", "AssigneeUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_project_tasks_TenantId_ProjectId",
                table: "project_tasks",
                columns: new[] { "TenantId", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_project_templates_TenantId_Name",
                table: "project_templates",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_project_types_DefaultTemplateId",
                table: "project_types",
                column: "DefaultTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_project_types_TenantId_Name",
                table: "project_types",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_project_valuations_CertifiedByConsultantId",
                table: "project_valuations",
                column: "CertifiedByConsultantId");

            migrationBuilder.CreateIndex(
                name: "IX_project_valuations_InvoiceId",
                table: "project_valuations",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_project_valuations_ProjectId",
                table: "project_valuations",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_project_valuations_TenantId_ProjectId_Status",
                table: "project_valuations",
                columns: new[] { "TenantId", "ProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_project_valuations_TenantId_ValuationNumber",
                table: "project_valuations",
                columns: new[] { "TenantId", "ValuationNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_project_variations_ProjectId",
                table: "project_variations",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_project_variations_TenantId_ProjectId_Status",
                table: "project_variations",
                columns: new[] { "TenantId", "ProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_project_variations_TenantId_VariationNumber",
                table: "project_variations",
                columns: new[] { "TenantId", "VariationNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_project_worker_rates_ProjectWorkerId",
                table: "project_worker_rates",
                column: "ProjectWorkerId");

            migrationBuilder.CreateIndex(
                name: "IX_project_worker_rates_TenantId_ProjectWorkerId_EffectiveFrom",
                table: "project_worker_rates",
                columns: new[] { "TenantId", "ProjectWorkerId", "EffectiveFrom" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_project_workers_ProjectId",
                table: "project_workers",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_project_workers_TechnicianId",
                table: "project_workers",
                column: "TechnicianId");

            migrationBuilder.CreateIndex(
                name: "IX_project_workers_TenantId_ProjectId",
                table: "project_workers",
                columns: new[] { "TenantId", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_projects_BranchId",
                table: "projects",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_projects_ClientId",
                table: "projects",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_projects_ProjectManagerUserId",
                table: "projects",
                column: "ProjectManagerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_projects_ProjectTypeId",
                table: "projects",
                column: "ProjectTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_projects_SiteId",
                table: "projects",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "IX_projects_StageId",
                table: "projects",
                column: "StageId");

            migrationBuilder.CreateIndex(
                name: "IX_projects_TenantId_ClientId",
                table: "projects",
                columns: new[] { "TenantId", "ClientId" });

            migrationBuilder.CreateIndex(
                name: "IX_projects_TenantId_ProjectManagerUserId",
                table: "projects",
                columns: new[] { "TenantId", "ProjectManagerUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_projects_TenantId_ProjectNumber",
                table: "projects",
                columns: new[] { "TenantId", "ProjectNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_projects_TenantId_StageId",
                table: "projects",
                columns: new[] { "TenantId", "StageId" });

            migrationBuilder.AddForeignKey(
                name: "FK_activity_attendees_consultants_ConsultantId",
                table: "activity_attendees",
                column: "ConsultantId",
                principalTable: "consultants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_attendance_entries_projects_ProjectId",
                table: "attendance_entries",
                column: "ProjectId",
                principalTable: "projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_contracts_projects_SourceProjectId",
                table: "contracts",
                column: "SourceProjectId",
                principalTable: "projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_expenses_projects_ProjectId",
                table: "expenses",
                column: "ProjectId",
                principalTable: "projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_material_issues_projects_ProjectId",
                table: "material_issues",
                column: "ProjectId",
                principalTable: "projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_purchase_orders_projects_ProjectId",
                table: "purchase_orders",
                column: "ProjectId",
                principalTable: "projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_supplier_bills_projects_ProjectId",
                table: "supplier_bills",
                column: "ProjectId",
                principalTable: "projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_tenant_invoices_project_valuations_ProjectValuationId",
                table: "tenant_invoices",
                column: "ProjectValuationId",
                principalTable: "project_valuations",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_tenant_invoices_projects_ProjectId",
                table: "tenant_invoices",
                column: "ProjectId",
                principalTable: "projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_tenant_quotations_projects_ConvertedToProjectId",
                table: "tenant_quotations",
                column: "ConvertedToProjectId",
                principalTable: "projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_work_orders_projects_ProjectId",
                table: "work_orders",
                column: "ProjectId",
                principalTable: "projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
