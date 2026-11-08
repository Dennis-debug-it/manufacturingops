using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class LeafCollectionFleet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "fuel_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    FilledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Litres = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    Cost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    OdometerKm = table.Column<decimal>(type: "numeric(18,1)", precision: 18, scale: 1, nullable: true),
                    FullTank = table.Column<bool>(type: "boolean", nullable: false),
                    Station = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    RecordedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    RecordedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fuel_logs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_fuel_logs_assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_fuel_logs_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "telematics_connections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    BaseUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Username = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    EncryptedPassword = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LastSyncAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastSyncVehicles = table.Column<int>(type: "integer", nullable: false),
                    LastSyncError = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_telematics_connections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_telematics_connections_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "vehicle_profiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    RegistrationNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    VehicleType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Make = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    VehicleModel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Year = table.Column<int>(type: "integer", nullable: true),
                    PayloadKg = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    FuelType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Route = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DriverName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    DriverPhone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    InsuranceExpiresOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    InspectionExpiresOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SpeedGovernorExpiresOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    OdometerMeterId = table.Column<Guid>(type: "uuid", nullable: true),
                    EngineHoursMeterId = table.Column<Guid>(type: "uuid", nullable: true),
                    TrackerDeviceId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LastLatitude = table.Column<double>(type: "double precision", nullable: true),
                    LastLongitude = table.Column<double>(type: "double precision", nullable: true),
                    LastSpeedKph = table.Column<decimal>(type: "numeric(8,1)", precision: 8, scale: 1, nullable: true),
                    LastPositionAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastPositionSource = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vehicle_profiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_vehicle_profiles_assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_vehicle_profiles_meters_EngineHoursMeterId",
                        column: x => x.EngineHoursMeterId,
                        principalTable: "meters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_vehicle_profiles_meters_OdometerMeterId",
                        column: x => x.OdometerMeterId,
                        principalTable: "meters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_vehicle_profiles_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_fuel_logs_AssetId",
                table: "fuel_logs",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_fuel_logs_TenantId_AssetId_FilledAt",
                table: "fuel_logs",
                columns: new[] { "TenantId", "AssetId", "FilledAt" });

            migrationBuilder.CreateIndex(
                name: "IX_telematics_connections_TenantId",
                table: "telematics_connections",
                column: "TenantId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_profiles_AssetId",
                table: "vehicle_profiles",
                column: "AssetId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_profiles_EngineHoursMeterId",
                table: "vehicle_profiles",
                column: "EngineHoursMeterId");

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_profiles_OdometerMeterId",
                table: "vehicle_profiles",
                column: "OdometerMeterId");

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_profiles_TenantId_RegistrationNumber",
                table: "vehicle_profiles",
                columns: new[] { "TenantId", "RegistrationNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_profiles_TenantId_TrackerDeviceId",
                table: "vehicle_profiles",
                columns: new[] { "TenantId", "TrackerDeviceId" },
                unique: true,
                filter: "\"TrackerDeviceId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fuel_logs");

            migrationBuilder.DropTable(
                name: "telematics_connections");

            migrationBuilder.DropTable(
                name: "vehicle_profiles");
        }
    }
}
