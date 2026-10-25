using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class AllowLeaveForStaffWithoutTechnicianProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "TechnicianId",
                table: "leave_requests",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "leave_requests",
                type: "uuid",
                nullable: true);

            // Existing leave belongs to technicians; record the user account behind each one too.
            migrationBuilder.Sql("""
                UPDATE leave_requests AS l
                SET "UserId" = t."UserId"
                FROM technicians AS t
                WHERE l."TechnicianId" = t."Id" AND t."UserId" IS NOT NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_leave_requests_TenantId_UserId",
                table: "leave_requests",
                columns: new[] { "TenantId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_leave_requests_UserId",
                table: "leave_requests",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_leave_requests_users_UserId",
                table: "leave_requests",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_leave_requests_users_UserId",
                table: "leave_requests");

            migrationBuilder.DropIndex(
                name: "IX_leave_requests_TenantId_UserId",
                table: "leave_requests");

            migrationBuilder.DropIndex(
                name: "IX_leave_requests_UserId",
                table: "leave_requests");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "leave_requests");

            // Leave from staff without a technician profile can't be kept once a technician is required.
            migrationBuilder.Sql("DELETE FROM leave_requests WHERE \"TechnicianId\" IS NULL;");

            migrationBuilder.AlterColumn<Guid>(
                name: "TechnicianId",
                table: "leave_requests",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
