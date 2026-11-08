using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class InitialBaseline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "activity_reminder_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    NotificationKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ForTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_activity_reminder_logs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "approval_requests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovalMatrixId = table.Column<Guid>(type: "uuid", nullable: true),
                    DocumentType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CurrentLevelOrder = table.Column<int>(type: "integer", nullable: false),
                    InitiatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_approval_requests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "compliance_standards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    TemplateKey = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_compliance_standards", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "idempotency_records",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Method = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    StatusCode = table.Column<int>(type: "integer", nullable: false),
                    ResponseBody = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_idempotency_records", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "license_add_ons",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    MonthlyPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    AnnualPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    IncludedMessages = table.Column<int>(type: "integer", nullable: false),
                    ExtraMessagePrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ExtraNumberPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_license_add_ons", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "license_plans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    OfficeSeatMonthlyPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    OfficeSeatAnnualPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    FieldSeatMonthlyPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    FieldSeatAnnualPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    MinOfficeSeats = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_license_plans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "mrr_snapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SnapshotDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Mrr = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ActiveTenants = table.Column<int>(type: "integer", nullable: false),
                    TrialTenants = table.Column<int>(type: "integer", nullable: false),
                    PaidTenants = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mrr_snapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "pending_signups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    FullName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CompanyName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Country = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Industry = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    RequestedIp = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pending_signups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "permission_groups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_permission_groups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "platform_announcements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Body = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    Audience = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_platform_announcements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "platform_document_templates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Type = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    PreviewText = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Subject = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    HeaderHtml = table.Column<string>(type: "character varying(12000)", maxLength: 12000, nullable: true),
                    BodyHtml = table.Column<string>(type: "character varying(64000)", maxLength: 64000, nullable: true),
                    FooterHtml = table.Column<string>(type: "character varying(12000)", maxLength: 12000, nullable: true),
                    TermsHtml = table.Column<string>(type: "character varying(24000)", maxLength: 24000, nullable: true),
                    SignatureHtml = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    PageSize = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Orientation = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ShowLogo = table.Column<bool>(type: "boolean", nullable: false),
                    ShowTenantBranding = table.Column<bool>(type: "boolean", nullable: false),
                    ShowPoweredByEcosys = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_platform_document_templates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "platform_settings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    JsonValue = table.Column<string>(type: "character varying(64000)", maxLength: 64000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_platform_settings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "system_template_packs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Code = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ContractorType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    IsSystem = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_system_template_packs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tenants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    CompanyName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Country = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TimeZoneId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Industry = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ContactName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ContactEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ContactPhone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    County = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TaxPin = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LegalName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    TradingName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PostalAddress = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PhysicalAddress = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Website = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    VatNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RegistrationNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PlanName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LicenseStatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    MaxUsers = table.Column<int>(type: "integer", nullable: true),
                    MaxBranches = table.Column<int>(type: "integer", nullable: true),
                    TrialStartsAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TrialEndsAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TrialExtensionUsed = table.Column<bool>(type: "boolean", nullable: false),
                    TrialExtendedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TrialExtendedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    SubscriptionStartsAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SubscriptionEndsAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SuspendedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeactivatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeactivatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeactivationReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    LogoUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PrimaryColor = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SecondaryColor = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ShowPoweredByEcosys = table.Column<bool>(type: "boolean", nullable: false),
                    ReportTemplateConfigJson = table.Column<string>(type: "text", nullable: true),
                    DocumentDesignsJson = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "whatsapp_opt_outs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PhoneNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    OptedOutAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Keyword = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_whatsapp_opt_outs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "approval_request_steps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovalRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    LevelOrder = table.Column<int>(type: "integer", nullable: false),
                    LevelName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ApproverType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    RequiredPermission = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ApproverUserIdsCsv = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DecidedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DecidedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    DueAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReminderSentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_approval_request_steps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_approval_request_steps_approval_requests_ApprovalRequestId",
                        column: x => x.ApprovalRequestId,
                        principalTable: "approval_requests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "standard_requirements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    StandardId = table.Column<Guid>(type: "uuid", nullable: false),
                    Clause = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Guidance = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    EvidenceSources = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_standard_requirements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_standard_requirements_compliance_standards_StandardId",
                        column: x => x.StandardId,
                        principalTable: "compliance_standards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "group_capabilities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PermissionGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    Capability = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_group_capabilities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_group_capabilities_permission_groups_PermissionGroupId",
                        column: x => x.PermissionGroupId,
                        principalTable: "permission_groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "approval_matrices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_approval_matrices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_approval_matrices_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "asset_categories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    TemplatePackId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceAssetCategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    ParentCategoryName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Code = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ContractorType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Icon = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsSystemTemplate = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DefaultServiceIntervalMonths = table.Column<int>(type: "integer", nullable: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    DefaultCriticality = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: true),
                    DefaultMeterTypes = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_asset_categories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_asset_categories_system_template_packs_TemplatePackId",
                        column: x => x.TemplatePackId,
                        principalTable: "system_template_packs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_asset_categories_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "branches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentBranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Location = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    Address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ContactPerson = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_branches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_branches_branches_ParentBranchId",
                        column: x => x.ParentBranchId,
                        principalTable: "branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_branches_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "certification_definitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Issuer = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    RenewalPeriodMonths = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_certification_definitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_certification_definitions_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "email_outbox_messages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    EventKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    TemplateKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    RecipientEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    RecipientName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SenderName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SenderEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ReplyToEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Subject = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    HtmlBody = table.Column<string>(type: "character varying(64000)", maxLength: 64000, nullable: true),
                    TextBody = table.Column<string>(type: "character varying(32000)", maxLength: 32000, nullable: true),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    MaxAttempts = table.Column<int>(type: "integer", nullable: false),
                    NextAttemptAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastAttemptAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ErrorCategory = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    TriggeredByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CcAddressesJson = table.Column<string>(type: "text", nullable: true),
                    AttachmentsJson = table.Column<string>(type: "text", nullable: true),
                    DocumentType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: true),
                    TriggeredAutomatically = table.Column<bool>(type: "boolean", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_email_outbox_messages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_email_outbox_messages_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "email_settings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    Provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Host = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Port = table.Column<int>(type: "integer", nullable: false),
                    UseSsl = table.Column<bool>(type: "boolean", nullable: false),
                    Username = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ReplyToEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    FinanceReplyToEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    OperationsReplyToEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Password = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    EncryptedSecret = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    SenderName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    SenderAddress = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ManagedEmailAddress = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ManagedEmailStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "Pending"),
                    ManagedEmailStatusCheckedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ManagedEmailStatusDetail = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    InboundEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    UseCustomSmtp = table.Column<bool>(type: "boolean", nullable: false),
                    CustomFromAddress = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    EmailSignature = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    IsConfigured = table.Column<bool>(type: "boolean", nullable: false),
                    LastTestedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_email_settings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_email_settings_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

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
                name: "exchange_rates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    BaseCurrency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    QuoteCurrency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Rate = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exchange_rates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_exchange_rates_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "factory_settings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    IndustryTemplate = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ProductionUnit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ProductionUnitShort = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    UseGroupLevel = table.Column<bool>(type: "boolean", nullable: false),
                    UseSubSectionLevel = table.Column<bool>(type: "boolean", nullable: false),
                    WeeklyWindowEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    WeeklyWindowDay = table.Column<int>(type: "integer", nullable: false),
                    WeeklyWindowStart = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    WeeklyWindowEnd = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    LowCropStartMonth = table.Column<int>(type: "integer", nullable: true),
                    LowCropEndMonth = table.Column<int>(type: "integer", nullable: true),
                    TagPrefix = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: "TAG"),
                    NextTagNumber = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    RequireWorkOrderApproval = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    AutoApproveBreakdowns = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    ReworkWindowDays = table.Column<int>(type: "integer", nullable: false, defaultValue: 30),
                    OperatorReportingEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    OperatorReportKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ShiftsJson = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    PmHoursPerTechnicianWeek = table.Column<decimal>(type: "numeric(6,1)", precision: 6, scale: 1, nullable: false, defaultValue: 30m),
                    WindowCarryoverEscalateAt = table.Column<int>(type: "integer", nullable: false, defaultValue: 2),
                    WindowCarryoverToOverhaulAt = table.Column<int>(type: "integer", nullable: false, defaultValue: 3),
                    ProductionUnitValue = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    VatRatePercent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false, defaultValue: 16m),
                    PeakSeasonMonths = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false, defaultValue: "3,4,5,10,11,12"),
                    PersonalDataRetentionYears = table.Column<int>(type: "integer", nullable: false, defaultValue: 7),
                    DataResidency = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_factory_settings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_factory_settings_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "import_batches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ImportType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    FileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    TotalRows = table.Column<int>(type: "integer", nullable: false),
                    SuccessfulRows = table.Column<int>(type: "integer", nullable: false),
                    FailedRows = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_import_batches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_import_batches_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "intake_protocols",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    SourceType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CriteriaJson = table.Column<string>(type: "character varying(32000)", maxLength: 32000, nullable: false),
                    ActionsJson = table.Column<string>(type: "character varying(32000)", maxLength: 32000, nullable: false),
                    SourceConfigJson = table.Column<string>(type: "character varying(32000)", maxLength: 32000, nullable: false),
                    LastTriggeredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastTriggerStatus = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    LastError = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_intake_protocols", x => x.Id);
                    table.ForeignKey(
                        name: "FK_intake_protocols_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "leave_types",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    IsPaid = table.Column<bool>(type: "boolean", nullable: false),
                    AnnualAllowanceDays = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_leave_types", x => x.Id);
                    table.ForeignKey(
                        name: "FK_leave_types_tenants_TenantId",
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
                name: "org_nodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Level = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Location = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    TemplateKey = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ExternalId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ExternalSystem = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    WindowDay = table.Column<int>(type: "integer", nullable: true),
                    WindowEnabled = table.Column<bool>(type: "boolean", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_org_nodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_org_nodes_org_nodes_ParentId",
                        column: x => x.ParentId,
                        principalTable: "org_nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_org_nodes_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "platform_expenses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpenseDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Category = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Vendor = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    PaymentMethod = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    AttachmentUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_platform_expenses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_platform_expenses_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "platform_quotations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    QuotationNumber = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CustomerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CustomerEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Currency = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    Subtotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DiscountRate = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxRate = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: false),
                    TaxAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ValidUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ConvertedInvoiceId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_platform_quotations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_platform_quotations_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "rate_cards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Category = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    UnitRate = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Unit = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    SkillLevel = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    TimeType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    TaxRate = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rate_cards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_rate_cards_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "role_permissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CanViewWorkOrders = table.Column<bool>(type: "boolean", nullable: false),
                    CanCreateWorkOrders = table.Column<bool>(type: "boolean", nullable: false),
                    CanAssignWorkOrders = table.Column<bool>(type: "boolean", nullable: false),
                    CanCompleteWorkOrders = table.Column<bool>(type: "boolean", nullable: false),
                    CanViewKnowledge = table.Column<bool>(type: "boolean", nullable: false),
                    CanManageKnowledge = table.Column<bool>(type: "boolean", nullable: false),
                    CanViewMaterials = table.Column<bool>(type: "boolean", nullable: false),
                    CanRequestMaterials = table.Column<bool>(type: "boolean", nullable: false),
                    CanManageMaterials = table.Column<bool>(type: "boolean", nullable: false),
                    CanApproveMaterials = table.Column<bool>(type: "boolean", nullable: false),
                    CanIssueMaterials = table.Column<bool>(type: "boolean", nullable: false),
                    CanManageAssets = table.Column<bool>(type: "boolean", nullable: false),
                    CanManageSettings = table.Column<bool>(type: "boolean", nullable: false),
                    CanViewReports = table.Column<bool>(type: "boolean", nullable: false),
                    CanViewFinance = table.Column<bool>(type: "boolean", nullable: false),
                    CanManageFinance = table.Column<bool>(type: "boolean", nullable: false),
                    CanViewFinanceRates = table.Column<bool>(type: "boolean", nullable: false),
                    CanManageFinanceRates = table.Column<bool>(type: "boolean", nullable: false),
                    CanViewFinanceApprovals = table.Column<bool>(type: "boolean", nullable: false),
                    CanManageFinanceApprovals = table.Column<bool>(type: "boolean", nullable: false),
                    CanViewBillableWork = table.Column<bool>(type: "boolean", nullable: false),
                    CanManageBillableWork = table.Column<bool>(type: "boolean", nullable: false),
                    CanOverrideMaterialStock = table.Column<bool>(type: "boolean", nullable: false),
                    CanViewCrm = table.Column<bool>(type: "boolean", nullable: false),
                    CanManageCrm = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_role_permissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_role_permissions_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sla_definitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sla_definitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sla_definitions_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sla_matrix_cells",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Priority = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Criticality = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: false),
                    ResponseHours = table.Column<decimal>(type: "numeric(9,2)", precision: 9, scale: 2, nullable: false),
                    RepairHours = table.Column<decimal>(type: "numeric(9,2)", precision: 9, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sla_matrix_cells", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sla_matrix_cells_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "suppliers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ContactPerson = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    LeadTimeDays = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_suppliers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_suppliers_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tenant_api_keys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Label = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    KeyPrefix = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    KeyHash = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    LastUsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_api_keys", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tenant_api_keys_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tenant_finance_defaults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    EnableVat = table.Column<bool>(type: "boolean", nullable: false),
                    TaxName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DefaultVatRate = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    DefaultPaymentTerms = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DefaultInvoiceDueDays = table.Column<int>(type: "integer", nullable: false),
                    DefaultQuotationValidityDays = table.Column<int>(type: "integer", nullable: false),
                    DefaultExpenseApprovalRequired = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_finance_defaults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tenant_finance_defaults_tenants_TenantId",
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
                    BillingCycle = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    NextBillingDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SuspendedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SuspensionReason = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    GracePeriodDays = table.Column<int>(type: "integer", nullable: false),
                    IsPerpetual = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    OverdueWarningSentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    OfficeSeats = table.Column<int>(type: "integer", nullable: false),
                    FieldSeats = table.Column<int>(type: "integer", nullable: false),
                    MaxBranchesOverride = table.Column<int>(type: "integer", nullable: true),
                    OfficeSeatPriceOverride = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    FieldSeatPriceOverride = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    EmailIngestionOverride = table.Column<bool>(type: "boolean", nullable: true),
                    MonitoringIntegrationOverride = table.Column<bool>(type: "boolean", nullable: true),
                    WhatsAppAddOn = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_licenses", x => x.Id);
                    table.CheckConstraint("CK_tenant_licenses_FieldSeats_NonNegative", "\"FieldSeats\" >= 0");
                    table.CheckConstraint("CK_tenant_licenses_OfficeSeats_Positive", "\"OfficeSeats\" > 0");
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
                name: "tenant_module_settings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ModuleKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ValueJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_module_settings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tenant_module_settings_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tenant_notification_recipients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipientGroup = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_notification_recipients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tenant_notification_recipients_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tenant_notification_settings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    NotificationKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    EmailEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    InAppEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    SmsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_notification_settings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tenant_notification_settings_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tenant_operations_policies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequireTechnicianSignature = table.Column<bool>(type: "boolean", nullable: false),
                    RequireClientSignature = table.Column<bool>(type: "boolean", nullable: false),
                    AllowRemoteCompletion = table.Column<bool>(type: "boolean", nullable: false),
                    AutoSendServiceReportOnApproval = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_operations_policies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tenant_operations_policies_tenants_TenantId",
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
                name: "tenant_template_pack_installations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceTemplatePackId = table.Column<Guid>(type: "uuid", nullable: false),
                    InstalledByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    InstalledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_template_pack_installations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tenant_template_pack_installations_system_template_packs_So~",
                        column: x => x.SourceTemplatePackId,
                        principalTable: "system_template_packs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_tenant_template_pack_installations_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tenant_webhooks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    EventsJson = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    SecretHash = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_webhooks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tenant_webhooks_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "time_attendance_settings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoundingMinutes = table.Column<int>(type: "integer", nullable: false),
                    OvertimeThresholdHoursPerDay = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    GraceMinutes = table.Column<int>(type: "integer", nullable: false),
                    AutoClockOutAfterHours = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_time_attendance_settings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_time_attendance_settings_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "whatsapp_conversations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContactName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ContactPhone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastMessagePreview = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    LastMessageAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UnreadCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_whatsapp_conversations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_whatsapp_conversations_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "whatsapp_outbox_messages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    NotificationKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    TemplateName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Language = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ParametersJson = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ToPhone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RecipientName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RecipientUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    RecipientPortalUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    EntityType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    MaxAttempts = table.Column<int>(type: "integer", nullable: false),
                    NextAttemptAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastAttemptAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeliveredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReadAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FailedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ProviderMessageId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IdempotencyKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_whatsapp_outbox_messages", x => x.Id);
                    table.CheckConstraint("CK_whatsapp_outbox_messages_status", "\"Status\" IN ('Pending', 'Sending', 'Sent', 'Delivered', 'Read', 'Failed', 'Skipped')");
                    table.ForeignKey(
                        name: "FK_whatsapp_outbox_messages_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "whatsapp_settings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessAccountId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    PhoneNumberId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    AccessToken = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    WebhookVerifyToken = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    AutoReplyEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    AutoReplyMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    AllowedNotificationKeys = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_whatsapp_settings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_whatsapp_settings_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "whatsapp_templates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Category = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Language = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    BodyPreview = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_whatsapp_templates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_whatsapp_templates_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "whatsapp_verification_codes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClientPortalUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    PhoneNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CodeHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    ConsumedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_whatsapp_verification_codes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_whatsapp_verification_codes_tenants_TenantId",
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
                name: "approval_levels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovalMatrixId = table.Column<Guid>(type: "uuid", nullable: false),
                    LevelOrder = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    MinAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    MaxAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    ApproverType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    RequiredPermission = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ApproverUserIdsCsv = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ReminderAfterHours = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_approval_levels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_approval_levels_approval_matrices_ApprovalMatrixId",
                        column: x => x.ApprovalMatrixId,
                        principalTable: "approval_matrices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "asset_category_fields",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetCategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    FieldName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FieldLabel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    FieldType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DropdownOptions = table.Column<string>(type: "text", nullable: true),
                    Unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    HelpText = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_asset_category_fields", x => x.Id);
                    table.ForeignKey(
                        name: "FK_asset_category_fields_asset_categories_AssetCategoryId",
                        column: x => x.AssetCategoryId,
                        principalTable: "asset_categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "failure_codes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AssetCategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_failure_codes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_failure_codes_asset_categories_AssetCategoryId",
                        column: x => x.AssetCategoryId,
                        principalTable: "asset_categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_failure_codes_tenants_TenantId",
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
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    TemplatePackId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceTemplateId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssetCategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    Category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Code = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    WorkType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false, defaultValue: "PreventiveMaintenance"),
                    DefaultIntervalMonths = table.Column<int>(type: "integer", nullable: true),
                    EstimatedDurationHours = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    AutoScheduleByDefault = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    IsPlatformSeed = table.Column<bool>(type: "boolean", nullable: false),
                    IsSystemTemplate = table.Column<bool>(type: "boolean", nullable: false),
                    OptionalReportSectionsJson = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pm_templates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_pm_templates_asset_categories_AssetCategoryId",
                        column: x => x.AssetCategoryId,
                        principalTable: "asset_categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_pm_templates_system_template_packs_TemplatePackId",
                        column: x => x.TemplatePackId,
                        principalTable: "system_template_packs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_pm_templates_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "assignment_groups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SkillArea = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
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
                name: "numbering_settings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    DocumentType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Prefix = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Suffix = table.Column<string>(type: "text", nullable: true),
                    NextNumber = table.Column<long>(type: "bigint", nullable: false),
                    PaddingLength = table.Column<int>(type: "integer", nullable: false),
                    ResetFrequency = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IncludeYear = table.Column<bool>(type: "boolean", nullable: false),
                    YearFormat = table.Column<string>(type: "text", nullable: false),
                    IncludeMonth = table.Column<bool>(type: "boolean", nullable: false),
                    Separator = table.Column<string>(type: "text", nullable: false),
                    IsLocked = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    LastResetAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_numbering_settings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_numbering_settings_branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_numbering_settings_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    DefaultBranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    FullName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    HasEmail = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    Username = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PasswordHash = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    JobTitle = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Department = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    HasAllBranchAccess = table.Column<bool>(type: "boolean", nullable: false),
                    MustChangePassword = table.Column<bool>(type: "boolean", nullable: false),
                    InviteTokenHash = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    InviteTokenExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    InviteAcceptedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastCredentialSentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FailedLoginCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    LockedOutUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastFailedLoginAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    WhatsAppNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    WhatsAppVerifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    WhatsAppOptedInAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    WhatsAppOptedOutAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_users_branches_DefaultBranchId",
                        column: x => x.DefaultBranchId,
                        principalTable: "branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_users_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "job_plans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    WorkType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    AssetCategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    EstimatedHours = table.Column<decimal>(type: "numeric(9,2)", precision: 9, scale: 2, nullable: true),
                    RequiredCertificationId = table.Column<Guid>(type: "uuid", nullable: true),
                    RequiredPermitTypes = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    NeedsLineStop = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_plans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_job_plans_asset_categories_AssetCategoryId",
                        column: x => x.AssetCategoryId,
                        principalTable: "asset_categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_job_plans_certification_definitions_RequiredCertificationId",
                        column: x => x.RequiredCertificationId,
                        principalTable: "certification_definitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_job_plans_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "email_delivery_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    OutboxMessageId = table.Column<Guid>(type: "uuid", nullable: true),
                    EventKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    TemplateKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    RecipientEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Subject = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: true),
                    LastAttemptAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NextAttemptAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ErrorCategory = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    TriggeredByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ProviderMessageId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CcAddressesJson = table.Column<string>(type: "text", nullable: true),
                    DocumentType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: true),
                    TriggeredAutomatically = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_email_delivery_logs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_email_delivery_logs_email_outbox_messages_OutboxMessageId",
                        column: x => x.OutboxMessageId,
                        principalTable: "email_outbox_messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_email_delivery_logs_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "audit_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    FactoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    StandardId = table.Column<Guid>(type: "uuid", nullable: true),
                    Kind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ScheduledFor = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Auditor = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Outcome = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastReminderDays = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_audit_events_compliance_standards_StandardId",
                        column: x => x.StandardId,
                        principalTable: "compliance_standards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_audit_events_org_nodes_FactoryId",
                        column: x => x.FactoryId,
                        principalTable: "org_nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "cost_centres",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    FactoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    OrgNodeId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    ExternalId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ExternalSystem = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cost_centres", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cost_centres_org_nodes_FactoryId",
                        column: x => x.FactoryId,
                        principalTable: "org_nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_cost_centres_org_nodes_OrgNodeId",
                        column: x => x.OrgNodeId,
                        principalTable: "org_nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "glass_register_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    FactoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Location = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Material = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_glass_register_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_glass_register_items_org_nodes_FactoryId",
                        column: x => x.FactoryId,
                        principalTable: "org_nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "maintenance_windows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    FactoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    WindowDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StartsAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndsAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CapacityHours = table.Column<decimal>(type: "numeric(10,1)", precision: 10, scale: 1, nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    MovedFromDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SkipReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    PackedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PackedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    HandedBackAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    HandedBackByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    HandbackNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    HandbackSignatureDataUrl = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_maintenance_windows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_maintenance_windows_org_nodes_FactoryId",
                        column: x => x.FactoryId,
                        principalTable: "org_nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_maintenance_windows_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "overhaul_plans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    FactoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PlannedStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PlannedEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Budget = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ActualStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ActualEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_overhaul_plans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_overhaul_plans_org_nodes_FactoryId",
                        column: x => x.FactoryId,
                        principalTable: "org_nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_overhaul_plans_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "scheduled_reports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ReportType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Frequency = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DayOfMonth = table.Column<int>(type: "integer", nullable: true),
                    DayOfWeek = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    TimeOfDay = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    DateRangePreset = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    GroupBy = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    ClientId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    OrgNodeId = table.Column<Guid>(type: "uuid", nullable: true),
                    Recipients = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    LastRunAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NextRunAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastRunStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_scheduled_reports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_scheduled_reports_org_nodes_OrgNodeId",
                        column: x => x.OrgNodeId,
                        principalTable: "org_nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_scheduled_reports_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "stores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    FactoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Location = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IsMain = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_stores_org_nodes_FactoryId",
                        column: x => x.FactoryId,
                        principalTable: "org_nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stores_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "platform_invoices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceNumber = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    QuotationId = table.Column<Guid>(type: "uuid", nullable: true),
                    CustomerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CustomerEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Currency = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    Subtotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DiscountRate = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxRate = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: false),
                    TaxAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    AmountPaid = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Balance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IssueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_platform_invoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_platform_invoices_platform_quotations_QuotationId",
                        column: x => x.QuotationId,
                        principalTable: "platform_quotations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_platform_invoices_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "platform_quotation_lines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    QuotationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Taxable = table.Column<bool>(type: "boolean", nullable: false),
                    LineTotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_platform_quotation_lines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_platform_quotation_lines_platform_quotations_QuotationId",
                        column: x => x.QuotationId,
                        principalTable: "platform_quotations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "clients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ClientType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Location = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    ContactPerson = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ContactPhone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    SlaPlan = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SlaDefinitionId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActiveStartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ActiveDurationValue = table.Column<int>(type: "integer", nullable: true),
                    ActiveDurationUnit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ActiveEndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsProspect = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_clients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_clients_sla_definitions_SlaDefinitionId",
                        column: x => x.SlaDefinitionId,
                        principalTable: "sla_definitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_clients_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sla_rules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    SlaDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Priority = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ResponseTargetHours = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    ResolutionTargetHours = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    BusinessHoursOnly = table.Column<bool>(type: "boolean", nullable: false),
                    BusinessHoursStart = table.Column<TimeSpan>(type: "interval", nullable: true),
                    BusinessHoursEnd = table.Column<TimeSpan>(type: "interval", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sla_rules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sla_rules_sla_definitions_SlaDefinitionId",
                        column: x => x.SlaDefinitionId,
                        principalTable: "sla_definitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_sla_rules_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "contractors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Services = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ContactPerson = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    RegistrationNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TaxPin = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    ExternalId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ExternalSystem = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contractors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_contractors_suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_contractors_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "purchase_orders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PoNumber = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false, defaultValue: "Draft"),
                    OrderedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpectedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: "KES"),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_orders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_purchase_orders_suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_orders_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tenant_webhook_deliveries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    WebhookId = table.Column<Guid>(type: "uuid", nullable: false),
                    Event = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ResponseStatusCode = table.Column<int>(type: "integer", nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DurationMs = table.Column<int>(type: "integer", nullable: true),
                    AttemptedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_webhook_deliveries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tenant_webhook_deliveries_tenant_webhooks_WebhookId",
                        column: x => x.WebhookId,
                        principalTable: "tenant_webhooks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_tenant_webhook_deliveries_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "whatsapp_messages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Direction = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Body = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TemplateName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_whatsapp_messages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_whatsapp_messages_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_whatsapp_messages_whatsapp_conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "whatsapp_conversations",
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

            migrationBuilder.CreateTable(
                name: "check_schedules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PmTemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Frequency = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    OrgNodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsRoute = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_check_schedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_check_schedules_org_nodes_OrgNodeId",
                        column: x => x.OrgNodeId,
                        principalTable: "org_nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_check_schedules_pm_templates_PmTemplateId",
                        column: x => x.PmTemplateId,
                        principalTable: "pm_templates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_check_schedules_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "pm_template_sections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PmTemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    SectionName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pm_template_sections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_pm_template_sections_pm_templates_PmTemplateId",
                        column: x => x.PmTemplateId,
                        principalTable: "pm_templates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "pm_template_versions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PmTemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    SnapshotJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pm_template_versions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_pm_template_versions_pm_templates_PmTemplateId",
                        column: x => x.PmTemplateId,
                        principalTable: "pm_templates",
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
                name: "access_scopes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    PermissionGroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    Capability = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    ScopeType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ScopeEntityId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_access_scopes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_access_scopes_permission_groups_PermissionGroupId",
                        column: x => x.PermissionGroupId,
                        principalTable: "permission_groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_access_scopes_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "activities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    StartsAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndsAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AllDay = table.Column<bool>(type: "boolean", nullable: false),
                    Location = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    OrganizerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Agenda = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Minutes = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: true),
                    MinutesPublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    MinutesPublishedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReminderMinutesBefore = table.Column<int>(type: "integer", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancellationReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_activities", x => x.Id);
                    table.CheckConstraint("CK_activities_status", "\"Status\" IN ('Scheduled', 'Held', 'Cancelled')");
                    table.CheckConstraint("CK_activities_times", "\"EndsAt\" >= \"StartsAt\"");
                    table.CheckConstraint("CK_activities_type", "\"Type\" IN ('Meeting', 'SiteVisit', 'Inspection', 'Call')");
                    table.ForeignKey(
                        name: "FK_activities_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_activities_users_OrganizerUserId",
                        column: x => x.OrganizerUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "attachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    MimeType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    StoragePath = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    PublicUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    UploadedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SharedWithClient = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_attachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_attachments_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_attachments_users_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EntityName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EntityId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Details = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Severity = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_logs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_audit_logs_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_audit_logs_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "calendar_feed_tokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastUsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_calendar_feed_tokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_calendar_feed_tokens_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_calendar_feed_tokens_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "finance_approval_rules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovalArea = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ThresholdAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    RequiredRole = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RequiredApproverUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    RequiredAssignmentGroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_finance_approval_rules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_finance_approval_rules_assignment_groups_RequiredAssignment~",
                        column: x => x.RequiredAssignmentGroupId,
                        principalTable: "assignment_groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_finance_approval_rules_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_finance_approval_rules_users_RequiredApproverUserId",
                        column: x => x.RequiredApproverUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    EntityType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: true),
                    Severity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ReadAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ArchivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PushPendingSince = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notifications", x => x.Id);
                    table.CheckConstraint("CK_notifications_severity", "\"Severity\" IN ('Info', 'Warning', 'Critical')");
                    table.ForeignKey(
                        name: "FK_notifications_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_notifications_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "password_reset_tokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Purpose = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    SupersededAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RequestedIp = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_password_reset_tokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_password_reset_tokens_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "permission_thresholds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    PermissionGroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    Capability = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    MaximumAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_permission_thresholds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_permission_thresholds_permission_groups_PermissionGroupId",
                        column: x => x.PermissionGroupId,
                        principalTable: "permission_groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_permission_thresholds_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "push_subscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Endpoint = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    P256dh = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Auth = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UserAgent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    LastSuccessAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_push_subscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_push_subscriptions_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "stock_counts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CountNumber = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsBlind = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    Category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    StartedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PostedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    PostedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_counts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_stock_counts_branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_counts_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_stock_counts_users_PostedByUserId",
                        column: x => x.PostedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_stock_counts_users_StartedByUserId",
                        column: x => x.StartedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "user_branch_assignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_branch_assignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_branch_assignments_branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_branch_assignments_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_branch_assignments_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_certifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CertificationDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CertificateNumber = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    IssuedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpiresOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_certifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_certifications_certification_definitions_Certification~",
                        column: x => x.CertificationDefinitionId,
                        principalTable: "certification_definitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_certifications_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_certifications_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_dashboard_layouts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    LayoutJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_dashboard_layouts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_dashboard_layouts_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_dashboard_layouts_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_notification_preferences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    NotificationKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    EmailEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    InAppEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    PushEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    WhatsAppEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    SmsEnabled = table.Column<bool>(type: "boolean", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_notification_preferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_notification_preferences_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_org_node_assignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgNodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_org_node_assignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_org_node_assignments_org_nodes_OrgNodeId",
                        column: x => x.OrgNodeId,
                        principalTable: "org_nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_org_node_assignments_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_org_node_assignments_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_permission_groups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PermissionGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_permission_groups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_permission_groups_permission_groups_PermissionGroupId",
                        column: x => x.PermissionGroupId,
                        principalTable: "permission_groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_permission_groups_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_permission_overrides",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Capability = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    IsAllowed = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_permission_overrides", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_permission_overrides_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_permissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CanViewWorkOrders = table.Column<bool>(type: "boolean", nullable: false),
                    CanCreateWorkOrders = table.Column<bool>(type: "boolean", nullable: false),
                    CanAssignWorkOrders = table.Column<bool>(type: "boolean", nullable: false),
                    CanCompleteWorkOrders = table.Column<bool>(type: "boolean", nullable: false),
                    CanViewKnowledge = table.Column<bool>(type: "boolean", nullable: false),
                    CanManageKnowledge = table.Column<bool>(type: "boolean", nullable: false),
                    CanViewMaterials = table.Column<bool>(type: "boolean", nullable: false),
                    CanRequestMaterials = table.Column<bool>(type: "boolean", nullable: false),
                    CanManageMaterials = table.Column<bool>(type: "boolean", nullable: false),
                    CanApproveMaterials = table.Column<bool>(type: "boolean", nullable: false),
                    CanIssueMaterials = table.Column<bool>(type: "boolean", nullable: false),
                    CanManageAssets = table.Column<bool>(type: "boolean", nullable: false),
                    CanManageSettings = table.Column<bool>(type: "boolean", nullable: false),
                    CanViewReports = table.Column<bool>(type: "boolean", nullable: false),
                    CanViewFinance = table.Column<bool>(type: "boolean", nullable: false),
                    CanManageFinance = table.Column<bool>(type: "boolean", nullable: false),
                    CanViewFinanceRates = table.Column<bool>(type: "boolean", nullable: false),
                    CanManageFinanceRates = table.Column<bool>(type: "boolean", nullable: false),
                    CanViewFinanceApprovals = table.Column<bool>(type: "boolean", nullable: false),
                    CanManageFinanceApprovals = table.Column<bool>(type: "boolean", nullable: false),
                    CanViewBillableWork = table.Column<bool>(type: "boolean", nullable: false),
                    CanManageBillableWork = table.Column<bool>(type: "boolean", nullable: false),
                    CanOverrideMaterialStock = table.Column<bool>(type: "boolean", nullable: false),
                    CanViewCrm = table.Column<bool>(type: "boolean", nullable: false),
                    CanManageCrm = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_permissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_permissions_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_sessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    JwtId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    LoginAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LogoutAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsRevoked = table.Column<bool>(type: "boolean", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_sessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_sessions_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_sessions_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "job_plan_steps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    JobPlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    Instruction = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    EstimatedMinutes = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_plan_steps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_job_plan_steps_job_plans_JobPlanId",
                        column: x => x.JobPlanId,
                        principalTable: "job_plans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "maintenance_budgets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    FactoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    CostCentreId = table.Column<Guid>(type: "uuid", nullable: true),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Category = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_maintenance_budgets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_maintenance_budgets_cost_centres_CostCentreId",
                        column: x => x.CostCentreId,
                        principalTable: "cost_centres",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_maintenance_budgets_org_nodes_FactoryId",
                        column: x => x.FactoryId,
                        principalTable: "org_nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "maintenance_window_handback_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    WindowId = table.Column<Guid>(type: "uuid", nullable: false),
                    Prompt = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    Done = table.Column<bool>(type: "boolean", nullable: false),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DoneByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DoneAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_maintenance_window_handback_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_maintenance_window_handback_items_maintenance_windows_Windo~",
                        column: x => x.WindowId,
                        principalTable: "maintenance_windows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "purchase_requisitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequisitionNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    FactoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    StoreId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    NeededBy = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RequestedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DecidedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RejectReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_requisitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_purchase_requisitions_org_nodes_FactoryId",
                        column: x => x.FactoryId,
                        principalTable: "org_nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_requisitions_stores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "stores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "store_bins",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    StoreId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_store_bins", x => x.Id);
                    table.ForeignKey(
                        name: "FK_store_bins_stores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "stores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "platform_invoice_lines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Taxable = table.Column<bool>(type: "boolean", nullable: false),
                    LineTotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_platform_invoice_lines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_platform_invoice_lines_platform_invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "platform_invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "platform_payments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentNumber = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    Method = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PaidAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_platform_payments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_platform_payments_platform_invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "platform_invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_platform_payments_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "client_contacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    FullName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    JobTitle = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Role = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_client_contacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_client_contacts_clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "client_portal_users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    FullName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    InvitedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    WhatsAppNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    WhatsAppVerifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    WhatsAppConsentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    WhatsAppConsentRecordedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    WhatsAppConsentMethod = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ReceivesWhatsApp = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_client_portal_users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_client_portal_users_clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_client_portal_users_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "client_preferential_rates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    RateName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    RateType = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    ItemType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Unit = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    DefaultRate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PreferentialRate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    RequiresApproval = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_client_preferential_rates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_client_preferential_rates_clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_client_preferential_rates_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_client_preferential_rates_users_ApprovedByUserId",
                        column: x => x.ApprovedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_client_preferential_rates_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "contracts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContractNumber = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ContractType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Status = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DurationMonths = table.Column<int>(type: "integer", nullable: true),
                    BillingModel = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Currency = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    ContractValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    RenewalNoticeDays = table.Column<int>(type: "integer", nullable: false),
                    RenewalBehavior = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    IncludedServices = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ExcludedServices = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ActivatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SubmittedForApprovalAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpiryCheckedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RenewalOwnerUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    AccountManagerUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    InternalApproverUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    RenewalStartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RenewedFromContractId = table.Column<Guid>(type: "uuid", nullable: true),
                    RenewedToContractId = table.Column<Guid>(type: "uuid", nullable: true),
                    TerminationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TerminationReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    SuspensionReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CancellationReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    LastReminderSentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NextReminderAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AutoExpireEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    RenewalRisk = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    GracePeriodDays = table.Column<int>(type: "integer", nullable: false),
                    AllowPreferentialRatesDuringGracePeriod = table.Column<bool>(type: "boolean", nullable: false),
                    AllowSlaDuringGracePeriod = table.Column<bool>(type: "boolean", nullable: false),
                    GracePeriodPolicy = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ResponseTimeHours = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    ResolutionTimeHours = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    WorkingHoursPolicy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PauseSlaOnHold = table.Column<bool>(type: "boolean", nullable: false),
                    EscalationEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    PriorityLevel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    GracePeriodAllowsSla = table.Column<bool>(type: "boolean", nullable: false),
                    SlaMatrixJson = table.Column<string>(type: "text", nullable: true),
                    IsArchived = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contracts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_contracts_clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_contracts_contracts_RenewedFromContractId",
                        column: x => x.RenewedFromContractId,
                        principalTable: "contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_contracts_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_contracts_users_AccountManagerUserId",
                        column: x => x.AccountManagerUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_contracts_users_InternalApproverUserId",
                        column: x => x.InternalApproverUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_contracts_users_RenewalOwnerUserId",
                        column: x => x.RenewalOwnerUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "email_intake_settings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    IntakeEmailAddress = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    MailboxProvider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Host = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Port = table.Column<int>(type: "integer", nullable: false),
                    UseSsl = table.Column<bool>(type: "boolean", nullable: false),
                    Username = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    EncryptedPassword = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    DefaultClientId = table.Column<Guid>(type: "uuid", nullable: true),
                    DefaultBranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    DefaultAssignmentGroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    DefaultPriority = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreateWorkOrderFromUnknownSender = table.Column<bool>(type: "boolean", nullable: false),
                    SubjectParsingRules = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    AllowedSenderDomains = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    LastCheckedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsConnectionHealthy = table.Column<bool>(type: "boolean", nullable: false),
                    LastError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    AutoReplyEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    ImapUidValidity = table.Column<long>(type: "bigint", nullable: true),
                    ImapLastUid = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_email_intake_settings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_email_intake_settings_assignment_groups_DefaultAssignmentGr~",
                        column: x => x.DefaultAssignmentGroupId,
                        principalTable: "assignment_groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_email_intake_settings_branches_DefaultBranchId",
                        column: x => x.DefaultBranchId,
                        principalTable: "branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_email_intake_settings_clients_DefaultClientId",
                        column: x => x.DefaultClientId,
                        principalTable: "clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_email_intake_settings_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sites",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    SiteCode = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    SiteName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SiteType = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    StreetAddress = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    AreaEstate = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    TownCity = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    County = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Country = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Region = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ContactPerson = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ContactPhone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ContactEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    AlternateContact = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    OperatingHours = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AccessNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    SpecialInstructions = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Latitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    Longitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sites_clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_sites_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
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
                name: "maintenance_agreements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    AgreementNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ContractorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Scope = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    FactoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Value = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    ResponseHours = table.Column<int>(type: "integer", nullable: true),
                    RepairHours = table.Column<int>(type: "integer", nullable: true),
                    VisitEveryMonths = table.Column<int>(type: "integer", nullable: true),
                    NextVisitDue = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RenewalNoticeDays = table.Column<int>(type: "integer", nullable: false),
                    RenewalAlertSentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_maintenance_agreements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_maintenance_agreements_contractors_ContractorId",
                        column: x => x.ContractorId,
                        principalTable: "contractors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_maintenance_agreements_org_nodes_FactoryId",
                        column: x => x.FactoryId,
                        principalTable: "org_nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "pm_template_questions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PmTemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    SectionId = table.Column<Guid>(type: "uuid", nullable: true),
                    SectionName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Prompt = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    HelperText = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ResponseType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    DefaultValue = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    MinValue = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    MaxValue = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    EvidenceRequired = table.Column<bool>(type: "boolean", nullable: false),
                    TechnicianRemarksAllowed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    RequiresNoteOnFail = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    OptionsJson = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    FailAction = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    FeedsMeterType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pm_template_questions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_pm_template_questions_pm_template_sections_SectionId",
                        column: x => x.SectionId,
                        principalTable: "pm_template_sections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_pm_template_questions_pm_templates_PmTemplateId",
                        column: x => x.PmTemplateId,
                        principalTable: "pm_templates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "action_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActivityId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    AssigneeUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssigneeExternalName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    EntityType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_action_items", x => x.Id);
                    table.CheckConstraint("CK_action_items_status", "\"Status\" IN ('Open', 'Done', 'Cancelled')");
                    table.ForeignKey(
                        name: "FK_action_items_activities_ActivityId",
                        column: x => x.ActivityId,
                        principalTable: "activities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_action_items_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_action_items_users_AssigneeUserId",
                        column: x => x.AssigneeUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "activity_minutes_addenda",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActivityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Text = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    AuthorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_activity_minutes_addenda", x => x.Id);
                    table.ForeignKey(
                        name: "FK_activity_minutes_addenda_activities_ActivityId",
                        column: x => x.ActivityId,
                        principalTable: "activities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_activity_minutes_addenda_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_activity_minutes_addenda_users_AuthorUserId",
                        column: x => x.AuthorUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "contractor_documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContractorId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
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
                    table.PrimaryKey("PK_contractor_documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_contractor_documents_attachments_AttachmentId",
                        column: x => x.AttachmentId,
                        principalTable: "attachments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_contractor_documents_contractors_ContractorId",
                        column: x => x.ContractorId,
                        principalTable: "contractors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "contractor_invoices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContractorId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceNumber = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    InvoiceDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    VatAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    MatchIssues = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    AttachmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DecidedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DecisionNote = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contractor_invoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_contractor_invoices_attachments_AttachmentId",
                        column: x => x.AttachmentId,
                        principalTable: "attachments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_contractor_invoices_contractors_ContractorId",
                        column: x => x.ContractorId,
                        principalTable: "contractors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "material_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ItemName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UnitOfMeasure = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    QuantityOnHand = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ReorderLevel = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    UnitCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Barcode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PreferredSupplierId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReorderQuantity = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsCriticalSpare = table.Column<bool>(type: "boolean", nullable: false),
                    IsSerialised = table.Column<bool>(type: "boolean", nullable: false),
                    IsRepairable = table.Column<bool>(type: "boolean", nullable: false),
                    PurchaseUnitOfMeasure = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    PurchaseUnitFactor = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 1m),
                    ExternalId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ExternalSystem = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    IsChemical = table.Column<bool>(type: "boolean", nullable: false),
                    ChemicalKind = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    HazardClass = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    FoodGradeClass = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    SdsAttachmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    SdsIssuedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_material_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_material_items_attachments_SdsAttachmentId",
                        column: x => x.SdsAttachmentId,
                        principalTable: "attachments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_material_items_suppliers_PreferredSupplierId",
                        column: x => x.PreferredSupplierId,
                        principalTable: "suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_material_items_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "waste_disposals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    FactoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    DisposedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    WasteType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Unit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ContractorId = table.Column<Guid>(type: "uuid", nullable: true),
                    HandlerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    LicenceNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ConsignmentNote = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    AttachmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    RecordedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_waste_disposals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_waste_disposals_attachments_AttachmentId",
                        column: x => x.AttachmentId,
                        principalTable: "attachments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_waste_disposals_contractors_ContractorId",
                        column: x => x.ContractorId,
                        principalTable: "contractors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_waste_disposals_org_nodes_FactoryId",
                        column: x => x.FactoryId,
                        principalTable: "org_nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "store_counts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CountNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    StoreId = table.Column<Guid>(type: "uuid", nullable: false),
                    BinId = table.Column<Guid>(type: "uuid", nullable: true),
                    CriticalOnly = table.Column<bool>(type: "boolean", nullable: false),
                    IsBlind = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    StartedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PostedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PostedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    VarianceValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_store_counts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_store_counts_store_bins_BinId",
                        column: x => x.BinId,
                        principalTable: "store_bins",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_store_counts_stores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "stores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "activity_attendees",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActivityId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClientContactId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExternalName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ExternalEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Response = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Attended = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_activity_attendees", x => x.Id);
                    table.CheckConstraint("CK_activity_attendees_one_kind", "(CASE WHEN \"UserId\" IS NULL THEN 0 ELSE 1 END) + (CASE WHEN \"ClientContactId\" IS NULL THEN 0 ELSE 1 END) + (CASE WHEN \"ExternalEmail\" IS NULL AND \"ExternalName\" IS NULL THEN 0 ELSE 1 END) = 1");
                    table.CheckConstraint("CK_activity_attendees_response", "\"Response\" IN ('Pending', 'Accepted', 'Declined')");
                    table.ForeignKey(
                        name: "FK_activity_attendees_activities_ActivityId",
                        column: x => x.ActivityId,
                        principalTable: "activities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_activity_attendees_client_portal_users_ClientContactId",
                        column: x => x.ClientContactId,
                        principalTable: "client_portal_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_activity_attendees_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_activity_attendees_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "contract_asset_categories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContractId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetCategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssetCategoryName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contract_asset_categories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_contract_asset_categories_asset_categories_AssetCategoryId",
                        column: x => x.AssetCategoryId,
                        principalTable: "asset_categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_contract_asset_categories_contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_contract_asset_categories_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "contract_lifecycle_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContractId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    OldStatus = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    NewStatus = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    Note = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contract_lifecycle_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_contract_lifecycle_events_contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_contract_lifecycle_events_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_contract_lifecycle_events_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "contract_rate_cards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContractId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Currency = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EffectiveTo = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contract_rate_cards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_contract_rate_cards_contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_contract_rate_cards_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "contract_reminder_rules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContractId = table.Column<Guid>(type: "uuid", nullable: true),
                    DaysBeforeExpiry = table.Column<int>(type: "integer", nullable: false),
                    NotifyAccountManager = table.Column<bool>(type: "boolean", nullable: false),
                    NotifyRenewalOwner = table.Column<bool>(type: "boolean", nullable: false),
                    NotifySupervisor = table.Column<bool>(type: "boolean", nullable: false),
                    NotifyFinance = table.Column<bool>(type: "boolean", nullable: false),
                    NotifyTenantAdmin = table.Column<bool>(type: "boolean", nullable: false),
                    EmailEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    InAppEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    EscalationEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    EscalationAfterDays = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contract_reminder_rules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_contract_reminder_rules_contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_contract_reminder_rules_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "contract_renewals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginalContractId = table.Column<Guid>(type: "uuid", nullable: false),
                    RenewedContractId = table.Column<Guid>(type: "uuid", nullable: true),
                    RenewalStatus = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    RenewalStartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RenewalCompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contract_renewals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_contract_renewals_contracts_OriginalContractId",
                        column: x => x.OriginalContractId,
                        principalTable: "contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_contract_renewals_contracts_RenewedContractId",
                        column: x => x.RenewedContractId,
                        principalTable: "contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_contract_renewals_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_contract_renewals_users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "contract_work_order_types",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContractId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkOrderTypeId = table.Column<Guid>(type: "uuid", nullable: true),
                    WorkOrderTypeName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contract_work_order_types", x => x.Id);
                    table.ForeignKey(
                        name: "FK_contract_work_order_types_contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_contract_work_order_types_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "contract_sites",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContractId = table.Column<Guid>(type: "uuid", nullable: false),
                    SiteId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contract_sites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_contract_sites_contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_contract_sites_sites_SiteId",
                        column: x => x.SiteId,
                        principalTable: "sites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_contract_sites_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "systems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    SiteId = table.Column<Guid>(type: "uuid", nullable: false),
                    SystemName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SystemType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Manufacturer = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Model = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    SerialNumber = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    InstallationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_systems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_systems_clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_systems_sites_SiteId",
                        column: x => x.SiteId,
                        principalTable: "sites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_systems_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "branch_material_stocks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    MaterialId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuantityOnHand = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ReorderLevel = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    UnitCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_branch_material_stocks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_branch_material_stocks_branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_branch_material_stocks_material_items_MaterialId",
                        column: x => x.MaterialId,
                        principalTable: "material_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_branch_material_stocks_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "job_plan_resources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    JobPlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    MaterialItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_plan_resources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_job_plan_resources_job_plans_JobPlanId",
                        column: x => x.JobPlanId,
                        principalTable: "job_plans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_job_plan_resources_material_items_MaterialItemId",
                        column: x => x.MaterialItemId,
                        principalTable: "material_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "purchase_order_lines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchaseOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    MaterialItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    ItemName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    QuantityOrdered = table.Column<decimal>(type: "numeric(14,3)", precision: 14, scale: 3, nullable: false),
                    QuantityReceived = table.Column<decimal>(type: "numeric(14,3)", precision: 14, scale: 3, nullable: false),
                    UnitCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    LineTotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_order_lines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_purchase_order_lines_material_items_MaterialItemId",
                        column: x => x.MaterialItemId,
                        principalTable: "material_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_order_lines_purchase_orders_PurchaseOrderId",
                        column: x => x.PurchaseOrderId,
                        principalTable: "purchase_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_purchase_order_lines_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "stock_count_lines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    StockCountId = table.Column<Guid>(type: "uuid", nullable: false),
                    MaterialItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpectedQuantity = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CountedQuantity = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    UnitCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    NeedsRecount = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CountedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CountedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_count_lines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_stock_count_lines_material_items_MaterialItemId",
                        column: x => x.MaterialItemId,
                        principalTable: "material_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_count_lines_stock_counts_StockCountId",
                        column: x => x.StockCountId,
                        principalTable: "stock_counts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_stock_count_lines_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "stock_transfers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromBranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    ToBranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    MaterialId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompletedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ReferenceNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_transfers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_stock_transfers_branches_FromBranchId",
                        column: x => x.FromBranchId,
                        principalTable: "branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_transfers_branches_ToBranchId",
                        column: x => x.ToBranchId,
                        principalTable: "branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_transfers_material_items_MaterialId",
                        column: x => x.MaterialId,
                        principalTable: "material_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_transfers_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_stock_transfers_users_ApprovedByUserId",
                        column: x => x.ApprovedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_stock_transfers_users_CompletedByUserId",
                        column: x => x.CompletedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_stock_transfers_users_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "store_stocks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    StoreId = table.Column<Guid>(type: "uuid", nullable: false),
                    MaterialItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    BinId = table.Column<Guid>(type: "uuid", nullable: true),
                    OnHand = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Reserved = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    MinLevel = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ReorderPoint = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    MaxLevel = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    AverageCost = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    LastCountedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LowAlertSentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_store_stocks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_store_stocks_material_items_MaterialItemId",
                        column: x => x.MaterialItemId,
                        principalTable: "material_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_store_stocks_store_bins_BinId",
                        column: x => x.BinId,
                        principalTable: "store_bins",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_store_stocks_stores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "stores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "vendor_catalogue_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    MaterialItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    VendorPartNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    LeadTimeDays = table.Column<int>(type: "integer", nullable: true),
                    MinOrderQuantity = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    ValidUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsPreferred = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vendor_catalogue_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_vendor_catalogue_items_material_items_MaterialItemId",
                        column: x => x.MaterialItemId,
                        principalTable: "material_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_vendor_catalogue_items_suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "store_count_lines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    StoreCountId = table.Column<Guid>(type: "uuid", nullable: false),
                    MaterialItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpectedQuantity = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CountedQuantity = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    UnitCost = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_store_count_lines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_store_count_lines_material_items_MaterialItemId",
                        column: x => x.MaterialItemId,
                        principalTable: "material_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_store_count_lines_store_counts_StoreCountId",
                        column: x => x.StoreCountId,
                        principalTable: "store_counts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "contract_rate_rules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    RateCardId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkOrderTypeId = table.Column<Guid>(type: "uuid", nullable: true),
                    WorkOrderTypeName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    AssetCategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssetCategoryName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    SiteId = table.Column<Guid>(type: "uuid", nullable: true),
                    Priority = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    BillingModel = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    CalloutFee = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    FixedVisitFee = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    LabourHourlyRate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    OvertimeHourlyRate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    WeekendHourlyRate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    PublicHolidayHourlyRate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    EmergencyMultiplier = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    TravelCharge = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    MaterialMarkupPercent = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    DiscountPercent = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    MinimumBillableHours = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    IncludedLabourHours = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    VatApplicable = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contract_rate_rules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_contract_rate_rules_asset_categories_AssetCategoryId",
                        column: x => x.AssetCategoryId,
                        principalTable: "asset_categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_contract_rate_rules_contract_rate_cards_RateCardId",
                        column: x => x.RateCardId,
                        principalTable: "contract_rate_cards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_contract_rate_rules_sites_SiteId",
                        column: x => x.SiteId,
                        principalTable: "sites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_contract_rate_rules_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "assets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: true),
                    SiteId = table.Column<Guid>(type: "uuid", nullable: true),
                    SystemId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssetCategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    OrgNodeId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssetName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AssetCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AssetType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    OwnershipType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SerialNumber = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Manufacturer = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Model = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    InstallationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CommissioningDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DecommissionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DecommissionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    WarrantyExpiryDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    WarrantyStartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    WarrantyPeriodMonths = table.Column<int>(type: "integer", nullable: true),
                    YearOfManufacture = table.Column<int>(type: "integer", nullable: true),
                    CapacityRating = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PhysicalDescription = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Location = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    BuildingBlock = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    FloorLevel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RoomArea = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LocationDescription = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RecommendedPmFrequency = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PmFrequencyValue = table.Column<int>(type: "integer", nullable: true),
                    PmFrequencyUnit = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    AutoSchedulePm = table.Column<bool>(type: "boolean", nullable: false),
                    LastPmDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NextPmDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    MeterLabel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CurrentMeterReading = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    MeterInterval = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    MeterBuffer = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    DefaultAssignmentGroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    Notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ParentAssetId = table.Column<Guid>(type: "uuid", nullable: true),
                    Criticality = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: true),
                    OperationalStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "Operational"),
                    LostProductionPerHour = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    DisposalDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DisposalMethod = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ExternalId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ExternalSystem = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_assets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_assets_asset_categories_AssetCategoryId",
                        column: x => x.AssetCategoryId,
                        principalTable: "asset_categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_assets_assets_ParentAssetId",
                        column: x => x.ParentAssetId,
                        principalTable: "assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_assets_assignment_groups_DefaultAssignmentGroupId",
                        column: x => x.DefaultAssignmentGroupId,
                        principalTable: "assignment_groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_assets_branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_assets_clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_assets_org_nodes_OrgNodeId",
                        column: x => x.OrgNodeId,
                        principalTable: "org_nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_assets_sites_SiteId",
                        column: x => x.SiteId,
                        principalTable: "sites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_assets_systems_SystemId",
                        column: x => x.SystemId,
                        principalTable: "systems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_assets_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "purchase_order_receipt_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchaseOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchaseOrderLineId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(14,3)", precision: 14, scale: 3, nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReceivedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReceivedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_order_receipt_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_purchase_order_receipt_events_branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_purchase_order_receipt_events_purchase_order_lines_Purchase~",
                        column: x => x.PurchaseOrderLineId,
                        principalTable: "purchase_order_lines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_purchase_order_receipt_events_purchase_orders_PurchaseOrder~",
                        column: x => x.PurchaseOrderId,
                        principalTable: "purchase_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_purchase_order_receipt_events_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "asset_custom_field_values",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    FieldDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Value = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_asset_custom_field_values", x => x.Id);
                    table.ForeignKey(
                        name: "FK_asset_custom_field_values_asset_category_fields_FieldDefini~",
                        column: x => x.FieldDefinitionId,
                        principalTable: "asset_category_fields",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_asset_custom_field_values_assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_asset_custom_field_values_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "asset_documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DocumentType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CurrentVersion = table.Column<int>(type: "integer", nullable: false),
                    ExpiryDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsArchived = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_asset_documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_asset_documents_assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_asset_documents_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "asset_lifecycle_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FromOrgNodeId = table.Column<Guid>(type: "uuid", nullable: true),
                    ToOrgNodeId = table.Column<Guid>(type: "uuid", nullable: true),
                    FromParentAssetId = table.Column<Guid>(type: "uuid", nullable: true),
                    ToParentAssetId = table.Column<Guid>(type: "uuid", nullable: true),
                    FromValue = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ToValue = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    PerformedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    PerformedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_asset_lifecycle_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_asset_lifecycle_events_assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_asset_lifecycle_events_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "asset_parts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    MaterialItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuantityFitted = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Position = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_asset_parts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_asset_parts_assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_asset_parts_material_items_MaterialItemId",
                        column: x => x.MaterialItemId,
                        principalTable: "material_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "asset_tags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    TagNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Symbology = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IssuedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RetiredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RetiredReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ReplacedByTagId = table.Column<Guid>(type: "uuid", nullable: true),
                    PrintCount = table.Column<int>(type: "integer", nullable: false),
                    LastPrintedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_asset_tags", x => x.Id);
                    table.ForeignKey(
                        name: "FK_asset_tags_assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_asset_tags_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "check_runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CheckScheduleId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgNodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    PmTemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    TemplateVersion = table.Column<int>(type: "integer", nullable: false),
                    PeriodKey = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ShiftName = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    DueFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DueBy = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompletedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    FailedCount = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    SignatureDataUrl = table.Column<string>(type: "text", nullable: true),
                    MissedNotifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_check_runs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_check_runs_assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_check_runs_check_schedules_CheckScheduleId",
                        column: x => x.CheckScheduleId,
                        principalTable: "check_schedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_check_runs_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "check_schedule_assets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CheckScheduleId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_check_schedule_assets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_check_schedule_assets_assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_check_schedule_assets_check_schedules_CheckScheduleId",
                        column: x => x.CheckScheduleId,
                        principalTable: "check_schedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "contract_assets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContractId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contract_assets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_contract_assets_assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_contract_assets_contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_contract_assets_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "contract_pm_entitlements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContractId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssetCategory = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    WorkOrderType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    VisitsPerPeriod = table.Column<int>(type: "integer", nullable: false),
                    PeriodType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    IncludedVisits = table.Column<int>(type: "integer", nullable: false),
                    UsedVisits = table.Column<int>(type: "integer", nullable: false),
                    RemainingVisits = table.Column<int>(type: "integer", nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contract_pm_entitlements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_contract_pm_entitlements_assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_contract_pm_entitlements_contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_contract_pm_entitlements_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
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
                name: "maintenance_agreement_assets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    AgreementId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_maintenance_agreement_assets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_maintenance_agreement_assets_assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_maintenance_agreement_assets_maintenance_agreements_Agreeme~",
                        column: x => x.AgreementId,
                        principalTable: "maintenance_agreements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "meters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    MeterType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Unit = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    IsCumulative = table.Column<bool>(type: "boolean", nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CurrentValue = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    LastReadingAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    MinThreshold = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    MaxThreshold = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    ExternalId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ExternalSystem = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_meters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_meters_assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_meters_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "monitoring_webhook_integrations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    ToolType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    EndpointSlug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SecretHash = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    DefaultClientId = table.Column<Guid>(type: "uuid", nullable: true),
                    DefaultAssetId = table.Column<Guid>(type: "uuid", nullable: true),
                    DefaultBranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    DefaultAssignmentGroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    DefaultPriority = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreateWorkOrderOnAlert = table.Column<bool>(type: "boolean", nullable: false),
                    PayloadMappingJson = table.Column<string>(type: "character varying(16000)", maxLength: 16000, nullable: true),
                    LastReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastStatus = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LastError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_monitoring_webhook_integrations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_monitoring_webhook_integrations_assets_DefaultAssetId",
                        column: x => x.DefaultAssetId,
                        principalTable: "assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_monitoring_webhook_integrations_assignment_groups_DefaultAs~",
                        column: x => x.DefaultAssignmentGroupId,
                        principalTable: "assignment_groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_monitoring_webhook_integrations_branches_DefaultBranchId",
                        column: x => x.DefaultBranchId,
                        principalTable: "branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_monitoring_webhook_integrations_clients_DefaultClientId",
                        column: x => x.DefaultClientId,
                        principalTable: "clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_monitoring_webhook_integrations_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "spare_units",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    MaterialItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    SerialNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    StoreId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssetId = table.Column<Guid>(type: "uuid", nullable: true),
                    RepairSupplierId = table.Column<Guid>(type: "uuid", nullable: true),
                    RepairReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SentForRepairAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpectedBackAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RepairCount = table.Column<int>(type: "integer", nullable: false),
                    TotalRepairCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    UnitCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_spare_units", x => x.Id);
                    table.ForeignKey(
                        name: "FK_spare_units_assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_spare_units_material_items_MaterialItemId",
                        column: x => x.MaterialItemId,
                        principalTable: "material_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_spare_units_stores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "stores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_spare_units_suppliers_RepairSupplierId",
                        column: x => x.RepairSupplierId,
                        principalTable: "suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "asset_document_versions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    AttachmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    UploadedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UploadedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_asset_document_versions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_asset_document_versions_asset_documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "asset_documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_asset_document_versions_attachments_AttachmentId",
                        column: x => x.AttachmentId,
                        principalTable: "attachments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_asset_document_versions_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "check_run_answers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CheckRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestionId = table.Column<Guid>(type: "uuid", nullable: true),
                    SectionName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Prompt = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ResponseType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Unit = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    MinValue = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    MaxValue = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    Value = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    NumberValue = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    Passed = table.Column<bool>(type: "boolean", nullable: true),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    AttachmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    FailAction = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RaisedWorkOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_check_run_answers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_check_run_answers_check_runs_CheckRunId",
                        column: x => x.CheckRunId,
                        principalTable: "check_runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "meter_readings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    MeterId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Value = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Delta = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    ReadAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IsReset = table.Column<bool>(type: "boolean", nullable: false),
                    IsOutOfRange = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    RecordedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    RecordedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_meter_readings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_meter_readings_assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "assets",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_meter_readings_meters_MeterId",
                        column: x => x.MeterId,
                        principalTable: "meters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_meter_readings_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "preventive_maintenance_plans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    SiteId = table.Column<Guid>(type: "uuid", nullable: true),
                    PmTemplateId = table.Column<Guid>(type: "uuid", nullable: true),
                    DefaultAssignmentGroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    TriggerType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Frequency = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FrequencyUnit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    FrequencyInterval = table.Column<int>(type: "integer", nullable: false),
                    PreferredDayOfWeek = table.Column<int>(type: "integer", nullable: true),
                    PreferredDayOfMonth = table.Column<int>(type: "integer", nullable: true),
                    MeterInterval = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    MeterBuffer = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    DaysBeforeDue = table.Column<int>(type: "integer", nullable: false),
                    AutoSchedule = table.Column<bool>(type: "boolean", nullable: false),
                    AutoAssign = table.Column<bool>(type: "boolean", nullable: false),
                    NotifyOnGeneration = table.Column<bool>(type: "boolean", nullable: false),
                    LastPmDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NextPmDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastGeneratedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastPmWorkOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ContinueWithoutContract = table.Column<bool>(type: "boolean", nullable: false),
                    HoldReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    HeldAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LapsedContractId = table.Column<Guid>(type: "uuid", nullable: true),
                    OrgNodeId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ScheduleMode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Floating"),
                    TolerancePercent = table.Column<int>(type: "integer", nullable: false, defaultValue: 10),
                    MeterId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastMeterValue = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    NextMeterDue = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    Priority = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Medium"),
                    EstimatedHours = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    NeedsLineStop = table.Column<bool>(type: "boolean", nullable: false),
                    Basis = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsRoute = table.Column<bool>(type: "boolean", nullable: false),
                    DeferReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DeferredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeferRisk = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_preventive_maintenance_plans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_preventive_maintenance_plans_assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_preventive_maintenance_plans_assignment_groups_DefaultAssig~",
                        column: x => x.DefaultAssignmentGroupId,
                        principalTable: "assignment_groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_preventive_maintenance_plans_branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_preventive_maintenance_plans_meters_MeterId",
                        column: x => x.MeterId,
                        principalTable: "meters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_preventive_maintenance_plans_org_nodes_OrgNodeId",
                        column: x => x.OrgNodeId,
                        principalTable: "org_nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_preventive_maintenance_plans_pm_templates_PmTemplateId",
                        column: x => x.PmTemplateId,
                        principalTable: "pm_templates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_preventive_maintenance_plans_sites_SiteId",
                        column: x => x.SiteId,
                        principalTable: "sites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_preventive_maintenance_plans_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "spare_unit_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    SpareUnitId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    WorkOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssetId = table.Column<Guid>(type: "uuid", nullable: true),
                    StoreId = table.Column<Guid>(type: "uuid", nullable: true),
                    Cost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_spare_unit_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_spare_unit_events_spare_units_SpareUnitId",
                        column: x => x.SpareUnitId,
                        principalTable: "spare_units",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "pm_plan_route_assets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pm_plan_route_assets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_pm_plan_route_assets_assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_pm_plan_route_assets_preventive_maintenance_plans_PlanId",
                        column: x => x.PlanId,
                        principalTable: "preventive_maintenance_plans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "assignment_group_members",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignmentGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    TechnicianId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsLead = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_assignment_group_members", x => x.Id);
                    table.ForeignKey(
                        name: "FK_assignment_group_members_assignment_groups_AssignmentGroupId",
                        column: x => x.AssignmentGroupId,
                        principalTable: "assignment_groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_assignment_group_members_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "attendance_entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    TechnicianId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ClockInAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ClockOutAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    BreakMinutes = table.Column<int>(type: "integer", nullable: false),
                    BreakStartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false, defaultValue: "ClockedIn"),
                    LastLocation = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CurrentWorkOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_attendance_entries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_attendance_entries_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "compliance_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgNodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Regulation = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IntervalMonths = table.Column<int>(type: "integer", nullable: false),
                    LastDoneAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Provider = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RaiseJobDaysBefore = table.Column<int>(type: "integer", nullable: true),
                    OpenWorkOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    TemplateKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LastDueAlertDays = table.Column<int>(type: "integer", nullable: true),
                    LastOverdueAlertAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_compliance_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_compliance_items_assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_compliance_items_org_nodes_OrgNodeId",
                        column: x => x.OrgNodeId,
                        principalTable: "org_nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_compliance_items_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_compliance_items_users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "compliance_records",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    DoneAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Result = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ValidUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CertificateNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Examiner = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CertificateAttachmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    WorkOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    RecordedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_compliance_records", x => x.Id);
                    table.ForeignKey(
                        name: "FK_compliance_records_attachments_CertificateAttachmentId",
                        column: x => x.CertificateAttachmentId,
                        principalTable: "attachments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_compliance_records_compliance_items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "compliance_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "consent_records",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    TechnicianId = table.Column<Guid>(type: "uuid", nullable: true),
                    SubjectName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Purpose = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    GivenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Method = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    WithdrawnAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RecordedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_consent_records", x => x.Id);
                    table.ForeignKey(
                        name: "FK_consent_records_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "contractor_invoice_lines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    Description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contractor_invoice_lines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_contractor_invoice_lines_contractor_invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "contractor_invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "direct_purchases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    MaterialItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    Quantity = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    UnitCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    WorkOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    WorkOrderMaterialUsageId = table.Column<Guid>(type: "uuid", nullable: true),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: true),
                    SupplierName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SupplierReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PurchasedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    InvoiceReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ReconciledByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReconciledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_direct_purchases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_direct_purchases_branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_direct_purchases_material_items_MaterialItemId",
                        column: x => x.MaterialItemId,
                        principalTable: "material_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_direct_purchases_suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_direct_purchases_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_direct_purchases_users_PurchasedByUserId",
                        column: x => x.PurchasedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_direct_purchases_users_ReconciledByUserId",
                        column: x => x.ReconciledByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "email_intake_messages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    MessageId = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    FromAddress = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    FromName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Subject = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Outcome = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Detail = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    WorkOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: true),
                    AutoReplySent = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_email_intake_messages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_email_intake_messages_tenants_TenantId",
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
                });

            migrationBuilder.CreateTable(
                name: "food_safety_records",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    FactoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DoneAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AssetId = table.Column<Guid>(type: "uuid", nullable: true),
                    Area = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Passed = table.Column<bool>(type: "boolean", nullable: false),
                    Method = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    FerrousRejected = table.Column<bool>(type: "boolean", nullable: true),
                    NonFerrousRejected = table.Column<bool>(type: "boolean", nullable: true),
                    StainlessRejected = table.Column<bool>(type: "boolean", nullable: true),
                    ItemsChecked = table.Column<int>(type: "integer", nullable: true),
                    ItemsDamaged = table.Column<int>(type: "integer", nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DoneByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    VerifiedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CorrectiveWorkOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_food_safety_records", x => x.Id);
                    table.ForeignKey(
                        name: "FK_food_safety_records_assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_food_safety_records_org_nodes_FactoryId",
                        column: x => x.FactoryId,
                        principalTable: "org_nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "held_parts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    MaterialItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    Quantity = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    UnitCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    SourceReference = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    PartsReturnId = table.Column<Guid>(type: "uuid", nullable: true),
                    WorkOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResolutionReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ResolutionNote = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ResolvedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_held_parts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_held_parts_branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_held_parts_material_items_MaterialItemId",
                        column: x => x.MaterialItemId,
                        principalTable: "material_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_held_parts_suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_held_parts_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_held_parts_users_ResolvedByUserId",
                        column: x => x.ResolvedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

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
                });

            migrationBuilder.CreateTable(
                name: "leave_requests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    TechnicianId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    LeaveTypeCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    TotalDays = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "Pending"),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DecisionNote = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DecidedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_leave_requests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_leave_requests_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_leave_requests_users_DecidedByUserId",
                        column: x => x.DecidedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_leave_requests_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
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
                });

            migrationBuilder.CreateTable(
                name: "maintenance_window_jobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    WindowId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    AddedAutomatically = table.Column<bool>(type: "boolean", nullable: false),
                    Outcome = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_maintenance_window_jobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_maintenance_window_jobs_maintenance_windows_WindowId",
                        column: x => x.WindowId,
                        principalTable: "maintenance_windows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "material_issue_lines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    MaterialIssueId = table.Column<Guid>(type: "uuid", nullable: false),
                    MaterialItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuantityRequested = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    QuantityIssued = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    QuantityReturned = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Unit = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    UnitCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_material_issue_lines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_material_issue_lines_material_items_MaterialItemId",
                        column: x => x.MaterialItemId,
                        principalTable: "material_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "material_issues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    WorkOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssetId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: true),
                    SiteId = table.Column<Guid>(type: "uuid", nullable: true),
                    SystemId = table.Column<Guid>(type: "uuid", nullable: true),
                    IssuedToUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    IssueNumber = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    IssueStatus = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    RequestedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    IssuedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    IssuedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ApprovedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RejectedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    RejectedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    TotalEstimatedCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalIssuedCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_material_issues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_material_issues_assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_material_issues_branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_material_issues_clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_material_issues_sites_SiteId",
                        column: x => x.SiteId,
                        principalTable: "sites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_material_issues_systems_SystemId",
                        column: x => x.SystemId,
                        principalTable: "systems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_material_issues_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_material_issues_users_ApprovedByUserId",
                        column: x => x.ApprovedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_material_issues_users_IssuedByUserId",
                        column: x => x.IssuedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_material_issues_users_IssuedToUserId",
                        column: x => x.IssuedToUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_material_issues_users_RejectedByUserId",
                        column: x => x.RejectedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_material_issues_users_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "material_request_lines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MaterialRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    MaterialItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuantityRequested = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    QuantityIssued = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    QuantityUsed = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    QuantityReturned = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_material_request_lines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_material_request_lines_material_items_MaterialItemId",
                        column: x => x.MaterialItemId,
                        principalTable: "material_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "material_requests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    WorkOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_material_requests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_material_requests_branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_material_requests_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_material_requests_users_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "non_conformities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    FactoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    StandardId = table.Column<Guid>(type: "uuid", nullable: true),
                    RequirementId = table.Column<Guid>(type: "uuid", nullable: true),
                    AuditEventId = table.Column<Guid>(type: "uuid", nullable: true),
                    Source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Severity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Finding = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    RootCause = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CorrectiveAction = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClosureEvidence = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClosedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RaisedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    LastOverdueAlertAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_non_conformities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_non_conformities_audit_events_AuditEventId",
                        column: x => x.AuditEventId,
                        principalTable: "audit_events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_non_conformities_compliance_standards_StandardId",
                        column: x => x.StandardId,
                        principalTable: "compliance_standards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_non_conformities_org_nodes_FactoryId",
                        column: x => x.FactoryId,
                        principalTable: "org_nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_non_conformities_standard_requirements_RequirementId",
                        column: x => x.RequirementId,
                        principalTable: "standard_requirements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_non_conformities_users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "overhaul_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    FactoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    OverhaulPlanId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssetId = table.Column<Guid>(type: "uuid", nullable: true),
                    WorkOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Priority = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    EstimatedHours = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    EstimatedCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    ActualCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    SparesNote = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ContractorName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ProgressPercent = table.Column<int>(type: "integer", nullable: false),
                    NeedsRecommissioning = table.Column<bool>(type: "boolean", nullable: false),
                    RecommissionedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RecommissionedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RecommissioningNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    AddedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_overhaul_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_overhaul_items_assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_overhaul_items_org_nodes_FactoryId",
                        column: x => x.FactoryId,
                        principalTable: "org_nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_overhaul_items_overhaul_plans_OverhaulPlanId",
                        column: x => x.OverhaulPlanId,
                        principalTable: "overhaul_plans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_overhaul_items_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
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
                });

            migrationBuilder.CreateTable(
                name: "parts_return_lines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PartsReturnId = table.Column<Guid>(type: "uuid", nullable: false),
                    MaterialItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkOrderMaterialUsageId = table.Column<Guid>(type: "uuid", nullable: true),
                    Quantity = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Condition = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_parts_return_lines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_parts_return_lines_material_items_MaterialItemId",
                        column: x => x.MaterialItemId,
                        principalTable: "material_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "parts_returns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    WorkOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReturnNumber = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcessedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_parts_returns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_parts_returns_branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_parts_returns_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_parts_returns_users_ProcessedByUserId",
                        column: x => x.ProcessedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_parts_returns_users_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pm_plan_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DueMeterValue = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OnTime = table.Column<bool>(type: "boolean", nullable: true),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ActorName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pm_plan_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_pm_plan_events_preventive_maintenance_plans_PlanId",
                        column: x => x.PlanId,
                        principalTable: "preventive_maintenance_plans",
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
                });

            migrationBuilder.CreateTable(
                name: "purchase_requisition_lines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequisitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    MaterialItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    Description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: true),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    PurchaseOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_requisition_lines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_purchase_requisition_lines_material_items_MaterialItemId",
                        column: x => x.MaterialItemId,
                        principalTable: "material_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_requisition_lines_purchase_orders_PurchaseOrderId",
                        column: x => x.PurchaseOrderId,
                        principalTable: "purchase_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_purchase_requisition_lines_purchase_requisitions_Requisitio~",
                        column: x => x.RequisitionId,
                        principalTable: "purchase_requisitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_purchase_requisition_lines_suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "suppliers",
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
                });

            migrationBuilder.CreateTable(
                name: "stock_movements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    StoreId = table.Column<Guid>(type: "uuid", nullable: true),
                    UnitCost = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    MaterialId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    MaterialRequestId = table.Column<Guid>(type: "uuid", nullable: true),
                    MovementType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    QuantityChange = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    BalanceAfter = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ReferenceNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    BatchOrSerial = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_movements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_stock_movements_branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_stock_movements_material_items_MaterialId",
                        column: x => x.MaterialId,
                        principalTable: "material_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_stock_movements_material_requests_MaterialRequestId",
                        column: x => x.MaterialRequestId,
                        principalTable: "material_requests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_stock_movements_stores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "stores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_movements_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_stock_movements_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "stock_reservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    StoreId = table.Column<Guid>(type: "uuid", nullable: false),
                    MaterialItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    IssuedQuantity = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ReservedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    KittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    KittedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_reservations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_stock_reservations_material_items_MaterialItemId",
                        column: x => x.MaterialItemId,
                        principalTable: "material_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_reservations_stores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "stores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "store_transfers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    TransferNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    FromStoreId = table.Column<Guid>(type: "uuid", nullable: false),
                    ToStoreId = table.Column<Guid>(type: "uuid", nullable: false),
                    MaterialItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    SerialNumbers = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IsInterFactory = table.Column<bool>(type: "boolean", nullable: false),
                    UnitCost = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    WorkOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    RequestedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DispatchedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DispatchedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReceivedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RejectReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_store_transfers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_store_transfers_material_items_MaterialItemId",
                        column: x => x.MaterialItemId,
                        principalTable: "material_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_store_transfers_stores_FromStoreId",
                        column: x => x.FromStoreId,
                        principalTable: "stores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_store_transfers_stores_ToStoreId",
                        column: x => x.ToStoreId,
                        principalTable: "stores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
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
                });

            migrationBuilder.CreateTable(
                name: "technicians",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    FullName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    SkillCategory = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    AssignmentGroup = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    AvailabilityStatus = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false, defaultValue: "Available"),
                    AvailabilityNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AvailabilityUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AvailabilityUpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastKnownLatitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    LastKnownLongitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    LastLocationAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsTrackingActive = table.Column<bool>(type: "boolean", nullable: false),
                    ActiveWorkOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Trade = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    HomeOrgNodeId = table.Column<Guid>(type: "uuid", nullable: true),
                    HourlyRate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    OvertimeMultiplier = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: false, defaultValue: 1.5m),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_technicians", x => x.Id);
                    table.ForeignKey(
                        name: "FK_technicians_branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_technicians_org_nodes_HomeOrgNodeId",
                        column: x => x.HomeOrgNodeId,
                        principalTable: "org_nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_technicians_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_technicians_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
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
                name: "work_orders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: true),
                    SiteId = table.Column<Guid>(type: "uuid", nullable: true),
                    SystemId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssetId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssignmentGroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    ContractId = table.Column<Guid>(type: "uuid", nullable: true),
                    RateCardId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssignmentType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    WorkOrderNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Priority = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    WorkflowStatusId = table.Column<Guid>(type: "uuid", nullable: true),
                    WorkType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "CorrectiveMaintenance"),
                    ServiceType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    EstimatedDuration = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    ActualDuration = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    AssignedTechnicianId = table.Column<Guid>(type: "uuid", nullable: true),
                    LeadTechnicianId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssignedTechnicianIdsJson = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ScheduledStartAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ScheduledEndAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    WorkStartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ArrivalAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DepartureAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    HoldReason = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    HoldNote = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    HoldStartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    HoldEndedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClosedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    WorkDoneNotes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    JobCardNotes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    AcknowledgedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AcknowledgementComments = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    AcknowledgementDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReportStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    ReportNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ReportGeneratedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReportApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReportSentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReportProblemText = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ReportFindingsText = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ReportWorkDoneText = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ReportEditedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReportEditedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsPreventiveMaintenance = table.Column<bool>(type: "boolean", nullable: false),
                    PreventiveMaintenancePlanId = table.Column<Guid>(type: "uuid", nullable: true),
                    PmTemplateId = table.Column<Guid>(type: "uuid", nullable: true),
                    PmTemplateNameSnapshot = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    PmTemplateVersionSnapshot = table.Column<int>(type: "integer", nullable: true),
                    WorkOrderTemplateId = table.Column<Guid>(type: "uuid", nullable: true),
                    PlannedMaterialsJson = table.Column<string>(type: "text", nullable: true),
                    ParentWorkOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    FollowUpOfWorkOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    PreviousStatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    SlaResponseDeadline = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SlaResolutionDeadline = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AppliedSlaSource = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    AppliedResponseHours = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    AppliedResolutionHours = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    SlaResponseBreached = table.Column<bool>(type: "boolean", nullable: false),
                    SlaResolutionBreached = table.Column<bool>(type: "boolean", nullable: false),
                    SlaResponseBreachedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SlaResolutionBreachedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    BillingModel = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    CoverageStatus = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    PricingStatus = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    AppliedRateSource = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    EstimatedLabourHours = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    EstimatedMaterialCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    EstimatedCharge = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    FinalCharge = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    PricingNotes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ContractSnapshotJson = table.Column<string>(type: "text", nullable: true),
                    SlaSnapshotJson = table.Column<string>(type: "text", nullable: true),
                    PricingSnapshotJson = table.Column<string>(type: "text", nullable: true),
                    NotifyClient = table.Column<bool>(type: "boolean", nullable: false),
                    ClientContactEmail = table.Column<string>(type: "text", nullable: true),
                    TrackingToken = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    IsRepeatVisit = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    RepeatVisitCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    OrgNodeId = table.Column<Guid>(type: "uuid", nullable: true),
                    RequestSource = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    RequestedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ApprovedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    PlannedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    VerifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    VerifiedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    VerificationNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    IsBreakdown = table.Column<bool>(type: "boolean", nullable: false),
                    DowntimeStartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DowntimeEndedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NeedsLineStop = table.Column<bool>(type: "boolean", nullable: false),
                    WindowCarryoverCount = table.Column<int>(type: "integer", nullable: false),
                    RequiredCertificationId = table.Column<Guid>(type: "uuid", nullable: true),
                    RequiredPermitTypes = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ContractorId = table.Column<Guid>(type: "uuid", nullable: true),
                    MaintenanceAgreementId = table.Column<Guid>(type: "uuid", nullable: true),
                    FailureProblemCodeId = table.Column<Guid>(type: "uuid", nullable: true),
                    FailureCauseCodeId = table.Column<Guid>(type: "uuid", nullable: true),
                    FailureRemedyCodeId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReworkOfWorkOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    JobPlanId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_work_orders", x => x.Id);
                    table.CheckConstraint("CK_work_orders_assignment_type", "\"AssignmentType\" IN ('Unassigned', 'IndividualTechnician', 'MultipleTechnicians', 'AssignmentGroup')");
                    table.CheckConstraint("CK_work_orders_hold_reason", "\"HoldReason\" IS NULL OR \"HoldReason\" IN ('Awaiting Materials', 'Awaiting Client', 'Awaiting Vendor', 'Awaiting Approval', 'Awaiting Site Access', 'Awaiting Spares', 'Other', 'Waiting for Window', 'Waiting for Shutdown', 'Waiting for Contractor')");
                    table.CheckConstraint("CK_work_orders_priority", "\"Priority\" IN ('Low', 'Medium', 'High', 'Critical')");
                    table.CheckConstraint("CK_work_orders_status", "\"Status\" IN ('New', 'Open', 'Assigned', 'Scheduled', 'In Progress', 'On Hold', 'Waiting on Parts', 'Waiting on Client', 'Completed', 'Client Accepted', 'Closed', 'Cancelled', 'Rejected', 'Reopened', 'Requested', 'Approved', 'Planned', 'Verified')");
                    table.CheckConstraint("CK_work_orders_work_type", "\"WorkType\" IN ('NewInstallation', 'PreventiveMaintenance', 'CorrectiveMaintenance', 'InspectionAssessment', 'Corrective', 'Preventive', 'Inspection', 'Statutory', 'Modification', 'Safety')");
                    table.ForeignKey(
                        name: "FK_work_orders_assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_work_orders_assignment_groups_AssignmentGroupId",
                        column: x => x.AssignmentGroupId,
                        principalTable: "assignment_groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_work_orders_branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_work_orders_certification_definitions_RequiredCertification~",
                        column: x => x.RequiredCertificationId,
                        principalTable: "certification_definitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_work_orders_clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_work_orders_contract_rate_cards_RateCardId",
                        column: x => x.RateCardId,
                        principalTable: "contract_rate_cards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_work_orders_contractors_ContractorId",
                        column: x => x.ContractorId,
                        principalTable: "contractors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_work_orders_contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_work_orders_failure_codes_FailureCauseCodeId",
                        column: x => x.FailureCauseCodeId,
                        principalTable: "failure_codes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_work_orders_failure_codes_FailureProblemCodeId",
                        column: x => x.FailureProblemCodeId,
                        principalTable: "failure_codes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_work_orders_failure_codes_FailureRemedyCodeId",
                        column: x => x.FailureRemedyCodeId,
                        principalTable: "failure_codes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_work_orders_job_plans_JobPlanId",
                        column: x => x.JobPlanId,
                        principalTable: "job_plans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_work_orders_maintenance_agreements_MaintenanceAgreementId",
                        column: x => x.MaintenanceAgreementId,
                        principalTable: "maintenance_agreements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_work_orders_org_nodes_OrgNodeId",
                        column: x => x.OrgNodeId,
                        principalTable: "org_nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_work_orders_pm_templates_PmTemplateId",
                        column: x => x.PmTemplateId,
                        principalTable: "pm_templates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_work_orders_preventive_maintenance_plans_PreventiveMaintena~",
                        column: x => x.PreventiveMaintenancePlanId,
                        principalTable: "preventive_maintenance_plans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_work_orders_sites_SiteId",
                        column: x => x.SiteId,
                        principalTable: "sites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_work_orders_systems_SystemId",
                        column: x => x.SystemId,
                        principalTable: "systems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_work_orders_technicians_AssignedTechnicianId",
                        column: x => x.AssignedTechnicianId,
                        principalTable: "technicians",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_work_orders_technicians_LeadTechnicianId",
                        column: x => x.LeadTechnicianId,
                        principalTable: "technicians",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_work_orders_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_work_orders_users_ClosedByUserId",
                        column: x => x.ClosedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_work_orders_work_order_templates_WorkOrderTemplateId",
                        column: x => x.WorkOrderTemplateId,
                        principalTable: "work_order_templates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_work_orders_work_orders_FollowUpOfWorkOrderId",
                        column: x => x.FollowUpOfWorkOrderId,
                        principalTable: "work_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_work_orders_work_orders_ParentWorkOrderId",
                        column: x => x.ParentWorkOrderId,
                        principalTable: "work_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_work_orders_work_orders_ReworkOfWorkOrderId",
                        column: x => x.ReworkOfWorkOrderId,
                        principalTable: "work_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_work_orders_workflow_statuses_WorkflowStatusId",
                        column: x => x.WorkflowStatusId,
                        principalTable: "workflow_statuses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

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
                name: "work_order_checklist_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    PmTemplateQuestionId = table.Column<Guid>(type: "uuid", nullable: true),
                    SectionName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    SectionOrder = table.Column<int>(type: "integer", nullable: false),
                    QuestionText = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    HelperText = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    InputType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    QuestionType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    DefaultValue = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    MinValue = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    MaxValue = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    EvidenceRequired = table.Column<bool>(type: "boolean", nullable: false),
                    TechnicianRemarksAllowed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    RequiresNoteOnFail = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    ResponseValue = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    NumberValue = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    TextValue = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    AttachmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Remarks = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    FailureNote = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    IsCompleted = table.Column<bool>(type: "boolean", nullable: false),
                    CompletedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    OptionsJson = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_work_order_checklist_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_work_order_checklist_items_attachments_AttachmentId",
                        column: x => x.AttachmentId,
                        principalTable: "attachments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_work_order_checklist_items_pm_template_questions_PmTemplate~",
                        column: x => x.PmTemplateQuestionId,
                        principalTable: "pm_template_questions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_work_order_checklist_items_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_work_order_checklist_items_users_CompletedByUserId",
                        column: x => x.CompletedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_work_order_checklist_items_work_orders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "work_orders",
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
                    OldStatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    NewStatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Reason = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    Note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Latitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    Longitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    MetadataJson = table.Column<string>(type: "text", nullable: true),
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
                name: "work_order_material_usages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    MaterialItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetId = table.Column<Guid>(type: "uuid", nullable: true),
                    QuantityUsed = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    UnitCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Chargeable = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    UsedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    UsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_work_order_material_usages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_work_order_material_usages_assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_work_order_material_usages_material_items_MaterialItemId",
                        column: x => x.MaterialItemId,
                        principalTable: "material_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_work_order_material_usages_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_work_order_material_usages_users_UsedByUserId",
                        column: x => x.UsedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_work_order_material_usages_work_orders_WorkOrderId",
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
                name: "work_order_photo_evidence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttachmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Caption = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IncludeInReport = table.Column<bool>(type: "boolean", nullable: false),
                    UploadedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_work_order_photo_evidence", x => x.Id);
                    table.ForeignKey(
                        name: "FK_work_order_photo_evidence_attachments_AttachmentId",
                        column: x => x.AttachmentId,
                        principalTable: "attachments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_work_order_photo_evidence_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_work_order_photo_evidence_users_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_work_order_photo_evidence_work_orders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "work_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "work_order_signatures",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    SignatureType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SignerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SignerRole = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    SignatureDataUrl = table.Column<string>(type: "character varying(32000)", maxLength: 32000, nullable: false),
                    Comment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CapturedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CapturedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_work_order_signatures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_work_order_signatures_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_work_order_signatures_users_CapturedByUserId",
                        column: x => x.CapturedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_work_order_signatures_work_orders_WorkOrderId",
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
                    LastArrivalAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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

            migrationBuilder.CreateTable(
                name: "work_order_time_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    TechnicianId = table.Column<Guid>(type: "uuid", nullable: true),
                    TechnicianName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsBilled = table.Column<bool>(type: "boolean", nullable: false),
                    IsOvertime = table.Column<bool>(type: "boolean", nullable: false),
                    LabourCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_work_order_time_logs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_work_order_time_logs_technicians_TechnicianId",
                        column: x => x.TechnicianId,
                        principalTable: "technicians",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_work_order_time_logs_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_work_order_time_logs_work_orders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "work_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "work_order_visits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    TechnicianId = table.Column<Guid>(type: "uuid", nullable: false),
                    TravelStartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ArrivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DepartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TravelMinutes = table.Column<int>(type: "integer", nullable: false),
                    OnSiteMinutes = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_work_order_visits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_work_order_visits_technicians_TechnicianId",
                        column: x => x.TechnicianId,
                        principalTable: "technicians",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_work_order_visits_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_work_order_visits_work_orders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "work_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
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

            migrationBuilder.CreateTable(
                name: "tool_assignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ToolId = table.Column<Guid>(type: "uuid", nullable: false),
                    TechnicianId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    CheckedOutAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CheckedOutByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpectedReturnAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ConditionOut = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    AcknowledgedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReturnedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReturnedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ConditionIn = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    ReturnNotes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tool_assignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tool_assignments_technicians_TechnicianId",
                        column: x => x.TechnicianId,
                        principalTable: "technicians",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tool_assignments_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_tool_assignments_users_CheckedOutByUserId",
                        column: x => x.CheckedOutByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tool_assignments_users_ReturnedByUserId",
                        column: x => x.ReturnedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_tool_assignments_work_orders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "work_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "tools",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ToolCode = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SerialNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Manufacturer = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PurchaseDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PurchaseCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    HomeBranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Condition = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CalibrationIntervalDays = table.Column<int>(type: "integer", nullable: true),
                    LastCalibratedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NextCalibrationDue = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CurrentAssignmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tools", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tools_branches_HomeBranchId",
                        column: x => x.HomeBranchId,
                        principalTable: "branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_tools_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_tools_tool_assignments_CurrentAssignmentId",
                        column: x => x.CurrentAssignmentId,
                        principalTable: "tool_assignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "tool_calibrations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ToolId = table.Column<Guid>(type: "uuid", nullable: false),
                    CalibratedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NextDueOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CertificateNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PerformedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Result = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    RecordedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tool_calibrations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tool_calibrations_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_tool_calibrations_tools_ToolId",
                        column: x => x.ToolId,
                        principalTable: "tools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "license_add_ons",
                columns: new[] { "Id", "AnnualPrice", "Code", "CreatedAt", "DisplayName", "ExtraMessagePrice", "ExtraNumberPrice", "IncludedMessages", "IsActive", "MonthlyPrice", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("55555555-5555-5555-5555-555555555571"), 15000m, "whatsapp_notifications", new DateTime(2026, 10, 24, 0, 0, 0, 0, DateTimeKind.Utc), "WhatsApp Notifications", 3m, 0m, 500, true, 1500m, null },
                    { new Guid("55555555-5555-5555-5555-555555555572"), 45000m, "whatsapp_business", new DateTime(2026, 10, 24, 0, 0, 0, 0, DateTimeKind.Utc), "WhatsApp Business", 3m, 2500m, 1500, true, 4500m, null }
                });

            migrationBuilder.InsertData(
                table: "license_plans",
                columns: new[] { "Id", "CreatedAt", "DisplayName", "FieldSeatAnnualPrice", "FieldSeatMonthlyPrice", "IsActive", "MinOfficeSeats", "OfficeSeatAnnualPrice", "OfficeSeatMonthlyPrice", "PlanCode", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("55555555-5555-5555-5555-555555555561"), new DateTime(2026, 10, 24, 0, 0, 0, 0, DateTimeKind.Utc), "Ecosys ServiceOps Essentials", 0m, 0m, true, 2, 0m, 0m, "essentials", null },
                    { new Guid("55555555-5555-5555-5555-555555555562"), new DateTime(2026, 10, 24, 0, 0, 0, 0, DateTimeKind.Utc), "Ecosys ServiceOps Professional", 0m, 0m, true, 3, 0m, 0m, "professional", null },
                    { new Guid("55555555-5555-5555-5555-555555555563"), new DateTime(2026, 10, 24, 0, 0, 0, 0, DateTimeKind.Utc), "Ecosys ServiceOps Enterprise", 0m, 0m, true, 10, 0m, 0m, "enterprise", null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_access_scopes_PermissionGroupId",
                table: "access_scopes",
                column: "PermissionGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_access_scopes_UserId",
                table: "access_scopes",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_action_items_ActivityId",
                table: "action_items",
                column: "ActivityId");

            migrationBuilder.CreateIndex(
                name: "IX_action_items_AssigneeUserId",
                table: "action_items",
                column: "AssigneeUserId");

            migrationBuilder.CreateIndex(
                name: "IX_action_items_Status_DueDate",
                table: "action_items",
                columns: new[] { "Status", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_action_items_TenantId_ActivityId",
                table: "action_items",
                columns: new[] { "TenantId", "ActivityId" });

            migrationBuilder.CreateIndex(
                name: "IX_action_items_TenantId_AssigneeUserId_Status",
                table: "action_items",
                columns: new[] { "TenantId", "AssigneeUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_action_items_TenantId_EntityType_EntityId",
                table: "action_items",
                columns: new[] { "TenantId", "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_activities_OrganizerUserId",
                table: "activities",
                column: "OrganizerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_activities_Status_StartsAt",
                table: "activities",
                columns: new[] { "Status", "StartsAt" });

            migrationBuilder.CreateIndex(
                name: "IX_activities_TenantId_EntityType_EntityId",
                table: "activities",
                columns: new[] { "TenantId", "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_activities_TenantId_OrganizerUserId_StartsAt",
                table: "activities",
                columns: new[] { "TenantId", "OrganizerUserId", "StartsAt" });

            migrationBuilder.CreateIndex(
                name: "IX_activities_TenantId_StartsAt",
                table: "activities",
                columns: new[] { "TenantId", "StartsAt" });

            migrationBuilder.CreateIndex(
                name: "IX_activity_attendees_ActivityId",
                table: "activity_attendees",
                column: "ActivityId");

            migrationBuilder.CreateIndex(
                name: "IX_activity_attendees_ClientContactId",
                table: "activity_attendees",
                column: "ClientContactId");

            migrationBuilder.CreateIndex(
                name: "IX_activity_attendees_TenantId_ActivityId",
                table: "activity_attendees",
                columns: new[] { "TenantId", "ActivityId" });

            migrationBuilder.CreateIndex(
                name: "IX_activity_attendees_TenantId_UserId",
                table: "activity_attendees",
                columns: new[] { "TenantId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_activity_attendees_UserId",
                table: "activity_attendees",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_activity_minutes_addenda_ActivityId",
                table: "activity_minutes_addenda",
                column: "ActivityId");

            migrationBuilder.CreateIndex(
                name: "IX_activity_minutes_addenda_AuthorUserId",
                table: "activity_minutes_addenda",
                column: "AuthorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_activity_minutes_addenda_TenantId_ActivityId",
                table: "activity_minutes_addenda",
                columns: new[] { "TenantId", "ActivityId" });

            migrationBuilder.CreateIndex(
                name: "IX_activity_reminder_logs_CreatedAt",
                table: "activity_reminder_logs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_activity_reminder_logs_TenantId_EntityId_NotificationKey_Fo~",
                table: "activity_reminder_logs",
                columns: new[] { "TenantId", "EntityId", "NotificationKey", "ForTime" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_approval_levels_ApprovalMatrixId_LevelOrder",
                table: "approval_levels",
                columns: new[] { "ApprovalMatrixId", "LevelOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_approval_matrices_TenantId_DocumentType",
                table: "approval_matrices",
                columns: new[] { "TenantId", "DocumentType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_approval_request_steps_ApprovalRequestId",
                table: "approval_request_steps",
                column: "ApprovalRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_approval_request_steps_TenantId_Status",
                table: "approval_request_steps",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_approval_requests_TenantId_DocumentType_DocumentId",
                table: "approval_requests",
                columns: new[] { "TenantId", "DocumentType", "DocumentId" });

            migrationBuilder.CreateIndex(
                name: "IX_approval_requests_TenantId_Status",
                table: "approval_requests",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_asset_categories_TemplatePackId_Code",
                table: "asset_categories",
                columns: new[] { "TemplatePackId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_asset_categories_TenantId_Name",
                table: "asset_categories",
                columns: new[] { "TenantId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_asset_category_fields_AssetCategoryId",
                table: "asset_category_fields",
                column: "AssetCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_asset_custom_field_values_AssetId_FieldDefinitionId",
                table: "asset_custom_field_values",
                columns: new[] { "AssetId", "FieldDefinitionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_asset_custom_field_values_FieldDefinitionId",
                table: "asset_custom_field_values",
                column: "FieldDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_asset_custom_field_values_TenantId",
                table: "asset_custom_field_values",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_asset_document_versions_AttachmentId",
                table: "asset_document_versions",
                column: "AttachmentId");

            migrationBuilder.CreateIndex(
                name: "IX_asset_document_versions_DocumentId",
                table: "asset_document_versions",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_asset_document_versions_TenantId_DocumentId_VersionNumber",
                table: "asset_document_versions",
                columns: new[] { "TenantId", "DocumentId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_asset_documents_AssetId",
                table: "asset_documents",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_asset_documents_TenantId_AssetId",
                table: "asset_documents",
                columns: new[] { "TenantId", "AssetId" });

            migrationBuilder.CreateIndex(
                name: "IX_asset_documents_TenantId_UpdatedAt",
                table: "asset_documents",
                columns: new[] { "TenantId", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_asset_lifecycle_events_AssetId",
                table: "asset_lifecycle_events",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_asset_lifecycle_events_TenantId_AssetId_OccurredAt",
                table: "asset_lifecycle_events",
                columns: new[] { "TenantId", "AssetId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_asset_parts_AssetId_MaterialItemId_Position",
                table: "asset_parts",
                columns: new[] { "AssetId", "MaterialItemId", "Position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_asset_parts_MaterialItemId",
                table: "asset_parts",
                column: "MaterialItemId");

            migrationBuilder.CreateIndex(
                name: "IX_asset_parts_TenantId_MaterialItemId",
                table: "asset_parts",
                columns: new[] { "TenantId", "MaterialItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_asset_tags_AssetId",
                table: "asset_tags",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_asset_tags_TenantId_AssetId_Active",
                table: "asset_tags",
                columns: new[] { "TenantId", "AssetId" },
                unique: true,
                filter: "\"Status\" = 'Active'");

            migrationBuilder.CreateIndex(
                name: "IX_asset_tags_TenantId_TagNumber",
                table: "asset_tags",
                columns: new[] { "TenantId", "TagNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_asset_tags_TenantId_UpdatedAt",
                table: "asset_tags",
                columns: new[] { "TenantId", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_assets_AssetCategoryId",
                table: "assets",
                column: "AssetCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_assets_BranchId",
                table: "assets",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_assets_ClientId",
                table: "assets",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_assets_DefaultAssignmentGroupId",
                table: "assets",
                column: "DefaultAssignmentGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_assets_OrgNodeId",
                table: "assets",
                column: "OrgNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_assets_ParentAssetId",
                table: "assets",
                column: "ParentAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_assets_SiteId",
                table: "assets",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "IX_assets_SystemId",
                table: "assets",
                column: "SystemId");

            migrationBuilder.CreateIndex(
                name: "IX_assets_TenantId_AssetCode",
                table: "assets",
                columns: new[] { "TenantId", "AssetCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_assets_TenantId_ExternalSystem_ExternalId",
                table: "assets",
                columns: new[] { "TenantId", "ExternalSystem", "ExternalId" });

            migrationBuilder.CreateIndex(
                name: "IX_assets_TenantId_OrgNodeId",
                table: "assets",
                columns: new[] { "TenantId", "OrgNodeId" });

            migrationBuilder.CreateIndex(
                name: "IX_assets_TenantId_UpdatedAt",
                table: "assets",
                columns: new[] { "TenantId", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_assignment_group_members_AssignmentGroupId",
                table: "assignment_group_members",
                column: "AssignmentGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_assignment_group_members_TechnicianId",
                table: "assignment_group_members",
                column: "TechnicianId");

            migrationBuilder.CreateIndex(
                name: "IX_assignment_group_members_TenantId_AssignmentGroupId_Technic~",
                table: "assignment_group_members",
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
                name: "IX_attachments_TenantId_EntityType_EntityId",
                table: "attachments",
                columns: new[] { "TenantId", "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_attachments_UploadedByUserId",
                table: "attachments",
                column: "UploadedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_attendance_entries_CurrentWorkOrderId",
                table: "attendance_entries",
                column: "CurrentWorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_attendance_entries_TechnicianId",
                table: "attendance_entries",
                column: "TechnicianId");

            migrationBuilder.CreateIndex(
                name: "IX_attendance_entries_TenantId_Status",
                table: "attendance_entries",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_attendance_entries_TenantId_TechnicianId_WorkDate",
                table: "attendance_entries",
                columns: new[] { "TenantId", "TechnicianId", "WorkDate" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_events_FactoryId",
                table: "audit_events",
                column: "FactoryId");

            migrationBuilder.CreateIndex(
                name: "IX_audit_events_StandardId",
                table: "audit_events",
                column: "StandardId");

            migrationBuilder.CreateIndex(
                name: "IX_audit_events_TenantId_ScheduledFor",
                table: "audit_events",
                columns: new[] { "TenantId", "ScheduledFor" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_TenantId_CreatedAt",
                table: "audit_logs",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_UserId",
                table: "audit_logs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_branch_material_stocks_BranchId",
                table: "branch_material_stocks",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_branch_material_stocks_MaterialId",
                table: "branch_material_stocks",
                column: "MaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_branch_material_stocks_TenantId_BranchId_MaterialId",
                table: "branch_material_stocks",
                columns: new[] { "TenantId", "BranchId", "MaterialId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_branches_ParentBranchId",
                table: "branches",
                column: "ParentBranchId");

            migrationBuilder.CreateIndex(
                name: "IX_branches_TenantId_Code",
                table: "branches",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_branches_TenantId_ParentBranchId_Name",
                table: "branches",
                columns: new[] { "TenantId", "ParentBranchId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_calendar_feed_tokens_TenantId_UserId",
                table: "calendar_feed_tokens",
                columns: new[] { "TenantId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_calendar_feed_tokens_TokenHash",
                table: "calendar_feed_tokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_calendar_feed_tokens_UserId",
                table: "calendar_feed_tokens",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_certification_definitions_TenantId_Name",
                table: "certification_definitions",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_check_run_answers_CheckRunId",
                table: "check_run_answers",
                column: "CheckRunId");

            migrationBuilder.CreateIndex(
                name: "IX_check_runs_AssetId",
                table: "check_runs",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_check_runs_CheckScheduleId_AssetId_PeriodKey",
                table: "check_runs",
                columns: new[] { "CheckScheduleId", "AssetId", "PeriodKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_check_runs_TenantId_OrgNodeId_DueFrom",
                table: "check_runs",
                columns: new[] { "TenantId", "OrgNodeId", "DueFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_check_runs_TenantId_Status_DueBy",
                table: "check_runs",
                columns: new[] { "TenantId", "Status", "DueBy" });

            migrationBuilder.CreateIndex(
                name: "IX_check_schedule_assets_AssetId",
                table: "check_schedule_assets",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_check_schedule_assets_CheckScheduleId_AssetId",
                table: "check_schedule_assets",
                columns: new[] { "CheckScheduleId", "AssetId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_check_schedules_OrgNodeId",
                table: "check_schedules",
                column: "OrgNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_check_schedules_PmTemplateId",
                table: "check_schedules",
                column: "PmTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_check_schedules_TenantId_OrgNodeId",
                table: "check_schedules",
                columns: new[] { "TenantId", "OrgNodeId" });

            migrationBuilder.CreateIndex(
                name: "IX_client_contacts_ClientId",
                table: "client_contacts",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_client_contacts_TenantId_ClientId",
                table: "client_contacts",
                columns: new[] { "TenantId", "ClientId" });

            migrationBuilder.CreateIndex(
                name: "IX_client_contacts_TenantId_Email",
                table: "client_contacts",
                columns: new[] { "TenantId", "Email" });

            migrationBuilder.CreateIndex(
                name: "IX_client_portal_users_ClientId",
                table: "client_portal_users",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_client_portal_users_Email",
                table: "client_portal_users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_client_portal_users_TenantId_ClientId",
                table: "client_portal_users",
                columns: new[] { "TenantId", "ClientId" });

            migrationBuilder.CreateIndex(
                name: "IX_client_preferential_rates_ApprovedByUserId",
                table: "client_preferential_rates",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_client_preferential_rates_ClientId",
                table: "client_preferential_rates",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_client_preferential_rates_CreatedByUserId",
                table: "client_preferential_rates",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_client_preferential_rates_TenantId_ClientId_RateName_ItemTy~",
                table: "client_preferential_rates",
                columns: new[] { "TenantId", "ClientId", "RateName", "ItemType", "EffectiveFrom" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_clients_SlaDefinitionId",
                table: "clients",
                column: "SlaDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_clients_TenantId_ClientName",
                table: "clients",
                columns: new[] { "TenantId", "ClientName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_clients_TenantId_IsProspect",
                table: "clients",
                columns: new[] { "TenantId", "IsProspect" });

            migrationBuilder.CreateIndex(
                name: "IX_compliance_items_AssetId",
                table: "compliance_items",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_compliance_items_OpenWorkOrderId",
                table: "compliance_items",
                column: "OpenWorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_compliance_items_OrgNodeId",
                table: "compliance_items",
                column: "OrgNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_compliance_items_OwnerUserId",
                table: "compliance_items",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_compliance_items_TenantId_IsActive_DueDate",
                table: "compliance_items",
                columns: new[] { "TenantId", "IsActive", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_compliance_items_TenantId_OrgNodeId_DueDate",
                table: "compliance_items",
                columns: new[] { "TenantId", "OrgNodeId", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_compliance_records_CertificateAttachmentId",
                table: "compliance_records",
                column: "CertificateAttachmentId");

            migrationBuilder.CreateIndex(
                name: "IX_compliance_records_ItemId",
                table: "compliance_records",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_compliance_records_TenantId_ItemId_DoneAt",
                table: "compliance_records",
                columns: new[] { "TenantId", "ItemId", "DoneAt" });

            migrationBuilder.CreateIndex(
                name: "IX_compliance_records_WorkOrderId",
                table: "compliance_records",
                column: "WorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_compliance_standards_TenantId_Code",
                table: "compliance_standards",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_consent_records_TechnicianId",
                table: "consent_records",
                column: "TechnicianId");

            migrationBuilder.CreateIndex(
                name: "IX_consent_records_UserId",
                table: "consent_records",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_contract_asset_categories_AssetCategoryId",
                table: "contract_asset_categories",
                column: "AssetCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_contract_asset_categories_ContractId",
                table: "contract_asset_categories",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_contract_asset_categories_TenantId_ContractId_AssetCategory~",
                table: "contract_asset_categories",
                columns: new[] { "TenantId", "ContractId", "AssetCategoryId", "AssetCategoryName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_contract_assets_AssetId",
                table: "contract_assets",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_contract_assets_ContractId",
                table: "contract_assets",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_contract_assets_TenantId_ContractId_AssetId",
                table: "contract_assets",
                columns: new[] { "TenantId", "ContractId", "AssetId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_contract_lifecycle_events_ContractId",
                table: "contract_lifecycle_events",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_contract_lifecycle_events_CreatedByUserId",
                table: "contract_lifecycle_events",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_contract_lifecycle_events_TenantId_ContractId_CreatedAt",
                table: "contract_lifecycle_events",
                columns: new[] { "TenantId", "ContractId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_contract_pm_entitlements_AssetId",
                table: "contract_pm_entitlements",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_contract_pm_entitlements_ContractId",
                table: "contract_pm_entitlements",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_contract_pm_entitlements_TenantId_ContractId_AssetId_AssetC~",
                table: "contract_pm_entitlements",
                columns: new[] { "TenantId", "ContractId", "AssetId", "AssetCategory", "WorkOrderType", "EffectiveFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_contract_rate_cards_ContractId",
                table: "contract_rate_cards",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_contract_rate_cards_TenantId_ContractId_Name",
                table: "contract_rate_cards",
                columns: new[] { "TenantId", "ContractId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_contract_rate_rules_AssetCategoryId",
                table: "contract_rate_rules",
                column: "AssetCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_contract_rate_rules_RateCardId",
                table: "contract_rate_rules",
                column: "RateCardId");

            migrationBuilder.CreateIndex(
                name: "IX_contract_rate_rules_SiteId",
                table: "contract_rate_rules",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "IX_contract_rate_rules_TenantId_RateCardId_SiteId_AssetCategor~",
                table: "contract_rate_rules",
                columns: new[] { "TenantId", "RateCardId", "SiteId", "AssetCategoryId", "WorkOrderTypeId", "Priority" });

            migrationBuilder.CreateIndex(
                name: "IX_contract_reminder_rules_ContractId",
                table: "contract_reminder_rules",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_contract_reminder_rules_TenantId_ContractId_DaysBeforeExpiry",
                table: "contract_reminder_rules",
                columns: new[] { "TenantId", "ContractId", "DaysBeforeExpiry" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_contract_renewals_OriginalContractId",
                table: "contract_renewals",
                column: "OriginalContractId");

            migrationBuilder.CreateIndex(
                name: "IX_contract_renewals_OwnerUserId",
                table: "contract_renewals",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_contract_renewals_RenewedContractId",
                table: "contract_renewals",
                column: "RenewedContractId");

            migrationBuilder.CreateIndex(
                name: "IX_contract_renewals_TenantId_OriginalContractId",
                table: "contract_renewals",
                columns: new[] { "TenantId", "OriginalContractId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_contract_sites_ContractId",
                table: "contract_sites",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_contract_sites_SiteId",
                table: "contract_sites",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "IX_contract_sites_TenantId_ContractId_SiteId",
                table: "contract_sites",
                columns: new[] { "TenantId", "ContractId", "SiteId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_contract_work_order_types_ContractId",
                table: "contract_work_order_types",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_contract_work_order_types_TenantId_ContractId_WorkOrderType~",
                table: "contract_work_order_types",
                columns: new[] { "TenantId", "ContractId", "WorkOrderTypeId", "WorkOrderTypeName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_contractor_documents_AttachmentId",
                table: "contractor_documents",
                column: "AttachmentId");

            migrationBuilder.CreateIndex(
                name: "IX_contractor_documents_ContractorId",
                table: "contractor_documents",
                column: "ContractorId");

            migrationBuilder.CreateIndex(
                name: "IX_contractor_documents_TenantId_ExpiresOn",
                table: "contractor_documents",
                columns: new[] { "TenantId", "ExpiresOn" });

            migrationBuilder.CreateIndex(
                name: "IX_contractor_invoice_lines_InvoiceId",
                table: "contractor_invoice_lines",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_contractor_invoice_lines_TenantId_WorkOrderId",
                table: "contractor_invoice_lines",
                columns: new[] { "TenantId", "WorkOrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_contractor_invoice_lines_WorkOrderId",
                table: "contractor_invoice_lines",
                column: "WorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_contractor_invoices_AttachmentId",
                table: "contractor_invoices",
                column: "AttachmentId");

            migrationBuilder.CreateIndex(
                name: "IX_contractor_invoices_ContractorId",
                table: "contractor_invoices",
                column: "ContractorId");

            migrationBuilder.CreateIndex(
                name: "IX_contractor_invoices_TenantId_ContractorId_InvoiceNumber",
                table: "contractor_invoices",
                columns: new[] { "TenantId", "ContractorId", "InvoiceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_contractors_SupplierId",
                table: "contractors",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_contractors_TenantId_Name",
                table: "contractors",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_contracts_AccountManagerUserId",
                table: "contracts",
                column: "AccountManagerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_contracts_ClientId",
                table: "contracts",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_contracts_InternalApproverUserId",
                table: "contracts",
                column: "InternalApproverUserId");

            migrationBuilder.CreateIndex(
                name: "IX_contracts_RenewalOwnerUserId",
                table: "contracts",
                column: "RenewalOwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_contracts_RenewedFromContractId",
                table: "contracts",
                column: "RenewedFromContractId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_contracts_TenantId_ClientId_Status_EndDate",
                table: "contracts",
                columns: new[] { "TenantId", "ClientId", "Status", "EndDate" });

            migrationBuilder.CreateIndex(
                name: "IX_contracts_TenantId_ContractNumber",
                table: "contracts",
                columns: new[] { "TenantId", "ContractNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cost_centres_FactoryId",
                table: "cost_centres",
                column: "FactoryId");

            migrationBuilder.CreateIndex(
                name: "IX_cost_centres_OrgNodeId",
                table: "cost_centres",
                column: "OrgNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_cost_centres_TenantId_Code",
                table: "cost_centres",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_direct_purchases_BranchId",
                table: "direct_purchases",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_direct_purchases_MaterialItemId",
                table: "direct_purchases",
                column: "MaterialItemId");

            migrationBuilder.CreateIndex(
                name: "IX_direct_purchases_PurchasedByUserId",
                table: "direct_purchases",
                column: "PurchasedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_direct_purchases_ReconciledByUserId",
                table: "direct_purchases",
                column: "ReconciledByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_direct_purchases_SupplierId",
                table: "direct_purchases",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_direct_purchases_TenantId_Status",
                table: "direct_purchases",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_direct_purchases_WorkOrderId",
                table: "direct_purchases",
                column: "WorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_email_delivery_logs_CreatedAt",
                table: "email_delivery_logs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_email_delivery_logs_EventKey",
                table: "email_delivery_logs",
                column: "EventKey");

            migrationBuilder.CreateIndex(
                name: "IX_email_delivery_logs_OutboxMessageId",
                table: "email_delivery_logs",
                column: "OutboxMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_email_delivery_logs_Status",
                table: "email_delivery_logs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_email_delivery_logs_TemplateKey",
                table: "email_delivery_logs",
                column: "TemplateKey");

            migrationBuilder.CreateIndex(
                name: "IX_email_delivery_logs_TenantId_DocumentType_DocumentId",
                table: "email_delivery_logs",
                columns: new[] { "TenantId", "DocumentType", "DocumentId" });

            migrationBuilder.CreateIndex(
                name: "IX_email_intake_messages_TenantId_CreatedAt",
                table: "email_intake_messages",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_email_intake_messages_TenantId_MessageId",
                table: "email_intake_messages",
                columns: new[] { "TenantId", "MessageId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_email_intake_messages_WorkOrderId",
                table: "email_intake_messages",
                column: "WorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_email_intake_settings_DefaultAssignmentGroupId",
                table: "email_intake_settings",
                column: "DefaultAssignmentGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_email_intake_settings_DefaultBranchId",
                table: "email_intake_settings",
                column: "DefaultBranchId");

            migrationBuilder.CreateIndex(
                name: "IX_email_intake_settings_DefaultClientId",
                table: "email_intake_settings",
                column: "DefaultClientId");

            migrationBuilder.CreateIndex(
                name: "IX_email_intake_settings_TenantId",
                table: "email_intake_settings",
                column: "TenantId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_email_outbox_messages_CreatedAt",
                table: "email_outbox_messages",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_email_outbox_messages_IdempotencyKey",
                table: "email_outbox_messages",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_email_outbox_messages_Status_NextAttemptAt",
                table: "email_outbox_messages",
                columns: new[] { "Status", "NextAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "IX_email_outbox_messages_TenantId_DocumentType_DocumentId",
                table: "email_outbox_messages",
                columns: new[] { "TenantId", "DocumentType", "DocumentId" });

            migrationBuilder.CreateIndex(
                name: "IX_email_settings_ManagedEmailAddress",
                table: "email_settings",
                column: "ManagedEmailAddress",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_email_settings_TenantId",
                table: "email_settings",
                column: "TenantId",
                unique: true);

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
                name: "IX_exchange_rates_TenantId_BaseCurrency_QuoteCurrency_Effectiv~",
                table: "exchange_rates",
                columns: new[] { "TenantId", "BaseCurrency", "QuoteCurrency", "EffectiveFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_factory_settings_OperatorReportKey",
                table: "factory_settings",
                column: "OperatorReportKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_factory_settings_TenantId",
                table: "factory_settings",
                column: "TenantId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_failure_codes_AssetCategoryId",
                table: "failure_codes",
                column: "AssetCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_failure_codes_TenantId_Kind_Code",
                table: "failure_codes",
                columns: new[] { "TenantId", "Kind", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_finance_approval_rules_RequiredApproverUserId",
                table: "finance_approval_rules",
                column: "RequiredApproverUserId");

            migrationBuilder.CreateIndex(
                name: "IX_finance_approval_rules_RequiredAssignmentGroupId",
                table: "finance_approval_rules",
                column: "RequiredAssignmentGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_finance_approval_rules_TenantId_ApprovalArea_Sequence",
                table: "finance_approval_rules",
                columns: new[] { "TenantId", "ApprovalArea", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_food_safety_records_AssetId",
                table: "food_safety_records",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_food_safety_records_CorrectiveWorkOrderId",
                table: "food_safety_records",
                column: "CorrectiveWorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_food_safety_records_FactoryId",
                table: "food_safety_records",
                column: "FactoryId");

            migrationBuilder.CreateIndex(
                name: "IX_food_safety_records_TenantId_FactoryId_Kind_DoneAt",
                table: "food_safety_records",
                columns: new[] { "TenantId", "FactoryId", "Kind", "DoneAt" });

            migrationBuilder.CreateIndex(
                name: "IX_glass_register_items_FactoryId",
                table: "glass_register_items",
                column: "FactoryId");

            migrationBuilder.CreateIndex(
                name: "IX_group_capabilities_PermissionGroupId",
                table: "group_capabilities",
                column: "PermissionGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_group_capabilities_TenantId_PermissionGroupId_Capability",
                table: "group_capabilities",
                columns: new[] { "TenantId", "PermissionGroupId", "Capability" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_held_parts_BranchId",
                table: "held_parts",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_held_parts_MaterialItemId",
                table: "held_parts",
                column: "MaterialItemId");

            migrationBuilder.CreateIndex(
                name: "IX_held_parts_ResolvedByUserId",
                table: "held_parts",
                column: "ResolvedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_held_parts_SupplierId",
                table: "held_parts",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_held_parts_TenantId_Status",
                table: "held_parts",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_held_parts_WorkOrderId",
                table: "held_parts",
                column: "WorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_idempotency_records_CreatedAt",
                table: "idempotency_records",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_idempotency_records_TenantId_UserId_Key",
                table: "idempotency_records",
                columns: new[] { "TenantId", "UserId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_import_batches_TenantId",
                table: "import_batches",
                column: "TenantId");

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
                name: "IX_intake_protocols_TenantId_Name",
                table: "intake_protocols",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_isolation_points_AssetId_Code",
                table: "isolation_points",
                columns: new[] { "AssetId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_job_plan_resources_JobPlanId",
                table: "job_plan_resources",
                column: "JobPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_job_plan_resources_MaterialItemId",
                table: "job_plan_resources",
                column: "MaterialItemId");

            migrationBuilder.CreateIndex(
                name: "IX_job_plan_steps_JobPlanId",
                table: "job_plan_steps",
                column: "JobPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_job_plans_AssetCategoryId",
                table: "job_plans",
                column: "AssetCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_job_plans_RequiredCertificationId",
                table: "job_plans",
                column: "RequiredCertificationId");

            migrationBuilder.CreateIndex(
                name: "IX_job_plans_TenantId_Name",
                table: "job_plans",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_leave_requests_DecidedByUserId",
                table: "leave_requests",
                column: "DecidedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_leave_requests_TechnicianId",
                table: "leave_requests",
                column: "TechnicianId");

            migrationBuilder.CreateIndex(
                name: "IX_leave_requests_TenantId_Status",
                table: "leave_requests",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_leave_requests_TenantId_TechnicianId",
                table: "leave_requests",
                columns: new[] { "TenantId", "TechnicianId" });

            migrationBuilder.CreateIndex(
                name: "IX_leave_requests_TenantId_UserId",
                table: "leave_requests",
                columns: new[] { "TenantId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_leave_requests_UserId",
                table: "leave_requests",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_leave_types_TenantId_Code",
                table: "leave_types",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_license_add_ons_Code",
                table: "license_add_ons",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_license_plans_PlanCode",
                table: "license_plans",
                column: "PlanCode",
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
                name: "IX_maintenance_agreement_assets_AgreementId_AssetId",
                table: "maintenance_agreement_assets",
                columns: new[] { "AgreementId", "AssetId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_maintenance_agreement_assets_AssetId",
                table: "maintenance_agreement_assets",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_maintenance_agreements_ContractorId",
                table: "maintenance_agreements",
                column: "ContractorId");

            migrationBuilder.CreateIndex(
                name: "IX_maintenance_agreements_FactoryId",
                table: "maintenance_agreements",
                column: "FactoryId");

            migrationBuilder.CreateIndex(
                name: "IX_maintenance_agreements_TenantId_AgreementNumber",
                table: "maintenance_agreements",
                columns: new[] { "TenantId", "AgreementNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_maintenance_agreements_TenantId_Status_EndDate",
                table: "maintenance_agreements",
                columns: new[] { "TenantId", "Status", "EndDate" });

            migrationBuilder.CreateIndex(
                name: "IX_maintenance_budgets_CostCentreId",
                table: "maintenance_budgets",
                column: "CostCentreId");

            migrationBuilder.CreateIndex(
                name: "IX_maintenance_budgets_FactoryId",
                table: "maintenance_budgets",
                column: "FactoryId");

            migrationBuilder.CreateIndex(
                name: "IX_maintenance_budgets_TenantId_FactoryId_Year_CostCentreId_Ca~",
                table: "maintenance_budgets",
                columns: new[] { "TenantId", "FactoryId", "Year", "CostCentreId", "Category" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_maintenance_window_handback_items_WindowId",
                table: "maintenance_window_handback_items",
                column: "WindowId");

            migrationBuilder.CreateIndex(
                name: "IX_maintenance_window_jobs_TenantId_WorkOrderId",
                table: "maintenance_window_jobs",
                columns: new[] { "TenantId", "WorkOrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_maintenance_window_jobs_WindowId_WorkOrderId",
                table: "maintenance_window_jobs",
                columns: new[] { "WindowId", "WorkOrderId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_maintenance_window_jobs_WorkOrderId",
                table: "maintenance_window_jobs",
                column: "WorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_maintenance_windows_FactoryId",
                table: "maintenance_windows",
                column: "FactoryId");

            migrationBuilder.CreateIndex(
                name: "IX_maintenance_windows_TenantId_FactoryId_WindowDate",
                table: "maintenance_windows",
                columns: new[] { "TenantId", "FactoryId", "WindowDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_maintenance_windows_TenantId_Status",
                table: "maintenance_windows",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_material_issue_lines_MaterialIssueId",
                table: "material_issue_lines",
                column: "MaterialIssueId");

            migrationBuilder.CreateIndex(
                name: "IX_material_issue_lines_MaterialItemId",
                table: "material_issue_lines",
                column: "MaterialItemId");

            migrationBuilder.CreateIndex(
                name: "IX_material_issues_ApprovedByUserId",
                table: "material_issues",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_material_issues_AssetId",
                table: "material_issues",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_material_issues_BranchId",
                table: "material_issues",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_material_issues_ClientId",
                table: "material_issues",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_material_issues_IssuedByUserId",
                table: "material_issues",
                column: "IssuedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_material_issues_IssuedToUserId",
                table: "material_issues",
                column: "IssuedToUserId");

            migrationBuilder.CreateIndex(
                name: "IX_material_issues_RejectedByUserId",
                table: "material_issues",
                column: "RejectedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_material_issues_RequestedByUserId",
                table: "material_issues",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_material_issues_SiteId",
                table: "material_issues",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "IX_material_issues_SystemId",
                table: "material_issues",
                column: "SystemId");

            migrationBuilder.CreateIndex(
                name: "IX_material_issues_TenantId_IssueNumber",
                table: "material_issues",
                columns: new[] { "TenantId", "IssueNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_material_issues_WorkOrderId",
                table: "material_issues",
                column: "WorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_material_items_PreferredSupplierId",
                table: "material_items",
                column: "PreferredSupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_material_items_SdsAttachmentId",
                table: "material_items",
                column: "SdsAttachmentId");

            migrationBuilder.CreateIndex(
                name: "IX_material_items_TenantId_Barcode",
                table: "material_items",
                columns: new[] { "TenantId", "Barcode" });

            migrationBuilder.CreateIndex(
                name: "IX_material_items_TenantId_ItemCode",
                table: "material_items",
                columns: new[] { "TenantId", "ItemCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_material_request_lines_MaterialItemId",
                table: "material_request_lines",
                column: "MaterialItemId");

            migrationBuilder.CreateIndex(
                name: "IX_material_request_lines_MaterialRequestId",
                table: "material_request_lines",
                column: "MaterialRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_material_requests_BranchId",
                table: "material_requests",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_material_requests_RequestedByUserId",
                table: "material_requests",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_material_requests_TenantId_RequestNumber",
                table: "material_requests",
                columns: new[] { "TenantId", "RequestNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_material_requests_WorkOrderId",
                table: "material_requests",
                column: "WorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_meter_readings_AssetId",
                table: "meter_readings",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_meter_readings_MeterId",
                table: "meter_readings",
                column: "MeterId");

            migrationBuilder.CreateIndex(
                name: "IX_meter_readings_TenantId_AssetId_ReadAt",
                table: "meter_readings",
                columns: new[] { "TenantId", "AssetId", "ReadAt" });

            migrationBuilder.CreateIndex(
                name: "IX_meter_readings_TenantId_MeterId_ReadAt",
                table: "meter_readings",
                columns: new[] { "TenantId", "MeterId", "ReadAt" });

            migrationBuilder.CreateIndex(
                name: "IX_meters_AssetId",
                table: "meters",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_meters_TenantId_AssetId_Name",
                table: "meters",
                columns: new[] { "TenantId", "AssetId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_meters_TenantId_UpdatedAt",
                table: "meters",
                columns: new[] { "TenantId", "UpdatedAt" });

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
                name: "IX_monitoring_webhook_integrations_DefaultAssetId",
                table: "monitoring_webhook_integrations",
                column: "DefaultAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_monitoring_webhook_integrations_DefaultAssignmentGroupId",
                table: "monitoring_webhook_integrations",
                column: "DefaultAssignmentGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_monitoring_webhook_integrations_DefaultBranchId",
                table: "monitoring_webhook_integrations",
                column: "DefaultBranchId");

            migrationBuilder.CreateIndex(
                name: "IX_monitoring_webhook_integrations_DefaultClientId",
                table: "monitoring_webhook_integrations",
                column: "DefaultClientId");

            migrationBuilder.CreateIndex(
                name: "IX_monitoring_webhook_integrations_EndpointSlug",
                table: "monitoring_webhook_integrations",
                column: "EndpointSlug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_monitoring_webhook_integrations_TenantId_Name",
                table: "monitoring_webhook_integrations",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_mrr_snapshots_SnapshotDate",
                table: "mrr_snapshots",
                column: "SnapshotDate",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_non_conformities_AuditEventId",
                table: "non_conformities",
                column: "AuditEventId");

            migrationBuilder.CreateIndex(
                name: "IX_non_conformities_FactoryId",
                table: "non_conformities",
                column: "FactoryId");

            migrationBuilder.CreateIndex(
                name: "IX_non_conformities_OwnerUserId",
                table: "non_conformities",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_non_conformities_RequirementId",
                table: "non_conformities",
                column: "RequirementId");

            migrationBuilder.CreateIndex(
                name: "IX_non_conformities_StandardId",
                table: "non_conformities",
                column: "StandardId");

            migrationBuilder.CreateIndex(
                name: "IX_non_conformities_TenantId_Number",
                table: "non_conformities",
                columns: new[] { "TenantId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_non_conformities_TenantId_Status",
                table: "non_conformities",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_non_conformities_WorkOrderId",
                table: "non_conformities",
                column: "WorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_notification_settings_TenantId",
                table: "notification_settings",
                column: "TenantId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_notifications_CreatedAt",
                table: "notifications",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_notifications_PushPendingSince",
                table: "notifications",
                column: "PushPendingSince",
                filter: "\"PushPendingSince\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_notifications_TenantId_UserId_ArchivedAt_CreatedAt",
                table: "notifications",
                columns: new[] { "TenantId", "UserId", "ArchivedAt", "CreatedAt" },
                descending: new[] { false, false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_notifications_UserId",
                table: "notifications",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_numbering_settings_BranchId",
                table: "numbering_settings",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_numbering_settings_TenantId_BranchId_DocumentType",
                table: "numbering_settings",
                columns: new[] { "TenantId", "BranchId", "DocumentType" },
                unique: true,
                filter: "\"BranchId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_numbering_settings_TenantId_DocumentType",
                table: "numbering_settings",
                columns: new[] { "TenantId", "DocumentType" },
                unique: true,
                filter: "\"BranchId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_org_nodes_ParentId",
                table: "org_nodes",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_org_nodes_TenantId_Code",
                table: "org_nodes",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_org_nodes_TenantId_Level",
                table: "org_nodes",
                columns: new[] { "TenantId", "Level" });

            migrationBuilder.CreateIndex(
                name: "IX_org_nodes_TenantId_ParentId_Name",
                table: "org_nodes",
                columns: new[] { "TenantId", "ParentId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_org_nodes_TenantId_UpdatedAt",
                table: "org_nodes",
                columns: new[] { "TenantId", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_overhaul_items_AssetId",
                table: "overhaul_items",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_overhaul_items_FactoryId",
                table: "overhaul_items",
                column: "FactoryId");

            migrationBuilder.CreateIndex(
                name: "IX_overhaul_items_OverhaulPlanId",
                table: "overhaul_items",
                column: "OverhaulPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_overhaul_items_TenantId_FactoryId_Status",
                table: "overhaul_items",
                columns: new[] { "TenantId", "FactoryId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_overhaul_items_TenantId_WorkOrderId",
                table: "overhaul_items",
                columns: new[] { "TenantId", "WorkOrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_overhaul_items_WorkOrderId",
                table: "overhaul_items",
                column: "WorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_overhaul_plans_FactoryId",
                table: "overhaul_plans",
                column: "FactoryId");

            migrationBuilder.CreateIndex(
                name: "IX_overhaul_plans_TenantId_FactoryId_PlannedStart",
                table: "overhaul_plans",
                columns: new[] { "TenantId", "FactoryId", "PlannedStart" });

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
                name: "IX_parts_return_lines_MaterialItemId",
                table: "parts_return_lines",
                column: "MaterialItemId");

            migrationBuilder.CreateIndex(
                name: "IX_parts_return_lines_PartsReturnId",
                table: "parts_return_lines",
                column: "PartsReturnId");

            migrationBuilder.CreateIndex(
                name: "IX_parts_returns_BranchId",
                table: "parts_returns",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_parts_returns_ProcessedByUserId",
                table: "parts_returns",
                column: "ProcessedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_parts_returns_RequestedByUserId",
                table: "parts_returns",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_parts_returns_TenantId_ReturnNumber",
                table: "parts_returns",
                columns: new[] { "TenantId", "ReturnNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_parts_returns_TenantId_Status",
                table: "parts_returns",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_parts_returns_TenantId_WorkOrderId",
                table: "parts_returns",
                columns: new[] { "TenantId", "WorkOrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_parts_returns_WorkOrderId",
                table: "parts_returns",
                column: "WorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_password_reset_tokens_TokenHash",
                table: "password_reset_tokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_password_reset_tokens_UserId_ExpiresAt",
                table: "password_reset_tokens",
                columns: new[] { "UserId", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_pending_signups_Email",
                table: "pending_signups",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_pending_signups_TokenHash",
                table: "pending_signups",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_permission_groups_TenantId_Name",
                table: "permission_groups",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_permission_thresholds_PermissionGroupId",
                table: "permission_thresholds",
                column: "PermissionGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_permission_thresholds_UserId",
                table: "permission_thresholds",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_platform_announcements_Status",
                table: "platform_announcements",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_platform_document_templates_Type_IsDefault",
                table: "platform_document_templates",
                columns: new[] { "Type", "IsDefault" });

            migrationBuilder.CreateIndex(
                name: "IX_platform_expenses_TenantId",
                table: "platform_expenses",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_platform_invoice_lines_InvoiceId",
                table: "platform_invoice_lines",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_platform_invoices_InvoiceNumber",
                table: "platform_invoices",
                column: "InvoiceNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_platform_invoices_QuotationId",
                table: "platform_invoices",
                column: "QuotationId");

            migrationBuilder.CreateIndex(
                name: "IX_platform_invoices_TenantId",
                table: "platform_invoices",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_platform_payments_InvoiceId",
                table: "platform_payments",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_platform_payments_PaymentNumber",
                table: "platform_payments",
                column: "PaymentNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_platform_payments_TenantId",
                table: "platform_payments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_platform_quotation_lines_QuotationId",
                table: "platform_quotation_lines",
                column: "QuotationId");

            migrationBuilder.CreateIndex(
                name: "IX_platform_quotations_QuotationNumber",
                table: "platform_quotations",
                column: "QuotationNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_platform_quotations_TenantId",
                table: "platform_quotations",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_platform_settings_Category",
                table: "platform_settings",
                column: "Category",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_pm_plan_events_PlanId",
                table: "pm_plan_events",
                column: "PlanId");

            migrationBuilder.CreateIndex(
                name: "IX_pm_plan_events_TenantId_Kind_OccurredAt",
                table: "pm_plan_events",
                columns: new[] { "TenantId", "Kind", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_pm_plan_events_TenantId_PlanId_OccurredAt",
                table: "pm_plan_events",
                columns: new[] { "TenantId", "PlanId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_pm_plan_events_WorkOrderId",
                table: "pm_plan_events",
                column: "WorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_pm_plan_route_assets_AssetId",
                table: "pm_plan_route_assets",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_pm_plan_route_assets_PlanId_AssetId",
                table: "pm_plan_route_assets",
                columns: new[] { "PlanId", "AssetId" },
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
                name: "IX_pm_template_questions_SectionId",
                table: "pm_template_questions",
                column: "SectionId");

            migrationBuilder.CreateIndex(
                name: "IX_pm_template_sections_PmTemplateId",
                table: "pm_template_sections",
                column: "PmTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_pm_template_versions_PmTemplateId_Version",
                table: "pm_template_versions",
                columns: new[] { "PmTemplateId", "Version" });

            migrationBuilder.CreateIndex(
                name: "IX_pm_templates_AssetCategoryId",
                table: "pm_templates",
                column: "AssetCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_pm_templates_TemplatePackId_Code",
                table: "pm_templates",
                columns: new[] { "TemplatePackId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_pm_templates_TenantId_Category_Name",
                table: "pm_templates",
                columns: new[] { "TenantId", "Category", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ppe_issues_TechnicianId",
                table: "ppe_issues",
                column: "TechnicianId");

            migrationBuilder.CreateIndex(
                name: "IX_ppe_issues_TenantId_IssuedOn",
                table: "ppe_issues",
                columns: new[] { "TenantId", "IssuedOn" });

            migrationBuilder.CreateIndex(
                name: "IX_preventive_maintenance_plans_AssetId",
                table: "preventive_maintenance_plans",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_preventive_maintenance_plans_BranchId",
                table: "preventive_maintenance_plans",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_preventive_maintenance_plans_DefaultAssignmentGroupId",
                table: "preventive_maintenance_plans",
                column: "DefaultAssignmentGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_preventive_maintenance_plans_MeterId",
                table: "preventive_maintenance_plans",
                column: "MeterId");

            migrationBuilder.CreateIndex(
                name: "IX_preventive_maintenance_plans_OrgNodeId",
                table: "preventive_maintenance_plans",
                column: "OrgNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_preventive_maintenance_plans_PmTemplateId",
                table: "preventive_maintenance_plans",
                column: "PmTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_preventive_maintenance_plans_SiteId",
                table: "preventive_maintenance_plans",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "IX_preventive_maintenance_plans_TenantId_OrgNodeId_Status",
                table: "preventive_maintenance_plans",
                columns: new[] { "TenantId", "OrgNodeId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_order_lines_MaterialItemId",
                table: "purchase_order_lines",
                column: "MaterialItemId");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_order_lines_PurchaseOrderId",
                table: "purchase_order_lines",
                column: "PurchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_order_lines_TenantId",
                table: "purchase_order_lines",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_order_receipt_events_BranchId",
                table: "purchase_order_receipt_events",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_order_receipt_events_PurchaseOrderId",
                table: "purchase_order_receipt_events",
                column: "PurchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_order_receipt_events_PurchaseOrderLineId",
                table: "purchase_order_receipt_events",
                column: "PurchaseOrderLineId");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_order_receipt_events_TenantId",
                table: "purchase_order_receipt_events",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_orders_SupplierId",
                table: "purchase_orders",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_orders_TenantId_PoNumber",
                table: "purchase_orders",
                columns: new[] { "TenantId", "PoNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_purchase_orders_TenantId_Status",
                table: "purchase_orders",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_requisition_lines_MaterialItemId",
                table: "purchase_requisition_lines",
                column: "MaterialItemId");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_requisition_lines_PurchaseOrderId",
                table: "purchase_requisition_lines",
                column: "PurchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_requisition_lines_RequisitionId",
                table: "purchase_requisition_lines",
                column: "RequisitionId");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_requisition_lines_SupplierId",
                table: "purchase_requisition_lines",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_requisition_lines_WorkOrderId",
                table: "purchase_requisition_lines",
                column: "WorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_requisitions_FactoryId",
                table: "purchase_requisitions",
                column: "FactoryId");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_requisitions_StoreId",
                table: "purchase_requisitions",
                column: "StoreId");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_requisitions_TenantId_RequisitionNumber",
                table: "purchase_requisitions",
                columns: new[] { "TenantId", "RequisitionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_purchase_requisitions_TenantId_Status",
                table: "purchase_requisitions",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_push_subscriptions_Endpoint",
                table: "push_subscriptions",
                column: "Endpoint",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_push_subscriptions_TenantId_UserId",
                table: "push_subscriptions",
                columns: new[] { "TenantId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_push_subscriptions_UserId",
                table: "push_subscriptions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_rate_cards_TenantId_Category_TimeType",
                table: "rate_cards",
                columns: new[] { "TenantId", "Category", "TimeType" });

            migrationBuilder.CreateIndex(
                name: "IX_rate_cards_TenantId_Name",
                table: "rate_cards",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_role_permissions_TenantId_Role",
                table: "role_permissions",
                columns: new[] { "TenantId", "Role" },
                unique: true);

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
                name: "IX_scheduled_reports_OrgNodeId",
                table: "scheduled_reports",
                column: "OrgNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_scheduled_reports_TenantId",
                table: "scheduled_reports",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_scheduled_reports_TenantId_IsActive",
                table: "scheduled_reports",
                columns: new[] { "TenantId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_sites_ClientId",
                table: "sites",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_sites_TenantId_SiteCode",
                table: "sites",
                columns: new[] { "TenantId", "SiteCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sla_definitions_TenantId_PlanName",
                table: "sla_definitions",
                columns: new[] { "TenantId", "PlanName" },
                unique: true);

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
                name: "IX_sla_matrix_cells_TenantId_Priority_Criticality",
                table: "sla_matrix_cells",
                columns: new[] { "TenantId", "Priority", "Criticality" },
                unique: true);

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
                name: "IX_sla_rules_SlaDefinitionId_Priority",
                table: "sla_rules",
                columns: new[] { "SlaDefinitionId", "Priority" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sla_rules_TenantId",
                table: "sla_rules",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_spare_unit_events_SpareUnitId_OccurredAt",
                table: "spare_unit_events",
                columns: new[] { "SpareUnitId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_spare_units_AssetId",
                table: "spare_units",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_spare_units_MaterialItemId",
                table: "spare_units",
                column: "MaterialItemId");

            migrationBuilder.CreateIndex(
                name: "IX_spare_units_RepairSupplierId",
                table: "spare_units",
                column: "RepairSupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_spare_units_StoreId",
                table: "spare_units",
                column: "StoreId");

            migrationBuilder.CreateIndex(
                name: "IX_spare_units_TenantId_MaterialItemId_SerialNumber",
                table: "spare_units",
                columns: new[] { "TenantId", "MaterialItemId", "SerialNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_spare_units_TenantId_Status",
                table: "spare_units",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_standard_requirements_StandardId_Sequence",
                table: "standard_requirements",
                columns: new[] { "StandardId", "Sequence" });

            migrationBuilder.CreateIndex(
                name: "IX_stock_count_lines_MaterialItemId",
                table: "stock_count_lines",
                column: "MaterialItemId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_count_lines_StockCountId_MaterialItemId",
                table: "stock_count_lines",
                columns: new[] { "StockCountId", "MaterialItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_stock_count_lines_TenantId",
                table: "stock_count_lines",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_counts_BranchId",
                table: "stock_counts",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_counts_PostedByUserId",
                table: "stock_counts",
                column: "PostedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_counts_StartedByUserId",
                table: "stock_counts",
                column: "StartedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_counts_TenantId_CountNumber",
                table: "stock_counts",
                columns: new[] { "TenantId", "CountNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_stock_counts_TenantId_Status",
                table: "stock_counts",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_stock_movements_BranchId",
                table: "stock_movements",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_movements_CreatedByUserId",
                table: "stock_movements",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_movements_MaterialId",
                table: "stock_movements",
                column: "MaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_movements_MaterialRequestId",
                table: "stock_movements",
                column: "MaterialRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_movements_StoreId",
                table: "stock_movements",
                column: "StoreId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_movements_TenantId_MaterialId_CreatedAt",
                table: "stock_movements",
                columns: new[] { "TenantId", "MaterialId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_stock_movements_TenantId_StoreId_CreatedAt",
                table: "stock_movements",
                columns: new[] { "TenantId", "StoreId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_stock_movements_WorkOrderId",
                table: "stock_movements",
                column: "WorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_reservations_MaterialItemId",
                table: "stock_reservations",
                column: "MaterialItemId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_reservations_StoreId_MaterialItemId_Status",
                table: "stock_reservations",
                columns: new[] { "StoreId", "MaterialItemId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_stock_reservations_TenantId_WorkOrderId",
                table: "stock_reservations",
                columns: new[] { "TenantId", "WorkOrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_stock_reservations_WorkOrderId",
                table: "stock_reservations",
                column: "WorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_transfers_ApprovedByUserId",
                table: "stock_transfers",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_transfers_CompletedByUserId",
                table: "stock_transfers",
                column: "CompletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_transfers_FromBranchId",
                table: "stock_transfers",
                column: "FromBranchId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_transfers_MaterialId",
                table: "stock_transfers",
                column: "MaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_transfers_RequestedByUserId",
                table: "stock_transfers",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_transfers_TenantId_ReferenceNumber",
                table: "stock_transfers",
                columns: new[] { "TenantId", "ReferenceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_stock_transfers_ToBranchId",
                table: "stock_transfers",
                column: "ToBranchId");

            migrationBuilder.CreateIndex(
                name: "IX_store_bins_StoreId_Code",
                table: "store_bins",
                columns: new[] { "StoreId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_store_count_lines_MaterialItemId",
                table: "store_count_lines",
                column: "MaterialItemId");

            migrationBuilder.CreateIndex(
                name: "IX_store_count_lines_StoreCountId_MaterialItemId",
                table: "store_count_lines",
                columns: new[] { "StoreCountId", "MaterialItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_store_counts_BinId",
                table: "store_counts",
                column: "BinId");

            migrationBuilder.CreateIndex(
                name: "IX_store_counts_StoreId",
                table: "store_counts",
                column: "StoreId");

            migrationBuilder.CreateIndex(
                name: "IX_store_counts_TenantId_CountNumber",
                table: "store_counts",
                columns: new[] { "TenantId", "CountNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_store_stocks_BinId",
                table: "store_stocks",
                column: "BinId");

            migrationBuilder.CreateIndex(
                name: "IX_store_stocks_MaterialItemId",
                table: "store_stocks",
                column: "MaterialItemId");

            migrationBuilder.CreateIndex(
                name: "IX_store_stocks_StoreId_MaterialItemId",
                table: "store_stocks",
                columns: new[] { "StoreId", "MaterialItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_store_stocks_TenantId_MaterialItemId",
                table: "store_stocks",
                columns: new[] { "TenantId", "MaterialItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_store_transfers_FromStoreId",
                table: "store_transfers",
                column: "FromStoreId");

            migrationBuilder.CreateIndex(
                name: "IX_store_transfers_MaterialItemId",
                table: "store_transfers",
                column: "MaterialItemId");

            migrationBuilder.CreateIndex(
                name: "IX_store_transfers_TenantId_Status",
                table: "store_transfers",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_store_transfers_TenantId_TransferNumber",
                table: "store_transfers",
                columns: new[] { "TenantId", "TransferNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_store_transfers_ToStoreId",
                table: "store_transfers",
                column: "ToStoreId");

            migrationBuilder.CreateIndex(
                name: "IX_store_transfers_WorkOrderId",
                table: "store_transfers",
                column: "WorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_stores_FactoryId",
                table: "stores",
                column: "FactoryId");

            migrationBuilder.CreateIndex(
                name: "IX_stores_TenantId_Code",
                table: "stores",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_stores_TenantId_FactoryId",
                table: "stores",
                columns: new[] { "TenantId", "FactoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_suppliers_TenantId_SupplierName",
                table: "suppliers",
                columns: new[] { "TenantId", "SupplierName" });

            migrationBuilder.CreateIndex(
                name: "IX_system_template_packs_Code",
                table: "system_template_packs",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_systems_ClientId",
                table: "systems",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_systems_SiteId",
                table: "systems",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "IX_systems_TenantId_SiteId_SystemName",
                table: "systems",
                columns: new[] { "TenantId", "SiteId", "SystemName" },
                unique: true);

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
                name: "IX_technicians_ActiveWorkOrderId",
                table: "technicians",
                column: "ActiveWorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_technicians_BranchId",
                table: "technicians",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_technicians_HomeOrgNodeId",
                table: "technicians",
                column: "HomeOrgNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_technicians_TenantId_Email",
                table: "technicians",
                columns: new[] { "TenantId", "Email" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_technicians_UserId",
                table: "technicians",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tenant_api_keys_TenantId_Label",
                table: "tenant_api_keys",
                columns: new[] { "TenantId", "Label" });

            migrationBuilder.CreateIndex(
                name: "IX_tenant_finance_defaults_TenantId",
                table: "tenant_finance_defaults",
                column: "TenantId",
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
                name: "IX_tenant_module_settings_TenantId_ModuleKey",
                table: "tenant_module_settings",
                columns: new[] { "TenantId", "ModuleKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tenant_notification_recipients_TenantId_RecipientGroup_Email",
                table: "tenant_notification_recipients",
                columns: new[] { "TenantId", "RecipientGroup", "Email" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tenant_notification_settings_TenantId_NotificationKey",
                table: "tenant_notification_settings",
                columns: new[] { "TenantId", "NotificationKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tenant_operations_policies_TenantId",
                table: "tenant_operations_policies",
                column: "TenantId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tenant_security_policies_TenantId",
                table: "tenant_security_policies",
                column: "TenantId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tenant_template_pack_installations_SourceTemplatePackId",
                table: "tenant_template_pack_installations",
                column: "SourceTemplatePackId");

            migrationBuilder.CreateIndex(
                name: "IX_tenant_template_pack_installations_TenantId_SourceTemplateP~",
                table: "tenant_template_pack_installations",
                columns: new[] { "TenantId", "SourceTemplatePackId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tenant_webhook_deliveries_TenantId_WebhookId_AttemptedAt",
                table: "tenant_webhook_deliveries",
                columns: new[] { "TenantId", "WebhookId", "AttemptedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_tenant_webhook_deliveries_WebhookId",
                table: "tenant_webhook_deliveries",
                column: "WebhookId");

            migrationBuilder.CreateIndex(
                name: "IX_tenant_webhooks_TenantId",
                table: "tenant_webhooks",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_tenants_Slug",
                table: "tenants",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_time_attendance_settings_TenantId",
                table: "time_attendance_settings",
                column: "TenantId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tool_assignments_CheckedOutByUserId",
                table: "tool_assignments",
                column: "CheckedOutByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_tool_assignments_ReturnedByUserId",
                table: "tool_assignments",
                column: "ReturnedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_tool_assignments_TechnicianId",
                table: "tool_assignments",
                column: "TechnicianId");

            migrationBuilder.CreateIndex(
                name: "IX_tool_assignments_TenantId_TechnicianId_ReturnedAt",
                table: "tool_assignments",
                columns: new[] { "TenantId", "TechnicianId", "ReturnedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_tool_assignments_ToolId",
                table: "tool_assignments",
                column: "ToolId");

            migrationBuilder.CreateIndex(
                name: "IX_tool_assignments_WorkOrderId",
                table: "tool_assignments",
                column: "WorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_tool_calibrations_TenantId",
                table: "tool_calibrations",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_tool_calibrations_ToolId",
                table: "tool_calibrations",
                column: "ToolId");

            migrationBuilder.CreateIndex(
                name: "IX_tools_CurrentAssignmentId",
                table: "tools",
                column: "CurrentAssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_tools_HomeBranchId",
                table: "tools",
                column: "HomeBranchId");

            migrationBuilder.CreateIndex(
                name: "IX_tools_TenantId_Status",
                table: "tools",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_tools_TenantId_ToolCode",
                table: "tools",
                columns: new[] { "TenantId", "ToolCode" },
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
                name: "IX_user_branch_assignments_BranchId",
                table: "user_branch_assignments",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_user_branch_assignments_TenantId_UserId_BranchId",
                table: "user_branch_assignments",
                columns: new[] { "TenantId", "UserId", "BranchId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_branch_assignments_UserId",
                table: "user_branch_assignments",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_user_certifications_CertificationDefinitionId",
                table: "user_certifications",
                column: "CertificationDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_user_certifications_TenantId_UserId_CertificationDefinition~",
                table: "user_certifications",
                columns: new[] { "TenantId", "UserId", "CertificationDefinitionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_certifications_UserId",
                table: "user_certifications",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_user_dashboard_layouts_TenantId_UserId",
                table: "user_dashboard_layouts",
                columns: new[] { "TenantId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_dashboard_layouts_UserId",
                table: "user_dashboard_layouts",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_user_notification_preferences_TenantId_UserId_NotificationK~",
                table: "user_notification_preferences",
                columns: new[] { "TenantId", "UserId", "NotificationKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_notification_preferences_UserId",
                table: "user_notification_preferences",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_user_org_node_assignments_OrgNodeId",
                table: "user_org_node_assignments",
                column: "OrgNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_user_org_node_assignments_TenantId_UserId_OrgNodeId",
                table: "user_org_node_assignments",
                columns: new[] { "TenantId", "UserId", "OrgNodeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_org_node_assignments_UserId",
                table: "user_org_node_assignments",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_user_permission_groups_PermissionGroupId",
                table: "user_permission_groups",
                column: "PermissionGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_user_permission_groups_TenantId_UserId_PermissionGroupId",
                table: "user_permission_groups",
                columns: new[] { "TenantId", "UserId", "PermissionGroupId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_permission_groups_UserId",
                table: "user_permission_groups",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_user_permission_overrides_TenantId_UserId_Capability",
                table: "user_permission_overrides",
                columns: new[] { "TenantId", "UserId", "Capability" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_permission_overrides_UserId",
                table: "user_permission_overrides",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_user_permissions_UserId",
                table: "user_permissions",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_sessions_JwtId",
                table: "user_sessions",
                column: "JwtId");

            migrationBuilder.CreateIndex(
                name: "IX_user_sessions_TenantId_UserId_LoginAt",
                table: "user_sessions",
                columns: new[] { "TenantId", "UserId", "LoginAt" });

            migrationBuilder.CreateIndex(
                name: "IX_user_sessions_UserId",
                table: "user_sessions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_users_DefaultBranchId",
                table: "users",
                column: "DefaultBranchId");

            migrationBuilder.CreateIndex(
                name: "IX_users_Email",
                table: "users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_TenantId_Username",
                table: "users",
                columns: new[] { "TenantId", "Username" },
                unique: true,
                filter: "\"Username\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_vendor_catalogue_items_MaterialItemId",
                table: "vendor_catalogue_items",
                column: "MaterialItemId");

            migrationBuilder.CreateIndex(
                name: "IX_vendor_catalogue_items_SupplierId_MaterialItemId",
                table: "vendor_catalogue_items",
                columns: new[] { "SupplierId", "MaterialItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_vendor_catalogue_items_TenantId_MaterialItemId",
                table: "vendor_catalogue_items",
                columns: new[] { "TenantId", "MaterialItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_waste_disposals_AttachmentId",
                table: "waste_disposals",
                column: "AttachmentId");

            migrationBuilder.CreateIndex(
                name: "IX_waste_disposals_ContractorId",
                table: "waste_disposals",
                column: "ContractorId");

            migrationBuilder.CreateIndex(
                name: "IX_waste_disposals_FactoryId",
                table: "waste_disposals",
                column: "FactoryId");

            migrationBuilder.CreateIndex(
                name: "IX_waste_disposals_TenantId_FactoryId_DisposedOn",
                table: "waste_disposals",
                columns: new[] { "TenantId", "FactoryId", "DisposedOn" });

            migrationBuilder.CreateIndex(
                name: "IX_whatsapp_conversations_TenantId_LastMessageAt",
                table: "whatsapp_conversations",
                columns: new[] { "TenantId", "LastMessageAt" });

            migrationBuilder.CreateIndex(
                name: "IX_whatsapp_messages_ConversationId",
                table: "whatsapp_messages",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_whatsapp_messages_TenantId",
                table: "whatsapp_messages",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_whatsapp_opt_outs_PhoneNumber",
                table: "whatsapp_opt_outs",
                column: "PhoneNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_whatsapp_outbox_messages_IdempotencyKey",
                table: "whatsapp_outbox_messages",
                column: "IdempotencyKey",
                unique: true,
                filter: "\"IdempotencyKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_whatsapp_outbox_messages_ProviderMessageId",
                table: "whatsapp_outbox_messages",
                column: "ProviderMessageId",
                unique: true,
                filter: "\"ProviderMessageId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_whatsapp_outbox_messages_Status_NextAttemptAt",
                table: "whatsapp_outbox_messages",
                columns: new[] { "Status", "NextAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "IX_whatsapp_outbox_messages_TenantId_CreatedAt",
                table: "whatsapp_outbox_messages",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_whatsapp_settings_TenantId",
                table: "whatsapp_settings",
                column: "TenantId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_whatsapp_templates_TenantId_Name",
                table: "whatsapp_templates",
                columns: new[] { "TenantId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_whatsapp_verification_codes_TenantId_ClientPortalUserId_Cre~",
                table: "whatsapp_verification_codes",
                columns: new[] { "TenantId", "ClientPortalUserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_whatsapp_verification_codes_TenantId_UserId_CreatedAt",
                table: "whatsapp_verification_codes",
                columns: new[] { "TenantId", "UserId", "CreatedAt" });

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
                name: "IX_work_order_checklist_items_AttachmentId",
                table: "work_order_checklist_items",
                column: "AttachmentId");

            migrationBuilder.CreateIndex(
                name: "IX_work_order_checklist_items_CompletedByUserId",
                table: "work_order_checklist_items",
                column: "CompletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_work_order_checklist_items_PmTemplateQuestionId",
                table: "work_order_checklist_items",
                column: "PmTemplateQuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_work_order_checklist_items_TenantId_WorkOrderId_SortOrder",
                table: "work_order_checklist_items",
                columns: new[] { "TenantId", "WorkOrderId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_work_order_checklist_items_WorkOrderId",
                table: "work_order_checklist_items",
                column: "WorkOrderId");

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

            migrationBuilder.CreateIndex(
                name: "IX_work_order_material_usages_AssetId",
                table: "work_order_material_usages",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_work_order_material_usages_MaterialItemId",
                table: "work_order_material_usages",
                column: "MaterialItemId");

            migrationBuilder.CreateIndex(
                name: "IX_work_order_material_usages_TenantId_WorkOrderId_MaterialIte~",
                table: "work_order_material_usages",
                columns: new[] { "TenantId", "WorkOrderId", "MaterialItemId", "UsedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_work_order_material_usages_UsedByUserId",
                table: "work_order_material_usages",
                column: "UsedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_work_order_material_usages_WorkOrderId",
                table: "work_order_material_usages",
                column: "WorkOrderId");

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
                name: "IX_work_order_photo_evidence_AttachmentId",
                table: "work_order_photo_evidence",
                column: "AttachmentId");

            migrationBuilder.CreateIndex(
                name: "IX_work_order_photo_evidence_TenantId_WorkOrderId_UploadedAt",
                table: "work_order_photo_evidence",
                columns: new[] { "TenantId", "WorkOrderId", "UploadedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_work_order_photo_evidence_UploadedByUserId",
                table: "work_order_photo_evidence",
                column: "UploadedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_work_order_photo_evidence_WorkOrderId",
                table: "work_order_photo_evidence",
                column: "WorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_work_order_signatures_CapturedByUserId",
                table: "work_order_signatures",
                column: "CapturedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_work_order_signatures_TenantId_WorkOrderId_SignatureType",
                table: "work_order_signatures",
                columns: new[] { "TenantId", "WorkOrderId", "SignatureType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_work_order_signatures_WorkOrderId",
                table: "work_order_signatures",
                column: "WorkOrderId");

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
                name: "IX_work_order_time_logs_TechnicianId",
                table: "work_order_time_logs",
                column: "TechnicianId");

            migrationBuilder.CreateIndex(
                name: "IX_work_order_time_logs_TenantId_WorkOrderId",
                table: "work_order_time_logs",
                columns: new[] { "TenantId", "WorkOrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_work_order_time_logs_WorkOrderId",
                table: "work_order_time_logs",
                column: "WorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_work_order_visits_TechnicianId",
                table: "work_order_visits",
                column: "TechnicianId");

            migrationBuilder.CreateIndex(
                name: "IX_work_order_visits_TenantId_WorkOrderId_TechnicianId",
                table: "work_order_visits",
                columns: new[] { "TenantId", "WorkOrderId", "TechnicianId" });

            migrationBuilder.CreateIndex(
                name: "IX_work_order_visits_WorkOrderId",
                table: "work_order_visits",
                column: "WorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_work_orders_AssetId",
                table: "work_orders",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_work_orders_AssignedTechnicianId",
                table: "work_orders",
                column: "AssignedTechnicianId");

            migrationBuilder.CreateIndex(
                name: "IX_work_orders_AssignmentGroupId",
                table: "work_orders",
                column: "AssignmentGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_work_orders_BranchId",
                table: "work_orders",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_work_orders_ClientId",
                table: "work_orders",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_work_orders_ClosedByUserId",
                table: "work_orders",
                column: "ClosedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_work_orders_ContractId",
                table: "work_orders",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_work_orders_ContractorId",
                table: "work_orders",
                column: "ContractorId");

            migrationBuilder.CreateIndex(
                name: "IX_work_orders_FailureCauseCodeId",
                table: "work_orders",
                column: "FailureCauseCodeId");

            migrationBuilder.CreateIndex(
                name: "IX_work_orders_FailureProblemCodeId",
                table: "work_orders",
                column: "FailureProblemCodeId");

            migrationBuilder.CreateIndex(
                name: "IX_work_orders_FailureRemedyCodeId",
                table: "work_orders",
                column: "FailureRemedyCodeId");

            migrationBuilder.CreateIndex(
                name: "IX_work_orders_FollowUpOfWorkOrderId",
                table: "work_orders",
                column: "FollowUpOfWorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_work_orders_JobPlanId",
                table: "work_orders",
                column: "JobPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_work_orders_LeadTechnicianId",
                table: "work_orders",
                column: "LeadTechnicianId");

            migrationBuilder.CreateIndex(
                name: "IX_work_orders_MaintenanceAgreementId",
                table: "work_orders",
                column: "MaintenanceAgreementId");

            migrationBuilder.CreateIndex(
                name: "IX_work_orders_OrgNodeId",
                table: "work_orders",
                column: "OrgNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_work_orders_ParentWorkOrderId",
                table: "work_orders",
                column: "ParentWorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_work_orders_PmTemplateId",
                table: "work_orders",
                column: "PmTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_work_orders_PreventiveMaintenancePlanId",
                table: "work_orders",
                column: "PreventiveMaintenancePlanId");

            migrationBuilder.CreateIndex(
                name: "IX_work_orders_RateCardId",
                table: "work_orders",
                column: "RateCardId");

            migrationBuilder.CreateIndex(
                name: "IX_work_orders_RequiredCertificationId",
                table: "work_orders",
                column: "RequiredCertificationId");

            migrationBuilder.CreateIndex(
                name: "IX_work_orders_ReworkOfWorkOrderId",
                table: "work_orders",
                column: "ReworkOfWorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_work_orders_SiteId",
                table: "work_orders",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "IX_work_orders_SystemId",
                table: "work_orders",
                column: "SystemId");

            migrationBuilder.CreateIndex(
                name: "IX_work_orders_TenantId_AssetId_FailureProblemCodeId_Completed~",
                table: "work_orders",
                columns: new[] { "TenantId", "AssetId", "FailureProblemCodeId", "CompletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_work_orders_TenantId_CreatedAt",
                table: "work_orders",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_work_orders_TenantId_DueDate",
                table: "work_orders",
                columns: new[] { "TenantId", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_work_orders_TenantId_OrgNodeId_Status",
                table: "work_orders",
                columns: new[] { "TenantId", "OrgNodeId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_work_orders_TenantId_ScheduledStartAt",
                table: "work_orders",
                columns: new[] { "TenantId", "ScheduledStartAt" },
                filter: "\"ScheduledStartAt\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_work_orders_TenantId_Status_CreatedAt",
                table: "work_orders",
                columns: new[] { "TenantId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_work_orders_TenantId_WorkOrderNumber",
                table: "work_orders",
                columns: new[] { "TenantId", "WorkOrderNumber" },
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
                name: "FK_assignment_group_members_technicians_TechnicianId",
                table: "assignment_group_members",
                column: "TechnicianId",
                principalTable: "technicians",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_attendance_entries_technicians_TechnicianId",
                table: "attendance_entries",
                column: "TechnicianId",
                principalTable: "technicians",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_attendance_entries_work_orders_CurrentWorkOrderId",
                table: "attendance_entries",
                column: "CurrentWorkOrderId",
                principalTable: "work_orders",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_compliance_items_work_orders_OpenWorkOrderId",
                table: "compliance_items",
                column: "OpenWorkOrderId",
                principalTable: "work_orders",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_compliance_records_work_orders_WorkOrderId",
                table: "compliance_records",
                column: "WorkOrderId",
                principalTable: "work_orders",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_consent_records_technicians_TechnicianId",
                table: "consent_records",
                column: "TechnicianId",
                principalTable: "technicians",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_contractor_invoice_lines_work_orders_WorkOrderId",
                table: "contractor_invoice_lines",
                column: "WorkOrderId",
                principalTable: "work_orders",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_direct_purchases_work_orders_WorkOrderId",
                table: "direct_purchases",
                column: "WorkOrderId",
                principalTable: "work_orders",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_email_intake_messages_work_orders_WorkOrderId",
                table: "email_intake_messages",
                column: "WorkOrderId",
                principalTable: "work_orders",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_escalation_fired_work_orders_WorkOrderId",
                table: "escalation_fired",
                column: "WorkOrderId",
                principalTable: "work_orders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_food_safety_records_work_orders_CorrectiveWorkOrderId",
                table: "food_safety_records",
                column: "CorrectiveWorkOrderId",
                principalTable: "work_orders",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_held_parts_work_orders_WorkOrderId",
                table: "held_parts",
                column: "WorkOrderId",
                principalTable: "work_orders",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_incidents_work_orders_CorrectiveWorkOrderId",
                table: "incidents",
                column: "CorrectiveWorkOrderId",
                principalTable: "work_orders",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_incidents_work_orders_WorkOrderId",
                table: "incidents",
                column: "WorkOrderId",
                principalTable: "work_orders",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_leave_requests_technicians_TechnicianId",
                table: "leave_requests",
                column: "TechnicianId",
                principalTable: "technicians",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_loto_locks_work_orders_WorkOrderId",
                table: "loto_locks",
                column: "WorkOrderId",
                principalTable: "work_orders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_loto_locks_work_permits_PermitId",
                table: "loto_locks",
                column: "PermitId",
                principalTable: "work_permits",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_maintenance_window_jobs_work_orders_WorkOrderId",
                table: "maintenance_window_jobs",
                column: "WorkOrderId",
                principalTable: "work_orders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_material_issue_lines_material_issues_MaterialIssueId",
                table: "material_issue_lines",
                column: "MaterialIssueId",
                principalTable: "material_issues",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_material_issues_work_orders_WorkOrderId",
                table: "material_issues",
                column: "WorkOrderId",
                principalTable: "work_orders",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_material_request_lines_material_requests_MaterialRequestId",
                table: "material_request_lines",
                column: "MaterialRequestId",
                principalTable: "material_requests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_material_requests_work_orders_WorkOrderId",
                table: "material_requests",
                column: "WorkOrderId",
                principalTable: "work_orders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_non_conformities_work_orders_WorkOrderId",
                table: "non_conformities",
                column: "WorkOrderId",
                principalTable: "work_orders",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_overhaul_items_work_orders_WorkOrderId",
                table: "overhaul_items",
                column: "WorkOrderId",
                principalTable: "work_orders",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_parts_request_items_parts_requests_PartsRequestId",
                table: "parts_request_items",
                column: "PartsRequestId",
                principalTable: "parts_requests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_parts_requests_work_orders_WorkOrderId",
                table: "parts_requests",
                column: "WorkOrderId",
                principalTable: "work_orders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_parts_return_lines_parts_returns_PartsReturnId",
                table: "parts_return_lines",
                column: "PartsReturnId",
                principalTable: "parts_returns",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_parts_returns_work_orders_WorkOrderId",
                table: "parts_returns",
                column: "WorkOrderId",
                principalTable: "work_orders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_pm_plan_events_work_orders_WorkOrderId",
                table: "pm_plan_events",
                column: "WorkOrderId",
                principalTable: "work_orders",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_pm_reports_work_orders_WorkOrderId",
                table: "pm_reports",
                column: "WorkOrderId",
                principalTable: "work_orders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ppe_issues_technicians_TechnicianId",
                table: "ppe_issues",
                column: "TechnicianId",
                principalTable: "technicians",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_purchase_requisition_lines_work_orders_WorkOrderId",
                table: "purchase_requisition_lines",
                column: "WorkOrderId",
                principalTable: "work_orders",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_roster_entries_technicians_TechnicianId",
                table: "roster_entries",
                column: "TechnicianId",
                principalTable: "technicians",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_sla_records_work_orders_WorkOrderId",
                table: "sla_records",
                column: "WorkOrderId",
                principalTable: "work_orders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_stock_movements_work_orders_WorkOrderId",
                table: "stock_movements",
                column: "WorkOrderId",
                principalTable: "work_orders",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_stock_reservations_work_orders_WorkOrderId",
                table: "stock_reservations",
                column: "WorkOrderId",
                principalTable: "work_orders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_store_transfers_work_orders_WorkOrderId",
                table: "store_transfers",
                column: "WorkOrderId",
                principalTable: "work_orders",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_technician_certificates_technicians_TechnicianId",
                table: "technician_certificates",
                column: "TechnicianId",
                principalTable: "technicians",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_technician_skills_technicians_TechnicianId",
                table: "technician_skills",
                column: "TechnicianId",
                principalTable: "technicians",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_technicians_work_orders_ActiveWorkOrderId",
                table: "technicians",
                column: "ActiveWorkOrderId",
                principalTable: "work_orders",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_tool_assignments_tools_ToolId",
                table: "tool_assignments",
                column: "ToolId",
                principalTable: "tools",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_contracts_users_AccountManagerUserId",
                table: "contracts");

            migrationBuilder.DropForeignKey(
                name: "FK_contracts_users_InternalApproverUserId",
                table: "contracts");

            migrationBuilder.DropForeignKey(
                name: "FK_contracts_users_RenewalOwnerUserId",
                table: "contracts");

            migrationBuilder.DropForeignKey(
                name: "FK_technicians_users_UserId",
                table: "technicians");

            migrationBuilder.DropForeignKey(
                name: "FK_tool_assignments_users_CheckedOutByUserId",
                table: "tool_assignments");

            migrationBuilder.DropForeignKey(
                name: "FK_tool_assignments_users_ReturnedByUserId",
                table: "tool_assignments");

            migrationBuilder.DropForeignKey(
                name: "FK_work_orders_users_ClosedByUserId",
                table: "work_orders");

            migrationBuilder.DropForeignKey(
                name: "FK_asset_categories_tenants_TenantId",
                table: "asset_categories");

            migrationBuilder.DropForeignKey(
                name: "FK_assets_tenants_TenantId",
                table: "assets");

            migrationBuilder.DropForeignKey(
                name: "FK_assignment_groups_tenants_TenantId",
                table: "assignment_groups");

            migrationBuilder.DropForeignKey(
                name: "FK_branches_tenants_TenantId",
                table: "branches");

            migrationBuilder.DropForeignKey(
                name: "FK_certification_definitions_tenants_TenantId",
                table: "certification_definitions");

            migrationBuilder.DropForeignKey(
                name: "FK_clients_tenants_TenantId",
                table: "clients");

            migrationBuilder.DropForeignKey(
                name: "FK_contract_rate_cards_tenants_TenantId",
                table: "contract_rate_cards");

            migrationBuilder.DropForeignKey(
                name: "FK_contractors_tenants_TenantId",
                table: "contractors");

            migrationBuilder.DropForeignKey(
                name: "FK_contracts_tenants_TenantId",
                table: "contracts");

            migrationBuilder.DropForeignKey(
                name: "FK_failure_codes_tenants_TenantId",
                table: "failure_codes");

            migrationBuilder.DropForeignKey(
                name: "FK_job_plans_tenants_TenantId",
                table: "job_plans");

            migrationBuilder.DropForeignKey(
                name: "FK_meters_tenants_TenantId",
                table: "meters");

            migrationBuilder.DropForeignKey(
                name: "FK_org_nodes_tenants_TenantId",
                table: "org_nodes");

            migrationBuilder.DropForeignKey(
                name: "FK_pm_templates_tenants_TenantId",
                table: "pm_templates");

            migrationBuilder.DropForeignKey(
                name: "FK_preventive_maintenance_plans_tenants_TenantId",
                table: "preventive_maintenance_plans");

            migrationBuilder.DropForeignKey(
                name: "FK_sites_tenants_TenantId",
                table: "sites");

            migrationBuilder.DropForeignKey(
                name: "FK_sla_definitions_tenants_TenantId",
                table: "sla_definitions");

            migrationBuilder.DropForeignKey(
                name: "FK_suppliers_tenants_TenantId",
                table: "suppliers");

            migrationBuilder.DropForeignKey(
                name: "FK_systems_tenants_TenantId",
                table: "systems");

            migrationBuilder.DropForeignKey(
                name: "FK_technicians_tenants_TenantId",
                table: "technicians");

            migrationBuilder.DropForeignKey(
                name: "FK_tool_assignments_tenants_TenantId",
                table: "tool_assignments");

            migrationBuilder.DropForeignKey(
                name: "FK_tools_tenants_TenantId",
                table: "tools");

            migrationBuilder.DropForeignKey(
                name: "FK_work_order_templates_tenants_TenantId",
                table: "work_order_templates");

            migrationBuilder.DropForeignKey(
                name: "FK_work_orders_tenants_TenantId",
                table: "work_orders");

            migrationBuilder.DropForeignKey(
                name: "FK_workflow_definitions_tenants_TenantId",
                table: "workflow_definitions");

            migrationBuilder.DropForeignKey(
                name: "FK_workflow_statuses_tenants_TenantId",
                table: "workflow_statuses");

            migrationBuilder.DropForeignKey(
                name: "FK_asset_categories_system_template_packs_TemplatePackId",
                table: "asset_categories");

            migrationBuilder.DropForeignKey(
                name: "FK_pm_templates_system_template_packs_TemplatePackId",
                table: "pm_templates");

            migrationBuilder.DropForeignKey(
                name: "FK_assets_asset_categories_AssetCategoryId",
                table: "assets");

            migrationBuilder.DropForeignKey(
                name: "FK_failure_codes_asset_categories_AssetCategoryId",
                table: "failure_codes");

            migrationBuilder.DropForeignKey(
                name: "FK_job_plans_asset_categories_AssetCategoryId",
                table: "job_plans");

            migrationBuilder.DropForeignKey(
                name: "FK_pm_templates_asset_categories_AssetCategoryId",
                table: "pm_templates");

            migrationBuilder.DropForeignKey(
                name: "FK_meters_assets_AssetId",
                table: "meters");

            migrationBuilder.DropForeignKey(
                name: "FK_preventive_maintenance_plans_assets_AssetId",
                table: "preventive_maintenance_plans");

            migrationBuilder.DropForeignKey(
                name: "FK_work_orders_assets_AssetId",
                table: "work_orders");

            migrationBuilder.DropForeignKey(
                name: "FK_preventive_maintenance_plans_assignment_groups_DefaultAssig~",
                table: "preventive_maintenance_plans");

            migrationBuilder.DropForeignKey(
                name: "FK_work_orders_assignment_groups_AssignmentGroupId",
                table: "work_orders");

            migrationBuilder.DropForeignKey(
                name: "FK_preventive_maintenance_plans_branches_BranchId",
                table: "preventive_maintenance_plans");

            migrationBuilder.DropForeignKey(
                name: "FK_technicians_branches_BranchId",
                table: "technicians");

            migrationBuilder.DropForeignKey(
                name: "FK_tools_branches_HomeBranchId",
                table: "tools");

            migrationBuilder.DropForeignKey(
                name: "FK_work_orders_branches_BranchId",
                table: "work_orders");

            migrationBuilder.DropForeignKey(
                name: "FK_contracts_clients_ClientId",
                table: "contracts");

            migrationBuilder.DropForeignKey(
                name: "FK_sites_clients_ClientId",
                table: "sites");

            migrationBuilder.DropForeignKey(
                name: "FK_systems_clients_ClientId",
                table: "systems");

            migrationBuilder.DropForeignKey(
                name: "FK_work_orders_clients_ClientId",
                table: "work_orders");

            migrationBuilder.DropForeignKey(
                name: "FK_maintenance_agreements_org_nodes_FactoryId",
                table: "maintenance_agreements");

            migrationBuilder.DropForeignKey(
                name: "FK_preventive_maintenance_plans_org_nodes_OrgNodeId",
                table: "preventive_maintenance_plans");

            migrationBuilder.DropForeignKey(
                name: "FK_technicians_org_nodes_HomeOrgNodeId",
                table: "technicians");

            migrationBuilder.DropForeignKey(
                name: "FK_work_orders_org_nodes_OrgNodeId",
                table: "work_orders");

            migrationBuilder.DropForeignKey(
                name: "FK_preventive_maintenance_plans_sites_SiteId",
                table: "preventive_maintenance_plans");

            migrationBuilder.DropForeignKey(
                name: "FK_systems_sites_SiteId",
                table: "systems");

            migrationBuilder.DropForeignKey(
                name: "FK_work_orders_sites_SiteId",
                table: "work_orders");

            migrationBuilder.DropForeignKey(
                name: "FK_work_orders_systems_SystemId",
                table: "work_orders");

            migrationBuilder.DropForeignKey(
                name: "FK_tool_assignments_technicians_TechnicianId",
                table: "tool_assignments");

            migrationBuilder.DropForeignKey(
                name: "FK_work_orders_technicians_AssignedTechnicianId",
                table: "work_orders");

            migrationBuilder.DropForeignKey(
                name: "FK_work_orders_technicians_LeadTechnicianId",
                table: "work_orders");

            migrationBuilder.DropForeignKey(
                name: "FK_tool_assignments_work_orders_WorkOrderId",
                table: "tool_assignments");

            migrationBuilder.DropForeignKey(
                name: "FK_tool_assignments_tools_ToolId",
                table: "tool_assignments");

            migrationBuilder.DropTable(
                name: "access_scopes");

            migrationBuilder.DropTable(
                name: "action_items");

            migrationBuilder.DropTable(
                name: "activity_attendees");

            migrationBuilder.DropTable(
                name: "activity_minutes_addenda");

            migrationBuilder.DropTable(
                name: "activity_reminder_logs");

            migrationBuilder.DropTable(
                name: "approval_levels");

            migrationBuilder.DropTable(
                name: "approval_request_steps");

            migrationBuilder.DropTable(
                name: "asset_custom_field_values");

            migrationBuilder.DropTable(
                name: "asset_document_versions");

            migrationBuilder.DropTable(
                name: "asset_lifecycle_events");

            migrationBuilder.DropTable(
                name: "asset_parts");

            migrationBuilder.DropTable(
                name: "asset_tags");

            migrationBuilder.DropTable(
                name: "assignment_group_members");

            migrationBuilder.DropTable(
                name: "attendance_entries");

            migrationBuilder.DropTable(
                name: "audit_logs");

            migrationBuilder.DropTable(
                name: "branch_material_stocks");

            migrationBuilder.DropTable(
                name: "calendar_feed_tokens");

            migrationBuilder.DropTable(
                name: "check_run_answers");

            migrationBuilder.DropTable(
                name: "check_schedule_assets");

            migrationBuilder.DropTable(
                name: "client_contacts");

            migrationBuilder.DropTable(
                name: "client_preferential_rates");

            migrationBuilder.DropTable(
                name: "compliance_records");

            migrationBuilder.DropTable(
                name: "consent_records");

            migrationBuilder.DropTable(
                name: "contract_asset_categories");

            migrationBuilder.DropTable(
                name: "contract_assets");

            migrationBuilder.DropTable(
                name: "contract_lifecycle_events");

            migrationBuilder.DropTable(
                name: "contract_pm_entitlements");

            migrationBuilder.DropTable(
                name: "contract_rate_rules");

            migrationBuilder.DropTable(
                name: "contract_reminder_rules");

            migrationBuilder.DropTable(
                name: "contract_renewals");

            migrationBuilder.DropTable(
                name: "contract_sites");

            migrationBuilder.DropTable(
                name: "contract_work_order_types");

            migrationBuilder.DropTable(
                name: "contractor_documents");

            migrationBuilder.DropTable(
                name: "contractor_invoice_lines");

            migrationBuilder.DropTable(
                name: "direct_purchases");

            migrationBuilder.DropTable(
                name: "email_delivery_logs");

            migrationBuilder.DropTable(
                name: "email_intake_messages");

            migrationBuilder.DropTable(
                name: "email_intake_settings");

            migrationBuilder.DropTable(
                name: "email_settings");

            migrationBuilder.DropTable(
                name: "escalation_fired");

            migrationBuilder.DropTable(
                name: "eta_notification_settings");

            migrationBuilder.DropTable(
                name: "exchange_rates");

            migrationBuilder.DropTable(
                name: "factory_settings");

            migrationBuilder.DropTable(
                name: "finance_approval_rules");

            migrationBuilder.DropTable(
                name: "food_safety_records");

            migrationBuilder.DropTable(
                name: "glass_register_items");

            migrationBuilder.DropTable(
                name: "group_capabilities");

            migrationBuilder.DropTable(
                name: "held_parts");

            migrationBuilder.DropTable(
                name: "idempotency_records");

            migrationBuilder.DropTable(
                name: "import_batches");

            migrationBuilder.DropTable(
                name: "incidents");

            migrationBuilder.DropTable(
                name: "intake_protocols");

            migrationBuilder.DropTable(
                name: "job_plan_resources");

            migrationBuilder.DropTable(
                name: "job_plan_steps");

            migrationBuilder.DropTable(
                name: "leave_requests");

            migrationBuilder.DropTable(
                name: "leave_types");

            migrationBuilder.DropTable(
                name: "license_add_ons");

            migrationBuilder.DropTable(
                name: "loto_locks");

            migrationBuilder.DropTable(
                name: "maintenance_agreement_assets");

            migrationBuilder.DropTable(
                name: "maintenance_budgets");

            migrationBuilder.DropTable(
                name: "maintenance_window_handback_items");

            migrationBuilder.DropTable(
                name: "maintenance_window_jobs");

            migrationBuilder.DropTable(
                name: "material_issue_lines");

            migrationBuilder.DropTable(
                name: "material_request_lines");

            migrationBuilder.DropTable(
                name: "meter_readings");

            migrationBuilder.DropTable(
                name: "monitoring_settings");

            migrationBuilder.DropTable(
                name: "monitoring_webhook_integrations");

            migrationBuilder.DropTable(
                name: "mrr_snapshots");

            migrationBuilder.DropTable(
                name: "non_conformities");

            migrationBuilder.DropTable(
                name: "notification_settings");

            migrationBuilder.DropTable(
                name: "notifications");

            migrationBuilder.DropTable(
                name: "numbering_settings");

            migrationBuilder.DropTable(
                name: "overhaul_items");

            migrationBuilder.DropTable(
                name: "parts_request_items");

            migrationBuilder.DropTable(
                name: "parts_return_lines");

            migrationBuilder.DropTable(
                name: "password_reset_tokens");

            migrationBuilder.DropTable(
                name: "pending_signups");

            migrationBuilder.DropTable(
                name: "permission_thresholds");

            migrationBuilder.DropTable(
                name: "platform_announcements");

            migrationBuilder.DropTable(
                name: "platform_document_templates");

            migrationBuilder.DropTable(
                name: "platform_expenses");

            migrationBuilder.DropTable(
                name: "platform_invoice_lines");

            migrationBuilder.DropTable(
                name: "platform_payments");

            migrationBuilder.DropTable(
                name: "platform_quotation_lines");

            migrationBuilder.DropTable(
                name: "platform_settings");

            migrationBuilder.DropTable(
                name: "pm_plan_events");

            migrationBuilder.DropTable(
                name: "pm_plan_route_assets");

            migrationBuilder.DropTable(
                name: "pm_reports");

            migrationBuilder.DropTable(
                name: "pm_template_versions");

            migrationBuilder.DropTable(
                name: "ppe_issues");

            migrationBuilder.DropTable(
                name: "purchase_order_receipt_events");

            migrationBuilder.DropTable(
                name: "purchase_requisition_lines");

            migrationBuilder.DropTable(
                name: "push_subscriptions");

            migrationBuilder.DropTable(
                name: "rate_cards");

            migrationBuilder.DropTable(
                name: "role_permissions");

            migrationBuilder.DropTable(
                name: "roster_entries");

            migrationBuilder.DropTable(
                name: "scheduled_reports");

            migrationBuilder.DropTable(
                name: "sla_escalations");

            migrationBuilder.DropTable(
                name: "sla_matrix_cells");

            migrationBuilder.DropTable(
                name: "sla_records");

            migrationBuilder.DropTable(
                name: "spare_unit_events");

            migrationBuilder.DropTable(
                name: "stock_count_lines");

            migrationBuilder.DropTable(
                name: "stock_movements");

            migrationBuilder.DropTable(
                name: "stock_reservations");

            migrationBuilder.DropTable(
                name: "stock_transfers");

            migrationBuilder.DropTable(
                name: "store_count_lines");

            migrationBuilder.DropTable(
                name: "store_stocks");

            migrationBuilder.DropTable(
                name: "store_transfers");

            migrationBuilder.DropTable(
                name: "technician_certificates");

            migrationBuilder.DropTable(
                name: "technician_skills");

            migrationBuilder.DropTable(
                name: "tenant_api_keys");

            migrationBuilder.DropTable(
                name: "tenant_finance_defaults");

            migrationBuilder.DropTable(
                name: "tenant_licenses");

            migrationBuilder.DropTable(
                name: "tenant_module_settings");

            migrationBuilder.DropTable(
                name: "tenant_notification_recipients");

            migrationBuilder.DropTable(
                name: "tenant_notification_settings");

            migrationBuilder.DropTable(
                name: "tenant_operations_policies");

            migrationBuilder.DropTable(
                name: "tenant_security_policies");

            migrationBuilder.DropTable(
                name: "tenant_template_pack_installations");

            migrationBuilder.DropTable(
                name: "tenant_webhook_deliveries");

            migrationBuilder.DropTable(
                name: "time_attendance_settings");

            migrationBuilder.DropTable(
                name: "tool_calibrations");

            migrationBuilder.DropTable(
                name: "training_records");

            migrationBuilder.DropTable(
                name: "user_branch_assignments");

            migrationBuilder.DropTable(
                name: "user_certifications");

            migrationBuilder.DropTable(
                name: "user_dashboard_layouts");

            migrationBuilder.DropTable(
                name: "user_notification_preferences");

            migrationBuilder.DropTable(
                name: "user_org_node_assignments");

            migrationBuilder.DropTable(
                name: "user_permission_groups");

            migrationBuilder.DropTable(
                name: "user_permission_overrides");

            migrationBuilder.DropTable(
                name: "user_permissions");

            migrationBuilder.DropTable(
                name: "user_sessions");

            migrationBuilder.DropTable(
                name: "vendor_catalogue_items");

            migrationBuilder.DropTable(
                name: "waste_disposals");

            migrationBuilder.DropTable(
                name: "whatsapp_messages");

            migrationBuilder.DropTable(
                name: "whatsapp_opt_outs");

            migrationBuilder.DropTable(
                name: "whatsapp_outbox_messages");

            migrationBuilder.DropTable(
                name: "whatsapp_settings");

            migrationBuilder.DropTable(
                name: "whatsapp_templates");

            migrationBuilder.DropTable(
                name: "whatsapp_verification_codes");

            migrationBuilder.DropTable(
                name: "work_order_assignment_history");

            migrationBuilder.DropTable(
                name: "work_order_assignments");

            migrationBuilder.DropTable(
                name: "work_order_checklist_items");

            migrationBuilder.DropTable(
                name: "work_order_events");

            migrationBuilder.DropTable(
                name: "work_order_material_usages");

            migrationBuilder.DropTable(
                name: "work_order_notes");

            migrationBuilder.DropTable(
                name: "work_order_photo_evidence");

            migrationBuilder.DropTable(
                name: "work_order_signatures");

            migrationBuilder.DropTable(
                name: "work_order_technician_assignments");

            migrationBuilder.DropTable(
                name: "work_order_time_logs");

            migrationBuilder.DropTable(
                name: "work_order_visits");

            migrationBuilder.DropTable(
                name: "work_permit_checks");

            migrationBuilder.DropTable(
                name: "client_portal_users");

            migrationBuilder.DropTable(
                name: "activities");

            migrationBuilder.DropTable(
                name: "approval_matrices");

            migrationBuilder.DropTable(
                name: "approval_requests");

            migrationBuilder.DropTable(
                name: "asset_category_fields");

            migrationBuilder.DropTable(
                name: "asset_documents");

            migrationBuilder.DropTable(
                name: "check_runs");

            migrationBuilder.DropTable(
                name: "compliance_items");

            migrationBuilder.DropTable(
                name: "contractor_invoices");

            migrationBuilder.DropTable(
                name: "email_outbox_messages");

            migrationBuilder.DropTable(
                name: "escalation_rules");

            migrationBuilder.DropTable(
                name: "isolation_points");

            migrationBuilder.DropTable(
                name: "cost_centres");

            migrationBuilder.DropTable(
                name: "maintenance_windows");

            migrationBuilder.DropTable(
                name: "material_issues");

            migrationBuilder.DropTable(
                name: "audit_events");

            migrationBuilder.DropTable(
                name: "standard_requirements");

            migrationBuilder.DropTable(
                name: "overhaul_plans");

            migrationBuilder.DropTable(
                name: "parts_requests");

            migrationBuilder.DropTable(
                name: "parts_returns");

            migrationBuilder.DropTable(
                name: "platform_invoices");

            migrationBuilder.DropTable(
                name: "purchase_order_lines");

            migrationBuilder.DropTable(
                name: "purchase_requisitions");

            migrationBuilder.DropTable(
                name: "sla_rules");

            migrationBuilder.DropTable(
                name: "spare_units");

            migrationBuilder.DropTable(
                name: "stock_counts");

            migrationBuilder.DropTable(
                name: "material_requests");

            migrationBuilder.DropTable(
                name: "store_counts");

            migrationBuilder.DropTable(
                name: "license_plans");

            migrationBuilder.DropTable(
                name: "tenant_webhooks");

            migrationBuilder.DropTable(
                name: "permission_groups");

            migrationBuilder.DropTable(
                name: "whatsapp_conversations");

            migrationBuilder.DropTable(
                name: "pm_template_questions");

            migrationBuilder.DropTable(
                name: "work_permits");

            migrationBuilder.DropTable(
                name: "check_schedules");

            migrationBuilder.DropTable(
                name: "compliance_standards");

            migrationBuilder.DropTable(
                name: "platform_quotations");

            migrationBuilder.DropTable(
                name: "purchase_orders");

            migrationBuilder.DropTable(
                name: "material_items");

            migrationBuilder.DropTable(
                name: "store_bins");

            migrationBuilder.DropTable(
                name: "pm_template_sections");

            migrationBuilder.DropTable(
                name: "attachments");

            migrationBuilder.DropTable(
                name: "stores");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "tenants");

            migrationBuilder.DropTable(
                name: "system_template_packs");

            migrationBuilder.DropTable(
                name: "asset_categories");

            migrationBuilder.DropTable(
                name: "assets");

            migrationBuilder.DropTable(
                name: "assignment_groups");

            migrationBuilder.DropTable(
                name: "branches");

            migrationBuilder.DropTable(
                name: "clients");

            migrationBuilder.DropTable(
                name: "sla_definitions");

            migrationBuilder.DropTable(
                name: "org_nodes");

            migrationBuilder.DropTable(
                name: "sites");

            migrationBuilder.DropTable(
                name: "systems");

            migrationBuilder.DropTable(
                name: "technicians");

            migrationBuilder.DropTable(
                name: "work_orders");

            migrationBuilder.DropTable(
                name: "contract_rate_cards");

            migrationBuilder.DropTable(
                name: "failure_codes");

            migrationBuilder.DropTable(
                name: "job_plans");

            migrationBuilder.DropTable(
                name: "maintenance_agreements");

            migrationBuilder.DropTable(
                name: "preventive_maintenance_plans");

            migrationBuilder.DropTable(
                name: "work_order_templates");

            migrationBuilder.DropTable(
                name: "workflow_statuses");

            migrationBuilder.DropTable(
                name: "contracts");

            migrationBuilder.DropTable(
                name: "certification_definitions");

            migrationBuilder.DropTable(
                name: "contractors");

            migrationBuilder.DropTable(
                name: "meters");

            migrationBuilder.DropTable(
                name: "pm_templates");

            migrationBuilder.DropTable(
                name: "workflow_definitions");

            migrationBuilder.DropTable(
                name: "suppliers");

            migrationBuilder.DropTable(
                name: "tools");

            migrationBuilder.DropTable(
                name: "tool_assignments");
        }
    }
}
