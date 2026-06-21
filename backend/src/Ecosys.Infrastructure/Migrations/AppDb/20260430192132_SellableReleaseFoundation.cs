using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class SellableReleaseFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_branches_TenantId_Name",
                table: "branches");

            migrationBuilder.AddColumn<DateTime>(
                name: "ArrivalAt",
                table: "work_orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AssignmentGroupId",
                table: "work_orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DepartureAt",
                table: "work_orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "WorkStartedAt",
                table: "work_orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ActiveWorkOrderId",
                table: "technicians",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsTrackingActive",
                table: "technicians",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "LastKnownLatitude",
                table: "technicians",
                type: "numeric(9,6)",
                precision: 9,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LastKnownLongitude",
                table: "technicians",
                type: "numeric(9,6)",
                precision: 9,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastLocationAt",
                table: "technicians",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PmTemplateId",
                table: "preventive_maintenance_plans",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ParentBranchId",
                table: "branches",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "assignment_groups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_assignment_groups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_assignment_groups_branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_assignment_groups_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "license_plans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    MaxUsers = table.Column<int>(type: "integer", nullable: true),
                    MaxBranches = table.Column<int>(type: "integer", nullable: true),
                    MaxAssets = table.Column<int>(type: "integer", nullable: true),
                    MonthlyWorkOrders = table.Column<int>(type: "integer", nullable: true),
                    EmailIngestion = table.Column<bool>(type: "boolean", nullable: false),
                    MonitoringIntegration = table.Column<bool>(type: "boolean", nullable: false),
                    AdvancedReports = table.Column<bool>(type: "boolean", nullable: false),
                    ClientPortal = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_license_plans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "monitoring_settings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EndpointLabel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    WebhookSecret = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DefaultBranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    DefaultPriority = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    AutoCreateWorkOrders = table.Column<bool>(type: "boolean", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_monitoring_settings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_monitoring_settings_branches_DefaultBranchId",
                        column: x => x.DefaultBranchId,
                        principalTable: "branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_monitoring_settings_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "notification_settings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmailAlertsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    SmsAlertsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    WorkOrderAssignmentAlerts = table.Column<bool>(type: "boolean", nullable: false),
                    LicenseExpiryAlerts = table.Column<bool>(type: "boolean", nullable: false),
                    DailyDigestEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_settings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_notification_settings_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "pm_templates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    AutoScheduleByDefault = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pm_templates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_pm_templates_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tenant_security_policies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    MinPasswordLength = table.Column<int>(type: "integer", nullable: false),
                    RequireUppercase = table.Column<bool>(type: "boolean", nullable: false),
                    RequireLowercase = table.Column<bool>(type: "boolean", nullable: false),
                    RequireDigit = table.Column<bool>(type: "boolean", nullable: false),
                    RequireSpecialCharacter = table.Column<bool>(type: "boolean", nullable: false),
                    PasswordRotationDays = table.Column<int>(type: "integer", nullable: false),
                    SessionTimeoutMinutes = table.Column<int>(type: "integer", nullable: false),
                    RequireMfa = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_security_policies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tenant_security_policies_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "work_order_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    EventType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Latitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    Longitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_work_order_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_work_order_events_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_work_order_events_users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_work_order_events_work_orders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "work_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "assignment_group_technicians",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignmentGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    TechnicianId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_assignment_group_technicians", x => x.Id);
                    table.ForeignKey(
                        name: "FK_assignment_group_technicians_assignment_groups_AssignmentGr~",
                        column: x => x.AssignmentGroupId,
                        principalTable: "assignment_groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_assignment_group_technicians_technicians_TechnicianId",
                        column: x => x.TechnicianId,
                        principalTable: "technicians",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_assignment_group_technicians_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tenant_licenses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    LicensePlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    StartsAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TrialEndsAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SuspendedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    GracePeriodDays = table.Column<int>(type: "integer", nullable: false),
                    MaxUsersOverride = table.Column<int>(type: "integer", nullable: true),
                    MaxBranchesOverride = table.Column<int>(type: "integer", nullable: true),
                    MaxAssetsOverride = table.Column<int>(type: "integer", nullable: true),
                    MonthlyWorkOrdersOverride = table.Column<int>(type: "integer", nullable: true),
                    EmailIngestionOverride = table.Column<bool>(type: "boolean", nullable: true),
                    MonitoringIntegrationOverride = table.Column<bool>(type: "boolean", nullable: true),
                    AdvancedReportsOverride = table.Column<bool>(type: "boolean", nullable: true),
                    ClientPortalOverride = table.Column<bool>(type: "boolean", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_licenses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tenant_licenses_license_plans_LicensePlanId",
                        column: x => x.LicensePlanId,
                        principalTable: "license_plans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tenant_licenses_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "pm_reports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    PmTemplateId = table.Column<Guid>(type: "uuid", nullable: true),
                    Summary = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    AnswersJson = table.Column<string>(type: "character varying(16000)", maxLength: 16000, nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pm_reports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_pm_reports_pm_templates_PmTemplateId",
                        column: x => x.PmTemplateId,
                        principalTable: "pm_templates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_pm_reports_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_pm_reports_work_orders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "work_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "pm_template_questions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PmTemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Prompt = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ResponseType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    OptionsJson = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pm_template_questions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_pm_template_questions_pm_templates_PmTemplateId",
                        column: x => x.PmTemplateId,
                        principalTable: "pm_templates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "license_plans",
                columns: new[] { "Id", "AdvancedReports", "ClientPortal", "CreatedAt", "DisplayName", "EmailIngestion", "IsActive", "MaxAssets", "MaxBranches", "MaxUsers", "MonitoringIntegration", "MonthlyWorkOrders", "PlanCode", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("33fe16d8-5b55-45aa-89e2-1af0f2e7367d"), true, true, new DateTime(2026, 4, 30, 0, 0, 0, 0, DateTimeKind.Utc), "Enterprise", true, true, null, null, null, true, null, "Enterprise", null },
                    { new Guid("4df8af7d-8514-45e7-ad42-f5d7b745fe71"), true, false, new DateTime(2026, 4, 30, 0, 0, 0, 0, DateTimeKind.Utc), "Professional", true, true, 2500, 10, 50, true, 5000, "Professional", null },
                    { new Guid("7e2bc113-bc77-4206-8c53-4696402f69c0"), false, false, new DateTime(2026, 4, 30, 0, 0, 0, 0, DateTimeKind.Utc), "Starter", true, true, 250, 3, 15, false, 500, "Starter", null },
                    { new Guid("d220965a-38b4-45d0-9870-a8e45627e21a"), false, false, new DateTime(2026, 4, 30, 0, 0, 0, 0, DateTimeKind.Utc), "Trial", false, true, 25, 1, 5, false, 50, "Trial", null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_work_orders_AssignmentGroupId",
                table: "work_orders",
                column: "AssignmentGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_technicians_ActiveWorkOrderId",
                table: "technicians",
                column: "ActiveWorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_preventive_maintenance_plans_PmTemplateId",
                table: "preventive_maintenance_plans",
                column: "PmTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_branches_ParentBranchId",
                table: "branches",
                column: "ParentBranchId");

            migrationBuilder.CreateIndex(
                name: "IX_branches_TenantId_ParentBranchId_Name",
                table: "branches",
                columns: new[] { "TenantId", "ParentBranchId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_assignment_group_technicians_AssignmentGroupId",
                table: "assignment_group_technicians",
                column: "AssignmentGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_assignment_group_technicians_TechnicianId",
                table: "assignment_group_technicians",
                column: "TechnicianId");

            migrationBuilder.CreateIndex(
                name: "IX_assignment_group_technicians_TenantId_AssignmentGroupId_Tec~",
                table: "assignment_group_technicians",
                columns: new[] { "TenantId", "AssignmentGroupId", "TechnicianId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_assignment_groups_BranchId",
                table: "assignment_groups",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_assignment_groups_TenantId_BranchId_Name",
                table: "assignment_groups",
                columns: new[] { "TenantId", "BranchId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_license_plans_PlanCode",
                table: "license_plans",
                column: "PlanCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_monitoring_settings_DefaultBranchId",
                table: "monitoring_settings",
                column: "DefaultBranchId");

            migrationBuilder.CreateIndex(
                name: "IX_monitoring_settings_TenantId",
                table: "monitoring_settings",
                column: "TenantId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_notification_settings_TenantId",
                table: "notification_settings",
                column: "TenantId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_pm_reports_PmTemplateId",
                table: "pm_reports",
                column: "PmTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_pm_reports_TenantId_WorkOrderId_GeneratedAt",
                table: "pm_reports",
                columns: new[] { "TenantId", "WorkOrderId", "GeneratedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_pm_reports_WorkOrderId",
                table: "pm_reports",
                column: "WorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_pm_template_questions_PmTemplateId",
                table: "pm_template_questions",
                column: "PmTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_pm_templates_TenantId_Category_Name",
                table: "pm_templates",
                columns: new[] { "TenantId", "Category", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tenant_licenses_LicensePlanId",
                table: "tenant_licenses",
                column: "LicensePlanId");

            migrationBuilder.CreateIndex(
                name: "IX_tenant_licenses_TenantId",
                table: "tenant_licenses",
                column: "TenantId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tenant_security_policies_TenantId",
                table: "tenant_security_policies",
                column: "TenantId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_work_order_events_ActorUserId",
                table: "work_order_events",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_work_order_events_TenantId_WorkOrderId_OccurredAt",
                table: "work_order_events",
                columns: new[] { "TenantId", "WorkOrderId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_work_order_events_WorkOrderId",
                table: "work_order_events",
                column: "WorkOrderId");

            migrationBuilder.AddForeignKey(
                name: "FK_branches_branches_ParentBranchId",
                table: "branches",
                column: "ParentBranchId",
                principalTable: "branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_preventive_maintenance_plans_pm_templates_PmTemplateId",
                table: "preventive_maintenance_plans",
                column: "PmTemplateId",
                principalTable: "pm_templates",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_technicians_work_orders_ActiveWorkOrderId",
                table: "technicians",
                column: "ActiveWorkOrderId",
                principalTable: "work_orders",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_work_orders_assignment_groups_AssignmentGroupId",
                table: "work_orders",
                column: "AssignmentGroupId",
                principalTable: "assignment_groups",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_branches_branches_ParentBranchId",
                table: "branches");

            migrationBuilder.DropForeignKey(
                name: "FK_preventive_maintenance_plans_pm_templates_PmTemplateId",
                table: "preventive_maintenance_plans");

            migrationBuilder.DropForeignKey(
                name: "FK_technicians_work_orders_ActiveWorkOrderId",
                table: "technicians");

            migrationBuilder.DropForeignKey(
                name: "FK_work_orders_assignment_groups_AssignmentGroupId",
                table: "work_orders");

            migrationBuilder.DropTable(
                name: "assignment_group_technicians");

            migrationBuilder.DropTable(
                name: "monitoring_settings");

            migrationBuilder.DropTable(
                name: "notification_settings");

            migrationBuilder.DropTable(
                name: "pm_reports");

            migrationBuilder.DropTable(
                name: "pm_template_questions");

            migrationBuilder.DropTable(
                name: "tenant_licenses");

            migrationBuilder.DropTable(
                name: "tenant_security_policies");

            migrationBuilder.DropTable(
                name: "work_order_events");

            migrationBuilder.DropTable(
                name: "assignment_groups");

            migrationBuilder.DropTable(
                name: "pm_templates");

            migrationBuilder.DropTable(
                name: "license_plans");

            migrationBuilder.DropIndex(
                name: "IX_work_orders_AssignmentGroupId",
                table: "work_orders");

            migrationBuilder.DropIndex(
                name: "IX_technicians_ActiveWorkOrderId",
                table: "technicians");

            migrationBuilder.DropIndex(
                name: "IX_preventive_maintenance_plans_PmTemplateId",
                table: "preventive_maintenance_plans");

            migrationBuilder.DropIndex(
                name: "IX_branches_ParentBranchId",
                table: "branches");

            migrationBuilder.DropIndex(
                name: "IX_branches_TenantId_ParentBranchId_Name",
                table: "branches");

            migrationBuilder.DropColumn(
                name: "ArrivalAt",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "AssignmentGroupId",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "DepartureAt",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "WorkStartedAt",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "ActiveWorkOrderId",
                table: "technicians");

            migrationBuilder.DropColumn(
                name: "IsTrackingActive",
                table: "technicians");

            migrationBuilder.DropColumn(
                name: "LastKnownLatitude",
                table: "technicians");

            migrationBuilder.DropColumn(
                name: "LastKnownLongitude",
                table: "technicians");

            migrationBuilder.DropColumn(
                name: "LastLocationAt",
                table: "technicians");

            migrationBuilder.DropColumn(
                name: "PmTemplateId",
                table: "preventive_maintenance_plans");

            migrationBuilder.DropColumn(
                name: "ParentBranchId",
                table: "branches");

            migrationBuilder.CreateIndex(
                name: "IX_branches_TenantId_Name",
                table: "branches",
                columns: new[] { "TenantId", "Name" },
                unique: true);
        }
    }
}
