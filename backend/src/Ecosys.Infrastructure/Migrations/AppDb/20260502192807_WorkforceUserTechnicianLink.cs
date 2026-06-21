using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class WorkforceUserTechnicianLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "technicians",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_technicians_UserId",
                table: "technicians",
                column: "UserId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_technicians_users_UserId",
                table: "technicians",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_technicians_users_UserId",
                table: "technicians");

            migrationBuilder.DropIndex(
                name: "IX_technicians_UserId",
                table: "technicians");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "technicians");
        }
    }
}
