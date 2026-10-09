using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Ecosys.Tests;

/// <summary>
/// First visit to a settings screen: two requests both find no settings row and both create one.
/// The loser must get the winner's row, not a "record with the same value already exists" error.
/// </summary>
public sealed class GetOrCreateRaceTests
{
    [PortalPostgresFact]
    public async Task GetOrCreate_WhenAnotherRequestCreatesTheRowFirst_ReturnsThatRow()
    {
        var connectionString = Environment.GetEnvironmentVariable("ECOSYS_TEST_DB_CONNECTION")!;
        var schema = "get_or_create_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(connectionString);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE SCHEMA {schema}", admin))
            await create.ExecuteNonQueryAsync();
        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(new NpgsqlConnectionStringBuilder(connectionString) { SearchPath = schema }.ConnectionString).Options;
            var tenant = new Tenant { IsActive = true };
            await using (var seed = new AppDbContext(options))
            {
                await seed.Database.ExecuteSqlRawAsync(seed.Database.GenerateCreateScript());
                seed.Tenants.Add(tenant);
                await seed.SaveChangesAsync();
            }

            await using var loser = new AppDbContext(options);
            var result = await loser.GetOrCreateAsync(
                loser.EmailIntakeSettings.Where(x => x.TenantId == tenant.Id),
                async () =>
                {
                    // The other request wins the race between our lookup and our insert.
                    await using var winner = new AppDbContext(options);
                    winner.EmailIntakeSettings.Add(NewSettings(tenant.Id, "winner"));
                    await winner.SaveChangesAsync();
                    return NewSettings(tenant.Id, "loser");
                },
                CancellationToken.None);

            Assert.Equal("winner", result.Host);
            await using var check = new AppDbContext(options);
            Assert.Equal(1, await check.EmailIntakeSettings.CountAsync(x => x.TenantId == tenant.Id));
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP SCHEMA {schema} CASCADE", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }

    private static EmailIntakeSetting NewSettings(Guid tenantId, string host) => new()
    {
        TenantId = tenantId,
        MailboxProvider = "IMAP",
        Host = host,
        Port = 993,
        DefaultPriority = "Medium",
    };
}
