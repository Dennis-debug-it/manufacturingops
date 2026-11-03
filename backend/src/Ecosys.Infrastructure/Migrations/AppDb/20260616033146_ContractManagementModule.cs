using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class ContractManagementModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AppliedRateSource",
                table: "work_orders",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AppliedResolutionHours",
                table: "work_orders",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AppliedResponseHours",
                table: "work_orders",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AppliedSlaSource",
                table: "work_orders",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BillingModel",
                table: "work_orders",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ContractId",
                table: "work_orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContractSnapshotJson",
                table: "work_orders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CoverageStatus",
                table: "work_orders",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "EstimatedCharge",
                table: "work_orders",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "EstimatedLabourHours",
                table: "work_orders",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "EstimatedMaterialCost",
                table: "work_orders",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FinalCharge",
                table: "work_orders",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PricingNotes",
                table: "work_orders",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PricingSnapshotJson",
                table: "work_orders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PricingStatus",
                table: "work_orders",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RateCardId",
                table: "work_orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SlaSnapshotJson",
                table: "work_orders",
                type: "text",
                nullable: true);

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
                    ResponseTimeHours = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    ResolutionTimeHours = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    WorkingHoursPolicy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PauseSlaOnHold = table.Column<bool>(type: "boolean", nullable: false),
                    EscalationEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    PriorityLevel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    GracePeriodAllowsSla = table.Column<bool>(type: "boolean", nullable: false),
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

            migrationBuilder.CreateIndex(
                name: "IX_work_orders_ContractId",
                table: "work_orders",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_work_orders_RateCardId",
                table: "work_orders",
                column: "RateCardId");

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

            migrationBuilder.AddForeignKey(
                name: "FK_work_orders_contract_rate_cards_RateCardId",
                table: "work_orders",
                column: "RateCardId",
                principalTable: "contract_rate_cards",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_work_orders_contracts_ContractId",
                table: "work_orders",
                column: "ContractId",
                principalTable: "contracts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_work_orders_contract_rate_cards_RateCardId",
                table: "work_orders");

            migrationBuilder.DropForeignKey(
                name: "FK_work_orders_contracts_ContractId",
                table: "work_orders");

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
                name: "contract_rate_cards");

            migrationBuilder.DropTable(
                name: "contracts");

            migrationBuilder.DropIndex(
                name: "IX_work_orders_ContractId",
                table: "work_orders");

            migrationBuilder.DropIndex(
                name: "IX_work_orders_RateCardId",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "AppliedRateSource",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "AppliedResolutionHours",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "AppliedResponseHours",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "AppliedSlaSource",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "BillingModel",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "ContractId",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "ContractSnapshotJson",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "CoverageStatus",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "EstimatedCharge",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "EstimatedLabourHours",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "EstimatedMaterialCost",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "FinalCharge",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "PricingNotes",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "PricingSnapshotJson",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "PricingStatus",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "RateCardId",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "SlaSnapshotJson",
                table: "work_orders");
        }
    }
}
