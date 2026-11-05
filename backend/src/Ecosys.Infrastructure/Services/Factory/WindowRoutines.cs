using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Errors;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Infrastructure.Services.Factory;

internal sealed record RoutineTemplate(string Name, IReadOnlyList<string> AssetClassCodes, bool Route, decimal Hours, IReadOnlyList<(string Prompt, string Type, string? Unit)> Steps);

/// <summary>
/// The standard weekly-window routines for a tea factory (scope Phase 3): lubrication, belt and
/// chain tension, CTC segment inspection, dryer and fluid bed cleaning, fan checks, panel
/// inspections and boiler checks. Each is set up as a weekly line-stop PM plan on the factory's
/// matching assets, so it is raised ahead of every window and queued into it.
/// </summary>
public static class WindowRoutines
{
    public const string WorkType = "WindowRoutine";
    public const string Category = "Weekly window";

    internal static readonly IReadOnlyList<RoutineTemplate> Tea =
    [
        new("Weekly lubrication round", ["TEA-CTC", "TEA-RV", "TEA-CFU", "TEA-CNV", "TEA-WFN", "TEA-SRT"], true, 3,
        [
            ("Grease bearings to the lubrication chart", "checkbox", null),
            ("Gearbox oil levels topped up", "checkbox", null),
            ("Chains oiled; no dry or seized links", "checkbox", null),
            ("Note any leaking seals", "text", null),
        ]),
        new("Belt and chain tension check", ["TEA-CNV", "TEA-WFN", "TEA-CTC", "TEA-RV"], true, 2,
        [
            ("Belts: tension and wear checked, guards refitted", "checkbox", null),
            ("Chains: tension and sprocket wear checked", "checkbox", null),
            ("Belts or chains to replace", "text", null),
        ]),
        new("CTC segment inspection", ["TEA-CTC"], false, 2,
        [
            ("Segment teeth: wear and chipping checked", "checkbox", null),
            ("Roller gap", "number", "mm"),
            ("Segments to re-cut or replace", "text", null),
        ]),
        new("Dryer and fluid bed cleaning", ["TEA-FBD"], false, 4,
        [
            ("Perforated bed and chambers cleaned", "checkbox", null),
            ("Cyclones and ducts cleared of fluff", "checkbox", null),
            ("Heat exchanger tubes inspected", "checkbox", null),
            ("Fan impellers clean and balanced", "checkbox", null),
        ]),
        new("Withering fan check", ["TEA-WFN"], true, 1.5m,
        [
            ("Blades and hub tight, no cracks", "checkbox", null),
            ("Motor mounting and cable glands secure", "checkbox", null),
            ("Motor current on trial run", "number", "A"),
        ]),
        new("Electrical panel inspection", ["TEA-TRF", "TEA-GEN", "TEA-MTR"], true, 2,
        [
            ("Panels dust-free and doors closing", "checkbox", null),
            ("No hot spots or burnt terminals", "checkbox", null),
            ("Earthing and covers in place", "checkbox", null),
        ]),
        new("Boiler weekly checks", ["TEA-BLR"], false, 3,
        [
            ("Water level gauges blown down and clear", "checkbox", null),
            ("Safety valves tested (lift)", "checkbox", null),
            ("Fire tubes and grate cleaned", "checkbox", null),
            ("Working pressure on restart", "number", "bar"),
        ]),
    ];

    /// <summary>
    /// Adds the routine checklists and, for one factory, a weekly line-stop PM plan per routine on
    /// its matching assets (a route when there are several). Routines the factory already has
    /// (by name) are left alone. Returns how many plans were added.
    /// </summary>
    public static async Task<int> InstallAsync(AppDbContext dbContext, IFactoryPmService pmService, Guid tenantId, Guid factoryId, DateTime firstDue, CancellationToken cancellationToken = default)
    {
        var templates = await EnsureTemplatesAsync(dbContext, tenantId, cancellationToken);
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
        foreach (var routine in Tea)
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

    private static async Task<Dictionary<string, Guid>> EnsureTemplatesAsync(AppDbContext dbContext, Guid tenantId, CancellationToken cancellationToken)
    {
        var existing = await dbContext.PmTemplates.Where(x => x.TenantId == tenantId && x.WorkType == WorkType && x.DeletedAt == null)
            .ToDictionaryAsync(x => x.Name, x => x.Id, StringComparer.OrdinalIgnoreCase, cancellationToken);
        foreach (var routine in Tea.Where(x => !existing.ContainsKey(x.Name)))
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
