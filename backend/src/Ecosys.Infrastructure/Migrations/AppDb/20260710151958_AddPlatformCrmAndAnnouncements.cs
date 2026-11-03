using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class AddPlatformCrmAndAnnouncements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "crm_contacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FullName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CompanyName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Role = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    LeadId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_crm_contacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_crm_contacts_platform_leads_LeadId",
                        column: x => x.LeadId,
                        principalTable: "platform_leads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "platform_announcements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Body = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    Audience = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_platform_announcements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "crm_activities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Summary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ContactId = table.Column<Guid>(type: "uuid", nullable: true),
                    LeadId = table.Column<Guid>(type: "uuid", nullable: true),
                    LoggedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    LoggedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_crm_activities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_crm_activities_crm_contacts_ContactId",
                        column: x => x.ContactId,
                        principalTable: "crm_contacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_crm_activities_platform_leads_LeadId",
                        column: x => x.LeadId,
                        principalTable: "platform_leads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "crm_tasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Priority = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RelatedContactId = table.Column<Guid>(type: "uuid", nullable: true),
                    RelatedLeadId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssignedToUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssignedToName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_crm_tasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_crm_tasks_crm_contacts_RelatedContactId",
                        column: x => x.RelatedContactId,
                        principalTable: "crm_contacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_crm_tasks_platform_leads_RelatedLeadId",
                        column: x => x.RelatedLeadId,
                        principalTable: "platform_leads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_crm_activities_ContactId",
                table: "crm_activities",
                column: "ContactId");

            migrationBuilder.CreateIndex(
                name: "IX_crm_activities_LeadId",
                table: "crm_activities",
                column: "LeadId");

            migrationBuilder.CreateIndex(
                name: "IX_crm_contacts_LeadId",
                table: "crm_contacts",
                column: "LeadId");

            migrationBuilder.CreateIndex(
                name: "IX_crm_tasks_RelatedContactId",
                table: "crm_tasks",
                column: "RelatedContactId");

            migrationBuilder.CreateIndex(
                name: "IX_crm_tasks_RelatedLeadId",
                table: "crm_tasks",
                column: "RelatedLeadId");

            migrationBuilder.CreateIndex(
                name: "IX_crm_tasks_Status",
                table: "crm_tasks",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_platform_announcements_Status",
                table: "platform_announcements",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "crm_activities");

            migrationBuilder.DropTable(
                name: "crm_tasks");

            migrationBuilder.DropTable(
                name: "platform_announcements");

            migrationBuilder.DropTable(
                name: "crm_contacts");
        }
    }
}
