using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class AddWorkOrderConcurrencyToken : Migration
    {
        // No-op: `xmin` is a PostgreSQL system column that already exists on every table. This
        // migration exists only to advance the model snapshot after mapping xmin as WorkOrder's
        // optimistic-concurrency token (F5-01); there is no schema change to apply.
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
