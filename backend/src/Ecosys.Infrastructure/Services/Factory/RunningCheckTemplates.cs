using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Infrastructure.Services.Factory;

internal sealed record CheckQuestionTemplate(
    string Prompt,
    string Type,
    string FailAction = CheckFailActions.None,
    string? Unit = null,
    decimal? Min = null,
    decimal? Max = null,
    string? Meter = null,
    bool Required = true);

internal sealed record CheckTemplate(string Name, string AssetClassCode, string Frequency, IReadOnlyList<CheckQuestionTemplate> Questions);

/// <summary>
/// The tea factory running-check templates (scope Phase 2): what an operator or artisan checks
/// on each shift or day. Limits are typical starting values; each factory adjusts them.
/// </summary>
public static class RunningCheckTemplates
{
    public const string WorkType = "RunningCheck";
    public const string Category = "Running checks";

    private const string WO = CheckFailActions.WorkOrder;
    private const string Window = CheckFailActions.WindowDefect;

    internal static readonly IReadOnlyList<CheckTemplate> Tea =
    [
        new("Withering fan shift check", "TEA-WFN", CheckFrequencies.PerShift,
        [
            new("Fan running without abnormal noise", "passfail", WO),
            new("No abnormal vibration", "passfail", WO),
            new("Belt tension and guard in place", "passfail", Window),
            new("Motor current", "reading", Unit: "A", Required: false),
        ]),
        new("CTC machine shift check", "TEA-CTC", CheckFrequencies.PerShift,
        [
            new("Roller gap set correctly", "passfail", Window),
            new("No abnormal noise or knocking", "passfail", WO),
            new("Bearing temperature", "reading", WO, "°C", Max: 80),
            new("Lubrication done", "yesno"),
            new("Leaf processed this shift", "reading", Unit: "kg", Meter: MeterTypes.Production, Required: false),
        ]),
        new("Fluid bed dryer shift check", "TEA-FBD", CheckFrequencies.PerShift,
        [
            new("Inlet air temperature", "reading", WO, "°C", 100, 140, MeterTypes.Temperature),
            new("Exhaust temperature", "reading", Unit: "°C", Required: false),
            new("Bed fluidising evenly", "passfail", WO),
            new("Fire detection and spark arrestor working", "passfail", WO),
            new("Dryer run hours", "reading", Unit: "h", Meter: MeterTypes.RunHours, Required: false),
        ]),
        new("Boiler shift log", "TEA-BLR", CheckFrequencies.PerShift,
        [
            new("Steam pressure", "reading", WO, "bar", 6, 10),
            new("Water level gauge glass normal", "passfail", WO),
            new("Blowdown done", "yesno"),
            new("Feed water TDS", "reading", Window, "ppm", Max: 3500, Required: false),
            new("Safety valve lever test", "passfail", WO),
            new("Fuel (firewood) stock enough for next shift", "yesno"),
            new("Boiler run hours", "reading", Unit: "h", Meter: MeterTypes.RunHours, Required: false),
        ]),
        new("Compressor daily check", "TEA-CMP", CheckFrequencies.Daily,
        [
            new("Discharge pressure", "reading", WO, "bar", 6, 8),
            new("Oil level correct", "passfail", Window),
            new("Condensate drained", "yesno"),
            new("Compressor run hours", "reading", Unit: "h", Meter: MeterTypes.RunHours, Required: false),
        ]),
        new("Generator daily check", "TEA-GEN", CheckFrequencies.Daily,
        [
            new("Fuel level", "reading", WO, "%", Min: 25),
            new("Battery voltage", "reading", WO, "V", 24, 28.5m),
            new("Coolant level correct", "passfail", WO),
            new("Engine oil level correct", "passfail", WO),
            new("Generator run hours", "reading", Unit: "h", Meter: MeterTypes.RunHours, Required: false),
        ]),
    ];

    /// <summary>The frequency a template is meant for, for suggesting it when a schedule is set up.</summary>
    public static string? SuggestedFrequency(string templateName) =>
        Tea.FirstOrDefault(x => x.Name == templateName)?.Frequency;

    /// <summary>Adds the tea running-check templates the tenant doesn't have yet (matched by name).</summary>
    public static async Task<int> InstallAsync(AppDbContext dbContext, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var existing = (await dbContext.PmTemplates.Where(x => x.TenantId == tenantId && x.WorkType == WorkType).Select(x => x.Name).ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var classes = await dbContext.AssetCategories.Where(x => x.TenantId == tenantId && x.Code != null).ToDictionaryAsync(x => x.Code!, x => x.Id, cancellationToken);

        var created = 0;
        foreach (var template in Tea.Where(x => !existing.Contains(x.Name)))
        {
            var entity = new PmTemplate
            {
                TenantId = tenantId,
                Name = template.Name,
                Category = Category,
                WorkType = WorkType,
                AssetCategoryId = classes.TryGetValue(template.AssetClassCode, out var classId) ? classId : null,
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
