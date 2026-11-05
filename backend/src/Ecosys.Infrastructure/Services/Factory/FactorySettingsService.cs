using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Infrastructure.Services.Factory;

public interface IFactorySettingsService
{
    /// <summary>Returns the tenant's settings, creating the tea defaults the first time they are asked for.</summary>
    Task<FactorySettings> GetOrCreateAsync(Guid tenantId, CancellationToken cancellationToken = default);
}

internal sealed class FactorySettingsService(AppDbContext dbContext) : IFactorySettingsService
{
    public async Task<FactorySettings> GetOrCreateAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var settings = await dbContext.FactorySettings.SingleOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken);
        if (settings is not null)
        {
            return settings;
        }

        var template = IndustryTemplates.Tea;
        settings = new FactorySettings
        {
            TenantId = tenantId,
            IndustryTemplate = template.Key,
            ProductionUnit = template.ProductionUnit,
            ProductionUnitShort = template.ProductionUnitShort
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
}
