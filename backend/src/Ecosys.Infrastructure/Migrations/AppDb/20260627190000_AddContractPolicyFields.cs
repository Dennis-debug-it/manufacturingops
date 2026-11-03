using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    public partial class AddContractPolicyFields : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GracePeriodPolicy",
                table: "contracts",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "None");

            migrationBuilder.AddColumn<string>(
                name: "RenewalBehavior",
                table: "contracts",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "ManualRenewalRequired");

            migrationBuilder.AddColumn<string>(
                name: "SlaMatrixJson",
                table: "contracts",
                type: "text",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GracePeriodPolicy",
                table: "contracts");

            migrationBuilder.DropColumn(
                name: "RenewalBehavior",
                table: "contracts");

            migrationBuilder.DropColumn(
                name: "SlaMatrixJson",
                table: "contracts");
        }
    }
}
