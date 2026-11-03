using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class AddWorkOrderTypeSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "WorkType",
                table: "work_orders",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "CorrectiveMaintenance");

            migrationBuilder.Sql("""
                UPDATE work_orders
                SET "WorkType" = 'PreventiveMaintenance'
                WHERE "IsPreventiveMaintenance" = TRUE;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WorkType",
                table: "work_orders");
        }
    }
}
