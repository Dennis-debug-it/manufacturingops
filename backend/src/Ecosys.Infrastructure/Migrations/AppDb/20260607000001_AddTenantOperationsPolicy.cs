using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosys.Infrastructure.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class AddTenantOperationsPolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS tenant_operations_policies (
                    "Id"                         uuid        NOT NULL DEFAULT gen_random_uuid(),
                    "TenantId"                   uuid        NOT NULL,
                    "RequireTechnicianSignature" boolean     NOT NULL DEFAULT true,
                    "RequireClientSignature"     boolean     NOT NULL DEFAULT true,
                    "AllowRemoteCompletion"      boolean     NOT NULL DEFAULT false,
                    "CreatedAt"                  timestamptz NOT NULL DEFAULT now(),
                    "UpdatedAt"                  timestamptz NULL,
                    CONSTRAINT pk_tenant_operations_policies PRIMARY KEY ("Id"),
                    CONSTRAINT fk_top_tenant FOREIGN KEY ("TenantId") REFERENCES tenants("Id") ON DELETE CASCADE
                );
                CREATE UNIQUE INDEX IF NOT EXISTS ix_top_tenant ON tenant_operations_policies ("TenantId");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TABLE IF EXISTS tenant_operations_policies;
                """);
        }
    }
}
