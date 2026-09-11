using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb;

/// <summary>
/// Reconciles the model snapshot with the MRR snapshot entity already introduced by
/// AddTenantTimeZoneAndMrrSnapshots. This migration intentionally has no SQL operations.
/// </summary>
public partial class ReconcileMrrSnapshotModel : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
