using Ecosys.Api.Controllers;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Ecosys.Tests;

/// <summary>
/// Audit TM-P1-08: licensed seats must hold under concurrency. EF InMemory has no transactions or row
/// locks, so this runs against real PostgreSQL — the same server the WebApplicationFactory tests use
/// (ECOSYS_TEST_DB_CONNECTION, default localhost:5432 postgres/postgres) — in a throwaway database.
/// </summary>
public sealed partial class AuthEmailSafetyTests
{
    [Fact]
    [Trait("Category", "PostgreSql")]
    public async Task Seats_ConcurrentPlatformAddsForTheLastSeat_OnlyOneSucceeds()
    {
        var serverConnection = Environment.GetEnvironmentVariable("ECOSYS_TEST_DB_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=ecosys_test;Username=postgres;Password=postgres";
        var databaseName = $"ecosys_seats_{Guid.NewGuid():N}";
        var databaseConnection = new NpgsqlConnectionStringBuilder(serverConnection) { Database = databaseName, Pooling = false }.ConnectionString;
        var adminConnection = new NpgsqlConnectionStringBuilder(serverConnection) { Database = "postgres", Pooling = false }.ConnectionString;

        await ExecuteAdminAsync(adminConnection, $"CREATE DATABASE \"{databaseName}\"");
        try
        {
            var tenantId = Guid.NewGuid();
            await using (var setup = CreatePostgresContext(databaseConnection))
            {
                await setup.Database.MigrateAsync();
                await SeedPlatformUserAsync(setup, "owner@platform.test");
                await SeedTenantAsync(setup, tenantId, "Race Co", "ops@race.test");
                var suite = await setup.LicensePlans.SingleAsync(x => x.PlanCode == ServiceOpsPlans.Suite);
                setup.TenantLicenses.Add(new TenantLicense
                {
                    TenantId = tenantId, LicensePlanId = suite.Id, Status = LicenseStatuses.Active,
                    StartsAt = DateTime.UtcNow.AddMonths(-1), ExpiresAt = DateTime.UtcNow.AddYears(1), LicensedUsers = 2,
                });
                setup.Users.Add(new User { TenantId = tenantId, FullName = "Admin", Email = "admin@race.test", Role = AppRoles.Admin, IsActive = true });
                await setup.SaveChangesAsync();
            }

            // Both requests see 1 of 2 seats used when they start; exactly one may take the last seat.
            var results = await Task.WhenAll(Enumerable.Range(0, 2).Select(async index =>
            {
                await using var db = CreatePostgresContext(databaseConnection);
                var controller = CreateAuditPlatformTenantsController(db, new LicenseGuardService(db));
                try
                {
                    await controller.CreateWorkspaceUser(
                        tenantId,
                        new CreatePlatformTenantUserRequest($"Racer {index}", $"racer{index}@race.test", null, "Viewer", null),
                        CancellationToken.None);
                    return "created";
                }
                catch (LicenseException ex) when (ex.ErrorCode == "license_users_limit_reached")
                {
                    return "rejected";
                }
            }));

            Assert.Equal(1, results.Count(x => x == "created"));
            Assert.Equal(1, results.Count(x => x == "rejected"));
            await using var verify = CreatePostgresContext(databaseConnection);
            Assert.Equal(2, await verify.Users.CountAsync(x => x.TenantId == tenantId && x.IsActive));
        }
        finally
        {
            await ExecuteAdminAsync(adminConnection, $"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)");
        }
    }

    private static AppDbContext CreatePostgresContext(string connectionString) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options);

    private static async Task ExecuteAdminAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}
