using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class VehicleLogAndStandingPasses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PassId",
                table: "visits",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "vehicle_movements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    FactoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Registration = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Purpose = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DriverName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DriverPhone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    VisitId = table.Column<Guid>(type: "uuid", nullable: true),
                    EnteredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EnteredByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ExitedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExitedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ExitNotes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vehicle_movements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_vehicle_movements_org_nodes_FactoryId",
                        column: x => x.FactoryId,
                        principalTable: "org_nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_vehicle_movements_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_vehicle_movements_visits_VisitId",
                        column: x => x.VisitId,
                        principalTable: "visits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "visitor_passes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PassNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    VisitorId = table.Column<Guid>(type: "uuid", nullable: false),
                    FactoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    HostUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    HostName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ContractorId = table.Column<Guid>(type: "uuid", nullable: true),
                    Purpose = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ValidFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ValidTo = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Days = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DailyFrom = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    DailyTo = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    BadgeToken = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    VehicleRegistration = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IssuedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RevokedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RevokedReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_visitor_passes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_visitor_passes_contractors_ContractorId",
                        column: x => x.ContractorId,
                        principalTable: "contractors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_visitor_passes_org_nodes_FactoryId",
                        column: x => x.FactoryId,
                        principalTable: "org_nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_visitor_passes_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_visitor_passes_users_HostUserId",
                        column: x => x.HostUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_visitor_passes_visitors_VisitorId",
                        column: x => x.VisitorId,
                        principalTable: "visitors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "visitor_pass_zones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PassId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccessZoneId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_visitor_pass_zones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_visitor_pass_zones_access_zones_AccessZoneId",
                        column: x => x.AccessZoneId,
                        principalTable: "access_zones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_visitor_pass_zones_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_visitor_pass_zones_visitor_passes_PassId",
                        column: x => x.PassId,
                        principalTable: "visitor_passes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_visits_PassId_ExpectedFrom",
                table: "visits",
                columns: new[] { "PassId", "ExpectedFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_movements_FactoryId",
                table: "vehicle_movements",
                column: "FactoryId");

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_movements_TenantId_FactoryId_ExitedAt",
                table: "vehicle_movements",
                columns: new[] { "TenantId", "FactoryId", "ExitedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_movements_TenantId_Registration_EnteredAt",
                table: "vehicle_movements",
                columns: new[] { "TenantId", "Registration", "EnteredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_movements_VisitId",
                table: "vehicle_movements",
                column: "VisitId");

            migrationBuilder.CreateIndex(
                name: "IX_visitor_pass_zones_AccessZoneId",
                table: "visitor_pass_zones",
                column: "AccessZoneId");

            migrationBuilder.CreateIndex(
                name: "IX_visitor_pass_zones_PassId_AccessZoneId",
                table: "visitor_pass_zones",
                columns: new[] { "PassId", "AccessZoneId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_visitor_pass_zones_TenantId",
                table: "visitor_pass_zones",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_visitor_passes_BadgeToken",
                table: "visitor_passes",
                column: "BadgeToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_visitor_passes_ContractorId",
                table: "visitor_passes",
                column: "ContractorId");

            migrationBuilder.CreateIndex(
                name: "IX_visitor_passes_FactoryId",
                table: "visitor_passes",
                column: "FactoryId");

            migrationBuilder.CreateIndex(
                name: "IX_visitor_passes_HostUserId",
                table: "visitor_passes",
                column: "HostUserId");

            migrationBuilder.CreateIndex(
                name: "IX_visitor_passes_TenantId_PassNumber",
                table: "visitor_passes",
                columns: new[] { "TenantId", "PassNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_visitor_passes_VisitorId",
                table: "visitor_passes",
                column: "VisitorId");

            migrationBuilder.AddForeignKey(
                name: "FK_visits_visitor_passes_PassId",
                table: "visits",
                column: "PassId",
                principalTable: "visitor_passes",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_visits_visitor_passes_PassId",
                table: "visits");

            migrationBuilder.DropTable(
                name: "vehicle_movements");

            migrationBuilder.DropTable(
                name: "visitor_pass_zones");

            migrationBuilder.DropTable(
                name: "visitor_passes");

            migrationBuilder.DropIndex(
                name: "IX_visits_PassId_ExpectedFrom",
                table: "visits");

            migrationBuilder.DropColumn(
                name: "PassId",
                table: "visits");
        }
    }
}
