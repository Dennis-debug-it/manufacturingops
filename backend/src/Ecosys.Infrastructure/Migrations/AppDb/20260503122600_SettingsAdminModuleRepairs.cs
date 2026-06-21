using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class SettingsAdminModuleRepairs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EncryptedSecret",
                table: "email_settings",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsConfigured",
                table: "email_settings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsEnabled",
                table: "email_settings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "LastError",
                table: "email_settings",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastTestedAt",
                table: "email_settings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Provider",
                table: "email_settings",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ReplyToEmail",
                table: "email_settings",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "email_intake_settings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    IntakeEmailAddress = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    MailboxProvider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Host = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Port = table.Column<int>(type: "integer", nullable: false),
                    UseSsl = table.Column<bool>(type: "boolean", nullable: false),
                    Username = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    EncryptedPassword = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    DefaultClientId = table.Column<Guid>(type: "uuid", nullable: true),
                    DefaultBranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    DefaultAssignmentGroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    DefaultPriority = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreateWorkOrderFromUnknownSender = table.Column<bool>(type: "boolean", nullable: false),
                    SubjectParsingRules = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    AllowedSenderDomains = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    LastCheckedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsConnectionHealthy = table.Column<bool>(type: "boolean", nullable: false),
                    LastError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
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
                name: "monitoring_webhook_integrations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    ToolType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    EndpointSlug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SecretHash = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    DefaultClientId = table.Column<Guid>(type: "uuid", nullable: true),
                    DefaultAssetId = table.Column<Guid>(type: "uuid", nullable: true),
                    DefaultBranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    DefaultAssignmentGroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    DefaultPriority = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreateWorkOrderOnAlert = table.Column<bool>(type: "boolean", nullable: false),
                    PayloadMappingJson = table.Column<string>(type: "character varying(16000)", maxLength: 16000, nullable: true),
                    LastReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastStatus = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LastError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_monitoring_webhook_integrations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_monitoring_webhook_integrations_assets_DefaultAssetId",
                        column: x => x.DefaultAssetId,
                        principalTable: "assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_monitoring_webhook_integrations_assignment_groups_DefaultAs~",
                        column: x => x.DefaultAssignmentGroupId,
                        principalTable: "assignment_groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_monitoring_webhook_integrations_branches_DefaultBranchId",
                        column: x => x.DefaultBranchId,
                        principalTable: "branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_monitoring_webhook_integrations_clients_DefaultClientId",
                        column: x => x.DefaultClientId,
                        principalTable: "clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_monitoring_webhook_integrations_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

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
                name: "IX_monitoring_webhook_integrations_DefaultAssetId",
                table: "monitoring_webhook_integrations",
                column: "DefaultAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_monitoring_webhook_integrations_DefaultAssignmentGroupId",
                table: "monitoring_webhook_integrations",
                column: "DefaultAssignmentGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_monitoring_webhook_integrations_DefaultBranchId",
                table: "monitoring_webhook_integrations",
                column: "DefaultBranchId");

            migrationBuilder.CreateIndex(
                name: "IX_monitoring_webhook_integrations_DefaultClientId",
                table: "monitoring_webhook_integrations",
                column: "DefaultClientId");

            migrationBuilder.CreateIndex(
                name: "IX_monitoring_webhook_integrations_EndpointSlug",
                table: "monitoring_webhook_integrations",
                column: "EndpointSlug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_monitoring_webhook_integrations_TenantId_Name",
                table: "monitoring_webhook_integrations",
                columns: new[] { "TenantId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "email_intake_settings");

            migrationBuilder.DropTable(
                name: "monitoring_webhook_integrations");

            migrationBuilder.DropColumn(
                name: "EncryptedSecret",
                table: "email_settings");

            migrationBuilder.DropColumn(
                name: "IsConfigured",
                table: "email_settings");

            migrationBuilder.DropColumn(
                name: "IsEnabled",
                table: "email_settings");

            migrationBuilder.DropColumn(
                name: "LastError",
                table: "email_settings");

            migrationBuilder.DropColumn(
                name: "LastTestedAt",
                table: "email_settings");

            migrationBuilder.DropColumn(
                name: "Provider",
                table: "email_settings");

            migrationBuilder.DropColumn(
                name: "ReplyToEmail",
                table: "email_settings");
        }
    }
}
