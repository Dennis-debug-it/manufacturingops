using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class CheckpointPhotos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "RequirePhoto",
                table: "checkpoints",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "PhotoAttachmentId",
                table: "checkpoint_scans",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PhotoRequired",
                table: "checkpoint_scans",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RequirePhoto",
                table: "checkpoints");

            migrationBuilder.DropColumn(
                name: "PhotoAttachmentId",
                table: "checkpoint_scans");

            migrationBuilder.DropColumn(
                name: "PhotoRequired",
                table: "checkpoint_scans");
        }
    }
}
