using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Errors;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Infrastructure.Reports;

public sealed record ClientTemplateSummary(string Key, string Name, string Description, Guid? InstalledTemplateId, int? InstalledVersion, bool? InstalledActive);
public sealed record ClientTemplateInstallResult(Guid TemplateId, string Name, int Version, bool Created);

/// <summary>
/// Checklist templates (with their own report layout) built for one client and installed into that
/// client's workspace only, unlike starter packs which every workspace can see. Installing again updates
/// the template in place, as a new version, so jobs already using it keep their history.
/// </summary>
public interface IClientTemplateInstaller
{
    Task<IReadOnlyCollection<ClientTemplateSummary>> ListAsync(Guid tenantId, CancellationToken cancellationToken);
    Task<ClientTemplateInstallResult> InstallAsync(Guid tenantId, string key, CancellationToken cancellationToken);
}

internal sealed class ClientTemplateInstaller(AppDbContext dbContext, IPmTemplateService pmTemplateService) : IClientTemplateInstaller
{
    private sealed record Definition(string Key, string Code, string Name, string Description, Func<PmTemplateWriteCommand> Build);

    private static readonly IReadOnlyList<Definition> Catalog =
    [
        new(
            SolarInstallationTemplate.LayoutKey,
            SolarInstallationTemplate.Code,
            SolarInstallationTemplate.Name,
            "Plexus Energy's Solar Power Installation Report: on-site checklist plus their report layout (PDF to client, editable Word download).",
            SolarInstallationTemplate.BuildCommand),
    ];

    public async Task<IReadOnlyCollection<ClientTemplateSummary>> ListAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        await EnsureTenantAsync(tenantId, cancellationToken);
        var codes = Catalog.Select(x => x.Code).ToList();
        var installed = await dbContext.PmTemplates
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId && x.DeletedAt == null && x.Code != null && codes.Contains(x.Code))
            .Select(x => new { x.Id, x.Code, x.Version, x.IsActive })
            .ToListAsync(cancellationToken);

        return Catalog.Select(def =>
        {
            var match = installed.FirstOrDefault(x => string.Equals(x.Code, def.Code, StringComparison.OrdinalIgnoreCase));
            return new ClientTemplateSummary(def.Key, def.Name, def.Description, match?.Id, match?.Version, match?.IsActive);
        }).ToList();
    }

    public async Task<ClientTemplateInstallResult> InstallAsync(Guid tenantId, string key, CancellationToken cancellationToken)
    {
        await EnsureTenantAsync(tenantId, cancellationToken);
        var definition = Catalog.FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase))
            ?? throw new NotFoundException("Client template was not found.");

        var existingId = await dbContext.PmTemplates
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId && x.DeletedAt == null && x.Code == definition.Code)
            .OrderByDescending(x => x.IsActive)
            .ThenByDescending(x => x.UpdatedAt ?? x.CreatedAt)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var template = existingId.HasValue
            ? await pmTemplateService.UpdateAsync(tenantId, existingId.Value, definition.Build(), cancellationToken)
            : await pmTemplateService.CreateAsync(tenantId, definition.Build(), cancellationToken);

        return new ClientTemplateInstallResult(template.Id, template.Name, template.Version, !existingId.HasValue);
    }

    private async Task EnsureTenantAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        if (!await dbContext.Tenants.IgnoreQueryFilters().AnyAsync(x => x.Id == tenantId, cancellationToken))
        {
            throw new NotFoundException("Tenant was not found.");
        }
    }
}
