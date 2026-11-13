using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Infrastructure.Services.Factory;

public interface IFactorySettingsService
{
    /// <summary>Returns the tenant's settings, creating the tea defaults the first time they are asked for.</summary>
    Task<FactorySettings> GetOrCreateAsync(Guid tenantId, CancellationToken cancellationToken = default);
}

public sealed class FactorySettingsService(AppDbContext dbContext) : IFactorySettingsService
{
    public async Task<FactorySettings> GetOrCreateAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var settings = await dbContext.FactorySettings.SingleOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken);
        if (settings is not null)
        {
            if (settings.OperatorReportKey is null)
            {
                settings.OperatorReportKey = NewReportKey();
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            return settings;
        }

        var template = IndustryTemplates.Default;
        settings = new FactorySettings
        {
            TenantId = tenantId,
            IndustryTemplate = template.Key,
            ProductionUnit = template.ProductionUnit,
            ProductionUnitShort = template.ProductionUnitShort,
            OperatorReportKey = NewReportKey()
        };

        dbContext.FactorySettings.Add(settings);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Another request created them first (unique index on TenantId); use theirs.
            dbContext.Entry(settings).State = EntityState.Detached;
            settings = await dbContext.FactorySettings.SingleAsync(x => x.TenantId == tenantId, cancellationToken);
        }

        return settings;
    }

    /// <summary>A URL-safe random key; 18 bytes is 144 bits, far beyond guessing.</summary>
    public static string NewReportKey() =>
        Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(18)).Replace('+', '-').Replace('/', '_');
}
