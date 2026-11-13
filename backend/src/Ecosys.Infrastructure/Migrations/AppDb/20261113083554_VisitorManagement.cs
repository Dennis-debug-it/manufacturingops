using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class VisitorManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OperationsAddOns",
                table: "tenant_licenses",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VisitOverstayGraceMinutes",
                table: "factory_settings",
                type: "integer",
                nullable: false,
                defaultValue: 30);

            migrationBuilder.AddColumn<int>(
                name: "VisitorRetentionMonths",
                table: "factory_settings",
                type: "integer",
                nullable: false,
                defaultValue: 12);

            migrationBuilder.AddColumn<bool>(
                name: "WalkInsNeedHostApproval",
                table: "factory_settings",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "access_zones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    FactoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgNodeId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Level = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RequiresEscort = table.Column<bool>(type: "boolean", nullable: false),
                    RequiresInduction = table.Column<bool>(type: "boolean", nullable: false),
                    RequiresPermit = table.Column<bool>(type: "boolean", nullable: false),
                    RequiredPpe = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_access_zones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_access_zones_org_nodes_FactoryId",
                        column: x => x.FactoryId,
                        principalTable: "org_nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_access_zones_org_nodes_OrgNodeId",
                        column: x => x.OrgNodeId,
                        principalTable: "org_nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_access_zones_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "visitors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    FullName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Company = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IdType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    IdNumberMasked = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    IdNumberHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ContractorId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsBlocked = table.Column<bool>(type: "boolean", nullable: false),
                    BlockedReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    BlockedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    LastVisitAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AnonymisedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_visitors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_visitors_contractors_ContractorId",
                        column: x => x.ContractorId,
                        principalTable: "contractors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_visitors_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "visits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    VisitNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    FactoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Purpose = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    HostUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    HostName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ExpectedFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpectedTo = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    WorkPermitId = table.Column<Guid>(type: "uuid", nullable: true),
                    ContractorId = table.Column<Guid>(type: "uuid", nullable: true),
                    VehicleRegistration = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DecidedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DecidedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DecisionNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_visits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_visits_contractors_ContractorId",
                        column: x => x.ContractorId,
                        principalTable: "contractors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_visits_org_nodes_FactoryId",
                        column: x => x.FactoryId,
                        principalTable: "org_nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_visits_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_visits_users_HostUserId",
                        column: x => x.HostUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_visits_work_orders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "work_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_visits_work_permits_WorkPermitId",
                        column: x => x.WorkPermitId,
                        principalTable: "work_permits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "visit_attendees",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    VisitId = table.Column<Guid>(type: "uuid", nullable: false),
                    VisitorId = table.Column<Guid>(type: "uuid", nullable: false),
                    BadgeNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    BadgeToken = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CheckedInAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CheckedInByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CheckedOutAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CheckedOutByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IdVerified = table.Column<bool>(type: "boolean", nullable: false),
                    InductionConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    PpeConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    EscortUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    EscortName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    OverrideReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    OverriddenByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AccountedForAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AccountedForByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_visit_attendees", x => x.Id);
                    table.ForeignKey(
                        name: "FK_visit_attendees_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_visit_attendees_visitors_VisitorId",
                        column: x => x.VisitorId,
                        principalTable: "visitors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_visit_attendees_visits_VisitId",
                        column: x => x.VisitId,
                        principalTable: "visits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "visit_zones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    VisitId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccessZoneId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_visit_zones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_visit_zones_access_zones_AccessZoneId",
                        column: x => x.AccessZoneId,
                        principalTable: "access_zones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_visit_zones_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_visit_zones_visits_VisitId",
                        column: x => x.VisitId,
                        principalTable: "visits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_access_zones_FactoryId",
                table: "access_zones",
                column: "FactoryId");

            migrationBuilder.CreateIndex(
                name: "IX_access_zones_OrgNodeId",
                table: "access_zones",
                column: "OrgNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_access_zones_TenantId_FactoryId_Name",
                table: "access_zones",
                columns: new[] { "TenantId", "FactoryId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_visit_attendees_BadgeToken",
                table: "visit_attendees",
                column: "BadgeToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_visit_attendees_TenantId",
                table: "visit_attendees",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_visit_attendees_VisitId_VisitorId",
                table: "visit_attendees",
                columns: new[] { "VisitId", "VisitorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_visit_attendees_VisitorId",
                table: "visit_attendees",
                column: "VisitorId");

            migrationBuilder.CreateIndex(
                name: "IX_visit_zones_AccessZoneId",
                table: "visit_zones",
                column: "AccessZoneId");

            migrationBuilder.CreateIndex(
                name: "IX_visit_zones_TenantId",
                table: "visit_zones",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_visit_zones_VisitId_AccessZoneId",
                table: "visit_zones",
                columns: new[] { "VisitId", "AccessZoneId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_visitors_ContractorId",
                table: "visitors",
                column: "ContractorId");

            migrationBuilder.CreateIndex(
                name: "IX_visitors_TenantId_IdNumberHash",
                table: "visitors",
                columns: new[] { "TenantId", "IdNumberHash" });

            migrationBuilder.CreateIndex(
                name: "IX_visitors_TenantId_Phone",
                table: "visitors",
                columns: new[] { "TenantId", "Phone" });

            migrationBuilder.CreateIndex(
                name: "IX_visits_ContractorId",
                table: "visits",
                column: "ContractorId");

            migrationBuilder.CreateIndex(
                name: "IX_visits_FactoryId",
                table: "visits",
                column: "FactoryId");

            migrationBuilder.CreateIndex(
                name: "IX_visits_HostUserId",
                table: "visits",
                column: "HostUserId");

            migrationBuilder.CreateIndex(
                name: "IX_visits_TenantId_FactoryId_Status_ExpectedFrom",
                table: "visits",
                columns: new[] { "TenantId", "FactoryId", "Status", "ExpectedFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_visits_TenantId_VisitNumber",
                table: "visits",
                columns: new[] { "TenantId", "VisitNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_visits_WorkOrderId",
                table: "visits",
                column: "WorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_visits_WorkPermitId",
                table: "visits",
                column: "WorkPermitId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "visit_attendees");

            migrationBuilder.DropTable(
                name: "visit_zones");

            migrationBuilder.DropTable(
                name: "visitors");

            migrationBuilder.DropTable(
                name: "access_zones");

            migrationBuilder.DropTable(
                name: "visits");

            migrationBuilder.DropColumn(
                name: "OperationsAddOns",
                table: "tenant_licenses");

            migrationBuilder.DropColumn(
                name: "VisitOverstayGraceMinutes",
                table: "factory_settings");

            migrationBuilder.DropColumn(
                name: "VisitorRetentionMonths",
                table: "factory_settings");

            migrationBuilder.DropColumn(
                name: "WalkInsNeedHostApproval",
                table: "factory_settings");
        }
    }
}
