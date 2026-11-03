using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class AddSalesMessagingSequencesAndViews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LeadEmailAddress",
                table: "tenant_sales_settings",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "WebFormEnabled",
                table: "tenant_sales_settings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "WebFormKey",
                table: "tenant_sales_settings",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AcceptedAt",
                table: "tenant_quotations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AcceptedByName",
                table: "tenant_quotations",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AcceptedByPortalUserId",
                table: "tenant_quotations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AcceptedByTitle",
                table: "tenant_quotations",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AcceptedFromIp",
                table: "tenant_quotations",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "sales_activities",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "LocationAccuracyMeters",
                table: "sales_activities",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "sales_activities",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SequenceEnrollmentId",
                table: "sales_activities",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "sales_saved_views",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Query = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    IsShared = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_saved_views", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "sales_sequences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    StopOnReply = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_sequences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "sales_sequence_enrollments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    SequenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    OpportunityId = table.Column<Guid>(type: "uuid", nullable: false),
                    NextStep = table.Column<int>(type: "integer", nullable: false),
                    NextDueAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    StoppedReason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    FinishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EnrolledByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_sequence_enrollments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sales_sequence_enrollments_sales_opportunities_OpportunityId",
                        column: x => x.OpportunityId,
                        principalTable: "sales_opportunities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_sales_sequence_enrollments_sales_sequences_SequenceId",
                        column: x => x.SequenceId,
                        principalTable: "sales_sequences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sales_sequence_steps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    SequenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    DelayDays = table.Column<int>(type: "integer", nullable: false),
                    Action = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Body = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_sequence_steps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sales_sequence_steps_sales_sequences_SequenceId",
                        column: x => x.SequenceId,
                        principalTable: "sales_sequences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tenant_sales_settings_WebFormKey",
                table: "tenant_sales_settings",
                column: "WebFormKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sales_saved_views_TenantId_UserId",
                table: "sales_saved_views",
                columns: new[] { "TenantId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_sales_sequence_enrollments_OpportunityId",
                table: "sales_sequence_enrollments",
                column: "OpportunityId");

            migrationBuilder.CreateIndex(
                name: "IX_sales_sequence_enrollments_SequenceId",
                table: "sales_sequence_enrollments",
                column: "SequenceId");

            migrationBuilder.CreateIndex(
                name: "IX_sales_sequence_enrollments_Status_NextDueAt",
                table: "sales_sequence_enrollments",
                columns: new[] { "Status", "NextDueAt" });

            migrationBuilder.CreateIndex(
                name: "IX_sales_sequence_enrollments_TenantId_OpportunityId",
                table: "sales_sequence_enrollments",
                columns: new[] { "TenantId", "OpportunityId" });

            migrationBuilder.CreateIndex(
                name: "IX_sales_sequence_steps_SequenceId",
                table: "sales_sequence_steps",
                column: "SequenceId");

            migrationBuilder.CreateIndex(
                name: "IX_sales_sequences_TenantId",
                table: "sales_sequences",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sales_saved_views");

            migrationBuilder.DropTable(
                name: "sales_sequence_enrollments");

            migrationBuilder.DropTable(
                name: "sales_sequence_steps");

            migrationBuilder.DropTable(
                name: "sales_sequences");

            migrationBuilder.DropIndex(
                name: "IX_tenant_sales_settings_WebFormKey",
                table: "tenant_sales_settings");

            migrationBuilder.DropColumn(
                name: "LeadEmailAddress",
                table: "tenant_sales_settings");

            migrationBuilder.DropColumn(
                name: "WebFormEnabled",
                table: "tenant_sales_settings");

            migrationBuilder.DropColumn(
                name: "WebFormKey",
                table: "tenant_sales_settings");

            migrationBuilder.DropColumn(
                name: "AcceptedAt",
                table: "tenant_quotations");

            migrationBuilder.DropColumn(
                name: "AcceptedByName",
                table: "tenant_quotations");

            migrationBuilder.DropColumn(
                name: "AcceptedByPortalUserId",
                table: "tenant_quotations");

            migrationBuilder.DropColumn(
                name: "AcceptedByTitle",
                table: "tenant_quotations");

            migrationBuilder.DropColumn(
                name: "AcceptedFromIp",
                table: "tenant_quotations");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "sales_activities");

            migrationBuilder.DropColumn(
                name: "LocationAccuracyMeters",
                table: "sales_activities");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "sales_activities");

            migrationBuilder.DropColumn(
                name: "SequenceEnrollmentId",
                table: "sales_activities");
        }
    }
}
