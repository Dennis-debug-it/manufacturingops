using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class AddProjectsRelease2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                name: "AdvanceInvoiceId",
                table: "projects",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AdvancePaymentAmount",
                table: "projects",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AdvanceRecoveryPercent",
                table: "projects",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DefectsLiabilityMonths",
                table: "projects",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "MaintenanceContractId",
                table: "projects",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RetentionCapAmount",
                table: "projects",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RetentionPercent",
                table: "projects",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RetentionReleaseAtCompletionPercent",
                table: "projects",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 50m);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceQuotationId",
                table: "projects",
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

            migrationBuilder.CreateTable(
                name: "project_budget_lines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Category = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
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
                name: "project_cost_entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Category = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    IncurredOn = table.Column<DateOnly>(type: "date", nullable: false),
                    RecordedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
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
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntryDate = table.Column<DateOnly>(type: "date", nullable: false),
                    AuthorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkCompleted = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    IssuesAndDelays = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    InstructionsReceived = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Weather = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
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
                name: "project_reminder_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    NotificationKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ForDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_reminder_logs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "project_retention_releases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Stage = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ReleasedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReleasedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
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
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    ValuationNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PeriodEnd = table.Column<DateOnly>(type: "date", nullable: false),
                    WorkDoneToDate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    MaterialsOnSite = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    GrossValueToDate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    RetentionToDate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    AdvanceRecoveredToDate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PreviouslyCertified = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    AmountDue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CertifiedByConsultantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CertificateReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CertifiedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    CertifiedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
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
                name: "project_workers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    TechnicianId = table.Column<Guid>(type: "uuid", nullable: true),
                    FullName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Trade = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    EngagementType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
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
                name: "withholding_certificates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    TaxType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RatePercent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CertificateNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CertificateDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    RecordedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_withholding_certificates", x => x.Id);
                    table.CheckConstraint("CK_withholding_certificates_amount", "\"Amount\" > 0");
                    table.CheckConstraint("CK_withholding_certificates_tax_type", "\"TaxType\" IN ('VAT', 'IncomeTax')");
                    table.ForeignKey(
                        name: "FK_withholding_certificates_clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_withholding_certificates_tenant_invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "tenant_invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_withholding_certificates_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "project_muster_entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectWorkerId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DayFraction = table.Column<decimal>(type: "numeric(3,2)", precision: 3, scale: 2, nullable: false),
                    MarkedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
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
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectWorkerId = table.Column<Guid>(type: "uuid", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    DailyRate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    SetByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
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
                name: "IX_project_budget_lines_ProjectId",
                table: "project_budget_lines",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_project_budget_lines_TenantId_ProjectId",
                table: "project_budget_lines",
                columns: new[] { "TenantId", "ProjectId" });

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
                name: "IX_withholding_certificates_ClientId",
                table: "withholding_certificates",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_withholding_certificates_InvoiceId",
                table: "withholding_certificates",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_withholding_certificates_TenantId_ClientId_CertificateNumber",
                table: "withholding_certificates",
                columns: new[] { "TenantId", "ClientId", "CertificateNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_withholding_certificates_TenantId_InvoiceId",
                table: "withholding_certificates",
                columns: new[] { "TenantId", "InvoiceId" });

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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

            migrationBuilder.DropTable(
                name: "project_budget_lines");

            migrationBuilder.DropTable(
                name: "project_cost_entries");

            migrationBuilder.DropTable(
                name: "project_diary_entries");

            migrationBuilder.DropTable(
                name: "project_muster_entries");

            migrationBuilder.DropTable(
                name: "project_reminder_logs");

            migrationBuilder.DropTable(
                name: "project_retention_releases");

            migrationBuilder.DropTable(
                name: "project_valuations");

            migrationBuilder.DropTable(
                name: "project_worker_rates");

            migrationBuilder.DropTable(
                name: "withholding_certificates");

            migrationBuilder.DropTable(
                name: "project_workers");

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
                name: "AdvanceInvoiceId",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "AdvancePaymentAmount",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "AdvanceRecoveryPercent",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "DefectsLiabilityMonths",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "MaintenanceContractId",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "RetentionCapAmount",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "RetentionPercent",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "RetentionReleaseAtCompletionPercent",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "SourceQuotationId",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                table: "expenses");

            migrationBuilder.DropColumn(
                name: "SourceProjectId",
                table: "contracts");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                table: "attendance_entries");
        }
    }
}
