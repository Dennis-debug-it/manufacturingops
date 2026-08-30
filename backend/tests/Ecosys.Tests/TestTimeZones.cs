using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Options;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Ecosys.Tests;

internal static class TestTimeZones
{
    /// <summary>The real resolver over a throwaway cache — tests exercise the same resolution chain as production.</summary>
    public static ITenantTimeZoneService Create(AppDbContext dbContext, string defaultTimeZone = "Africa/Nairobi") =>
        new TenantTimeZoneService(
            dbContext,
            new MemoryCache(new MemoryCacheOptions()),
            Options.Create(new TimeZoneOptions { DefaultTimeZone = defaultTimeZone }));
}
