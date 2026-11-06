using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Ecosys.Infrastructure.Services.Factory;

/// <summary>The records found for one evidence source, ready to show or print.</summary>
public sealed record EvidenceSection(string Source, string Title, string Summary, int Count, IReadOnlyList<string> Headers, IReadOnlyList<IReadOnlyList<string>> Rows);

public sealed record RequirementEvidence(Guid RequirementId, string Clause, string Title, string? Guidance, bool HasEvidence, IReadOnlyList<EvidenceSection> Sections, IReadOnlyList<string> Documents, int OpenNonConformities);

public sealed record EvidencePack(Guid StandardId, string StandardName, Guid FactoryId, string FactoryName, DateTime From, DateTime To, DateTime GeneratedAt, IReadOnlyList<RequirementEvidence> Requirements)
{
    public int Covered => Requirements.Count(x => x.HasEvidence);
}

public sealed record StandardTemplate(string Key, string Code, string Name, string Description, IReadOnlyList<(string Clause, string Title, string Guidance, string Sources)> Requirements);

public interface IComplianceFrameworkService
{
    IReadOnlyList<StandardTemplate> Templates { get; }

    /// <summary>Adds a standard (with its requirement list) from a template; returns the existing one if it is already there.</summary>
    Task<ComplianceStandard> InstallAsync(Guid tenantId, string templateKey, CancellationToken cancellationToken = default);

    Task<EvidenceSection> CollectAsync(Guid tenantId, Guid factoryId, string source, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);

    /// <summary>Every requirement of the standard with the records that prove it, for one factory and period.</summary>
    Task<EvidencePack> EvidencePackAsync(Guid tenantId, Guid standardId, Guid factoryId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);

    /// <summary>Audit reminders (30 and 7 days ahead) and weekly escalation of overdue non-conformities. Returns notifications sent.</summary>
    Task<int> RunAlertsAsync(Guid tenantId, DateTime utcNow, CancellationToken cancellationToken = default);
}

internal sealed class ComplianceFrameworkService(
    AppDbContext dbContext,
    IFactoryKpiService kpiService,
    IFactoryPmService pmService,
    INotificationDispatcher? notificationDispatcher = null) : IComplianceFrameworkService
{
    public const string AuditDueKey = "compliance.audit-due";
    public const string NonConformityOverdueKey = "compliance.nc-overdue";
    private const int MaxRows = 200;

    // Requirement codes are the factory's own shorthand by theme, not the standards' clause numbers:
    // confirm the clauses the auditor checks and edit them (requirements §4.1).
    public IReadOnlyList<StandardTemplate> Templates { get; } =
    [
        new("rainforest-alliance", "RA", "Rainforest Alliance (supply chain, maintenance evidence)",
            "Maintenance and engineering evidence for Rainforest Alliance supply chain audits: safe working conditions, incidents, training, energy, waste, chemicals and corrective actions.",
        [
            ("RA-SAFE-1", "Machinery is safe to work on: guarding, permits to work and lockout", "Permits for high-risk work, isolation records and planned maintenance of guards and safety devices.", "permits,loto,pm,running-checks"),
            ("RA-SAFE-2", "Workers have the PPE their work needs", "PPE issued to maintenance staff and replaced when due.", "ppe"),
            ("RA-SAFE-3", "First aid and fire equipment are in place and checked", "Fire extinguishers, hydrants and alarms inspected on schedule.", "fire-safety"),
            ("RA-SAFE-4", "Accidents and near misses are recorded and investigated", "Incident register with root cause and corrective work.", "incidents"),
            ("RA-SAFE-5", "Workers and contractors are trained for their work", "Induction and skills training, contractors included.", "training,contractors"),
            ("RA-ENV-1", "Energy is used efficiently", "Energy and boiler fuel per unit of production, and improvement actions.", "energy"),
            ("RA-ENV-2", "Waste is disposed of through licensed handlers", "Used oil, scrap, batteries and e-waste with consignment notes.", "waste"),
            ("RA-ENV-3", "Chemicals are registered and stored safely, with safety data sheets", "Lubricants and water treatment chemicals with current SDS.", "chemicals"),
            ("RA-MGT-1", "Non-conformities are corrected on time", "Audit findings logged as corrective actions with owner, due date and evidence of closure.", "non-conformities"),
        ]),
        new("fairtrade", "FT", "Fairtrade (hired labour, maintenance evidence)",
            "Occupational health and safety, environment and corrective action evidence from maintenance.",
        [
            ("FT-OHS-1", "Hazardous work is controlled", "Permits and isolation for high-risk maintenance work.", "permits,loto"),
            ("FT-OHS-2", "Protective equipment is provided free and used", "PPE issue records.", "ppe"),
            ("FT-OHS-3", "Accidents are recorded and prevented", "Incident and near-miss register.", "incidents"),
            ("FT-OHS-4", "Workers are trained in safe work", "Training records.", "training"),
            ("FT-ENV-1", "Waste and chemicals are handled responsibly", "Waste disposal and chemicals register.", "waste,chemicals"),
            ("FT-MGT-1", "Findings are corrected", "Non-conformities and corrective actions.", "non-conformities"),
        ]),
        new("iso-22000", "ISO22000", "ISO 22000 / FSSC 22000 / HACCP (maintenance prerequisites)",
            "Prerequisite programmes maintenance supports: cleaning, foreign-body control, food-grade lubricants, planned maintenance and calibration.",
        [
            ("PRP-CLEAN", "Equipment is cleaned to a schedule and verified", "Cleaning logs by asset or area, with method and verification.", "cleaning"),
            ("PRP-METAL", "Metal detectors are tested and reject test pieces", "Ferrous, non-ferrous and stainless test pieces at each check.", "metal-detector"),
            ("PRP-GLASS", "Glass and hard plastic are registered and checked", "Glass and hard plastic register and its periodic checks.", "glass"),
            ("PRP-LUBE", "Lubricants in food areas are food grade", "Lubricants used on machines with their food-grade class, and their SDS.", "lubricants,chemicals"),
            ("PRP-MAINT", "Equipment is maintained to a plan", "Planned maintenance completed on time, and running checks.", "pm,running-checks"),
            ("PRP-CAL", "Measuring equipment is calibrated", "Scales, weighbridges, thermometers and metal detectors calibrated on time.", "calibration"),
            ("PRP-CA", "Deviations are corrected", "Non-conformities and corrective actions.", "non-conformities"),
        ]),
        new("osha-statutory", "OSHA", "OSHA 2007 / DOSH statutory",
            "Statutory examinations and the safety system around maintenance work.",
        [
            ("OSHA-EXAM", "Boilers, pressure vessels and lifting equipment are examined by an approved person", "Certified examiner reports and certificates in date.", "statutory"),
            ("OSHA-FIRE", "Fire equipment is inspected", "Extinguishers, hydrants and alarms.", "fire-safety"),
            ("OSHA-PTW", "Dangerous work is done under a permit and isolation", "Permits to work and lockout records.", "permits,loto"),
            ("OSHA-INC", "Accidents are recorded and reportable ones reported", "Incident register.", "incidents"),
            ("OSHA-TRN", "Workers are trained and certified for their work", "Training records and contractor documents.", "training,contractors"),
            ("OSHA-PPE", "PPE is issued", "PPE issue records.", "ppe"),
        ]),
        new("iso-9001", "ISO9001", "ISO 9001 (maintenance processes)", "Controlled maintenance processes, calibration and corrective action.",
        [
            ("9001-7.1.5", "Monitoring and measuring resources are calibrated", "Calibration records.", "calibration"),
            ("9001-8.5", "Maintenance is planned and carried out under control", "PM compliance and running checks.", "pm,running-checks"),
            ("9001-10.2", "Non-conformities are corrected", "Corrective actions.", "non-conformities"),
        ]),
        new("iso-14001", "ISO14001", "ISO 14001 (environment)", "Environmental aspects maintenance controls.",
        [
            ("14001-WASTE", "Waste is controlled", "Waste disposal records.", "waste"),
            ("14001-CHEM", "Chemicals are controlled", "Chemicals register and SDS.", "chemicals"),
            ("14001-ENERGY", "Resource use is monitored", "Energy and fuel per production unit.", "energy"),
            ("14001-NC", "Non-conformities are corrected", "Corrective actions.", "non-conformities"),
        ]),
        new("iso-45001", "ISO45001", "ISO 45001 (occupational health and safety)", "OH&S controls for maintenance work.",
        [
            ("45001-8.1", "Operational controls for hazardous work", "Permits and lockout.", "permits,loto"),
            ("45001-7.2", "Competence", "Training and certificates.", "training"),
            ("45001-10.2", "Incidents and corrective action", "Incidents and non-conformities.", "incidents,non-conformities"),
        ]),
        new("iso-50001", "ISO50001", "ISO 50001 (energy)", "Energy performance of the factory.",
        [
            ("50001-6.6", "Energy performance is measured", "kWh and boiler fuel per unit of production.", "energy"),
            ("50001-8.1", "Significant energy users are maintained", "Planned maintenance on boilers, dryers and fans.", "pm"),
        ]),
        new("iso-55001", "ISO55001", "ISO 55001 (asset management)", "Asset management practice evidence.",
        [
            ("55001-8.1", "Assets are maintained to plan", "PM compliance and running checks.", "pm,running-checks"),
            ("55001-9.1", "Asset performance is monitored", "Statutory and calibration status.", "statutory,calibration"),
            ("55001-10.2", "Failures and non-conformities are acted on", "Incidents and corrective actions.", "incidents,non-conformities"),
        ]),
    ];

    public async Task<ComplianceStandard> InstallAsync(Guid tenantId, string templateKey, CancellationToken cancellationToken = default)
    {
        var template = Templates.FirstOrDefault(x => x.Key == templateKey) ?? throw new BusinessRuleException("Unknown standard template.");
        var existing = await dbContext.ComplianceStandards.FirstOrDefaultAsync(x => x.TenantId == tenantId && (x.TemplateKey == template.Key || x.Code == template.Code), cancellationToken);
        if (existing is not null) return existing;
        var standard = new ComplianceStandard { TenantId = tenantId, Code = template.Code, Name = template.Name, Description = template.Description, TemplateKey = template.Key };
        var sequence = 0;
        foreach (var (clause, title, guidance, sources) in template.Requirements)
        {
            standard.Requirements.Add(new StandardRequirement { TenantId = tenantId, Clause = clause, Title = title, Guidance = guidance, EvidenceSources = sources, Sequence = ++sequence });
        }

        dbContext.ComplianceStandards.Add(standard);
        await dbContext.SaveChangesAsync(cancellationToken);
        return standard;
    }

    public async Task<EvidencePack> EvidencePackAsync(Guid tenantId, Guid standardId, Guid factoryId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
    {
        var standard = await dbContext.ComplianceStandards.AsNoTracking().Include(x => x.Requirements)
            .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == standardId, cancellationToken) ?? throw new NotFoundException("Standard was not found.");
        var factory = await dbContext.OrgNodes.AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == factoryId && x.Level == OrgNodeLevels.Factory, cancellationToken)
            ?? throw new NotFoundException("Factory was not found.");
        var cache = new Dictionary<string, EvidenceSection>();
        var requirementIds = standard.Requirements.Select(x => x.Id).ToList();
        var documents = await dbContext.Attachments.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.EntityType == "StandardRequirement" && requirementIds.Contains(x.EntityId))
            .Select(x => new { x.EntityId, x.FileName }).ToListAsync(cancellationToken);
        var ncs = await dbContext.NonConformities.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.FactoryId == factoryId && x.RequirementId != null && requirementIds.Contains(x.RequirementId.Value)
                && (x.Status == NonConformityStatuses.Open || x.Status == NonConformityStatuses.InProgress || x.Status == NonConformityStatuses.Resolved))
            .Select(x => x.RequirementId!.Value).ToListAsync(cancellationToken);

        var result = new List<RequirementEvidence>();
        foreach (var requirement in standard.Requirements.OrderBy(x => x.Sequence))
        {
            var sections = new List<EvidenceSection>();
            foreach (var source in Ecosys.Domain.Entities.EvidenceSources.Parse(requirement.EvidenceSources).Where(x => x != Ecosys.Domain.Entities.EvidenceSources.Documents))
            {
                if (!cache.TryGetValue(source, out var section))
                {
                    section = await CollectAsync(tenantId, factoryId, source, fromUtc, toUtc, cancellationToken);
                    cache[source] = section;
                }

                sections.Add(section);
            }

            var docs = documents.Where(x => x.EntityId == requirement.Id).Select(x => x.FileName).ToList();
            result.Add(new RequirementEvidence(requirement.Id, requirement.Clause, requirement.Title, requirement.Guidance,
                sections.Any(x => x.Count > 0) || docs.Count > 0, sections, docs, ncs.Count(x => x == requirement.Id)));
        }

        return new EvidencePack(standard.Id, standard.Name, factory.Id, factory.Name, fromUtc, toUtc, DateTime.UtcNow, result);
    }

    public async Task<EvidenceSection> CollectAsync(Guid tenantId, Guid factoryId, string source, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
    {
        var nodes = await SubtreeAsync(tenantId, factoryId, cancellationToken);
        var title = Ecosys.Domain.Entities.EvidenceSources.Labels.GetValueOrDefault(source, source);
        static string D(DateTime? d) => d?.ToString("dd MMM yyyy") ?? "—";
        EvidenceSection Section(string summary, string[] headers, IEnumerable<string[]> rows)
        {
            var list = rows.Select(r => (IReadOnlyList<string>)r).ToList();
            return new EvidenceSection(source, title, summary, list.Count, headers, list.Take(MaxRows).ToList());
        }

        var jobs = dbContext.WorkOrders.AsNoTracking().Where(x => x.TenantId == tenantId && x.OrgNodeId != null && nodes.Contains(x.OrgNodeId.Value)).Select(x => x.Id);
        switch (source)
        {
            case Ecosys.Domain.Entities.EvidenceSources.Statutory:
            case Ecosys.Domain.Entities.EvidenceSources.Calibration:
            case Ecosys.Domain.Entities.EvidenceSources.FireSafety:
            {
                var kinds = source switch
                {
                    Ecosys.Domain.Entities.EvidenceSources.Calibration => new[] { ComplianceKinds.Calibration },
                    Ecosys.Domain.Entities.EvidenceSources.FireSafety => new[] { ComplianceKinds.FireSafety },
                    _ => new[] { ComplianceKinds.StatutoryInspection, ComplianceKinds.Certificate },
                };
                var items = await dbContext.ComplianceItems.AsNoTracking().Where(x => x.TenantId == tenantId && x.IsActive && kinds.Contains(x.Kind) && nodes.Contains(x.OrgNodeId))
                    .Select(x => new { x.Name, Asset = x.Asset != null ? x.Asset.AssetCode : null, x.DueDate,
                        Done = x.Records.Where(r => r.DoneAt >= fromUtc && r.DoneAt < toUtc).OrderByDescending(r => r.DoneAt).Select(r => new { r.DoneAt, r.Result, r.CertificateNumber, r.Examiner }).ToList() })
                    .ToListAsync(cancellationToken);
                var today = DateTime.UtcNow.Date;
                var rows = items.SelectMany(i => i.Done.Count == 0
                    ? [new[] { i.Name, i.Asset ?? "—", "Not done in period", "—", "—", D(i.DueDate) + (i.DueDate < today ? " (overdue)" : "") }]
                    : i.Done.Select(r => new[] { i.Name, i.Asset ?? "—", D(r.DoneAt), r.Result, $"{r.CertificateNumber}{(r.Examiner is null ? "" : $" · {r.Examiner}")}", D(i.DueDate) })).ToList();
                var done = items.Sum(i => i.Done.Count);
                var overdue = items.Count(i => i.DueDate < today);
                // Items not done in the period are listed, but evidence means something was done.
                var section = Section($"{items.Count} items, {done} done in the period, {overdue} overdue now.", ["Item", "Asset", "Done", "Result", "Certificate / examiner", "Next due"], rows);
                return section with { Count = done };
            }
            case Ecosys.Domain.Entities.EvidenceSources.Permits:
            {
                var rows = await dbContext.WorkPermits.AsNoTracking().Where(x => jobs.Contains(x.WorkOrderId) && x.ValidFrom >= fromUtc && x.ValidFrom < toUtc).OrderBy(x => x.ValidFrom)
                    .Select(x => new[] { x.PermitNumber, x.PermitType, x.WorkOrder!.WorkOrderNumber, x.Status, x.ApprovedByName ?? "—", x.ValidFrom.ToString("dd MMM yyyy HH:mm") }).ToListAsync(cancellationToken);
                return Section($"{rows.Count} permits to work issued.", ["Permit", "Type", "Job", "Status", "Authorised by", "From"], rows);
            }
            case Ecosys.Domain.Entities.EvidenceSources.Loto:
            {
                var rows = await dbContext.LotoLocks.AsNoTracking().Where(x => jobs.Contains(x.WorkOrderId) && x.AppliedAt >= fromUtc && x.AppliedAt < toUtc).OrderBy(x => x.AppliedAt)
                    .Select(x => new[] { x.LockNumber, x.IsolationPoint!.Code, x.WorkOrder!.WorkOrderNumber, x.AppliedByName, x.ZeroEnergyVerified ? "Yes" : "No", x.RemovedAt != null ? "Removed" : "On" }).ToListAsync(cancellationToken);
                return Section($"{rows.Count} isolations applied.", ["Lock", "Point", "Job", "Applied by", "Zero energy proved", "Now"], rows);
            }
            case Ecosys.Domain.Entities.EvidenceSources.Incidents:
            {
                var rows = await dbContext.Incidents.AsNoTracking().Where(x => x.TenantId == tenantId && nodes.Contains(x.OrgNodeId) && x.OccurredAt >= fromUtc && x.OccurredAt < toUtc).OrderBy(x => x.OccurredAt)
                    .Select(x => new[] { x.IncidentNumber, x.OccurredAt.ToString("dd MMM yyyy"), x.Kind, x.Severity, x.Title, x.Status, x.RootCause ?? "—" }).ToListAsync(cancellationToken);
                return Section($"{rows.Count} incidents and near misses recorded.", ["No.", "Date", "Kind", "Severity", "What", "Status", "Root cause"], rows);
            }
            case Ecosys.Domain.Entities.EvidenceSources.Training:
            {
                var rows = await dbContext.TrainingRecords.AsNoTracking()
                    .Where(x => x.TenantId == tenantId && x.CompletedOn >= fromUtc && x.CompletedOn < toUtc && (x.Technician == null || x.Technician.HomeOrgNodeId == null || nodes.Contains(x.Technician.HomeOrgNodeId.Value)))
                    .OrderBy(x => x.CompletedOn)
                    .Select(x => new[] { x.PersonName, x.IsContractor ? x.Company ?? "Contractor" : "Staff", x.Course, x.Category, x.CompletedOn.ToString("dd MMM yyyy"), x.ExpiresOn != null ? x.ExpiresOn.Value.ToString("dd MMM yyyy") : "—" })
                    .ToListAsync(cancellationToken);
                return Section($"{rows.Count} training records.", ["Person", "Staff / company", "Course", "Category", "Completed", "Expires"], rows);
            }
            case Ecosys.Domain.Entities.EvidenceSources.Ppe:
            {
                var rows = await dbContext.PpeIssues.AsNoTracking()
                    .Where(x => x.TenantId == tenantId && x.IssuedOn >= fromUtc && x.IssuedOn < toUtc && (x.Technician == null || x.Technician.HomeOrgNodeId == null || nodes.Contains(x.Technician.HomeOrgNodeId.Value)))
                    .OrderBy(x => x.IssuedOn)
                    .Select(x => new[] { x.PersonName, x.Item, x.Quantity.ToString(), x.IssuedOn.ToString("dd MMM yyyy"), x.IssuedByName ?? "—" }).ToListAsync(cancellationToken);
                return Section($"{rows.Count} PPE issues.", ["Person", "Item", "Qty", "Issued", "By"], rows);
            }
            case Ecosys.Domain.Entities.EvidenceSources.Chemicals:
            {
                var today = DateTime.UtcNow.Date;
                var rows = await dbContext.MaterialItems.AsNoTracking().Where(x => x.TenantId == tenantId && x.IsActive && x.IsChemical).OrderBy(x => x.ItemName)
                    .Select(x => new { x.ItemName, x.ChemicalKind, x.HazardClass, x.FoodGradeClass, x.SdsAttachmentId, x.SdsIssuedOn }).ToListAsync(cancellationToken);
                var current = rows.Count(x => x.SdsAttachmentId != null && (x.SdsIssuedOn == null || x.SdsIssuedOn.Value.AddYears(5) >= today));
                return Section($"{rows.Count} chemicals registered, {current} with a current safety data sheet.", ["Chemical", "Kind", "Hazard", "Food grade", "SDS"],
                    rows.Select(x => new[] { x.ItemName, x.ChemicalKind ?? "—", x.HazardClass ?? "—", x.FoodGradeClass ?? "—",
                        x.SdsAttachmentId is null ? "Missing" : x.SdsIssuedOn is { } d && d.AddYears(5) < today ? "Review due" : $"On file{(x.SdsIssuedOn is { } i ? $" ({i:dd MMM yyyy})" : "")}" }));
            }
            case Ecosys.Domain.Entities.EvidenceSources.Lubricants:
            {
                var stores = dbContext.Stores.Where(x => x.TenantId == tenantId && x.FactoryId == factoryId).Select(x => x.Id);
                var rows = await dbContext.StockMovements.AsNoTracking()
                    .Where(x => x.TenantId == tenantId && x.StoreId != null && stores.Contains(x.StoreId.Value) && x.MovementType == StoreMovementTypes.Issue
                        && x.Material!.ChemicalKind == ChemicalKinds.Lubricant && x.CreatedAt >= fromUtc && x.CreatedAt < toUtc)
                    .OrderBy(x => x.CreatedAt)
                    .Select(x => new[] { x.CreatedAt.ToString("dd MMM yyyy"), x.Material!.ItemName, x.Material.FoodGradeClass ?? "Not food grade", x.Quantity.ToString("0.##") + " " + x.Material.UnitOfMeasure,
                        x.WorkOrder != null && x.WorkOrder.Asset != null ? x.WorkOrder.Asset.AssetCode : "—", x.WorkOrder != null ? x.WorkOrder.WorkOrderNumber : "—" })
                    .ToListAsync(cancellationToken);
                var notFood = rows.Count(r => r[2] == "Not food grade");
                return Section($"{rows.Count} lubricant issues to machines{(notFood > 0 ? $", {notFood} not food grade" : ", all food grade")}.", ["Date", "Lubricant", "Grade", "Quantity", "Machine", "Job"], rows);
            }
            case Ecosys.Domain.Entities.EvidenceSources.Waste:
            {
                var rows = await dbContext.WasteDisposals.AsNoTracking().Where(x => x.TenantId == tenantId && x.FactoryId == factoryId && x.DisposedOn >= fromUtc && x.DisposedOn < toUtc).OrderBy(x => x.DisposedOn)
                    .Select(x => new[] { x.DisposedOn.ToString("dd MMM yyyy"), x.WasteType, x.Quantity.ToString("0.##") + " " + x.Unit, x.HandlerName, x.LicenceNumber ?? "—", x.ConsignmentNote ?? "—" }).ToListAsync(cancellationToken);
                return Section($"{rows.Count} disposals.", ["Date", "Waste", "Quantity", "Handler", "Licence", "Consignment note"], rows);
            }
            case Ecosys.Domain.Entities.EvidenceSources.Cleaning:
            case Ecosys.Domain.Entities.EvidenceSources.MetalDetector:
            case Ecosys.Domain.Entities.EvidenceSources.Glass:
            {
                var kind = source == Ecosys.Domain.Entities.EvidenceSources.Cleaning ? FoodSafetyKinds.Cleaning : source == Ecosys.Domain.Entities.EvidenceSources.MetalDetector ? FoodSafetyKinds.MetalDetector : FoodSafetyKinds.GlassCheck;
                var records = await dbContext.FoodSafetyRecords.AsNoTracking().Where(x => x.TenantId == tenantId && x.FactoryId == factoryId && x.Kind == kind && x.DoneAt >= fromUtc && x.DoneAt < toUtc)
                    .OrderBy(x => x.DoneAt)
                    .Select(x => new { x.DoneAt, Where = x.Asset != null ? x.Asset.AssetCode : x.Area, x.Passed, x.Method, x.FerrousRejected, x.NonFerrousRejected, x.StainlessRejected, x.ItemsChecked, x.ItemsDamaged, x.DoneByName, x.VerifiedByName })
                    .ToListAsync(cancellationToken);
                string Yn(bool? b) => b is null ? "—" : b.Value ? "Rejected" : "MISSED";
                var failed = records.Count(x => !x.Passed);
                if (kind == FoodSafetyKinds.MetalDetector)
                    return Section($"{records.Count} checks, {failed} failed.", ["When", "Detector", "Ferrous", "Non-ferrous", "Stainless", "Result", "By"],
                        records.Select(x => new[] { x.DoneAt.ToString("dd MMM yyyy HH:mm"), x.Where ?? "—", Yn(x.FerrousRejected), Yn(x.NonFerrousRejected), Yn(x.StainlessRejected), x.Passed ? "Pass" : "Fail", x.DoneByName }));
                if (kind == FoodSafetyKinds.GlassCheck)
                {
                    var register = await dbContext.GlassRegisterItems.AsNoTracking().Where(x => x.TenantId == tenantId && x.FactoryId == factoryId && x.IsActive).SumAsync(x => x.Quantity, cancellationToken);
                    return Section($"{register} items on the register; {records.Count} checks, {failed} found damage.", ["When", "Area", "Checked", "Damaged", "Result", "By"],
                        records.Select(x => new[] { x.DoneAt.ToString("dd MMM yyyy"), x.Where ?? "—", x.ItemsChecked?.ToString() ?? "—", x.ItemsDamaged?.ToString() ?? "0", x.Passed ? "Intact" : "Damage", x.DoneByName }));
                }

                return Section($"{records.Count} cleanings, {failed} failed verification.", ["When", "Asset / area", "Method", "Result", "By", "Verified by"],
                    records.Select(x => new[] { x.DoneAt.ToString("dd MMM yyyy"), x.Where ?? "—", x.Method ?? "—", x.Passed ? "Pass" : "Fail", x.DoneByName, x.VerifiedByName ?? "—" }));
            }
            case Ecosys.Domain.Entities.EvidenceSources.Energy:
            {
                var usage = await kpiService.MeterUsageAsync(tenantId, nodes, fromUtc, toUtc, cancellationToken);
                var production = usage.TryGetValue(MeterTypes.Production, out var p) ? p.Quantity : 0;
                var rows = usage.OrderBy(x => x.Key).Select(x => new[] { x.Key, $"{x.Value.Quantity:0.##} {x.Value.Unit}", production > 0 && x.Key != MeterTypes.Production ? $"{x.Value.Quantity / production:0.####} per {p.Unit}" : "—" }).ToList();
                return Section(production > 0 ? $"Production {production:0} {p.Unit} in the period." : "No production recorded on meters in the period.", ["Meter type", "Total", "Per production unit"], rows);
            }
            case Ecosys.Domain.Entities.EvidenceSources.PreventiveMaintenance:
            {
                var pm = await pmService.ComplianceAsync(tenantId, nodes, fromUtc, toUtc, cancellationToken);
                return Section(pm.Due == 0 ? "No planned maintenance fell due in the period." : $"{pm.Due} PM due, {pm.OnTime} on time, {pm.Late} late, {pm.Skipped} skipped, {pm.Overdue} overdue: {pm.Percent}% compliance.",
                    ["Plan", "Asset", "Due", "On time", "Late", "Skipped", "Overdue"],
                    pm.Plans.Select(x => new[] { x.Name, x.AssetCode ?? "—", x.Due.ToString(), x.OnTime.ToString(), x.Late.ToString(), x.Skipped.ToString(), x.Overdue.ToString() }));
            }
            case Ecosys.Domain.Entities.EvidenceSources.RunningChecks:
            {
                var runs = await dbContext.CheckRuns.AsNoTracking().Where(x => x.TenantId == tenantId && nodes.Contains(x.OrgNodeId) && x.DueFrom >= fromUtc && x.DueFrom < toUtc)
                    .GroupBy(x => x.Status).Select(g => new { g.Key, Count = g.Count(), Failed = g.Sum(x => x.FailedCount) }).ToListAsync(cancellationToken);
                var completed = runs.Where(x => x.Key == CheckRunStatuses.Completed).Sum(x => x.Count);
                var section = Section($"{runs.Sum(x => x.Count)} running checks due, {completed} completed, {runs.Where(x => x.Key == CheckRunStatuses.Missed).Sum(x => x.Count)} missed.",
                    ["Status", "Checks", "Failed items"], runs.Select(x => new[] { x.Key, x.Count.ToString(), x.Failed.ToString() }));
                return section with { Count = completed };
            }
            case Ecosys.Domain.Entities.EvidenceSources.NonConformities:
            {
                var rows = await dbContext.NonConformities.AsNoTracking().Where(x => x.TenantId == tenantId && x.FactoryId == factoryId && x.CreatedAt < toUtc && (x.ClosedAt == null || x.ClosedAt >= fromUtc))
                    .OrderBy(x => x.CreatedAt)
                    .Select(x => new[] { x.Number, x.Severity, x.Finding, x.CorrectiveAction ?? "—", x.DueDate != null ? x.DueDate.Value.ToString("dd MMM yyyy") : "—", x.Status, x.ClosedAt != null ? x.ClosedAt.Value.ToString("dd MMM yyyy") : "—" })
                    .ToListAsync(cancellationToken);
                return Section($"{rows.Count} non-conformities open or closed in the period, {rows.Count(r => r[5] == NonConformityStatuses.Closed)} closed.", ["No.", "Severity", "Finding", "Corrective action", "Due", "Status", "Closed"], rows);
            }
            case Ecosys.Domain.Entities.EvidenceSources.Contractors:
            {
                var today = DateTime.UtcNow.Date;
                var rows = await dbContext.ContractorDocuments.AsNoTracking().Where(x => x.TenantId == tenantId && x.Contractor!.IsActive).OrderBy(x => x.Contractor!.Name).ThenBy(x => x.DocumentType)
                    .Select(x => new { x.Contractor!.Name, x.DocumentType, x.Reference, x.ExpiresOn }).ToListAsync(cancellationToken);
                return Section($"{rows.Select(x => x.Name).Distinct().Count()} contractors, {rows.Count(x => x.ExpiresOn < today)} documents expired.", ["Contractor", "Document", "Reference", "Expires", "Status"],
                    rows.Select(x => new[] { x.Name, x.DocumentType, x.Reference ?? "—", D(x.ExpiresOn), x.ExpiresOn < today ? "Expired" : "Current" }));
            }
            default:
                return Section("No records.", [], []);
        }
    }

    public async Task<int> RunAlertsAsync(Guid tenantId, DateTime utcNow, CancellationToken cancellationToken = default)
    {
        var today = utcNow.Date;
        var sent = 0;
        var audits = await dbContext.AuditEvents.Include(x => x.Standard).Include(x => x.Factory)
            .Where(x => x.TenantId == tenantId && x.Status == "Planned" && x.ScheduledFor >= today && x.ScheduledFor <= today.AddDays(30)).ToListAsync(cancellationToken);
        var dueAudits = new List<AuditEvent>();
        foreach (var audit in audits)
        {
            var step = (audit.ScheduledFor.Date - today).TotalDays <= 7 ? 7 : 30;
            if (audit.LastReminderDays is null || step < audit.LastReminderDays)
            {
                audit.LastReminderDays = step;
                dueAudits.Add(audit);
            }
        }

        var overdue = await dbContext.NonConformities.Include(x => x.Factory)
            .Where(x => x.TenantId == tenantId && x.DueDate != null && x.DueDate < today
                && (x.Status == NonConformityStatuses.Open || x.Status == NonConformityStatuses.InProgress)
                && (x.LastOverdueAlertAt == null || x.LastOverdueAlertAt <= utcNow.AddDays(-7)))
            .ToListAsync(cancellationToken);
        foreach (var nc in overdue) nc.LastOverdueAlertAt = utcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        if (notificationDispatcher is null) return 0;

        foreach (var audit in dueAudits)
        {
            var recipients = (await FactoryRecipients.SupervisorsAsync(dbContext, tenantId, audit.FactoryId, cancellationToken)).ToList();
            if (recipients.Count == 0) continue;
            var what = $"{audit.Kind}{(audit.Standard is null ? "" : $" ({audit.Standard.Name})")}";
            await notificationDispatcher.NotifyAsync(new NotificationRequest(tenantId, AuditDueKey, recipients,
                $"{what} on {audit.ScheduledFor:dd MMM}", $"{audit.Factory?.Name}: {what} on {audit.ScheduledFor:dd MMM yyyy}. Check the evidence pack and close open findings.",
                "/maintenance/compliance?tab=audits", nameof(AuditEvent), audit.Id,
                new Dictionary<string, string?> { ["Audit"] = what, ["Factory"] = audit.Factory?.Name, ["Date"] = audit.ScheduledFor.ToString("dd MMM yyyy") },
                NotificationSeverities.Info), cancellationToken);
            sent++;
        }

        foreach (var group in overdue.GroupBy(x => x.FactoryId))
        {
            var recipients = new HashSet<Guid>(await FactoryRecipients.SupervisorsAsync(dbContext, tenantId, group.Key, cancellationToken));
            foreach (var owner in group.Where(x => x.OwnerUserId != null).Select(x => x.OwnerUserId!.Value)) recipients.Add(owner);
            if (recipients.Count == 0) continue;
            var lines = group.OrderBy(x => x.DueDate).Select(x => $"{x.Number} ({x.Severity}) due {x.DueDate:dd MMM}: {x.Finding}").ToList();
            await notificationDispatcher.NotifyAsync(new NotificationRequest(tenantId, NonConformityOverdueKey, recipients.ToList(),
                $"{lines.Count} corrective action{(lines.Count == 1 ? "" : "s")} overdue", string.Join("; ", lines.Take(3)) + (lines.Count > 3 ? $" and {lines.Count - 3} more" : ""),
                "/maintenance/compliance?tab=findings", nameof(NonConformity), null,
                new Dictionary<string, string?> { ["Count"] = lines.Count.ToString(), ["Items"] = string.Join("\n", lines), ["Factory"] = group.First().Factory?.Name },
                NotificationSeverities.Warning), cancellationToken);
            sent++;
        }

        return sent;
    }

    private async Task<List<Guid>> SubtreeAsync(Guid tenantId, Guid root, CancellationToken cancellationToken)
    {
        var edges = await dbContext.OrgNodes.AsNoTracking().Where(x => x.TenantId == tenantId).Select(x => new { x.Id, x.ParentId }).ToListAsync(cancellationToken);
        var result = new List<Guid> { root };
        for (var i = 0; i < result.Count; i++)
        {
            result.AddRange(edges.Where(x => x.ParentId == result[i]).Select(x => x.Id));
        }

        return result;
    }
}

/// <summary>Sends audit reminders and overdue corrective-action escalations once a day (checked every 6 hours).</summary>
public sealed class ComplianceFrameworkWorker(IServiceScopeFactory scopeFactory, ILogger<ComplianceFrameworkWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(6));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var service = scope.ServiceProvider.GetRequiredService<IComplianceFrameworkService>();
                var tenants = await db.AuditEvents.AsNoTracking().IgnoreQueryFilters().Where(x => x.Status == "Planned").Select(x => x.TenantId)
                    .Union(db.NonConformities.AsNoTracking().IgnoreQueryFilters().Where(x => x.Status == NonConformityStatuses.Open || x.Status == NonConformityStatuses.InProgress).Select(x => x.TenantId))
                    .Distinct().ToListAsync(stoppingToken);
                foreach (var tenantId in tenants)
                {
                    await service.RunAlertsAsync(tenantId, DateTime.UtcNow, stoppingToken);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Compliance framework alert cycle failed.");
            }

            await timer.WaitForNextTickAsync(stoppingToken);
        }
    }
}

/* ── Data protection ─────────────────────────────────────────────────────── */

public sealed record RetentionResult(int TrainingAnonymised, int PpeAnonymised, int ConsentsRemoved);

public interface IDataProtectionService
{
    /// <summary>Everything held about one person (a user and/or their technician record), for a subject access request.</summary>
    Task<IReadOnlyDictionary<string, object?>> ExportAsync(Guid tenantId, Guid? userId, Guid? technicianId, CancellationToken cancellationToken = default);

    /// <summary>Replaces a person's identifying details with a placeholder, keeping the maintenance records the law requires. The person must be inactive.</summary>
    Task<string> EraseAsync(Guid tenantId, Guid? userId, Guid? technicianId, CancellationToken cancellationToken = default);

    /// <summary>Anonymises records of non-staff (contractors, visitors) older than the retention period, and removes old withdrawn consents.</summary>
    Task<RetentionResult> ApplyRetentionAsync(Guid tenantId, DateTime utcNow, CancellationToken cancellationToken = default);
}

internal sealed class DataProtectionService(AppDbContext dbContext, IFactorySettingsService settingsService) : IDataProtectionService
{
    public async Task<IReadOnlyDictionary<string, object?>> ExportAsync(Guid tenantId, Guid? userId, Guid? technicianId, CancellationToken cancellationToken = default)
    {
        var (user, technician) = await LoadAsync(tenantId, userId, technicianId, cancellationToken);
        var techId = technician?.Id;
        var uid = user?.Id;
        var name = technician?.FullName ?? user?.FullName ?? string.Empty;
        return new Dictionary<string, object?>
        {
            ["exportedAt"] = DateTime.UtcNow,
            ["user"] = user is null ? null : new { user.Id, user.FullName, user.Email, user.Role, user.JobTitle, user.IsActive, user.CreatedAt },
            ["technician"] = technician is null ? null : new { technician.Id, technician.FullName, technician.Email, technician.Phone, technician.Trade, technician.HourlyRate, technician.IsActive },
            ["certificates"] = techId is null ? null : await dbContext.TechnicianCertificates.AsNoTracking().Where(x => x.TechnicianId == techId)
                .Select(x => new { Certificate = x.CertificationDefinition!.Name, x.CertificateNumber, x.IssuedOn, x.ExpiresOn }).ToListAsync(cancellationToken),
            ["skills"] = techId is null ? null : await dbContext.TechnicianSkills.AsNoTracking().Where(x => x.TechnicianId == techId).Select(x => new { x.Skill, x.Level }).ToListAsync(cancellationToken),
            ["roster"] = techId is null ? null : await dbContext.RosterEntries.AsNoTracking().Where(x => x.TechnicianId == techId).OrderBy(x => x.WorkDate).Select(x => new { x.WorkDate, x.Kind, x.ShiftName }).ToListAsync(cancellationToken),
            ["timeLogged"] = techId is null ? null : await dbContext.WorkOrderTimeLogs.AsNoTracking().Where(x => x.TechnicianId == techId).OrderBy(x => x.StartedAt)
                .Select(x => new { x.WorkOrder!.WorkOrderNumber, x.StartedAt, x.DurationMinutes, x.IsOvertime }).ToListAsync(cancellationToken),
            ["training"] = await dbContext.TrainingRecords.AsNoTracking().Where(x => x.TenantId == tenantId && ((techId != null && x.TechnicianId == techId) || (x.TechnicianId == null && x.PersonName == name)))
                .Select(x => new { x.Course, x.Category, x.CompletedOn, x.ExpiresOn, x.Trainer }).ToListAsync(cancellationToken),
            ["ppe"] = await dbContext.PpeIssues.AsNoTracking().Where(x => x.TenantId == tenantId && ((techId != null && x.TechnicianId == techId) || (x.TechnicianId == null && x.PersonName == name)))
                .Select(x => new { x.Item, x.Size, x.Quantity, x.IssuedOn }).ToListAsync(cancellationToken),
            ["incidentsNamingThem"] = await dbContext.Incidents.AsNoTracking().Where(x => x.TenantId == tenantId && x.PersonsInvolved != null && x.PersonsInvolved.Contains(name))
                .Select(x => new { x.IncidentNumber, x.OccurredAt, x.Kind, x.Title }).ToListAsync(cancellationToken),
            ["consents"] = await dbContext.ConsentRecords.AsNoTracking().Where(x => x.TenantId == tenantId && ((uid != null && x.UserId == uid) || (techId != null && x.TechnicianId == techId)))
                .Select(x => new { x.Purpose, x.GivenAt, x.Method, x.WithdrawnAt }).ToListAsync(cancellationToken),
        };
    }

    public async Task<string> EraseAsync(Guid tenantId, Guid? userId, Guid? technicianId, CancellationToken cancellationToken = default)
    {
        var (user, technician) = await LoadAsync(tenantId, userId, technicianId, cancellationToken);
        if (user?.IsActive == true || technician?.IsActive == true) throw new BusinessRuleException("Deactivate the person first: only people who have left can be erased.");
        var oldName = technician?.FullName ?? user?.FullName ?? string.Empty;
        var placeholder = $"Erased person {(technician?.Id ?? user!.Id).ToString("N")[..6]}";
        if (technician is not null)
        {
            technician.FullName = placeholder;
            technician.Email = string.Empty;
            technician.Phone = null;
            technician.HourlyRate = null;
            foreach (var log in await dbContext.WorkOrderTimeLogs.Where(x => x.TechnicianId == technician.Id).ToListAsync(cancellationToken)) log.TechnicianName = placeholder;
            foreach (var record in await dbContext.TrainingRecords.Where(x => x.TechnicianId == technician.Id).ToListAsync(cancellationToken)) record.PersonName = placeholder;
            foreach (var issue in await dbContext.PpeIssues.Where(x => x.TechnicianId == technician.Id).ToListAsync(cancellationToken)) issue.PersonName = placeholder;
        }

        if (user is not null)
        {
            user.FullName = placeholder;
            user.Email = $"erased-{user.Id:N}@erased.invalid";
            user.JobTitle = null;
        }

        foreach (var consent in await dbContext.ConsentRecords.Where(x => x.TenantId == tenantId && ((user != null && x.UserId == user.Id) || (technician != null && x.TechnicianId == technician.Id))).ToListAsync(cancellationToken))
        {
            consent.SubjectName = placeholder;
        }

        if (!string.IsNullOrWhiteSpace(oldName))
        {
            foreach (var incident in await dbContext.Incidents.Where(x => x.TenantId == tenantId && x.PersonsInvolved != null && x.PersonsInvolved.Contains(oldName)).ToListAsync(cancellationToken))
            {
                incident.PersonsInvolved = incident.PersonsInvolved!.Replace(oldName, placeholder);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return placeholder;
    }

    public async Task<RetentionResult> ApplyRetentionAsync(Guid tenantId, DateTime utcNow, CancellationToken cancellationToken = default)
    {
        var settings = await settingsService.GetOrCreateAsync(tenantId, cancellationToken);
        var cutoff = utcNow.Date.AddYears(-Math.Max(settings.PersonalDataRetentionYears, 1));
        var training = await dbContext.TrainingRecords.Where(x => x.TenantId == tenantId && x.TechnicianId == null && x.CompletedOn < cutoff && !x.PersonName.StartsWith("Former ")).ToListAsync(cancellationToken);
        foreach (var record in training) record.PersonName = $"Former {(record.IsContractor ? "contractor" : "worker")}";
        var ppe = await dbContext.PpeIssues.Where(x => x.TenantId == tenantId && x.TechnicianId == null && x.IssuedOn < cutoff && !x.PersonName.StartsWith("Former ")).ToListAsync(cancellationToken);
        foreach (var issue in ppe) issue.PersonName = "Former worker";
        var consents = await dbContext.ConsentRecords.Where(x => x.TenantId == tenantId && x.WithdrawnAt != null && x.WithdrawnAt < cutoff).ToListAsync(cancellationToken);
        dbContext.ConsentRecords.RemoveRange(consents);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new RetentionResult(training.Count, ppe.Count, consents.Count);
    }

    private async Task<(User? User, Technician? Technician)> LoadAsync(Guid tenantId, Guid? userId, Guid? technicianId, CancellationToken cancellationToken)
    {
        if (userId is null && technicianId is null) throw new BusinessRuleException("Choose the person.");
        var technician = technicianId is { } t
            ? await dbContext.Technicians.SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == t, cancellationToken) ?? throw new NotFoundException("Person was not found.")
            : null;
        var uid = userId ?? technician?.UserId;
        var user = uid is { } u ? await dbContext.Users.SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == u, cancellationToken) : null;
        if (userId is not null && user is null) throw new NotFoundException("Person was not found.");
        technician ??= user is null ? null : await dbContext.Technicians.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.UserId == user.Id, cancellationToken);
        return (user, technician);
    }
}
