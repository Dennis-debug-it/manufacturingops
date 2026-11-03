using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Ecosys.Infrastructure.Data;

public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var apiProjectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Ecosys.Api"));

        var configuration = new ConfigurationBuilder()
            .SetBasePath(apiProjectPath)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = Resolve(Environment.GetEnvironmentVariable("ECOSYS_DB_CONNECTION"))
            ?? Resolve(configuration.GetConnectionString("DefaultConnection"))
            ?? throw new InvalidOperationException(
                "Database connection string was not configured. Set ECOSYS_DB_CONNECTION or ConnectionStrings__DefaultConnection " +
                "to a valid Npgsql connection string (for example \"Host=localhost;Port=5432;Database=ecosys;Username=ecosys;Password=...\").");

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new AppDbContext(optionsBuilder.Options);
    }

    // Treats blank values and unsubstituted placeholders (e.g. "SET_IN_ENV" in appsettings.json)
    // as "not configured", so callers get an actionable message instead of an Npgsql parse failure.
    private static string? Resolve(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        if (string.Equals(trimmed, "SET_IN_ENV", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        // A usable Npgsql connection string is a semicolon-delimited list of key=value pairs.
        return trimmed.Contains('=') ? trimmed : null;
    }
}
