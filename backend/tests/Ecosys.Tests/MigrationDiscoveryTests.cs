using Ecosys.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Ecosys.Tests;

public sealed class MigrationDiscoveryTests
{
    private static AppDbContext Context() => new(new DbContextOptionsBuilder<AppDbContext>()
        // Script generation uses the real PostgreSQL migration provider without opening a connection.
        .UseNpgsql("Host=localhost;Database=migration_discovery;Username=unused;Password=unused")
        .Options);

    [Fact]
    public void TheBaselineIsTheFirstMigration()
    {
        using var context = Context();
        Assert.Equal(MigrationBaseline.BaselineMigrationId, context.Database.GetMigrations().First());
    }

    [Fact]
    public void TheBaselineCreatesTheFactoryAndCoreTables()
    {
        using var context = Context();
        var script = context.GetService<IMigrator>().GenerateScript(null, MigrationBaseline.BaselineMigrationId);

        foreach (var table in new[] { "tenants", "users", "org_nodes", "work_orders", "assets", "material_items", "contractors", "maintenance_agreements" })
            Assert.Contains($"CREATE TABLE {table} (", script);
        Assert.Contains("CREATE UNIQUE INDEX \"IX_sites_TenantId_SiteCode\"", script);
        Assert.Contains("CREATE INDEX \"IX_work_orders_TenantId_Status_CreatedAt\"", script);
    }
}
