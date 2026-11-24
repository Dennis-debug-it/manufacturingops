using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class AddWoodFuel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "fuel_species",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    KgPerStackedM3 = table.Column<decimal>(type: "numeric(10,1)", precision: 10, scale: 1, nullable: false),
                    EnergyMjPerKg = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fuel_species", x => x.Id);
                    table.ForeignKey(
                        name: "FK_fuel_species_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "fuel_suppliers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    PaymentDetails = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    KraPin = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    IdLast4 = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: true),
                    SourceArea = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PermitNumber = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    PermitExpiresOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fuel_suppliers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_fuel_suppliers_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "production_days",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    FactoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateTime>(type: "date", nullable: false),
                    MadeTeaKg = table.Column<decimal>(type: "numeric(12,1)", precision: 12, scale: 1, nullable: false),
                    GreenLeafKg = table.Column<decimal>(type: "numeric(12,1)", precision: 12, scale: 1, nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    EnteredByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_production_days", x => x.Id);
                    table.ForeignKey(
                        name: "FK_production_days_org_nodes_FactoryId",
                        column: x => x.FactoryId,
                        principalTable: "org_nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_production_days_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "wood_fuel_settings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    FactoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    DefaultMeasureBasis = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    MoistureRule = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    BaseMoisturePct = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    RejectMoisturePct = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    DeductionPctPerPoint = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    PriceBandsJson = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    DefaultKgPerLoad = table.Column<decimal>(type: "numeric(12,1)", precision: 12, scale: 1, nullable: true),
                    BurnWindowDays = table.Column<int>(type: "integer", nullable: false),
                    LowCoverDays = table.Column<int>(type: "integer", nullable: false),
                    SeasoningDays = table.Column<int>(type: "integer", nullable: false),
                    TargetKgPerUnit = table.Column<decimal>(type: "numeric(8,3)", precision: 8, scale: 3, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wood_fuel_settings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_wood_fuel_settings_org_nodes_FactoryId",
                        column: x => x.FactoryId,
                        principalTable: "org_nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_wood_fuel_settings_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "wood_stacks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    FactoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    SpeciesId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    BuiltOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReadyFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wood_stacks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_wood_stacks_fuel_species_SpeciesId",
                        column: x => x.SpeciesId,
                        principalTable: "fuel_species",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_wood_stacks_org_nodes_FactoryId",
                        column: x => x.FactoryId,
                        principalTable: "org_nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_wood_stacks_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "fuel_price_agreements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: true),
                    SpeciesId = table.Column<Guid>(type: "uuid", nullable: true),
                    FuelForm = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    PricePerTonne = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    ValidFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ValidTo = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fuel_price_agreements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_fuel_price_agreements_fuel_species_SpeciesId",
                        column: x => x.SpeciesId,
                        principalTable: "fuel_species",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_fuel_price_agreements_fuel_suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "fuel_suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_fuel_price_agreements_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "wood_deliveries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    FactoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeliveryNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: true),
                    SpeciesId = table.Column<Guid>(type: "uuid", nullable: true),
                    FuelForm = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SourceArea = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    TransportPermit = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    SupplierNoteNumber = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    VehicleRegistration = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    DriverName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    VehicleMovementId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    MeasureBasis = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    GrossKg = table.Column<decimal>(type: "numeric(12,1)", precision: 12, scale: 1, nullable: true),
                    TareKg = table.Column<decimal>(type: "numeric(12,1)", precision: 12, scale: 1, nullable: true),
                    StackedM3 = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    Loads = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                    KgPerLoad = table.Column<decimal>(type: "numeric(12,1)", precision: 12, scale: 1, nullable: true),
                    NetKg = table.Column<decimal>(type: "numeric(12,1)", precision: 12, scale: 1, nullable: false),
                    AverageMoisturePct = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    Decision = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    DecisionReason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    RejectedKg = table.Column<decimal>(type: "numeric(12,1)", precision: 12, scale: 1, nullable: false),
                    DeductedKg = table.Column<decimal>(type: "numeric(12,1)", precision: 12, scale: 1, nullable: false),
                    PayableKg = table.Column<decimal>(type: "numeric(12,1)", precision: 12, scale: 1, nullable: false),
                    PricePerTonne = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    PricePercent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    AmountPayable = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    StackId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReceivedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReceivedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wood_deliveries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_wood_deliveries_fuel_species_SpeciesId",
                        column: x => x.SpeciesId,
                        principalTable: "fuel_species",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_wood_deliveries_fuel_suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "fuel_suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_wood_deliveries_org_nodes_FactoryId",
                        column: x => x.FactoryId,
                        principalTable: "org_nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_wood_deliveries_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_wood_deliveries_wood_stacks_StackId",
                        column: x => x.StackId,
                        principalTable: "wood_stacks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "wood_issues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    FactoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    StackId = table.Column<Guid>(type: "uuid", nullable: true),
                    IssuedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Shift = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Kg = table.Column<decimal>(type: "numeric(12,1)", precision: 12, scale: 1, nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IssuedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    IssuedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    MeterReadingId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wood_issues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_wood_issues_assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_wood_issues_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_wood_issues_wood_stacks_StackId",
                        column: x => x.StackId,
                        principalTable: "wood_stacks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "wood_stock_counts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    FactoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    StackId = table.Column<Guid>(type: "uuid", nullable: false),
                    CountedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LedgerKg = table.Column<decimal>(type: "numeric(12,1)", precision: 12, scale: 1, nullable: false),
                    CountedKg = table.Column<decimal>(type: "numeric(12,1)", precision: 12, scale: 1, nullable: false),
                    VarianceKg = table.Column<decimal>(type: "numeric(12,1)", precision: 12, scale: 1, nullable: false),
                    Method = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Reason = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CountedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wood_stock_counts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_wood_stock_counts_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_wood_stock_counts_wood_stacks_StackId",
                        column: x => x.StackId,
                        principalTable: "wood_stacks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "wood_stock_movements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    FactoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    StackId = table.Column<Guid>(type: "uuid", nullable: true),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    KgChange = table.Column<decimal>(type: "numeric(12,1)", precision: 12, scale: 1, nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeliveryId = table.Column<Guid>(type: "uuid", nullable: true),
                    IssueId = table.Column<Guid>(type: "uuid", nullable: true),
                    CountId = table.Column<Guid>(type: "uuid", nullable: true),
                    Reason = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wood_stock_movements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_wood_stock_movements_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_wood_stock_movements_wood_stacks_StackId",
                        column: x => x.StackId,
                        principalTable: "wood_stacks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "wood_moisture_samples",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeliveryId = table.Column<Guid>(type: "uuid", nullable: false),
                    ValuePct = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    MeasuredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wood_moisture_samples", x => x.Id);
                    table.ForeignKey(
                        name: "FK_wood_moisture_samples_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_wood_moisture_samples_wood_deliveries_DeliveryId",
                        column: x => x.DeliveryId,
                        principalTable: "wood_deliveries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_fuel_price_agreements_SpeciesId",
                table: "fuel_price_agreements",
                column: "SpeciesId");

            migrationBuilder.CreateIndex(
                name: "IX_fuel_price_agreements_SupplierId",
                table: "fuel_price_agreements",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_fuel_price_agreements_TenantId_SupplierId_ValidFrom",
                table: "fuel_price_agreements",
                columns: new[] { "TenantId", "SupplierId", "ValidFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_fuel_species_TenantId_Name",
                table: "fuel_species",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_fuel_suppliers_TenantId_Name",
                table: "fuel_suppliers",
                columns: new[] { "TenantId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_production_days_FactoryId",
                table: "production_days",
                column: "FactoryId");

            migrationBuilder.CreateIndex(
                name: "IX_production_days_TenantId_FactoryId_Date",
                table: "production_days",
                columns: new[] { "TenantId", "FactoryId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_wood_deliveries_FactoryId",
                table: "wood_deliveries",
                column: "FactoryId");

            migrationBuilder.CreateIndex(
                name: "IX_wood_deliveries_SpeciesId",
                table: "wood_deliveries",
                column: "SpeciesId");

            migrationBuilder.CreateIndex(
                name: "IX_wood_deliveries_StackId",
                table: "wood_deliveries",
                column: "StackId");

            migrationBuilder.CreateIndex(
                name: "IX_wood_deliveries_SupplierId",
                table: "wood_deliveries",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_wood_deliveries_TenantId_DeliveryNumber",
                table: "wood_deliveries",
                columns: new[] { "TenantId", "DeliveryNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_wood_deliveries_TenantId_FactoryId_ReceivedAt",
                table: "wood_deliveries",
                columns: new[] { "TenantId", "FactoryId", "ReceivedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_wood_fuel_settings_FactoryId",
                table: "wood_fuel_settings",
                column: "FactoryId");

            migrationBuilder.CreateIndex(
                name: "IX_wood_fuel_settings_TenantId_FactoryId",
                table: "wood_fuel_settings",
                columns: new[] { "TenantId", "FactoryId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_wood_issues_AssetId",
                table: "wood_issues",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_wood_issues_StackId",
                table: "wood_issues",
                column: "StackId");

            migrationBuilder.CreateIndex(
                name: "IX_wood_issues_TenantId_FactoryId_IssuedAt",
                table: "wood_issues",
                columns: new[] { "TenantId", "FactoryId", "IssuedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_wood_moisture_samples_DeliveryId",
                table: "wood_moisture_samples",
                column: "DeliveryId");

            migrationBuilder.CreateIndex(
                name: "IX_wood_moisture_samples_TenantId_DeliveryId",
                table: "wood_moisture_samples",
                columns: new[] { "TenantId", "DeliveryId" });

            migrationBuilder.CreateIndex(
                name: "IX_wood_stacks_FactoryId",
                table: "wood_stacks",
                column: "FactoryId");

            migrationBuilder.CreateIndex(
                name: "IX_wood_stacks_SpeciesId",
                table: "wood_stacks",
                column: "SpeciesId");

            migrationBuilder.CreateIndex(
                name: "IX_wood_stacks_TenantId_FactoryId_Code",
                table: "wood_stacks",
                columns: new[] { "TenantId", "FactoryId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_wood_stock_counts_StackId",
                table: "wood_stock_counts",
                column: "StackId");

            migrationBuilder.CreateIndex(
                name: "IX_wood_stock_counts_TenantId_StackId_CountedAt",
                table: "wood_stock_counts",
                columns: new[] { "TenantId", "StackId", "CountedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_wood_stock_movements_StackId",
                table: "wood_stock_movements",
                column: "StackId");

            migrationBuilder.CreateIndex(
                name: "IX_wood_stock_movements_TenantId_FactoryId_OccurredAt",
                table: "wood_stock_movements",
                columns: new[] { "TenantId", "FactoryId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_wood_stock_movements_TenantId_StackId",
                table: "wood_stock_movements",
                columns: new[] { "TenantId", "StackId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fuel_price_agreements");

            migrationBuilder.DropTable(
                name: "production_days");

            migrationBuilder.DropTable(
                name: "wood_fuel_settings");

            migrationBuilder.DropTable(
                name: "wood_issues");

            migrationBuilder.DropTable(
                name: "wood_moisture_samples");

            migrationBuilder.DropTable(
                name: "wood_stock_counts");

            migrationBuilder.DropTable(
                name: "wood_stock_movements");

            migrationBuilder.DropTable(
                name: "wood_deliveries");

            migrationBuilder.DropTable(
                name: "fuel_suppliers");

            migrationBuilder.DropTable(
                name: "wood_stacks");

            migrationBuilder.DropTable(
                name: "fuel_species");
        }
    }
}
