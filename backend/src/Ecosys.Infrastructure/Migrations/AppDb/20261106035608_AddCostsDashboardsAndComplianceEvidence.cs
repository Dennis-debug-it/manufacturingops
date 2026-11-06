using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class AddCostsDashboardsAndComplianceEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OrgNodeId",
                table: "scheduled_reports",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DataResidency",
                table: "factory_settings",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PeakSeasonMonths",
                table: "factory_settings",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "3,4,5,10,11,12");

            migrationBuilder.AddColumn<int>(
                name: "PersonalDataRetentionYears",
                table: "factory_settings",
                type: "integer",
                nullable: false,
                defaultValue: 7);

            migrationBuilder.AddColumn<decimal>(
                name: "ProductionUnitValue",
                table: "factory_settings",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "VatRatePercent",
                table: "factory_settings",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 16m);

            migrationBuilder.AddColumn<decimal>(
                name: "VatAmount",
                table: "contractor_invoices",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

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
                        name: "FK_consent_records_technicians_TechnicianId",
                        column: x => x.TechnicianId,
                        principalTable: "technicians",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_consent_records_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
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
                    table.ForeignKey(
                        name: "FK_food_safety_records_work_orders_CorrectiveWorkOrderId",
                        column: x => x.CorrectiveWorkOrderId,
                        principalTable: "work_orders",
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
                    table.ForeignKey(
                        name: "FK_non_conformities_work_orders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "work_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_scheduled_reports_OrgNodeId",
                table: "scheduled_reports",
                column: "OrgNodeId");

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
                name: "IX_standard_requirements_StandardId_Sequence",
                table: "standard_requirements",
                columns: new[] { "StandardId", "Sequence" });

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

            migrationBuilder.AddForeignKey(
                name: "FK_scheduled_reports_org_nodes_OrgNodeId",
                table: "scheduled_reports",
                column: "OrgNodeId",
                principalTable: "org_nodes",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_scheduled_reports_org_nodes_OrgNodeId",
                table: "scheduled_reports");

            migrationBuilder.DropTable(
                name: "consent_records");

            migrationBuilder.DropTable(
                name: "food_safety_records");

            migrationBuilder.DropTable(
                name: "glass_register_items");

            migrationBuilder.DropTable(
                name: "maintenance_budgets");

            migrationBuilder.DropTable(
                name: "non_conformities");

            migrationBuilder.DropTable(
                name: "waste_disposals");

            migrationBuilder.DropTable(
                name: "cost_centres");

            migrationBuilder.DropTable(
                name: "audit_events");

            migrationBuilder.DropTable(
                name: "standard_requirements");

            migrationBuilder.DropTable(
                name: "compliance_standards");

            migrationBuilder.DropIndex(
                name: "IX_scheduled_reports_OrgNodeId",
                table: "scheduled_reports");

            migrationBuilder.DropColumn(
                name: "OrgNodeId",
                table: "scheduled_reports");

            migrationBuilder.DropColumn(
                name: "DataResidency",
                table: "factory_settings");

            migrationBuilder.DropColumn(
                name: "PeakSeasonMonths",
                table: "factory_settings");

            migrationBuilder.DropColumn(
                name: "PersonalDataRetentionYears",
                table: "factory_settings");

            migrationBuilder.DropColumn(
                name: "ProductionUnitValue",
                table: "factory_settings");

            migrationBuilder.DropColumn(
                name: "VatRatePercent",
                table: "factory_settings");

            migrationBuilder.DropColumn(
                name: "VatAmount",
                table: "contractor_invoices");
        }
    }
}
