using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class AddAuditLogTenantCreatedAtIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // EC-015. Every read of the audit log — the tenant list endpoint, its CSV export, and the
            // platform view when it filters by tenant — is "WHERE TenantId = @t [AND CreatedAt range]
            // ORDER BY CreatedAt DESC". The pre-existing IX_audit_logs_TenantId is the FK index and
            // only narrows the tenant, leaving a sort of the tenant's whole history on every page
            // request. This composite serves the filter and the ordering together, so paging becomes
            // an index scan of one page's worth of rows. Postgres reads a btree backwards, so the
            // ascending index covers the DESC ordering without a separate descending index.
            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_TenantId_CreatedAt",
                table: "audit_logs",
                columns: new[] { "TenantId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_audit_logs_TenantId_CreatedAt",
                table: "audit_logs");
        }
    }
}
