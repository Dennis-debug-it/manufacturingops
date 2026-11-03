using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class Phase2BOperationalDepth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ActualDuration",
                table: "work_orders",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ClosedAt",
                table: "work_orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ClosedByUserId",
                table: "work_orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "EstimatedDuration",
                table: "work_orders",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JobCardNotes",
                table: "work_orders",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ServiceType",
                table: "work_orders",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SiteId",
                table: "work_orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AttachmentId",
                table: "work_order_checklist_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FailureNote",
                table: "work_order_checklist_items",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "NumberValue",
                table: "work_order_checklist_items",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QuestionType",
                table: "work_order_checklist_items",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "RequiresNoteOnFail",
                table: "work_order_checklist_items",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "TextValue",
                table: "work_order_checklist_items",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Unit",
                table: "work_order_checklist_items",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AutoAssign",
                table: "preventive_maintenance_plans",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "DaysBeforeDue",
                table: "preventive_maintenance_plans",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "DefaultAssignmentGroupId",
                table: "preventive_maintenance_plans",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FrequencyInterval",
                table: "preventive_maintenance_plans",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "FrequencyUnit",
                table: "preventive_maintenance_plans",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "LastGeneratedAt",
                table: "preventive_maintenance_plans",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastPmWorkOrderId",
                table: "preventive_maintenance_plans",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MeterBuffer",
                table: "preventive_maintenance_plans",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MeterInterval",
                table: "preventive_maintenance_plans",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "NotifyOnGeneration",
                table: "preventive_maintenance_plans",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "PreferredDayOfMonth",
                table: "preventive_maintenance_plans",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PreferredDayOfWeek",
                table: "preventive_maintenance_plans",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SiteId",
                table: "preventive_maintenance_plans",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TriggerType",
                table: "preventive_maintenance_plans",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "AssetCategoryId",
                table: "pm_templates",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "EstimatedDurationHours",
                table: "pm_templates",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DisplayOrder",
                table: "pm_template_questions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "QuestionType",
                table: "pm_template_questions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "RequiresNoteOnFail",
                table: "pm_template_questions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "SectionId",
                table: "pm_template_questions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Unit",
                table: "pm_template_questions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsLocked",
                table: "numbering_settings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Separator",
                table: "numbering_settings",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Suffix",
                table: "numbering_settings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "YearFormat",
                table: "numbering_settings",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "AssetCategoryId",
                table: "assets",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BuildingBlock",
                table: "assets",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CapacityRating",
                table: "assets",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CommissioningDate",
                table: "assets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CurrentMeterReading",
                table: "assets",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DecommissionDate",
                table: "assets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DefaultAssignmentGroupId",
                table: "assets",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FloorLevel",
                table: "assets",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LocationDescription",
                table: "assets",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MeterBuffer",
                table: "assets",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MeterInterval",
                table: "assets",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MeterLabel",
                table: "assets",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OwnershipType",
                table: "assets",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PhysicalDescription",
                table: "assets",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RoomArea",
                table: "assets",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SiteId",
                table: "assets",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "YearOfManufacture",
                table: "assets",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "asset_categories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentCategoryName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Icon = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_asset_categories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_asset_categories_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "attachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    MimeType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    StoragePath = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    PublicUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    UploadedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_attachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_attachments_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_attachments_users_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pm_template_sections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PmTemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    SectionName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pm_template_sections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_pm_template_sections_pm_templates_PmTemplateId",
                        column: x => x.PmTemplateId,
                        principalTable: "pm_templates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sites",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    SiteCode = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    SiteName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SiteType = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    StreetAddress = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    AreaEstate = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    TownCity = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    County = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Country = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Region = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ContactPerson = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ContactPhone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ContactEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    AlternateContact = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    OperatingHours = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AccessNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    SpecialInstructions = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
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
                name: "asset_category_fields",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetCategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    FieldName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FieldLabel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    FieldType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DropdownOptions = table.Column<string>(type: "text", nullable: true),
                    Unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_asset_category_fields", x => x.Id);
                    table.ForeignKey(
                        name: "FK_asset_category_fields_asset_categories_AssetCategoryId",
                        column: x => x.AssetCategoryId,
                        principalTable: "asset_categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "asset_custom_field_values",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    FieldDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Value = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_asset_custom_field_values", x => x.Id);
                    table.ForeignKey(
                        name: "FK_asset_custom_field_values_asset_category_fields_FieldDefini~",
                        column: x => x.FieldDefinitionId,
                        principalTable: "asset_category_fields",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_asset_custom_field_values_assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_asset_custom_field_values_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_work_orders_ClosedByUserId",
                table: "work_orders",
                column: "ClosedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_work_orders_SiteId",
                table: "work_orders",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "IX_work_order_checklist_items_AttachmentId",
                table: "work_order_checklist_items",
                column: "AttachmentId");

            migrationBuilder.CreateIndex(
                name: "IX_preventive_maintenance_plans_DefaultAssignmentGroupId",
                table: "preventive_maintenance_plans",
                column: "DefaultAssignmentGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_preventive_maintenance_plans_SiteId",
                table: "preventive_maintenance_plans",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "IX_pm_templates_AssetCategoryId",
                table: "pm_templates",
                column: "AssetCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_pm_template_questions_SectionId",
                table: "pm_template_questions",
                column: "SectionId");

            migrationBuilder.CreateIndex(
                name: "IX_assets_AssetCategoryId",
                table: "assets",
                column: "AssetCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_assets_DefaultAssignmentGroupId",
                table: "assets",
                column: "DefaultAssignmentGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_assets_SiteId",
                table: "assets",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "IX_asset_categories_TenantId_Name",
                table: "asset_categories",
                columns: new[] { "TenantId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_asset_category_fields_AssetCategoryId",
                table: "asset_category_fields",
                column: "AssetCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_asset_custom_field_values_AssetId_FieldDefinitionId",
                table: "asset_custom_field_values",
                columns: new[] { "AssetId", "FieldDefinitionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_asset_custom_field_values_FieldDefinitionId",
                table: "asset_custom_field_values",
                column: "FieldDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_asset_custom_field_values_TenantId",
                table: "asset_custom_field_values",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_attachments_TenantId_EntityType_EntityId",
                table: "attachments",
                columns: new[] { "TenantId", "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_attachments_UploadedByUserId",
                table: "attachments",
                column: "UploadedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_pm_template_sections_PmTemplateId",
                table: "pm_template_sections",
                column: "PmTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_sites_ClientId",
                table: "sites",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_sites_TenantId_SiteCode",
                table: "sites",
                columns: new[] { "TenantId", "SiteCode" });

            migrationBuilder.AddForeignKey(
                name: "FK_assets_asset_categories_AssetCategoryId",
                table: "assets",
                column: "AssetCategoryId",
                principalTable: "asset_categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_assets_assignment_groups_DefaultAssignmentGroupId",
                table: "assets",
                column: "DefaultAssignmentGroupId",
                principalTable: "assignment_groups",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_assets_sites_SiteId",
                table: "assets",
                column: "SiteId",
                principalTable: "sites",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_pm_template_questions_pm_template_sections_SectionId",
                table: "pm_template_questions",
                column: "SectionId",
                principalTable: "pm_template_sections",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_pm_templates_asset_categories_AssetCategoryId",
                table: "pm_templates",
                column: "AssetCategoryId",
                principalTable: "asset_categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_preventive_maintenance_plans_assignment_groups_DefaultAssig~",
                table: "preventive_maintenance_plans",
                column: "DefaultAssignmentGroupId",
                principalTable: "assignment_groups",
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
                name: "FK_work_order_checklist_items_attachments_AttachmentId",
                table: "work_order_checklist_items",
                column: "AttachmentId",
                principalTable: "attachments",
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
                name: "FK_work_orders_users_ClosedByUserId",
                table: "work_orders",
                column: "ClosedByUserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_assets_asset_categories_AssetCategoryId",
                table: "assets");

            migrationBuilder.DropForeignKey(
                name: "FK_assets_assignment_groups_DefaultAssignmentGroupId",
                table: "assets");

            migrationBuilder.DropForeignKey(
                name: "FK_assets_sites_SiteId",
                table: "assets");

            migrationBuilder.DropForeignKey(
                name: "FK_pm_template_questions_pm_template_sections_SectionId",
                table: "pm_template_questions");

            migrationBuilder.DropForeignKey(
                name: "FK_pm_templates_asset_categories_AssetCategoryId",
                table: "pm_templates");

            migrationBuilder.DropForeignKey(
                name: "FK_preventive_maintenance_plans_assignment_groups_DefaultAssig~",
                table: "preventive_maintenance_plans");

            migrationBuilder.DropForeignKey(
                name: "FK_preventive_maintenance_plans_sites_SiteId",
                table: "preventive_maintenance_plans");

            migrationBuilder.DropForeignKey(
                name: "FK_work_order_checklist_items_attachments_AttachmentId",
                table: "work_order_checklist_items");

            migrationBuilder.DropForeignKey(
                name: "FK_work_orders_sites_SiteId",
                table: "work_orders");

            migrationBuilder.DropForeignKey(
                name: "FK_work_orders_users_ClosedByUserId",
                table: "work_orders");

            migrationBuilder.DropTable(
                name: "asset_custom_field_values");

            migrationBuilder.DropTable(
                name: "attachments");

            migrationBuilder.DropTable(
                name: "pm_template_sections");

            migrationBuilder.DropTable(
                name: "sites");

            migrationBuilder.DropTable(
                name: "asset_category_fields");

            migrationBuilder.DropTable(
                name: "asset_categories");

            migrationBuilder.DropIndex(
                name: "IX_work_orders_ClosedByUserId",
                table: "work_orders");

            migrationBuilder.DropIndex(
                name: "IX_work_orders_SiteId",
                table: "work_orders");

            migrationBuilder.DropIndex(
                name: "IX_work_order_checklist_items_AttachmentId",
                table: "work_order_checklist_items");

            migrationBuilder.DropIndex(
                name: "IX_preventive_maintenance_plans_DefaultAssignmentGroupId",
                table: "preventive_maintenance_plans");

            migrationBuilder.DropIndex(
                name: "IX_preventive_maintenance_plans_SiteId",
                table: "preventive_maintenance_plans");

            migrationBuilder.DropIndex(
                name: "IX_pm_templates_AssetCategoryId",
                table: "pm_templates");

            migrationBuilder.DropIndex(
                name: "IX_pm_template_questions_SectionId",
                table: "pm_template_questions");

            migrationBuilder.DropIndex(
                name: "IX_assets_AssetCategoryId",
                table: "assets");

            migrationBuilder.DropIndex(
                name: "IX_assets_DefaultAssignmentGroupId",
                table: "assets");

            migrationBuilder.DropIndex(
                name: "IX_assets_SiteId",
                table: "assets");

            migrationBuilder.DropColumn(
                name: "ActualDuration",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "ClosedAt",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "ClosedByUserId",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "EstimatedDuration",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "JobCardNotes",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "ServiceType",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "SiteId",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "AttachmentId",
                table: "work_order_checklist_items");

            migrationBuilder.DropColumn(
                name: "FailureNote",
                table: "work_order_checklist_items");

            migrationBuilder.DropColumn(
                name: "NumberValue",
                table: "work_order_checklist_items");

            migrationBuilder.DropColumn(
                name: "QuestionType",
                table: "work_order_checklist_items");

            migrationBuilder.DropColumn(
                name: "RequiresNoteOnFail",
                table: "work_order_checklist_items");

            migrationBuilder.DropColumn(
                name: "TextValue",
                table: "work_order_checklist_items");

            migrationBuilder.DropColumn(
                name: "Unit",
                table: "work_order_checklist_items");

            migrationBuilder.DropColumn(
                name: "AutoAssign",
                table: "preventive_maintenance_plans");

            migrationBuilder.DropColumn(
                name: "DaysBeforeDue",
                table: "preventive_maintenance_plans");

            migrationBuilder.DropColumn(
                name: "DefaultAssignmentGroupId",
                table: "preventive_maintenance_plans");

            migrationBuilder.DropColumn(
                name: "FrequencyInterval",
                table: "preventive_maintenance_plans");

            migrationBuilder.DropColumn(
                name: "FrequencyUnit",
                table: "preventive_maintenance_plans");

            migrationBuilder.DropColumn(
                name: "LastGeneratedAt",
                table: "preventive_maintenance_plans");

            migrationBuilder.DropColumn(
                name: "LastPmWorkOrderId",
                table: "preventive_maintenance_plans");

            migrationBuilder.DropColumn(
                name: "MeterBuffer",
                table: "preventive_maintenance_plans");

            migrationBuilder.DropColumn(
                name: "MeterInterval",
                table: "preventive_maintenance_plans");

            migrationBuilder.DropColumn(
                name: "NotifyOnGeneration",
                table: "preventive_maintenance_plans");

            migrationBuilder.DropColumn(
                name: "PreferredDayOfMonth",
                table: "preventive_maintenance_plans");

            migrationBuilder.DropColumn(
                name: "PreferredDayOfWeek",
                table: "preventive_maintenance_plans");

            migrationBuilder.DropColumn(
                name: "SiteId",
                table: "preventive_maintenance_plans");

            migrationBuilder.DropColumn(
                name: "TriggerType",
                table: "preventive_maintenance_plans");

            migrationBuilder.DropColumn(
                name: "AssetCategoryId",
                table: "pm_templates");

            migrationBuilder.DropColumn(
                name: "EstimatedDurationHours",
                table: "pm_templates");

            migrationBuilder.DropColumn(
                name: "DisplayOrder",
                table: "pm_template_questions");

            migrationBuilder.DropColumn(
                name: "QuestionType",
                table: "pm_template_questions");

            migrationBuilder.DropColumn(
                name: "RequiresNoteOnFail",
                table: "pm_template_questions");

            migrationBuilder.DropColumn(
                name: "SectionId",
                table: "pm_template_questions");

            migrationBuilder.DropColumn(
                name: "Unit",
                table: "pm_template_questions");

            migrationBuilder.DropColumn(
                name: "IsLocked",
                table: "numbering_settings");

            migrationBuilder.DropColumn(
                name: "Separator",
                table: "numbering_settings");

            migrationBuilder.DropColumn(
                name: "Suffix",
                table: "numbering_settings");

            migrationBuilder.DropColumn(
                name: "YearFormat",
                table: "numbering_settings");

            migrationBuilder.DropColumn(
                name: "AssetCategoryId",
                table: "assets");

            migrationBuilder.DropColumn(
                name: "BuildingBlock",
                table: "assets");

            migrationBuilder.DropColumn(
                name: "CapacityRating",
                table: "assets");

            migrationBuilder.DropColumn(
                name: "CommissioningDate",
                table: "assets");

            migrationBuilder.DropColumn(
                name: "CurrentMeterReading",
                table: "assets");

            migrationBuilder.DropColumn(
                name: "DecommissionDate",
                table: "assets");

            migrationBuilder.DropColumn(
                name: "DefaultAssignmentGroupId",
                table: "assets");

            migrationBuilder.DropColumn(
                name: "FloorLevel",
                table: "assets");

            migrationBuilder.DropColumn(
                name: "LocationDescription",
                table: "assets");

            migrationBuilder.DropColumn(
                name: "MeterBuffer",
                table: "assets");

            migrationBuilder.DropColumn(
                name: "MeterInterval",
                table: "assets");

            migrationBuilder.DropColumn(
                name: "MeterLabel",
                table: "assets");

            migrationBuilder.DropColumn(
                name: "OwnershipType",
                table: "assets");

            migrationBuilder.DropColumn(
                name: "PhysicalDescription",
                table: "assets");

            migrationBuilder.DropColumn(
                name: "RoomArea",
                table: "assets");

            migrationBuilder.DropColumn(
                name: "SiteId",
                table: "assets");

            migrationBuilder.DropColumn(
                name: "YearOfManufacture",
                table: "assets");
        }
    }
}
