using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class TeamManagementModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AvailabilityNote",
                table: "technicians",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AvailabilityStatus",
                table: "technicians",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "Available");

            migrationBuilder.AddColumn<DateTime>(
                name: "AvailabilityUntil",
                table: "technicians",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AvailabilityUpdatedAt",
                table: "technicians",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "certification_definitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Issuer = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    RenewalPeriodMonths = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_certification_definitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_certification_definitions_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_certifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CertificationDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CertificateNumber = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    IssuedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpiresOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_certifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_certifications_certification_definitions_Certification~",
                        column: x => x.CertificationDefinitionId,
                        principalTable: "certification_definitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_certifications_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_certifications_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_certification_definitions_TenantId_Name",
                table: "certification_definitions",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_certifications_CertificationDefinitionId",
                table: "user_certifications",
                column: "CertificationDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_user_certifications_TenantId_UserId_CertificationDefinition~",
                table: "user_certifications",
                columns: new[] { "TenantId", "UserId", "CertificationDefinitionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_certifications_UserId",
                table: "user_certifications",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_certifications");

            migrationBuilder.DropTable(
                name: "certification_definitions");

            migrationBuilder.DropColumn(
                name: "AvailabilityNote",
                table: "technicians");

            migrationBuilder.DropColumn(
                name: "AvailabilityStatus",
                table: "technicians");

            migrationBuilder.DropColumn(
                name: "AvailabilityUntil",
                table: "technicians");

            migrationBuilder.DropColumn(
                name: "AvailabilityUpdatedAt",
                table: "technicians");
        }
    }
}
