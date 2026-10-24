using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class AddCalendarActivitiesAndWhatsApp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AllowedNotificationKeys",
                table: "whatsapp_settings",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WhatsAppNumber",
                table: "users",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "WhatsAppOptedInAt",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "WhatsAppOptedOutAt",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "WhatsAppVerifiedAt",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "WhatsAppEnabled",
                table: "tenant_licenses",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ReceivesWhatsApp",
                table: "client_portal_users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "WhatsAppConsentAt",
                table: "client_portal_users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WhatsAppConsentMethod",
                table: "client_portal_users",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WhatsAppConsentRecordedByUserId",
                table: "client_portal_users",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WhatsAppNumber",
                table: "client_portal_users",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "WhatsAppVerifiedAt",
                table: "client_portal_users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "activities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    StartsAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndsAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AllDay = table.Column<bool>(type: "boolean", nullable: false),
                    Location = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    OrganizerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Agenda = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Minutes = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: true),
                    MinutesPublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    MinutesPublishedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReminderMinutesBefore = table.Column<int>(type: "integer", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancellationReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_activities", x => x.Id);
                    table.CheckConstraint("CK_activities_status", "\"Status\" IN ('Scheduled', 'Held', 'Cancelled')");
                    table.CheckConstraint("CK_activities_times", "\"EndsAt\" >= \"StartsAt\"");
                    table.CheckConstraint("CK_activities_type", "\"Type\" IN ('Meeting', 'SiteVisit', 'Inspection', 'Call')");
                    table.ForeignKey(
                        name: "FK_activities_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_activities_users_OrganizerUserId",
                        column: x => x.OrganizerUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "activity_reminder_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    NotificationKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ForTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_activity_reminder_logs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "calendar_feed_tokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastUsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_calendar_feed_tokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_calendar_feed_tokens_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_calendar_feed_tokens_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "whatsapp_opt_outs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PhoneNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    OptedOutAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Keyword = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_whatsapp_opt_outs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "whatsapp_outbox_messages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    NotificationKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    TemplateName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Language = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ParametersJson = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ToPhone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RecipientName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RecipientUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    RecipientPortalUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    EntityType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    MaxAttempts = table.Column<int>(type: "integer", nullable: false),
                    NextAttemptAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastAttemptAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeliveredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReadAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FailedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ProviderMessageId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IdempotencyKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_whatsapp_outbox_messages", x => x.Id);
                    table.CheckConstraint("CK_whatsapp_outbox_messages_status", "\"Status\" IN ('Pending', 'Sending', 'Sent', 'Delivered', 'Read', 'Failed', 'Skipped')");
                    table.ForeignKey(
                        name: "FK_whatsapp_outbox_messages_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "whatsapp_verification_codes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClientPortalUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    PhoneNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CodeHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    ConsumedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_whatsapp_verification_codes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_whatsapp_verification_codes_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "action_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActivityId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    AssigneeUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssigneeExternalName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    EntityType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_action_items", x => x.Id);
                    table.CheckConstraint("CK_action_items_status", "\"Status\" IN ('Open', 'Done', 'Cancelled')");
                    table.ForeignKey(
                        name: "FK_action_items_activities_ActivityId",
                        column: x => x.ActivityId,
                        principalTable: "activities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_action_items_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_action_items_users_AssigneeUserId",
                        column: x => x.AssigneeUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "activity_attendees",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActivityId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ConsultantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClientContactId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExternalName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ExternalEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Response = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Attended = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_activity_attendees", x => x.Id);
                    table.CheckConstraint("CK_activity_attendees_one_kind", "(CASE WHEN \"UserId\" IS NULL THEN 0 ELSE 1 END) + (CASE WHEN \"ConsultantId\" IS NULL THEN 0 ELSE 1 END) + (CASE WHEN \"ClientContactId\" IS NULL THEN 0 ELSE 1 END) + (CASE WHEN \"ExternalEmail\" IS NULL AND \"ExternalName\" IS NULL THEN 0 ELSE 1 END) = 1");
                    table.CheckConstraint("CK_activity_attendees_response", "\"Response\" IN ('Pending', 'Accepted', 'Declined')");
                    table.ForeignKey(
                        name: "FK_activity_attendees_activities_ActivityId",
                        column: x => x.ActivityId,
                        principalTable: "activities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_activity_attendees_client_portal_users_ClientContactId",
                        column: x => x.ClientContactId,
                        principalTable: "client_portal_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_activity_attendees_consultants_ConsultantId",
                        column: x => x.ConsultantId,
                        principalTable: "consultants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_activity_attendees_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_activity_attendees_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "activity_minutes_addenda",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActivityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Text = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    AuthorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_activity_minutes_addenda", x => x.Id);
                    table.ForeignKey(
                        name: "FK_activity_minutes_addenda_activities_ActivityId",
                        column: x => x.ActivityId,
                        principalTable: "activities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_activity_minutes_addenda_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_activity_minutes_addenda_users_AuthorUserId",
                        column: x => x.AuthorUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_action_items_ActivityId",
                table: "action_items",
                column: "ActivityId");

            migrationBuilder.CreateIndex(
                name: "IX_action_items_AssigneeUserId",
                table: "action_items",
                column: "AssigneeUserId");

            migrationBuilder.CreateIndex(
                name: "IX_action_items_Status_DueDate",
                table: "action_items",
                columns: new[] { "Status", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_action_items_TenantId_ActivityId",
                table: "action_items",
                columns: new[] { "TenantId", "ActivityId" });

            migrationBuilder.CreateIndex(
                name: "IX_action_items_TenantId_AssigneeUserId_Status",
                table: "action_items",
                columns: new[] { "TenantId", "AssigneeUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_action_items_TenantId_EntityType_EntityId",
                table: "action_items",
                columns: new[] { "TenantId", "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_activities_OrganizerUserId",
                table: "activities",
                column: "OrganizerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_activities_Status_StartsAt",
                table: "activities",
                columns: new[] { "Status", "StartsAt" });

            migrationBuilder.CreateIndex(
                name: "IX_activities_TenantId_EntityType_EntityId",
                table: "activities",
                columns: new[] { "TenantId", "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_activities_TenantId_OrganizerUserId_StartsAt",
                table: "activities",
                columns: new[] { "TenantId", "OrganizerUserId", "StartsAt" });

            migrationBuilder.CreateIndex(
                name: "IX_activities_TenantId_StartsAt",
                table: "activities",
                columns: new[] { "TenantId", "StartsAt" });

            migrationBuilder.CreateIndex(
                name: "IX_activity_attendees_ActivityId",
                table: "activity_attendees",
                column: "ActivityId");

            migrationBuilder.CreateIndex(
                name: "IX_activity_attendees_ClientContactId",
                table: "activity_attendees",
                column: "ClientContactId");

            migrationBuilder.CreateIndex(
                name: "IX_activity_attendees_ConsultantId",
                table: "activity_attendees",
                column: "ConsultantId");

            migrationBuilder.CreateIndex(
                name: "IX_activity_attendees_TenantId_ActivityId",
                table: "activity_attendees",
                columns: new[] { "TenantId", "ActivityId" });

            migrationBuilder.CreateIndex(
                name: "IX_activity_attendees_TenantId_UserId",
                table: "activity_attendees",
                columns: new[] { "TenantId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_activity_attendees_UserId",
                table: "activity_attendees",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_activity_minutes_addenda_ActivityId",
                table: "activity_minutes_addenda",
                column: "ActivityId");

            migrationBuilder.CreateIndex(
                name: "IX_activity_minutes_addenda_AuthorUserId",
                table: "activity_minutes_addenda",
                column: "AuthorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_activity_minutes_addenda_TenantId_ActivityId",
                table: "activity_minutes_addenda",
                columns: new[] { "TenantId", "ActivityId" });

            migrationBuilder.CreateIndex(
                name: "IX_activity_reminder_logs_CreatedAt",
                table: "activity_reminder_logs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_activity_reminder_logs_TenantId_EntityId_NotificationKey_Fo~",
                table: "activity_reminder_logs",
                columns: new[] { "TenantId", "EntityId", "NotificationKey", "ForTime" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_calendar_feed_tokens_TenantId_UserId",
                table: "calendar_feed_tokens",
                columns: new[] { "TenantId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_calendar_feed_tokens_TokenHash",
                table: "calendar_feed_tokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_calendar_feed_tokens_UserId",
                table: "calendar_feed_tokens",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_whatsapp_opt_outs_PhoneNumber",
                table: "whatsapp_opt_outs",
                column: "PhoneNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_whatsapp_outbox_messages_IdempotencyKey",
                table: "whatsapp_outbox_messages",
                column: "IdempotencyKey",
                unique: true,
                filter: "\"IdempotencyKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_whatsapp_outbox_messages_ProviderMessageId",
                table: "whatsapp_outbox_messages",
                column: "ProviderMessageId",
                unique: true,
                filter: "\"ProviderMessageId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_whatsapp_outbox_messages_Status_NextAttemptAt",
                table: "whatsapp_outbox_messages",
                columns: new[] { "Status", "NextAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "IX_whatsapp_outbox_messages_TenantId_CreatedAt",
                table: "whatsapp_outbox_messages",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_whatsapp_verification_codes_TenantId_ClientPortalUserId_Cre~",
                table: "whatsapp_verification_codes",
                columns: new[] { "TenantId", "ClientPortalUserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_whatsapp_verification_codes_TenantId_UserId_CreatedAt",
                table: "whatsapp_verification_codes",
                columns: new[] { "TenantId", "UserId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "action_items");

            migrationBuilder.DropTable(
                name: "activity_attendees");

            migrationBuilder.DropTable(
                name: "activity_minutes_addenda");

            migrationBuilder.DropTable(
                name: "activity_reminder_logs");

            migrationBuilder.DropTable(
                name: "calendar_feed_tokens");

            migrationBuilder.DropTable(
                name: "whatsapp_opt_outs");

            migrationBuilder.DropTable(
                name: "whatsapp_outbox_messages");

            migrationBuilder.DropTable(
                name: "whatsapp_verification_codes");

            migrationBuilder.DropTable(
                name: "activities");

            migrationBuilder.DropColumn(
                name: "AllowedNotificationKeys",
                table: "whatsapp_settings");

            migrationBuilder.DropColumn(
                name: "WhatsAppNumber",
                table: "users");

            migrationBuilder.DropColumn(
                name: "WhatsAppOptedInAt",
                table: "users");

            migrationBuilder.DropColumn(
                name: "WhatsAppOptedOutAt",
                table: "users");

            migrationBuilder.DropColumn(
                name: "WhatsAppVerifiedAt",
                table: "users");

            migrationBuilder.DropColumn(
                name: "WhatsAppEnabled",
                table: "tenant_licenses");

            migrationBuilder.DropColumn(
                name: "ReceivesWhatsApp",
                table: "client_portal_users");

            migrationBuilder.DropColumn(
                name: "WhatsAppConsentAt",
                table: "client_portal_users");

            migrationBuilder.DropColumn(
                name: "WhatsAppConsentMethod",
                table: "client_portal_users");

            migrationBuilder.DropColumn(
                name: "WhatsAppConsentRecordedByUserId",
                table: "client_portal_users");

            migrationBuilder.DropColumn(
                name: "WhatsAppNumber",
                table: "client_portal_users");

            migrationBuilder.DropColumn(
                name: "WhatsAppVerifiedAt",
                table: "client_portal_users");
        }
    }
}
