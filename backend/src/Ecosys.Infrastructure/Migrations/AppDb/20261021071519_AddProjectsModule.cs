using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class AddProjectsModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ProjectId",
                table: "work_orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProjectId",
                table: "material_issues",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SharedWithClient",
                table: "attachments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "consultants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    FirmName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Discipline = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ContactPerson = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    PhysicalAddress = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
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
                name: "project_stages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Category = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
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
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    ItemsJson = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
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
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DefaultTemplateId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
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
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProjectNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    SiteId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProjectTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    StageId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectManagerUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: "KES"),
                    OriginalValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ApprovedAdditions = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ApprovedOmissions = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ValueVersion = table.Column<int>(type: "integer", nullable: false),
                    PlannedStartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    PlannedEndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ActualStartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ActualEndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ProgressPercent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    ProgressOverridePercent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    ProgressOverrideByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProgressOverrideReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    VisibleInClientPortal = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    HandedOverAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClosedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClosureNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
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
                name: "project_consultants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConsultantId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleOnProject = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
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
                name: "project_members",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    TechnicianId = table.Column<Guid>(type: "uuid", nullable: true),
                    Role = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Trade = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    FromDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ToDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
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
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Weight = table.Column<decimal>(type: "numeric(9,2)", precision: 9, scale: 2, nullable: false),
                    PlannedDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ForecastDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    VerifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    VerifiedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
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
                name: "project_variations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    VariationNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ClientReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SubmittedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DecidedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DecisionNote = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
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
                name: "project_tasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    MilestoneId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    AssigneeUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Priority = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
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

            migrationBuilder.CreateIndex(
                name: "IX_work_orders_ProjectId",
                table: "work_orders",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_work_orders_TenantId_ProjectId",
                table: "work_orders",
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
                name: "IX_consultants_TenantId_FirmName",
                table: "consultants",
                columns: new[] { "TenantId", "FirmName" },
                unique: true);

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
                name: "FK_material_issues_projects_ProjectId",
                table: "material_issues",
                column: "ProjectId",
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_material_issues_projects_ProjectId",
                table: "material_issues");

            migrationBuilder.DropForeignKey(
                name: "FK_work_orders_projects_ProjectId",
                table: "work_orders");

            migrationBuilder.DropTable(
                name: "project_consultants");

            migrationBuilder.DropTable(
                name: "project_members");

            migrationBuilder.DropTable(
                name: "project_tasks");

            migrationBuilder.DropTable(
                name: "project_variations");

            migrationBuilder.DropTable(
                name: "consultants");

            migrationBuilder.DropTable(
                name: "project_milestones");

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
                name: "IX_material_issues_ProjectId",
                table: "material_issues");

            migrationBuilder.DropIndex(
                name: "IX_material_issues_TenantId_ProjectId",
                table: "material_issues");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                table: "material_issues");

            migrationBuilder.DropColumn(
                name: "SharedWithClient",
                table: "attachments");
        }
    }
}
