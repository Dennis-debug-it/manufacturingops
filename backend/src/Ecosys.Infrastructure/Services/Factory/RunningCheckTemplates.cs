using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Infrastructure.Services.Factory;

/// <summary>
/// Running-check templates (scope Phase 2): what an operator or artisan checks on each shift or day.
/// The checks themselves come from each industry pack (<see cref="IndustryTemplate.RunningChecks"/>);
/// limits are typical starting values each factory adjusts.
/// </summary>
public static class RunningCheckTemplates
{
    public const string WorkType = "RunningCheck";
    public const string Category = "Running checks";

    /// <summary>The frequency a template is meant for, for suggesting it when a schedule is set up.</summary>
    public static string? SuggestedFrequency(string templateName) =>
        IndustryTemplates.All.SelectMany(x => x.RunningChecks)
            .FirstOrDefault(x => templateName == x.Name || templateName.StartsWith(x.Name + " (", StringComparison.Ordinal))?.Frequency;

    /// <summary>
    /// Adds the running-check templates of every industry the tenant uses that it doesn't have yet. A check
    /// another industry already has under the same name (a boiler shift log) gets the industry's name added.
    /// </summary>
    public static async Task<int> InstallAsync(AppDbContext dbContext, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.PmTemplates.Where(x => x.TenantId == tenantId && x.WorkType == WorkType)
            .Select(x => new { x.Name, x.AssetCategoryId }).ToListAsync(cancellationToken);
        var classes = await dbContext.AssetCategories.Where(x => x.TenantId == tenantId && x.Code != null).ToDictionaryAsync(x => x.Code!, x => x.Id, cancellationToken);
        var industries = await IndustryTemplates.InUseAsync(dbContext, tenantId, cancellationToken);

        var created = 0;
        foreach (var (industry, template) in industries.SelectMany(i => i.RunningChecks.Select(c => (i, c))))
        {
            Guid? classId = classes.TryGetValue(template.AssetClassCode, out var id) ? id : null;
            if (existing.Any(x => x.AssetCategoryId == classId && (string.Equals(x.Name, template.Name, StringComparison.OrdinalIgnoreCase)
                    || x.Name.StartsWith(template.Name + " (", StringComparison.OrdinalIgnoreCase))))
                continue;
            var name = existing.Any(x => string.Equals(x.Name, template.Name, StringComparison.OrdinalIgnoreCase)) ? $"{template.Name} ({industry.Name})" : template.Name;
            existing.Add(new { Name = name, AssetCategoryId = classId });
            var entity = new PmTemplate
            {
                TenantId = tenantId,
                Name = name,
                Category = Category,
                WorkType = WorkType,
                AssetCategoryId = classId,
                Description = template.Frequency == CheckFrequencies.Daily ? "Daily running check" : "Running check, every shift",
                Version = 1,
                IsActive = true
            };
            var section = new PmTemplateSection { PmTemplateId = entity.Id, SectionName = "Checks", DisplayOrder = 1 };
            entity.Sections.Add(section);
            foreach (var (question, index) in template.Questions.Select((q, i) => (q, i)))
            {
                entity.Questions.Add(new PmTemplateQuestion
                {
                    PmTemplateId = entity.Id,
                    Section = section,
                    SectionName = section.SectionName,
                    Prompt = question.Prompt,
                    ResponseType = question.Type,
                    Unit = question.Unit,
                    MinValue = question.Min,
                    MaxValue = question.Max,
                    IsRequired = question.Required,
                    RequiresNoteOnFail = question.FailAction != CheckFailActions.None,
                    FailAction = question.FailAction,
                    FeedsMeterType = question.Meter,
                    DisplayOrder = index + 1,
                    IsActive = true
                });
            }

            dbContext.PmTemplates.Add(entity);
            created++;
        }

        if (created > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return created;
    }
}
