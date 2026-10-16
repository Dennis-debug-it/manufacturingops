using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Errors;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Infrastructure.Reports;

public sealed record ClientTemplateSummary(string Key, string Name, string Description, Guid? InstalledTemplateId, int? InstalledVersion, bool? InstalledActive);
public sealed record ClientTemplateInstallResult(Guid TemplateId, string Name, int Version, bool Created);

/// <summary>
/// Checklist templates (with their own report layout) built for one client and installed into that
/// client's workspace only, unlike starter packs which every workspace can see. Each template is locked
/// to its client's workspace and installs once; it can be installed again only after the workspace deletes it.
/// </summary>
public interface IClientTemplateInstaller
{
    Task<IReadOnlyCollection<ClientTemplateSummary>> ListAsync(Guid tenantId, CancellationToken cancellationToken);
    Task<ClientTemplateInstallResult> InstallAsync(Guid tenantId, string key, CancellationToken cancellationToken);
}

internal sealed class ClientTemplateInstaller(AppDbContext dbContext, IPmTemplateService pmTemplateService) : IClientTemplateInstaller
{
    private sealed record Definition(string Key, string Code, string Name, string Description, Func<string?, string?, bool> IsForTenant, Func<PmTemplateWriteCommand> Build);

    private static readonly IReadOnlyList<Definition> Catalog =
    [
        new(
            SolarInstallationTemplate.LayoutKey,
            SolarInstallationTemplate.Code,
            SolarInstallationTemplate.Name,
            "Plexus Energy's Solar Power Installation Report: on-site checklist plus their report layout (PDF to client, editable Word download).",
            SolarInstallationTemplate.IsForTenant,
            SolarInstallationTemplate.BuildCommand),
    ];

    public async Task<IReadOnlyCollection<ClientTemplateSummary>> ListAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var available = ForTenant(await LoadTenantAsync(tenantId, cancellationToken));
        if (available.Count == 0) return [];

        var codes = available.Select(x => x.Code).ToList();
        var installed = await dbContext.PmTemplates
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId && x.DeletedAt == null && x.Code != null && codes.Contains(x.Code))
            .Select(x => new { x.Id, x.Code, x.Version, x.IsActive })
            .ToListAsync(cancellationToken);

        return available.Select(def =>
        {
            var match = installed.FirstOrDefault(x => string.Equals(x.Code, def.Code, StringComparison.OrdinalIgnoreCase));
            return new ClientTemplateSummary(def.Key, def.Name, def.Description, match?.Id, match?.Version, match?.IsActive);
        }).ToList();
    }

    public async Task<ClientTemplateInstallResult> InstallAsync(Guid tenantId, string key, CancellationToken cancellationToken)
    {
        var tenant = await LoadTenantAsync(tenantId, cancellationToken);
        var definition = Catalog.FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase))
            ?? throw new NotFoundException("Client template was not found.");
        if (!definition.IsForTenant(tenant.Name, tenant.CompanyName))
        {
            throw new BusinessRuleException($"'{definition.Name}' is built for another client and can't be installed in this workspace.");
        }

        var alreadyInstalled = await dbContext.PmTemplates
            .IgnoreQueryFilters()
            .AnyAsync(x => x.TenantId == tenantId && x.DeletedAt == null && x.Code == definition.Code, cancellationToken);
        if (alreadyInstalled)
        {
            throw new ConflictException($"'{definition.Name}' is already installed in this workspace.");
        }

        var template = await pmTemplateService.CreateAsync(tenantId, definition.Build(), cancellationToken);
        return new ClientTemplateInstallResult(template.Id, template.Name, template.Version, Created: true);
    }

    private List<Definition> ForTenant((string Name, string CompanyName) tenant) =>
        Catalog.Where(x => x.IsForTenant(tenant.Name, tenant.CompanyName)).ToList();

    private async Task<(string Name, string CompanyName)> LoadTenantAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var tenant = await dbContext.Tenants
            .IgnoreQueryFilters()
            .Where(x => x.Id == tenantId)
            .Select(x => new { x.Name, x.CompanyName })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Tenant was not found.");
        return (tenant.Name, tenant.CompanyName);
    }
}
