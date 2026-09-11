using Ecosys.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Ecosys.Tests;

public sealed class MigrationDiscoveryTests
{
    [Fact]
    public void FinanceEmailReliability_IsIncludedInAppDbUpgradeScript()
    {
        // Script generation uses the real PostgreSQL migration provider without opening a connection.
        using var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=migration_discovery;Username=unused;Password=unused")
            .Options);

        const string migrationId = "20260908080000_ServiceOpsFinanceEmailReliability";
        Assert.Contains(migrationId, context.Database.GetMigrations());

        var script = context.GetService<IMigrator>().GenerateScript(
            "20260905061718_ServiceOpsProvisioningIdentityInvitations", migrationId);

        Assert.Contains("ALTER TABLE email_outbox_messages ADD \"AttachmentsJson\" text", script);
        Assert.Contains("ALTER TABLE email_delivery_logs ADD \"DocumentId\" uuid", script);
        Assert.Contains("CREATE UNIQUE INDEX \"IX_email_outbox_messages_IdempotencyKey\"", script);
        Assert.Contains(migrationId, script);
    }
}
