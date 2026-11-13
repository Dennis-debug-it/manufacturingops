using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Errors;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Infrastructure.Services.Factory;

/// <summary>
/// The standard weekly-window routines (scope Phase 3), from the factory's industry pack
/// (<see cref="IndustryTemplate.WindowRoutines"/>): for tea, lubrication, belt and chain tension, CTC
/// segments, dryer cleaning, fans, panels and boilers. Each is set up as a weekly line-stop PM plan on
/// the factory's matching assets, so it is raised ahead of every window and queued into it.
/// </summary>
public static class WindowRoutines
{
    public const string WorkType = "WindowRoutine";
    public const string Category = "Weekly window";

    /// <summary>
    /// Adds the routine checklists and, for one factory, a weekly line-stop PM plan per routine on
    /// its matching assets (a route when there are several). Routines the factory already has
    /// (by name) are left alone. Returns how many plans were added.
    /// </summary>
    public static async Task<int> InstallAsync(AppDbContext dbContext, IFactoryPmService pmService, Guid tenantId, Guid factoryId, DateTime firstDue, CancellationToken cancellationToken = default)
    {
        var industry = await IndustryTemplates.ForFactoryAsync(dbContext, tenantId, factoryId, cancellationToken);
        var templates = await EnsureTemplatesAsync(dbContext, tenantId, industry.WindowRoutines, cancellationToken);
        var subtree = await SubtreeAsync(dbContext, tenantId, factoryId, cancellationToken);
        var assets = await dbContext.Assets.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.ClientId == null && x.OrgNodeId != null && subtree.Contains(x.OrgNodeId.Value)
                && x.Status != "Inactive" && x.AssetCategory != null && x.AssetCategory.Code != null && x.ParentAssetId == null)
            .Select(x => new { x.Id, x.AssetCode, Code = x.AssetCategory!.Code! })
            .OrderBy(x => x.AssetCode)
            .ToListAsync(cancellationToken);
        var existing = await dbContext.PreventiveMaintenancePlans
            .Where(x => x.TenantId == tenantId && x.OrgNodeId != null && subtree.Contains(x.OrgNodeId.Value) && x.Name != null)
            .Select(x => x.Name!).ToListAsync(cancellationToken);

        var added = 0;
        foreach (var routine in industry.WindowRoutines)
        {
            var matching = assets.Where(x => routine.AssetClassCodes.Contains(x.Code)).ToList();
            if (matching.Count == 0)
            {
                continue;
            }

            var groups = routine.Route ? [matching] : matching.Select(x => new[] { x }.ToList()).ToList();
            foreach (var group in groups)
            {
                var name = routine.Route ? routine.Name : $"{routine.Name} – {group[0].AssetCode}";
                if (existing.Contains(name, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                await pmService.SaveAsync(tenantId, null, new SaveFactoryPmPlan(
                    name, group[0].Id, templates[routine.Name], PmTriggerTypes.Calendar, PmFrequencyUnits.Weeks, 1, firstDue,
                    null, null, null, PmScheduleModes.Fixed, 0, 3, "Medium", routine.Route ? routine.Hours * Math.Max(1, group.Count / 3m) : routine.Hours, true,
                    "Weekly window routine", group.Count > 1 ? group.Select(x => x.Id).ToList() : null), cancellationToken);
                added++;
            }
        }

        return added;
    }

    private static async Task<Dictionary<string, Guid>> EnsureTemplatesAsync(AppDbContext dbContext, Guid tenantId, IReadOnlyList<RoutineTemplate> routines, CancellationToken cancellationToken)
    {
        var existing = await dbContext.PmTemplates.Where(x => x.TenantId == tenantId && x.WorkType == WorkType && x.DeletedAt == null)
            .ToDictionaryAsync(x => x.Name, x => x.Id, StringComparer.OrdinalIgnoreCase, cancellationToken);
        foreach (var routine in routines.Where(x => !existing.ContainsKey(x.Name)))
        {
            var template = new PmTemplate { TenantId = tenantId, Name = routine.Name, Category = Category, WorkType = WorkType, Description = "Weekly window routine", EstimatedDurationHours = routine.Hours, IsActive = true };
            foreach (var (step, index) in routine.Steps.Select((s, i) => (s, i)))
            {
                template.Questions.Add(new PmTemplateQuestion
                {
                    PmTemplateId = template.Id, Prompt = step.Prompt, ResponseType = step.Type, Unit = step.Unit,
                    IsRequired = step.Type != "text", DisplayOrder = index + 1, IsActive = true
                });
            }

            dbContext.PmTemplates.Add(template);
            existing[routine.Name] = template.Id;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return existing;
    }

    private static async Task<HashSet<Guid>> SubtreeAsync(AppDbContext dbContext, Guid tenantId, Guid rootId, CancellationToken cancellationToken)
    {
        var nodes = await dbContext.OrgNodes.AsNoTracking().Where(x => x.TenantId == tenantId).Select(x => new { x.Id, x.ParentId }).ToListAsync(cancellationToken);
        if (nodes.All(x => x.Id != rootId))
        {
            throw new NotFoundException("The factory was not found.");
        }

        var result = new HashSet<Guid> { rootId };
        bool grew;
        do
        {
            grew = false;
            foreach (var node in nodes.Where(x => x.ParentId is { } parent && result.Contains(parent) && !result.Contains(x.Id)))
            {
                result.Add(node.Id);
                grew = true;
            }
        }
        while (grew);
        return result;
    }
}
