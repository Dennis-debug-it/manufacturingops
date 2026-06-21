using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class StabilizeServiceOpsFlows : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AssignedTechnicianIdsJson",
                table: "work_orders",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AssignmentType",
                table: "work_orders",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "LeadTechnicianId",
                table: "work_orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_work_orders_LeadTechnicianId",
                table: "work_orders",
                column: "LeadTechnicianId");

            migrationBuilder.AddForeignKey(
                name: "FK_work_orders_technicians_LeadTechnicianId",
                table: "work_orders",
                column: "LeadTechnicianId",
                principalTable: "technicians",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_work_orders_technicians_LeadTechnicianId",
                table: "work_orders");

            migrationBuilder.DropIndex(
                name: "IX_work_orders_LeadTechnicianId",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "AssignedTechnicianIdsJson",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "AssignmentType",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "LeadTechnicianId",
                table: "work_orders");
        }
    }
}
