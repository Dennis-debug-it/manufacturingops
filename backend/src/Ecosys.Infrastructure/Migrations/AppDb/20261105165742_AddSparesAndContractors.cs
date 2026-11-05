using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class AddSparesAndContractors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ContractorId",
                table: "work_orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "MaintenanceAgreementId",
                table: "work_orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "StoreId",
                table: "stock_movements",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "UnitCost",
                table: "stock_movements",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ChemicalKind",
                table: "material_items",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalId",
                table: "material_items",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalSystem",
                table: "material_items",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FoodGradeClass",
                table: "material_items",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HazardClass",
                table: "material_items",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsChemical",
                table: "material_items",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsCriticalSpare",
                table: "material_items",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsRepairable",
                table: "material_items",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsSerialised",
                table: "material_items",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "PurchaseUnitFactor",
                table: "material_items",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 1m);

            migrationBuilder.AddColumn<string>(
                name: "PurchaseUnitOfMeasure",
                table: "material_items",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SdsAttachmentId",
                table: "material_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SdsIssuedOn",
                table: "material_items",
                type: "timestamp with time zone",
                nullable: true);

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
                    table.ForeignKey(
                        name: "FK_stock_reservations_work_orders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "work_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
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
                    table.ForeignKey(
                        name: "FK_store_transfers_work_orders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "work_orders",
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
                    table.ForeignKey(
                        name: "FK_contractor_invoice_lines_work_orders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "work_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
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
                    table.ForeignKey(
                        name: "FK_purchase_requisition_lines_work_orders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "work_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
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

            migrationBuilder.CreateIndex(
                name: "IX_work_orders_ContractorId",
                table: "work_orders",
                column: "ContractorId");

            migrationBuilder.CreateIndex(
                name: "IX_work_orders_MaintenanceAgreementId",
                table: "work_orders",
                column: "MaintenanceAgreementId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_movements_StoreId",
                table: "stock_movements",
                column: "StoreId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_movements_TenantId_StoreId_CreatedAt",
                table: "stock_movements",
                columns: new[] { "TenantId", "StoreId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_material_items_SdsAttachmentId",
                table: "material_items",
                column: "SdsAttachmentId");

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

            migrationBuilder.AddForeignKey(
                name: "FK_material_items_attachments_SdsAttachmentId",
                table: "material_items",
                column: "SdsAttachmentId",
                principalTable: "attachments",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_stock_movements_stores_StoreId",
                table: "stock_movements",
                column: "StoreId",
                principalTable: "stores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_work_orders_contractors_ContractorId",
                table: "work_orders",
                column: "ContractorId",
                principalTable: "contractors",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_work_orders_maintenance_agreements_MaintenanceAgreementId",
                table: "work_orders",
                column: "MaintenanceAgreementId",
                principalTable: "maintenance_agreements",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_material_items_attachments_SdsAttachmentId",
                table: "material_items");

            migrationBuilder.DropForeignKey(
                name: "FK_stock_movements_stores_StoreId",
                table: "stock_movements");

            migrationBuilder.DropForeignKey(
                name: "FK_work_orders_contractors_ContractorId",
                table: "work_orders");

            migrationBuilder.DropForeignKey(
                name: "FK_work_orders_maintenance_agreements_MaintenanceAgreementId",
                table: "work_orders");

            migrationBuilder.DropTable(
                name: "asset_parts");

            migrationBuilder.DropTable(
                name: "contractor_documents");

            migrationBuilder.DropTable(
                name: "contractor_invoice_lines");

            migrationBuilder.DropTable(
                name: "maintenance_agreement_assets");

            migrationBuilder.DropTable(
                name: "purchase_requisition_lines");

            migrationBuilder.DropTable(
                name: "spare_unit_events");

            migrationBuilder.DropTable(
                name: "stock_reservations");

            migrationBuilder.DropTable(
                name: "store_count_lines");

            migrationBuilder.DropTable(
                name: "store_stocks");

            migrationBuilder.DropTable(
                name: "store_transfers");

            migrationBuilder.DropTable(
                name: "vendor_catalogue_items");

            migrationBuilder.DropTable(
                name: "contractor_invoices");

            migrationBuilder.DropTable(
                name: "maintenance_agreements");

            migrationBuilder.DropTable(
                name: "purchase_requisitions");

            migrationBuilder.DropTable(
                name: "spare_units");

            migrationBuilder.DropTable(
                name: "store_counts");

            migrationBuilder.DropTable(
                name: "contractors");

            migrationBuilder.DropTable(
                name: "store_bins");

            migrationBuilder.DropTable(
                name: "stores");

            migrationBuilder.DropIndex(
                name: "IX_work_orders_ContractorId",
                table: "work_orders");

            migrationBuilder.DropIndex(
                name: "IX_work_orders_MaintenanceAgreementId",
                table: "work_orders");

            migrationBuilder.DropIndex(
                name: "IX_stock_movements_StoreId",
                table: "stock_movements");

            migrationBuilder.DropIndex(
                name: "IX_stock_movements_TenantId_StoreId_CreatedAt",
                table: "stock_movements");

            migrationBuilder.DropIndex(
                name: "IX_material_items_SdsAttachmentId",
                table: "material_items");

            migrationBuilder.DropColumn(
                name: "ContractorId",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "MaintenanceAgreementId",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "StoreId",
                table: "stock_movements");

            migrationBuilder.DropColumn(
                name: "UnitCost",
                table: "stock_movements");

            migrationBuilder.DropColumn(
                name: "ChemicalKind",
                table: "material_items");

            migrationBuilder.DropColumn(
                name: "ExternalId",
                table: "material_items");

            migrationBuilder.DropColumn(
                name: "ExternalSystem",
                table: "material_items");

            migrationBuilder.DropColumn(
                name: "FoodGradeClass",
                table: "material_items");

            migrationBuilder.DropColumn(
                name: "HazardClass",
                table: "material_items");

            migrationBuilder.DropColumn(
                name: "IsChemical",
                table: "material_items");

            migrationBuilder.DropColumn(
                name: "IsCriticalSpare",
                table: "material_items");

            migrationBuilder.DropColumn(
                name: "IsRepairable",
                table: "material_items");

            migrationBuilder.DropColumn(
                name: "IsSerialised",
                table: "material_items");

            migrationBuilder.DropColumn(
                name: "PurchaseUnitFactor",
                table: "material_items");

            migrationBuilder.DropColumn(
                name: "PurchaseUnitOfMeasure",
                table: "material_items");

            migrationBuilder.DropColumn(
                name: "SdsAttachmentId",
                table: "material_items");

            migrationBuilder.DropColumn(
                name: "SdsIssuedOn",
                table: "material_items");
        }
    }
}
