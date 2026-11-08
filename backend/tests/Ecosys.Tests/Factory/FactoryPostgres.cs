using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Auth;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Ecosys.Tests.Factory;

/// <summary>
/// A throwaway PostgreSQL database built by the migrations, for checking that queries translate.
/// Needs a local server (ECOSYS_TEST_DB_CONNECTION, default localhost:5432 postgres/postgres).
/// </summary>
internal static class FactoryPostgres
{
    public static async Task RunAsync(string prefix, Func<string, Task> body)
    {
        var server = Environment.GetEnvironmentVariable("ECOSYS_TEST_DB_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=ecosys_test;Username=postgres;Password=postgres";
        var databaseName = $"ecosys_{prefix}_{Guid.NewGuid():N}";
        var database = new NpgsqlConnectionStringBuilder(server) { Database = databaseName, Pooling = false }.ConnectionString;
        var admin = new NpgsqlConnectionStringBuilder(server) { Database = "postgres", Pooling = false }.ConnectionString;
        await ExecuteAsync(admin, $"CREATE DATABASE \"{databaseName}\"");
        try
        {
            await using (var setup = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(database).Options))
            {
                await setup.Database.MigrateAsync();
            }

            await body(database);
        }
        finally
        {
            await ExecuteAsync(admin, $"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)");
        }
    }

    /// <summary>A tenant, an admin and a context for them, saved.</summary>
    public static async Task<(AppDbContext Db, FactoryTenantContext Context)> SeedAsync(string connection)
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var context = new FactoryTenantContext(tenantId, userId);
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options, context);
        db.Tenants.Add(new Tenant { Id = tenantId, Name = "Kericho", Slug = $"k-{Guid.NewGuid():N}"[..12], CompanyName = "Kericho Tea", Email = "ops@k.test", Country = "Kenya", IsActive = true });
        db.Users.Add(new User { Id = userId, TenantId = tenantId, FullName = "Admin", Email = $"a-{Guid.NewGuid():N}@k.test", Role = AppRoles.Admin, PasswordHash = string.Empty, IsActive = true });
        await db.SaveChangesAsync();
        return (db, context);
    }

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}
