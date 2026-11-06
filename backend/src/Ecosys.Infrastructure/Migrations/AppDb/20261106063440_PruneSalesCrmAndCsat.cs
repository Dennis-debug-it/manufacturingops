using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class PruneSalesCrmAndCsat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_tenant_quotations_sales_opportunities_SalesOpportunityId",
                table: "tenant_quotations");

            migrationBuilder.DropForeignKey(
                name: "FK_work_orders_sales_opportunities_SalesOpportunityId",
                table: "work_orders");

            migrationBuilder.DropTable(
                name: "crm_activities");

            migrationBuilder.DropTable(
                name: "crm_tasks");

            migrationBuilder.DropTable(
                name: "csat_surveys");

            migrationBuilder.DropTable(
                name: "sales_activities");

            migrationBuilder.DropTable(
                name: "sales_assignment_rules");

            migrationBuilder.DropTable(
                name: "sales_custom_field_values");

            migrationBuilder.DropTable(
                name: "sales_opportunity_contacts");

            migrationBuilder.DropTable(
                name: "sales_pipeline_stages");

            migrationBuilder.DropTable(
                name: "sales_saved_views");

            migrationBuilder.DropTable(
                name: "sales_sequence_enrollments");

            migrationBuilder.DropTable(
                name: "sales_sequence_steps");

            migrationBuilder.DropTable(
                name: "sales_stage_changes");

            migrationBuilder.DropTable(
                name: "sales_targets");

            migrationBuilder.DropTable(
                name: "sales_tenders");

            migrationBuilder.DropTable(
                name: "tenant_sales_settings");

            migrationBuilder.DropTable(
                name: "crm_contacts");

            migrationBuilder.DropTable(
                name: "sales_custom_fields");

            migrationBuilder.DropTable(
                name: "sales_sequences");

            migrationBuilder.DropTable(
                name: "sales_opportunities");

            migrationBuilder.DropTable(
                name: "platform_leads");

            migrationBuilder.DropTable(
                name: "sales_pipelines");

            migrationBuilder.DropIndex(
                name: "IX_work_orders_SalesOpportunityId",
                table: "work_orders");

            migrationBuilder.DropIndex(
                name: "IX_tenant_quotations_SalesOpportunityId",
                table: "tenant_quotations");

            migrationBuilder.DropColumn(
                name: "SalesOpportunityId",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "SalesOpportunityId",
                table: "tenant_quotations");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SalesOpportunityId",
                table: "work_orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SalesOpportunityId",
                table: "tenant_quotations",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "csat_surveys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Channel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: true),
                    Comment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RespondedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Score = table.Column<int>(type: "integer", nullable: true),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TechnicianId = table.Column<Guid>(type: "uuid", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_csat_surveys", x => x.Id);
                    table.ForeignKey(
                        name: "FK_csat_surveys_work_orders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "work_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "platform_leads",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConvertedTenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CompanySize = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ContactPersonName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ContactedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Country = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Industry = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Message = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PreferredContactMethod = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Status = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_platform_leads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_platform_leads_tenants_ConvertedTenantId",
                        column: x => x.ConvertedTenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "sales_assignment_rules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    LastAssignedIndex = table.Column<int>(type: "integer", nullable: false),
                    LocationContains = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Method = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    OwnerUserIds = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ServiceType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Source = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_assignment_rules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "sales_opportunities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: true),
                    SiteId = table.Column<Guid>(type: "uuid", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ContactEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ContactName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    ContactPhone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    EstimatedValue = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ExpectedCloseDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastActivityAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Location = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    LostAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LostReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    OwnerName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    PipelineId = table.Column<Guid>(type: "uuid", nullable: true),
                    Probability = table.Column<int>(type: "integer", nullable: false),
                    RenewalOfContractId = table.Column<Guid>(type: "uuid", nullable: true),
                    ServiceType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Source = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    SourceWorkOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    Stage = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    StageChangedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    StallReminderSentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    WonAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_opportunities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sales_opportunities_clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_sales_opportunities_sites_SiteId",
                        column: x => x.SiteId,
                        principalTable: "sites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_sales_opportunities_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sales_pipelines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    Module = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_pipelines", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "sales_saved_views",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsShared = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Query = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_saved_views", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "sales_sequences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    StopOnReply = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_sequences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "sales_targets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Month = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_targets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tenant_sales_settings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreateRenewalDeals = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DealVisibility = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DefaultOwnerUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    LeadEmailAddress = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    SendWeeklySummary = table.Column<bool>(type: "boolean", nullable: false),
                    StalledAfterDays = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    WebFormEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    WebFormKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    WeeklySummaryWeek = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_sales_settings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "crm_contacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LeadId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    FullName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Role = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_crm_contacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_crm_contacts_platform_leads_LeadId",
                        column: x => x.LeadId,
                        principalTable: "platform_leads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "sales_activities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OpportunityId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DueAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Latitude = table.Column<double>(type: "double precision", nullable: true),
                    LocationAccuracyMeters = table.Column<double>(type: "double precision", nullable: true),
                    LoggedByName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    LoggedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Longitude = table.Column<double>(type: "double precision", nullable: true),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReminderCount = table.Column<int>(type: "integer", nullable: false),
                    ReminderSentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SequenceEnrollmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Summary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_activities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sales_activities_sales_opportunities_OpportunityId",
                        column: x => x.OpportunityId,
                        principalTable: "sales_opportunities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sales_opportunity_contacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContactId = table.Column<Guid>(type: "uuid", nullable: false),
                    OpportunityId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_opportunity_contacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sales_opportunity_contacts_client_contacts_ContactId",
                        column: x => x.ContactId,
                        principalTable: "client_contacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_sales_opportunity_contacts_sales_opportunities_OpportunityId",
                        column: x => x.OpportunityId,
                        principalTable: "sales_opportunities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sales_stage_changes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OpportunityId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChangedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ChangedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    FromStage = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ToStage = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_stage_changes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sales_stage_changes_sales_opportunities_OpportunityId",
                        column: x => x.OpportunityId,
                        principalTable: "sales_opportunities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sales_tenders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: true),
                    OpportunityId = table.Column<Guid>(type: "uuid", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    BidBondAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    BidBondRequired = table.Column<bool>(type: "boolean", nullable: false),
                    ClarificationDeadline = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ContactEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ContactName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    ContactPhone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    DeadlineReminderStage = table.Column<int>(type: "integer", nullable: false),
                    DecidedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    EstimatedValue = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    IssuerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    OutcomeNotes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    OwnerName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Reference = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    SubmissionDeadline = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SubmissionMethod = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SubmittedValue = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Type = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_tenders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sales_tenders_clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_sales_tenders_sales_opportunities_OpportunityId",
                        column: x => x.OpportunityId,
                        principalTable: "sales_opportunities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_sales_tenders_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sales_custom_fields",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FieldType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    HelpText = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Key = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Label = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Options = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    PipelineId = table.Column<Guid>(type: "uuid", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_custom_fields", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sales_custom_fields_sales_pipelines_PipelineId",
                        column: x => x.PipelineId,
                        principalTable: "sales_pipelines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "sales_pipeline_stages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PipelineId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Probability = table.Column<int>(type: "integer", nullable: false),
                    RequiredFields = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_pipeline_stages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sales_pipeline_stages_sales_pipelines_PipelineId",
                        column: x => x.PipelineId,
                        principalTable: "sales_pipelines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sales_sequence_enrollments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OpportunityId = table.Column<Guid>(type: "uuid", nullable: false),
                    SequenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EnrolledByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    FinishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NextDueAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NextStep = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    StoppedReason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_sequence_enrollments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sales_sequence_enrollments_sales_opportunities_OpportunityId",
                        column: x => x.OpportunityId,
                        principalTable: "sales_opportunities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_sales_sequence_enrollments_sales_sequences_SequenceId",
                        column: x => x.SequenceId,
                        principalTable: "sales_sequences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sales_sequence_steps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SequenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Body = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    DelayDays = table.Column<int>(type: "integer", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_sequence_steps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sales_sequence_steps_sales_sequences_SequenceId",
                        column: x => x.SequenceId,
                        principalTable: "sales_sequences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "crm_activities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContactId = table.Column<Guid>(type: "uuid", nullable: true),
                    LeadId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LoggedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    LoggedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Summary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_crm_activities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_crm_activities_crm_contacts_ContactId",
                        column: x => x.ContactId,
                        principalTable: "crm_contacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_crm_activities_platform_leads_LeadId",
                        column: x => x.LeadId,
                        principalTable: "platform_leads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "crm_tasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RelatedContactId = table.Column<Guid>(type: "uuid", nullable: true),
                    RelatedLeadId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssignedToName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AssignedToUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Priority = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_crm_tasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_crm_tasks_crm_contacts_RelatedContactId",
                        column: x => x.RelatedContactId,
                        principalTable: "crm_contacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_crm_tasks_platform_leads_RelatedLeadId",
                        column: x => x.RelatedLeadId,
                        principalTable: "platform_leads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "sales_custom_field_values",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FieldId = table.Column<Guid>(type: "uuid", nullable: false),
                    OpportunityId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Value = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_custom_field_values", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sales_custom_field_values_sales_custom_fields_FieldId",
                        column: x => x.FieldId,
                        principalTable: "sales_custom_fields",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_sales_custom_field_values_sales_opportunities_OpportunityId",
                        column: x => x.OpportunityId,
                        principalTable: "sales_opportunities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_work_orders_SalesOpportunityId",
                table: "work_orders",
                column: "SalesOpportunityId");

            migrationBuilder.CreateIndex(
                name: "IX_tenant_quotations_SalesOpportunityId",
                table: "tenant_quotations",
                column: "SalesOpportunityId");

            migrationBuilder.CreateIndex(
                name: "IX_crm_activities_ContactId",
                table: "crm_activities",
                column: "ContactId");

            migrationBuilder.CreateIndex(
                name: "IX_crm_activities_LeadId",
                table: "crm_activities",
                column: "LeadId");

            migrationBuilder.CreateIndex(
                name: "IX_crm_contacts_LeadId",
                table: "crm_contacts",
                column: "LeadId");

            migrationBuilder.CreateIndex(
                name: "IX_crm_tasks_RelatedContactId",
                table: "crm_tasks",
                column: "RelatedContactId");

            migrationBuilder.CreateIndex(
                name: "IX_crm_tasks_RelatedLeadId",
                table: "crm_tasks",
                column: "RelatedLeadId");

            migrationBuilder.CreateIndex(
                name: "IX_crm_tasks_Status",
                table: "crm_tasks",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_csat_surveys_TenantId_RespondedAt",
                table: "csat_surveys",
                columns: new[] { "TenantId", "RespondedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_csat_surveys_TenantId_WorkOrderId",
                table: "csat_surveys",
                columns: new[] { "TenantId", "WorkOrderId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_csat_surveys_TokenHash",
                table: "csat_surveys",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_csat_surveys_WorkOrderId",
                table: "csat_surveys",
                column: "WorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_platform_leads_ConvertedTenantId",
                table: "platform_leads",
                column: "ConvertedTenantId");

            migrationBuilder.CreateIndex(
                name: "IX_platform_leads_CreatedAt",
                table: "platform_leads",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_platform_leads_Status",
                table: "platform_leads",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_sales_activities_OpportunityId",
                table: "sales_activities",
                column: "OpportunityId");

            migrationBuilder.CreateIndex(
                name: "IX_sales_activities_TenantId_DueAt",
                table: "sales_activities",
                columns: new[] { "TenantId", "DueAt" });

            migrationBuilder.CreateIndex(
                name: "IX_sales_activities_TenantId_OpportunityId_OccurredAt",
                table: "sales_activities",
                columns: new[] { "TenantId", "OpportunityId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_sales_assignment_rules_TenantId_SortOrder",
                table: "sales_assignment_rules",
                columns: new[] { "TenantId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_sales_custom_field_values_FieldId",
                table: "sales_custom_field_values",
                column: "FieldId");

            migrationBuilder.CreateIndex(
                name: "IX_sales_custom_field_values_OpportunityId_FieldId",
                table: "sales_custom_field_values",
                columns: new[] { "OpportunityId", "FieldId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sales_custom_field_values_TenantId_FieldId_Value",
                table: "sales_custom_field_values",
                columns: new[] { "TenantId", "FieldId", "Value" });

            migrationBuilder.CreateIndex(
                name: "IX_sales_custom_fields_PipelineId",
                table: "sales_custom_fields",
                column: "PipelineId");

            migrationBuilder.CreateIndex(
                name: "IX_sales_custom_fields_TenantId_Key",
                table: "sales_custom_fields",
                columns: new[] { "TenantId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sales_opportunities_ClientId",
                table: "sales_opportunities",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_sales_opportunities_SiteId",
                table: "sales_opportunities",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "IX_sales_opportunities_TenantId_ClientId",
                table: "sales_opportunities",
                columns: new[] { "TenantId", "ClientId" });

            migrationBuilder.CreateIndex(
                name: "IX_sales_opportunities_TenantId_OwnerUserId",
                table: "sales_opportunities",
                columns: new[] { "TenantId", "OwnerUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_sales_opportunities_TenantId_PipelineId",
                table: "sales_opportunities",
                columns: new[] { "TenantId", "PipelineId" });

            migrationBuilder.CreateIndex(
                name: "IX_sales_opportunities_TenantId_RenewalOfContractId",
                table: "sales_opportunities",
                columns: new[] { "TenantId", "RenewalOfContractId" });

            migrationBuilder.CreateIndex(
                name: "IX_sales_opportunities_TenantId_SourceWorkOrderId",
                table: "sales_opportunities",
                columns: new[] { "TenantId", "SourceWorkOrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_sales_opportunities_TenantId_Stage",
                table: "sales_opportunities",
                columns: new[] { "TenantId", "Stage" });

            migrationBuilder.CreateIndex(
                name: "IX_sales_opportunity_contacts_ContactId",
                table: "sales_opportunity_contacts",
                column: "ContactId");

            migrationBuilder.CreateIndex(
                name: "IX_sales_opportunity_contacts_OpportunityId_ContactId",
                table: "sales_opportunity_contacts",
                columns: new[] { "OpportunityId", "ContactId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sales_pipeline_stages_PipelineId_Key",
                table: "sales_pipeline_stages",
                columns: new[] { "PipelineId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sales_pipelines_TenantId_SortOrder",
                table: "sales_pipelines",
                columns: new[] { "TenantId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_sales_saved_views_TenantId_UserId",
                table: "sales_saved_views",
                columns: new[] { "TenantId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_sales_sequence_enrollments_OpportunityId",
                table: "sales_sequence_enrollments",
                column: "OpportunityId");

            migrationBuilder.CreateIndex(
                name: "IX_sales_sequence_enrollments_SequenceId",
                table: "sales_sequence_enrollments",
                column: "SequenceId");

            migrationBuilder.CreateIndex(
                name: "IX_sales_sequence_enrollments_Status_NextDueAt",
                table: "sales_sequence_enrollments",
                columns: new[] { "Status", "NextDueAt" });

            migrationBuilder.CreateIndex(
                name: "IX_sales_sequence_enrollments_TenantId_OpportunityId",
                table: "sales_sequence_enrollments",
                columns: new[] { "TenantId", "OpportunityId" });

            migrationBuilder.CreateIndex(
                name: "IX_sales_sequence_steps_SequenceId",
                table: "sales_sequence_steps",
                column: "SequenceId");

            migrationBuilder.CreateIndex(
                name: "IX_sales_sequences_TenantId",
                table: "sales_sequences",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_sales_stage_changes_OpportunityId_ChangedAt",
                table: "sales_stage_changes",
                columns: new[] { "OpportunityId", "ChangedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_sales_stage_changes_TenantId_ChangedAt",
                table: "sales_stage_changes",
                columns: new[] { "TenantId", "ChangedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_sales_targets_TenantId_Month_OwnerUserId",
                table: "sales_targets",
                columns: new[] { "TenantId", "Month", "OwnerUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sales_tenders_ClientId",
                table: "sales_tenders",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_sales_tenders_OpportunityId",
                table: "sales_tenders",
                column: "OpportunityId");

            migrationBuilder.CreateIndex(
                name: "IX_sales_tenders_TenantId_Status",
                table: "sales_tenders",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_sales_tenders_TenantId_SubmissionDeadline",
                table: "sales_tenders",
                columns: new[] { "TenantId", "SubmissionDeadline" });

            migrationBuilder.CreateIndex(
                name: "IX_tenant_sales_settings_TenantId",
                table: "tenant_sales_settings",
                column: "TenantId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tenant_sales_settings_WebFormKey",
                table: "tenant_sales_settings",
                column: "WebFormKey",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_tenant_quotations_sales_opportunities_SalesOpportunityId",
                table: "tenant_quotations",
                column: "SalesOpportunityId",
                principalTable: "sales_opportunities",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_work_orders_sales_opportunities_SalesOpportunityId",
                table: "work_orders",
                column: "SalesOpportunityId",
                principalTable: "sales_opportunities",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
