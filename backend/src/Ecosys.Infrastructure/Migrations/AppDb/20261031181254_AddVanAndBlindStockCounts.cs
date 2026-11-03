using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class AddVanAndBlindStockCounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsBlind",
                table: "stock_counts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "VanId",
                table: "stock_counts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "NeedsRecount",
                table: "stock_count_lines",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_stock_counts_VanId",
                table: "stock_counts",
                column: "VanId");

            migrationBuilder.AddForeignKey(
                name: "FK_stock_counts_vans_VanId",
                table: "stock_counts",
                column: "VanId",
                principalTable: "vans",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_stock_counts_vans_VanId",
                table: "stock_counts");

            migrationBuilder.DropIndex(
                name: "IX_stock_counts_VanId",
                table: "stock_counts");

            migrationBuilder.DropColumn(
                name: "IsBlind",
                table: "stock_counts");

            migrationBuilder.DropColumn(
                name: "VanId",
                table: "stock_counts");

            migrationBuilder.DropColumn(
                name: "NeedsRecount",
                table: "stock_count_lines");
        }
    }
}
