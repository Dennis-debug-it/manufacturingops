using Ecosys.Domain.Entities;
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
    /// <param name="JobTemplate">The work order template created alongside the checklist, so it can be picked
    /// straight away in New Work Order (that form offers work order templates, which carry the checklist).</param>
    private sealed record Definition(string Key, string Code, string Name, string Description, Func<string?, bool> IsForTenant, Func<PmTemplateWriteCommand> Build, JobTemplateDefaults JobTemplate);

    private sealed record JobTemplateDefaults(string Name, string JobType, string Description, string Priority);

    private static readonly IReadOnlyList<Definition> Catalog =
    [
        new(
            SolarInstallationTemplate.LayoutKey,
            SolarInstallationTemplate.Code,
            SolarInstallationTemplate.Name,
            "Plexus Energy's Solar Power Installation Report: on-site checklist plus their report layout (PDF to client, editable Word download).",
            SolarInstallationTemplate.IsForTenant,
            SolarInstallationTemplate.BuildCommand,
            new("Solar PV Installation", "NewInstallation", "Installation, testing and commissioning of a solar PV system.", "Medium")),
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
        if (!definition.IsForTenant(tenant.Slug))
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
        await EnsureJobTemplateAsync(tenantId, template.Id, definition.JobTemplate, cancellationToken);
        return new ClientTemplateInstallResult(template.Id, template.Name, template.Version, Created: true);
    }

    private async Task EnsureJobTemplateAsync(Guid tenantId, Guid checklistTemplateId, JobTemplateDefaults defaults, CancellationToken cancellationToken)
    {
        var sameName = await dbContext.WorkOrderTemplates
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Name == defaults.Name, cancellationToken);
        if (sameName is not null)
        {
            // Names are unique per workspace; reuse theirs, but never repoint one already tied to another checklist.
            if (sameName.ChecklistTemplateId is null)
            {
                sameName.ChecklistTemplateId = checklistTemplateId;
                sameName.IsActive = true;
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            return;
        }

        dbContext.WorkOrderTemplates.Add(new WorkOrderTemplate
        {
            TenantId = tenantId,
            Name = defaults.Name,
            JobType = defaults.JobType,
            Description = defaults.Description,
            DefaultPriority = defaults.Priority,
            ChecklistTemplateId = checklistTemplateId,
            IsActive = true,
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private List<Definition> ForTenant((string Slug, string Name) tenant) =>
        Catalog.Where(x => x.IsForTenant(tenant.Slug)).ToList();

    private async Task<(string Slug, string Name)> LoadTenantAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var tenant = await dbContext.Tenants
            .IgnoreQueryFilters()
            .Where(x => x.Id == tenantId)
            .Select(x => new { x.Slug, x.Name })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Tenant was not found.");
        return (tenant.Slug, tenant.Name);
    }
}
