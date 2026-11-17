using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class FactoryPricing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ExtraFieldUsers",
                table: "tenant_licenses",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ExtraOfficeUsers",
                table: "tenant_licenses",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "FactoryAnnualPriceOverride",
                table: "tenant_licenses",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LicensedFactories",
                table: "tenant_licenses",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PricingBasis",
                table: "tenant_licenses",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "PerSeat");

            migrationBuilder.AddColumn<decimal>(
                name: "FactoryAnnualPrice",
                table: "license_plans",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "price_books",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    VatPercent = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    VisitorsFactoryAnnualPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    SecurityFactoryAnnualPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    SecuritySuiteFactoryAnnualPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    IncludedOfficeUsersPerFactory = table.Column<int>(type: "integer", nullable: false),
                    IncludedFieldUsersPerFactory = table.Column<int>(type: "integer", nullable: false),
                    ExtraUserMonthlyPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DiscountTiersJson = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ImplementationFeeMin = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ImplementationFeeMax = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ImplementationFeeDefault = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    OnPremisesMultiplier = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    OnPremisesSetupFee = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PilotMonths = table.Column<int>(type: "integer", nullable: false),
                    PilotFee = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PilotCreditedToYearOne = table.Column<bool>(type: "boolean", nullable: false),
                    HardwareJson = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    QuoteValidDays = table.Column<int>(type: "integer", nullable: false),
                    QuoteTerms = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_price_books", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "price_quotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    QuoteNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CustomerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ContactName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ContactEmail = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Edition = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Visitors = table.Column<bool>(type: "boolean", nullable: false),
                    Security = table.Column<bool>(type: "boolean", nullable: false),
                    Factories = table.Column<int>(type: "integer", nullable: false),
                    ExtraOfficeUsers = table.Column<int>(type: "integer", nullable: false),
                    ExtraFieldUsers = table.Column<int>(type: "integer", nullable: false),
                    ImplementationFeePerFactory = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    OnPremises = table.Column<bool>(type: "boolean", nullable: false),
                    IncludePilot = table.Column<bool>(type: "boolean", nullable: false),
                    DiscountPercentOverride = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    HardwareJson = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ValidUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ResultJson = table.Column<string>(type: "text", nullable: false),
                    AnnualSubscription = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    YearOneTotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PreparedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_price_quotes", x => x.Id);
                });

            migrationBuilder.UpdateData(
                table: "license_plans",
                keyColumn: "Id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555561"),
                column: "FactoryAnnualPrice",
                value: 0m);

            migrationBuilder.UpdateData(
                table: "license_plans",
                keyColumn: "Id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555562"),
                column: "FactoryAnnualPrice",
                value: 0m);

            migrationBuilder.UpdateData(
                table: "license_plans",
                keyColumn: "Id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555563"),
                column: "FactoryAnnualPrice",
                value: 0m);

            migrationBuilder.CreateIndex(
                name: "IX_price_quotes_QuoteNumber",
                table: "price_quotes",
                column: "QuoteNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_price_quotes_TenantId",
                table: "price_quotes",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "price_books");

            migrationBuilder.DropTable(
                name: "price_quotes");

            migrationBuilder.DropColumn(
                name: "ExtraFieldUsers",
                table: "tenant_licenses");

            migrationBuilder.DropColumn(
                name: "ExtraOfficeUsers",
                table: "tenant_licenses");

            migrationBuilder.DropColumn(
                name: "FactoryAnnualPriceOverride",
                table: "tenant_licenses");

            migrationBuilder.DropColumn(
                name: "LicensedFactories",
                table: "tenant_licenses");

            migrationBuilder.DropColumn(
                name: "PricingBasis",
                table: "tenant_licenses");

            migrationBuilder.DropColumn(
                name: "FactoryAnnualPrice",
                table: "license_plans");
        }
    }
}
