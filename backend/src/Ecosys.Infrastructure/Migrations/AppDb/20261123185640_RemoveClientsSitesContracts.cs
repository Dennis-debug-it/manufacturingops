using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class RemoveClientsSitesContracts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_activity_attendees_client_portal_users_ClientContactId",
                table: "activity_attendees");

            migrationBuilder.DropForeignKey(
                name: "FK_assets_clients_ClientId",
                table: "assets");

            migrationBuilder.DropForeignKey(
                name: "FK_assets_sites_SiteId",
                table: "assets");

            migrationBuilder.DropForeignKey(
                name: "FK_assets_systems_SystemId",
                table: "assets");

            migrationBuilder.DropForeignKey(
                name: "FK_material_issues_clients_ClientId",
                table: "material_issues");

            migrationBuilder.DropForeignKey(
                name: "FK_material_issues_sites_SiteId",
                table: "material_issues");

            migrationBuilder.DropForeignKey(
                name: "FK_material_issues_systems_SystemId",
                table: "material_issues");

            migrationBuilder.DropForeignKey(
                name: "FK_monitoring_webhook_integrations_clients_DefaultClientId",
                table: "monitoring_webhook_integrations");

            migrationBuilder.DropForeignKey(
                name: "FK_preventive_maintenance_plans_sites_SiteId",
                table: "preventive_maintenance_plans");

            migrationBuilder.DropForeignKey(
                name: "FK_work_orders_clients_ClientId",
                table: "work_orders");

            migrationBuilder.DropForeignKey(
                name: "FK_work_orders_contract_rate_cards_RateCardId",
                table: "work_orders");

            migrationBuilder.DropForeignKey(
                name: "FK_work_orders_contracts_ContractId",
                table: "work_orders");

            migrationBuilder.DropForeignKey(
                name: "FK_work_orders_sites_SiteId",
                table: "work_orders");

            migrationBuilder.DropForeignKey(
                name: "FK_work_orders_systems_SystemId",
                table: "work_orders");

            migrationBuilder.DropTable(
                name: "client_contacts");

            migrationBuilder.DropTable(
                name: "client_portal_users");

            migrationBuilder.DropTable(
                name: "client_preferential_rates");

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
                name: "email_intake_messages");

            migrationBuilder.DropTable(
                name: "email_intake_settings");

            migrationBuilder.DropTable(
                name: "intake_protocols");

            migrationBuilder.DropTable(
                name: "rate_cards");

            migrationBuilder.DropTable(
                name: "systems");

            migrationBuilder.DropTable(
                name: "work_order_visits");

            migrationBuilder.DropTable(
                name: "contract_rate_cards");

            migrationBuilder.DropTable(
                name: "sites");

            migrationBuilder.DropTable(
                name: "contracts");

            migrationBuilder.DropTable(
                name: "clients");

            migrationBuilder.DropIndex(
                name: "IX_work_orders_ClientId",
                table: "work_orders");

            migrationBuilder.DropIndex(
                name: "IX_work_orders_ContractId",
                table: "work_orders");

            migrationBuilder.DropIndex(
                name: "IX_work_orders_RateCardId",
                table: "work_orders");

            migrationBuilder.DropIndex(
                name: "IX_work_orders_SiteId",
                table: "work_orders");

            migrationBuilder.DropIndex(
                name: "IX_work_orders_SystemId",
                table: "work_orders");

            migrationBuilder.DropIndex(
                name: "IX_whatsapp_verification_codes_TenantId_ClientPortalUserId_Cre~",
                table: "whatsapp_verification_codes");

            migrationBuilder.DropIndex(
                name: "IX_preventive_maintenance_plans_SiteId",
                table: "preventive_maintenance_plans");

            migrationBuilder.DropIndex(
                name: "IX_monitoring_webhook_integrations_DefaultClientId",
                table: "monitoring_webhook_integrations");

            migrationBuilder.DropIndex(
                name: "IX_material_issues_ClientId",
                table: "material_issues");

            migrationBuilder.DropIndex(
                name: "IX_material_issues_SiteId",
                table: "material_issues");

            migrationBuilder.DropIndex(
                name: "IX_material_issues_SystemId",
                table: "material_issues");

            migrationBuilder.DropIndex(
                name: "IX_assets_ClientId",
                table: "assets");

            migrationBuilder.DropIndex(
                name: "IX_assets_SiteId",
                table: "assets");

            migrationBuilder.DropIndex(
                name: "IX_assets_SystemId",
                table: "assets");

            migrationBuilder.DropIndex(
                name: "IX_activity_attendees_ClientContactId",
                table: "activity_attendees");

            migrationBuilder.DropCheckConstraint(
                name: "CK_activity_attendees_one_kind",
                table: "activity_attendees");

            migrationBuilder.DropColumn(
                name: "AcknowledgedByName",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "AcknowledgementComments",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "AcknowledgementDate",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "AppliedRateSource",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "ArrivalAt",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "BillingModel",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "ClientContactEmail",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "ClientId",
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
                name: "DepartureAt",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "EstimatedCharge",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "EstimatedMaterialCost",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "FinalCharge",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "NotifyClient",
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
                name: "SiteId",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "SystemId",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "ArrivalAt",
                table: "work_order_technician_assignments");

            migrationBuilder.DropColumn(
                name: "DepartureAt",
                table: "work_order_technician_assignments");

            migrationBuilder.DropColumn(
                name: "LastArrivalAt",
                table: "work_order_technician_assignments");

            migrationBuilder.DropColumn(
                name: "IsClientVisible",
                table: "work_order_notes");

            migrationBuilder.DropColumn(
                name: "ClientPortalUserId",
                table: "whatsapp_verification_codes");

            migrationBuilder.DropColumn(
                name: "RecipientPortalUserId",
                table: "whatsapp_outbox_messages");

            migrationBuilder.DropColumn(
                name: "ClientId",
                table: "whatsapp_conversations");

            migrationBuilder.DropColumn(
                name: "AllowRemoteCompletion",
                table: "tenant_operations_policies");

            migrationBuilder.DropColumn(
                name: "AutoSendServiceReportOnApproval",
                table: "tenant_operations_policies");

            migrationBuilder.DropColumn(
                name: "RequireClientSignature",
                table: "tenant_operations_policies");

            migrationBuilder.DropColumn(
                name: "EmailIngestionOverride",
                table: "tenant_licenses");

            migrationBuilder.DropColumn(
                name: "ClientId",
                table: "scheduled_reports");

            migrationBuilder.DropColumn(
                name: "ContinueWithoutContract",
                table: "preventive_maintenance_plans");

            migrationBuilder.DropColumn(
                name: "HeldAt",
                table: "preventive_maintenance_plans");

            migrationBuilder.DropColumn(
                name: "HoldReason",
                table: "preventive_maintenance_plans");

            migrationBuilder.DropColumn(
                name: "LapsedContractId",
                table: "preventive_maintenance_plans");

            migrationBuilder.DropColumn(
                name: "SiteId",
                table: "preventive_maintenance_plans");

            migrationBuilder.DropColumn(
                name: "DefaultClientId",
                table: "monitoring_webhook_integrations");

            migrationBuilder.DropColumn(
                name: "ClientId",
                table: "material_issues");

            migrationBuilder.DropColumn(
                name: "SiteId",
                table: "material_issues");

            migrationBuilder.DropColumn(
                name: "SystemId",
                table: "material_issues");

            migrationBuilder.DropColumn(
                name: "SharedWithClient",
                table: "attachments");

            migrationBuilder.DropColumn(
                name: "ClientId",
                table: "assets");

            migrationBuilder.DropColumn(
                name: "SiteId",
                table: "assets");

            migrationBuilder.DropColumn(
                name: "SystemId",
                table: "assets");

            migrationBuilder.DropColumn(
                name: "ClientContactId",
                table: "activity_attendees");

            migrationBuilder.AddCheckConstraint(
                name: "CK_activity_attendees_one_kind",
                table: "activity_attendees",
                sql: "(CASE WHEN \"UserId\" IS NULL THEN 0 ELSE 1 END) + (CASE WHEN \"ExternalEmail\" IS NULL AND \"ExternalName\" IS NULL THEN 0 ELSE 1 END) = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_activity_attendees_one_kind",
                table: "activity_attendees");

            migrationBuilder.AddColumn<string>(
                name: "AcknowledgedByName",
                table: "work_orders",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AcknowledgementComments",
                table: "work_orders",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AcknowledgementDate",
                table: "work_orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AppliedRateSource",
                table: "work_orders",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ArrivalAt",
                table: "work_orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BillingModel",
                table: "work_orders",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClientContactEmail",
                table: "work_orders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ClientId",
                table: "work_orders",
                type: "uuid",
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

            migrationBuilder.AddColumn<DateTime>(
                name: "DepartureAt",
                table: "work_orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "EstimatedCharge",
                table: "work_orders",
                type: "numeric(18,2)",
                precision: 18,
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

            migrationBuilder.AddColumn<bool>(
                name: "NotifyClient",
                table: "work_orders",
                type: "boolean",
                nullable: false,
                defaultValue: false);

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

            migrationBuilder.AddColumn<Guid>(
                name: "SiteId",
                table: "work_orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SystemId",
                table: "work_orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ArrivalAt",
                table: "work_order_technician_assignments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DepartureAt",
                table: "work_order_technician_assignments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastArrivalAt",
                table: "work_order_technician_assignments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsClientVisible",
                table: "work_order_notes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "ClientPortalUserId",
                table: "whatsapp_verification_codes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RecipientPortalUserId",
                table: "whatsapp_outbox_messages",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ClientId",
                table: "whatsapp_conversations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AllowRemoteCompletion",
                table: "tenant_operations_policies",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AutoSendServiceReportOnApproval",
                table: "tenant_operations_policies",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "RequireClientSignature",
                table: "tenant_operations_policies",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "EmailIngestionOverride",
                table: "tenant_licenses",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClientId",
                table: "scheduled_reports",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ContinueWithoutContract",
                table: "preventive_maintenance_plans",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "HeldAt",
                table: "preventive_maintenance_plans",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HoldReason",
                table: "preventive_maintenance_plans",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LapsedContractId",
                table: "preventive_maintenance_plans",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SiteId",
                table: "preventive_maintenance_plans",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DefaultClientId",
                table: "monitoring_webhook_integrations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ClientId",
                table: "material_issues",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SiteId",
                table: "material_issues",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SystemId",
                table: "material_issues",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SharedWithClient",
                table: "attachments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "ClientId",
                table: "assets",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SiteId",
                table: "assets",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SystemId",
                table: "assets",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ClientContactId",
                table: "activity_attendees",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "clients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SlaDefinitionId = table.Column<Guid>(type: "uuid", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActiveDurationUnit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ActiveDurationValue = table.Column<int>(type: "integer", nullable: true),
                    ActiveEndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ActiveStartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClientName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ClientType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ContactPerson = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ContactPhone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsProspect = table.Column<bool>(type: "boolean", nullable: false),
                    Location = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    Notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    SlaPlan = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
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
                name: "email_intake_messages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    AutoReplySent = table.Column<bool>(type: "boolean", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Detail = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    FromAddress = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    FromName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    MessageId = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Outcome = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Subject = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
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
                    table.ForeignKey(
                        name: "FK_email_intake_messages_work_orders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "work_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "intake_protocols",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActionsJson = table.Column<string>(type: "character varying(32000)", maxLength: 32000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CriteriaJson = table.Column<string>(type: "character varying(32000)", maxLength: 32000, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    LastError = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    LastTriggerStatus = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    LastTriggeredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Name = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    SourceConfigJson = table.Column<string>(type: "character varying(32000)", maxLength: 32000, nullable: false),
                    SourceType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
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
                name: "rate_cards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Category = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    SkillLevel = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    TaxRate = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: true),
                    TimeType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Unit = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    UnitRate = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
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
                name: "work_order_visits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TechnicianId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ArrivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DepartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    OnSiteMinutes = table.Column<int>(type: "integer", nullable: false),
                    TravelMinutes = table.Column<int>(type: "integer", nullable: false),
                    TravelStartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                name: "client_contacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    FullName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    JobTitle = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Role = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    FullName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    InvitedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ReceivesWhatsApp = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    WhatsAppConsentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    WhatsAppConsentMethod = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    WhatsAppConsentRecordedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    WhatsAppNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    WhatsAppVerifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
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
                    ApprovedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Currency = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    DefaultRate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ItemType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    PreferentialRate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    RateName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    RateType = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    RequiresApproval = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Unit = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
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
                    AccountManagerUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    InternalApproverUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    RenewalOwnerUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    RenewedFromContractId = table.Column<Guid>(type: "uuid", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActivatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AllowPreferentialRatesDuringGracePeriod = table.Column<bool>(type: "boolean", nullable: false),
                    AllowSlaDuringGracePeriod = table.Column<bool>(type: "boolean", nullable: false),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AutoExpireEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    BillingModel = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    CancellationReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ContractNumber = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ContractType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ContractValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Currency = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    DurationMonths = table.Column<int>(type: "integer", nullable: true),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EscalationEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ExcludedServices = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ExpiryCheckedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    GracePeriodAllowsSla = table.Column<bool>(type: "boolean", nullable: false),
                    GracePeriodDays = table.Column<int>(type: "integer", nullable: false),
                    GracePeriodPolicy = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    IncludedServices = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    IsArchived = table.Column<bool>(type: "boolean", nullable: false),
                    LastReminderSentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NextReminderAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    PauseSlaOnHold = table.Column<bool>(type: "boolean", nullable: false),
                    PriorityLevel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RenewalBehavior = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    RenewalNoticeDays = table.Column<int>(type: "integer", nullable: false),
                    RenewalRisk = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    RenewalStartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RenewedToContractId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResolutionTimeHours = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    ResponseTimeHours = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    SlaMatrixJson = table.Column<string>(type: "text", nullable: true),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    SubmittedForApprovalAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SuspensionReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    TerminationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TerminationReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    WorkingHoursPolicy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
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
                    DefaultAssignmentGroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    DefaultBranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    DefaultClientId = table.Column<Guid>(type: "uuid", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    AllowedSenderDomains = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    AutoReplyEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreateWorkOrderFromUnknownSender = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DefaultPriority = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    EncryptedPassword = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Host = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ImapLastUid = table.Column<long>(type: "bigint", nullable: true),
                    ImapUidValidity = table.Column<long>(type: "bigint", nullable: true),
                    IntakeEmailAddress = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsConnectionHealthy = table.Column<bool>(type: "boolean", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LastCheckedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    MailboxProvider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Port = table.Column<int>(type: "integer", nullable: false),
                    SubjectParsingRules = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UseSsl = table.Column<bool>(type: "boolean", nullable: false),
                    Username = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
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
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccessNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    AlternateContact = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AreaEstate = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ContactEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ContactPerson = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ContactPhone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Country = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    County = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Latitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    Longitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    OperatingHours = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Region = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SiteCode = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    SiteName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SiteType = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    SpecialInstructions = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    StreetAddress = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    TownCity = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
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
                name: "contract_asset_categories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetCategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    ContractId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    AssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContractId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    ContractId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EventType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    NewStatus = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    Note = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    OldStatus = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
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
                    AssetId = table.Column<Guid>(type: "uuid", nullable: true),
                    ContractId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetCategory = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IncludedVisits = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    PeriodType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    RemainingVisits = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UsedVisits = table.Column<int>(type: "integer", nullable: false),
                    VisitsPerPeriod = table.Column<int>(type: "integer", nullable: false),
                    WorkOrderType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false)
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
                    ContractId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Currency = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    EffectiveFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EffectiveTo = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
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
                    ContractId = table.Column<Guid>(type: "uuid", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DaysBeforeExpiry = table.Column<int>(type: "integer", nullable: false),
                    EmailEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    EscalationAfterDays = table.Column<int>(type: "integer", nullable: true),
                    EscalationEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    InAppEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    NotifyAccountManager = table.Column<bool>(type: "boolean", nullable: false),
                    NotifyFinance = table.Column<bool>(type: "boolean", nullable: false),
                    NotifyRenewalOwner = table.Column<bool>(type: "boolean", nullable: false),
                    NotifySupervisor = table.Column<bool>(type: "boolean", nullable: false),
                    NotifyTenantAdmin = table.Column<bool>(type: "boolean", nullable: false),
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
                    OriginalContractId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    RenewedContractId = table.Column<Guid>(type: "uuid", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    RenewalCompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RenewalStartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RenewalStatus = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
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
                    ContractId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    WorkOrderTypeId = table.Column<Guid>(type: "uuid", nullable: true),
                    WorkOrderTypeName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true)
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
                    ContractId = table.Column<Guid>(type: "uuid", nullable: false),
                    SiteId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    SiteId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    InstallationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Manufacturer = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Model = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    SerialNumber = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SystemName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SystemType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
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
                name: "contract_rate_rules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetCategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    RateCardId = table.Column<Guid>(type: "uuid", nullable: false),
                    SiteId = table.Column<Guid>(type: "uuid", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetCategoryName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    BillingModel = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    CalloutFee = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DiscountPercent = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    EmergencyMultiplier = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    FixedVisitFee = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    IncludedLabourHours = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    LabourHourlyRate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    MaterialMarkupPercent = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    MinimumBillableHours = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    OvertimeHourlyRate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Priority = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    PublicHolidayHourlyRate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    TravelCharge = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    VatApplicable = table.Column<bool>(type: "boolean", nullable: false),
                    WeekendHourlyRate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    WorkOrderTypeId = table.Column<Guid>(type: "uuid", nullable: true),
                    WorkOrderTypeName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true)
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
                name: "IX_work_orders_ClientId",
                table: "work_orders",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_work_orders_ContractId",
                table: "work_orders",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_work_orders_RateCardId",
                table: "work_orders",
                column: "RateCardId");

            migrationBuilder.CreateIndex(
                name: "IX_work_orders_SiteId",
                table: "work_orders",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "IX_work_orders_SystemId",
                table: "work_orders",
                column: "SystemId");

            migrationBuilder.CreateIndex(
                name: "IX_whatsapp_verification_codes_TenantId_ClientPortalUserId_Cre~",
                table: "whatsapp_verification_codes",
                columns: new[] { "TenantId", "ClientPortalUserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_preventive_maintenance_plans_SiteId",
                table: "preventive_maintenance_plans",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "IX_monitoring_webhook_integrations_DefaultClientId",
                table: "monitoring_webhook_integrations",
                column: "DefaultClientId");

            migrationBuilder.CreateIndex(
                name: "IX_material_issues_ClientId",
                table: "material_issues",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_material_issues_SiteId",
                table: "material_issues",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "IX_material_issues_SystemId",
                table: "material_issues",
                column: "SystemId");

            migrationBuilder.CreateIndex(
                name: "IX_assets_ClientId",
                table: "assets",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_assets_SiteId",
                table: "assets",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "IX_assets_SystemId",
                table: "assets",
                column: "SystemId");

            migrationBuilder.CreateIndex(
                name: "IX_activity_attendees_ClientContactId",
                table: "activity_attendees",
                column: "ClientContactId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_activity_attendees_one_kind",
                table: "activity_attendees",
                sql: "(CASE WHEN \"UserId\" IS NULL THEN 0 ELSE 1 END) + (CASE WHEN \"ClientContactId\" IS NULL THEN 0 ELSE 1 END) + (CASE WHEN \"ExternalEmail\" IS NULL AND \"ExternalName\" IS NULL THEN 0 ELSE 1 END) = 1");

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
                name: "IX_intake_protocols_TenantId_Name",
                table: "intake_protocols",
                columns: new[] { "TenantId", "Name" },
                unique: true);

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
                name: "IX_sites_ClientId",
                table: "sites",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_sites_TenantId_SiteCode",
                table: "sites",
                columns: new[] { "TenantId", "SiteCode" },
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

            migrationBuilder.AddForeignKey(
                name: "FK_activity_attendees_client_portal_users_ClientContactId",
                table: "activity_attendees",
                column: "ClientContactId",
                principalTable: "client_portal_users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_assets_clients_ClientId",
                table: "assets",
                column: "ClientId",
                principalTable: "clients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_assets_sites_SiteId",
                table: "assets",
                column: "SiteId",
                principalTable: "sites",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_assets_systems_SystemId",
                table: "assets",
                column: "SystemId",
                principalTable: "systems",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_material_issues_clients_ClientId",
                table: "material_issues",
                column: "ClientId",
                principalTable: "clients",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_material_issues_sites_SiteId",
                table: "material_issues",
                column: "SiteId",
                principalTable: "sites",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_material_issues_systems_SystemId",
                table: "material_issues",
                column: "SystemId",
                principalTable: "systems",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_monitoring_webhook_integrations_clients_DefaultClientId",
                table: "monitoring_webhook_integrations",
                column: "DefaultClientId",
                principalTable: "clients",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_preventive_maintenance_plans_sites_SiteId",
                table: "preventive_maintenance_plans",
                column: "SiteId",
                principalTable: "sites",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_work_orders_clients_ClientId",
                table: "work_orders",
                column: "ClientId",
                principalTable: "clients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

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

            migrationBuilder.AddForeignKey(
                name: "FK_work_orders_sites_SiteId",
                table: "work_orders",
                column: "SiteId",
                principalTable: "sites",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_work_orders_systems_SystemId",
                table: "work_orders",
                column: "SystemId",
                principalTable: "systems",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
