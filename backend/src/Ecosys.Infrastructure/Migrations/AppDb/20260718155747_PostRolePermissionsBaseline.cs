using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class PostRolePermissionsBaseline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_work_orders_status",
                table: "work_orders");

            migrationBuilder.AddColumn<bool>(
                name: "IsRepeatVisit",
                table: "work_orders",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "ParentWorkOrderId",
                table: "work_orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PlannedMaterialsJson",
                table: "work_orders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreviousStatus",
                table: "work_orders",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "QuoteId",
                table: "work_orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RepeatVisitCount",
                table: "work_orders",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "TrackingToken",
                table: "work_orders",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WorkOrderTemplateId",
                table: "work_orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WorkflowStatusId",
                table: "work_orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MetadataJson",
                table: "work_order_events",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "BusinessHoursEnd",
                table: "sla_rules",
                type: "interval",
                nullable: true);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "BusinessHoursStart",
                table: "sla_rules",
                type: "interval",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "sla_rules",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDefault",
                table: "sla_definitions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "Latitude",
                table: "sites",
                type: "numeric(9,6)",
                precision: 9,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Longitude",
                table: "sites",
                type: "numeric(9,6)",
                precision: 9,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Icon",
                table: "knowledge_categories",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPublic",
                table: "knowledge_categories",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "HelpfulCount",
                table: "knowledge_articles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "NotHelpfulCount",
                table: "knowledge_articles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ViewCount",
                table: "knowledge_articles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "escalation_rules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    TriggerCondition = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    TriggerValue = table.Column<int>(type: "integer", nullable: false),
                    Action = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    ActionValue = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CooldownMins = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_escalation_rules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_escalation_rules_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "eta_notification_settings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    SubjectTemplate = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    BodyTemplate = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_eta_notification_settings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_eta_notification_settings_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "knowledge_article_feedback",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ArticleId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientPortalUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsHelpful = table.Column<bool>(type: "boolean", nullable: false),
                    Comment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_knowledge_article_feedback", x => x.Id);
                    table.ForeignKey(
                        name: "FK_knowledge_article_feedback_client_portal_users_ClientPortal~",
                        column: x => x.ClientPortalUserId,
                        principalTable: "client_portal_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_knowledge_article_feedback_knowledge_articles_ArticleId",
                        column: x => x.ArticleId,
                        principalTable: "knowledge_articles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_knowledge_article_feedback_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "parts_requests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    WorkOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    TechnicianId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_parts_requests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_parts_requests_branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_parts_requests_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_parts_requests_users_TechnicianId",
                        column: x => x.TechnicianId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_parts_requests_work_orders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "work_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sla_escalations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    SlaRuleId = table.Column<Guid>(type: "uuid", nullable: false),
                    TriggerAtPercent = table.Column<int>(type: "integer", nullable: false),
                    NotifyRoles = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    EscalateToUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sla_escalations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sla_escalations_sla_rules_SlaRuleId",
                        column: x => x.SlaRuleId,
                        principalTable: "sla_rules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_sla_escalations_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_sla_escalations_users_EscalateToUserId",
                        column: x => x.EscalateToUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "sla_records",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    SlaRuleId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResponseBreachedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResolutionBreachedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsPaused = table.Column<bool>(type: "boolean", nullable: false),
                    PauseReason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    LastResponseEscalationPercent = table.Column<int>(type: "integer", nullable: true),
                    LastResolutionEscalationPercent = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sla_records", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sla_records_sla_rules_SlaRuleId",
                        column: x => x.SlaRuleId,
                        principalTable: "sla_rules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_sla_records_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_sla_records_work_orders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "work_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "work_order_notes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Content = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    IsClientVisible = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_work_order_notes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_work_order_notes_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_work_order_notes_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_work_order_notes_work_orders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "work_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "work_order_templates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    JobType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    EstimatedDurationMins = table.Column<int>(type: "integer", nullable: true),
                    DefaultPriority = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    RequiredSkillsJson = table.Column<string>(type: "text", nullable: true),
                    ChecklistTemplateId = table.Column<Guid>(type: "uuid", nullable: true),
                    DefaultMaterialsJson = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_work_order_templates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_work_order_templates_pm_templates_ChecklistTemplateId",
                        column: x => x.ChecklistTemplateId,
                        principalTable: "pm_templates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_work_order_templates_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "workflow_definitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workflow_definitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_workflow_definitions_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "escalation_fired",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    RuleId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    FiredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_escalation_fired", x => x.Id);
                    table.ForeignKey(
                        name: "FK_escalation_fired_escalation_rules_RuleId",
                        column: x => x.RuleId,
                        principalTable: "escalation_rules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_escalation_fired_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_escalation_fired_work_orders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "work_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "parts_request_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PartsRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    InventoryItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    QuantityRequested = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    QuantityApproved = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_parts_request_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_parts_request_items_material_items_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "material_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_parts_request_items_parts_requests_PartsRequestId",
                        column: x => x.PartsRequestId,
                        principalTable: "parts_requests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "workflow_statuses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkflowDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Color = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsInitial = table.Column<bool>(type: "boolean", nullable: false),
                    IsFinal = table.Column<bool>(type: "boolean", nullable: false),
                    AllowedNextStatusIdsJson = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workflow_statuses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_workflow_statuses_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_workflow_statuses_workflow_definitions_WorkflowDefinitionId",
                        column: x => x.WorkflowDefinitionId,
                        principalTable: "workflow_definitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_work_orders_ParentWorkOrderId",
                table: "work_orders",
                column: "ParentWorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_work_orders_QuoteId",
                table: "work_orders",
                column: "QuoteId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_work_orders_TrackingToken",
                table: "work_orders",
                column: "TrackingToken",
                unique: true,
                filter: "\"TrackingToken\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_work_orders_WorkflowStatusId",
                table: "work_orders",
                column: "WorkflowStatusId");

            migrationBuilder.CreateIndex(
                name: "IX_work_orders_WorkOrderTemplateId",
                table: "work_orders",
                column: "WorkOrderTemplateId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_work_orders_status",
                table: "work_orders",
                sql: "\"Status\" IN ('New', 'Open', 'Assigned', 'Scheduled', 'In Progress', 'On Hold', 'Waiting on Parts', 'Waiting on Client', 'Completed', 'Client Accepted', 'Closed', 'Cancelled', 'Rejected', 'Reopened')");

            migrationBuilder.CreateIndex(
                name: "IX_escalation_fired_RuleId_WorkOrderId_FiredAt",
                table: "escalation_fired",
                columns: new[] { "RuleId", "WorkOrderId", "FiredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_escalation_fired_TenantId",
                table: "escalation_fired",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_escalation_fired_WorkOrderId",
                table: "escalation_fired",
                column: "WorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_escalation_rules_TenantId",
                table: "escalation_rules",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_eta_notification_settings_TenantId_EventType",
                table: "eta_notification_settings",
                columns: new[] { "TenantId", "EventType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_article_feedback_ArticleId",
                table: "knowledge_article_feedback",
                column: "ArticleId");

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_article_feedback_ClientPortalUserId",
                table: "knowledge_article_feedback",
                column: "ClientPortalUserId");

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_article_feedback_TenantId_ArticleId",
                table: "knowledge_article_feedback",
                columns: new[] { "TenantId", "ArticleId" });

            migrationBuilder.CreateIndex(
                name: "IX_parts_request_items_InventoryItemId",
                table: "parts_request_items",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_parts_request_items_PartsRequestId",
                table: "parts_request_items",
                column: "PartsRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_parts_requests_BranchId",
                table: "parts_requests",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_parts_requests_TechnicianId",
                table: "parts_requests",
                column: "TechnicianId");

            migrationBuilder.CreateIndex(
                name: "IX_parts_requests_TenantId",
                table: "parts_requests",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_parts_requests_WorkOrderId",
                table: "parts_requests",
                column: "WorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_sla_escalations_EscalateToUserId",
                table: "sla_escalations",
                column: "EscalateToUserId");

            migrationBuilder.CreateIndex(
                name: "IX_sla_escalations_SlaRuleId",
                table: "sla_escalations",
                column: "SlaRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_sla_escalations_TenantId",
                table: "sla_escalations",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_sla_records_SlaRuleId",
                table: "sla_records",
                column: "SlaRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_sla_records_TenantId",
                table: "sla_records",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_sla_records_WorkOrderId",
                table: "sla_records",
                column: "WorkOrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_work_order_notes_CreatedByUserId",
                table: "work_order_notes",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_work_order_notes_TenantId_WorkOrderId_CreatedAt",
                table: "work_order_notes",
                columns: new[] { "TenantId", "WorkOrderId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_work_order_notes_WorkOrderId",
                table: "work_order_notes",
                column: "WorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_work_order_templates_ChecklistTemplateId",
                table: "work_order_templates",
                column: "ChecklistTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_work_order_templates_TenantId_Name",
                table: "work_order_templates",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_workflow_definitions_TenantId_Name",
                table: "workflow_definitions",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_workflow_statuses_TenantId",
                table: "workflow_statuses",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_workflow_statuses_WorkflowDefinitionId_Name",
                table: "workflow_statuses",
                columns: new[] { "WorkflowDefinitionId", "Name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_work_orders_tenant_quotations_QuoteId",
                table: "work_orders",
                column: "QuoteId",
                principalTable: "tenant_quotations",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_work_orders_work_order_templates_WorkOrderTemplateId",
                table: "work_orders",
                column: "WorkOrderTemplateId",
                principalTable: "work_order_templates",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_work_orders_work_orders_ParentWorkOrderId",
                table: "work_orders",
                column: "ParentWorkOrderId",
                principalTable: "work_orders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_work_orders_workflow_statuses_WorkflowStatusId",
                table: "work_orders",
                column: "WorkflowStatusId",
                principalTable: "workflow_statuses",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_work_orders_tenant_quotations_QuoteId",
                table: "work_orders");

            migrationBuilder.DropForeignKey(
                name: "FK_work_orders_work_order_templates_WorkOrderTemplateId",
                table: "work_orders");

            migrationBuilder.DropForeignKey(
                name: "FK_work_orders_work_orders_ParentWorkOrderId",
                table: "work_orders");

            migrationBuilder.DropForeignKey(
                name: "FK_work_orders_workflow_statuses_WorkflowStatusId",
                table: "work_orders");

            migrationBuilder.DropTable(
                name: "escalation_fired");

            migrationBuilder.DropTable(
                name: "eta_notification_settings");

            migrationBuilder.DropTable(
                name: "knowledge_article_feedback");

            migrationBuilder.DropTable(
                name: "parts_request_items");

            migrationBuilder.DropTable(
                name: "sla_escalations");

            migrationBuilder.DropTable(
                name: "sla_records");

            migrationBuilder.DropTable(
                name: "work_order_notes");

            migrationBuilder.DropTable(
                name: "work_order_templates");

            migrationBuilder.DropTable(
                name: "workflow_statuses");

            migrationBuilder.DropTable(
                name: "escalation_rules");

            migrationBuilder.DropTable(
                name: "parts_requests");

            migrationBuilder.DropTable(
                name: "workflow_definitions");

            migrationBuilder.DropIndex(
                name: "IX_work_orders_ParentWorkOrderId",
                table: "work_orders");

            migrationBuilder.DropIndex(
                name: "IX_work_orders_QuoteId",
                table: "work_orders");

            migrationBuilder.DropIndex(
                name: "IX_work_orders_TrackingToken",
                table: "work_orders");

            migrationBuilder.DropIndex(
                name: "IX_work_orders_WorkflowStatusId",
                table: "work_orders");

            migrationBuilder.DropIndex(
                name: "IX_work_orders_WorkOrderTemplateId",
                table: "work_orders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_work_orders_status",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "IsRepeatVisit",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "ParentWorkOrderId",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "PlannedMaterialsJson",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "PreviousStatus",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "QuoteId",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "RepeatVisitCount",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "TrackingToken",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "WorkOrderTemplateId",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "WorkflowStatusId",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "MetadataJson",
                table: "work_order_events");

            migrationBuilder.DropColumn(
                name: "BusinessHoursEnd",
                table: "sla_rules");

            migrationBuilder.DropColumn(
                name: "BusinessHoursStart",
                table: "sla_rules");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "sla_rules");

            migrationBuilder.DropColumn(
                name: "IsDefault",
                table: "sla_definitions");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "sites");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "sites");

            migrationBuilder.DropColumn(
                name: "Icon",
                table: "knowledge_categories");

            migrationBuilder.DropColumn(
                name: "IsPublic",
                table: "knowledge_categories");

            migrationBuilder.DropColumn(
                name: "HelpfulCount",
                table: "knowledge_articles");

            migrationBuilder.DropColumn(
                name: "NotHelpfulCount",
                table: "knowledge_articles");

            migrationBuilder.DropColumn(
                name: "ViewCount",
                table: "knowledge_articles");

            migrationBuilder.AddCheckConstraint(
                name: "CK_work_orders_status",
                table: "work_orders",
                sql: "\"Status\" IN ('New', 'Open', 'Assigned', 'Scheduled', 'In Progress', 'On Hold', 'Completed', 'Client Accepted', 'Closed', 'Cancelled', 'Rejected', 'Reopened')");
        }
    }
}
