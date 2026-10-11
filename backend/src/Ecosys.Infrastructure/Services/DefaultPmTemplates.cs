using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Infrastructure.Services;

/// <summary>
/// The four basic PM checklists every new workspace starts with (HVAC, Generator, UPS, Solar), whether
/// the company signed up itself or was created by the platform. Detailed trade checklists come from
/// starter packs. Adds nothing if the workspace already has PM templates.
/// </summary>
public static class DefaultPmTemplates
{
    public static async Task AddIfMissingAsync(AppDbContext dbContext, Guid tenantId, CancellationToken cancellationToken)
    {
        if (await dbContext.PmTemplates.AnyAsync(x => x.TenantId == tenantId, cancellationToken))
        {
            return;
        }

        var templates = new[]
        {
            CreatePmTemplate(tenantId, "HVAC", "HVAC PM Checklist",
            [
                ("Airflow", "Confirm supply and return airflow is unobstructed.", "yesno", true),
                ("Filters", "Inspect and clean or replace filters as needed.", "yesno", true),
                ("Refrigerant / Cooling", "Verify cooling performance is within expected range.", "yesno", true),
                ("Electrical", "Inspect contactors, terminals, and wiring for signs of overheating.", "yesno", true),
                ("Condensate Drain", "Check condensate drain line and tray for blockage or leaks.", "yesno", true),
                ("Recommendations", "Add technician recommendations for the client.", "text", false)
            ]),
            CreatePmTemplate(tenantId, "Generator", "Generator PM Checklist",
            [
                ("General Inspection", "Inspect generator enclosure and mounting condition.", "yesno", true),
                ("Fuel System", "Check fuel level, lines, and leaks.", "yesno", true),
                ("Battery System", "Test starter battery condition and terminals.", "yesno", true),
                ("Cooling System", "Inspect coolant level, hoses, and radiator condition.", "yesno", true),
                ("Running Test", "Perform running test and confirm stable output.", "passfail", true),
                ("Recommendations", "Capture follow-up actions or recommendations.", "text", false)
            ]),
            CreatePmTemplate(tenantId, "UPS", "UPS PM Checklist",
            [
                ("Visual Inspection", "Inspect UPS enclosure, indicators, and cable terminations.", "yesno", true),
                ("Electrical Checks", "Verify input and output voltages are within range.", "yesno", true),
                ("Battery Checks", "Check battery health, voltage, and corrosion signs.", "yesno", true),
                ("Alarms & Logs", "Review active alarms and event logs.", "yesno", true),
                ("Recommendations", "Document service recommendations.", "text", false)
            ]),
            CreatePmTemplate(tenantId, "Solar", "Solar PM Checklist",
            [
                ("Panels", "Inspect panel surfaces for dirt, cracks, or shading issues.", "yesno", true),
                ("Inverter", "Check inverter status indicators and error logs.", "yesno", true),
                ("Batteries", "Inspect battery bank condition where applicable.", "yesno", false),
                ("Cabling", "Verify cable routing, glands, and terminations are secure.", "yesno", true),
                ("Performance", "Confirm expected energy production or system output.", "yesno", true),
                ("Recommendations", "Record optimization or repair recommendations.", "text", false)
            ])
        };

        dbContext.PmTemplates.AddRange(templates);
    }

    private static PmTemplate CreatePmTemplate(
        Guid tenantId,
        string category,
        string name,
        IReadOnlyList<(string SectionName, string Prompt, string ResponseType, bool IsRequired)> questions)
    {
        var template = new PmTemplate
        {
            TenantId = tenantId,
            Category = category,
            Name = name,
            AutoScheduleByDefault = false
        };

        template.Questions = questions
            .Select((question, index) => new PmTemplateQuestion
            {
                SectionName = question.SectionName,
                Prompt = question.Prompt,
                ResponseType = question.ResponseType,
                DisplayOrder = index + 1,
                IsRequired = question.IsRequired
            })
            .ToList();

        return template;
    }
}
