using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class AddWorkOrderFollowUpLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "FollowUpOfWorkOrderId",
                table: "work_orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_work_orders_FollowUpOfWorkOrderId",
                table: "work_orders",
                column: "FollowUpOfWorkOrderId");

            migrationBuilder.AddForeignKey(
                name: "FK_work_orders_work_orders_FollowUpOfWorkOrderId",
                table: "work_orders",
                column: "FollowUpOfWorkOrderId",
                principalTable: "work_orders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_work_orders_work_orders_FollowUpOfWorkOrderId",
                table: "work_orders");

            migrationBuilder.DropIndex(
                name: "IX_work_orders_FollowUpOfWorkOrderId",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "FollowUpOfWorkOrderId",
                table: "work_orders");
        }
    }
}
