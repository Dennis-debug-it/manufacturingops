using System.Text.Json;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Errors;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace Ecosys.Infrastructure.Services;

// ── Command types ─────────────────────────────────────────────────────────────

public sealed record PmTemplateWriteCommand(
    string Name,
    string Category,
    string? Description,
    Guid? AssetCategoryId,
    IReadOnlyList<PmTemplateChecklistItemCommand> Checklist,
    bool IsActive,
    string? Code = null,
    string WorkType = "PreventiveMaintenance",
    int? DefaultIntervalMonths = null);

public sealed record PmTemplateChecklistItemCommand(
    string? SectionName,
    int? SectionOrder,
    string Question,
    string? HelperText,
    string Type,
    string? Unit,
    decimal? MinValue,
    decimal? MaxValue,
    string? DefaultValue,
    bool Required,
    bool EvidenceRequired,
    bool? TechnicianRemarksAllowed,
    bool RequiresNoteOnFail,
    int Order,
    IReadOnlyCollection<string>? Options);

// ── Interface ─────────────────────────────────────────────────────────────────

public interface IPmTemplateService
{
    Task<IReadOnlyCollection<PmTemplate>> ListAsync(Guid tenantId, CancellationToken ct);
    Task<PmTemplate> GetAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<PmTemplate> CreateAsync(Guid tenantId, PmTemplateWriteCommand command, CancellationToken ct);
    Task<PmTemplate> UpdateAsync(Guid tenantId, Guid id, PmTemplateWriteCommand command, CancellationToken ct);
    Task<PmTemplate> DuplicateAsync(Guid tenantId, Guid id, string? name, CancellationToken ct);
    Task<PmTemplate> SetActiveAsync(Guid tenantId, Guid id, bool isActive, CancellationToken ct);
    Task DeleteAsync(Guid tenantId, Guid id, Guid? deletedByUserId, CancellationToken ct);
}

// ── Implementation ────────────────────────────────────────────────────────────

public sealed class PmTemplateService(AppDbContext dbContext) : IPmTemplateService
{
    private static readonly HashSet<string> AllowedQuestionTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "checkbox", "text", "multilinetext", "boolean", "number", "reading",
        "date", "dropdown", "photo", "signature", "yesno", "passfail", "condition"
    };

    // ── Read ──────────────────────────────────────────────────────────────────

    public async Task<IReadOnlyCollection<PmTemplate>> ListAsync(Guid tenantId, CancellationToken ct)
    {
        return await Query(tenantId)
            .OrderBy(x => x.Category)
            .ThenBy(x => x.Name)
            .ThenByDescending(x => x.Version)
            .ToListAsync(ct);
    }

    public async Task<PmTemplate> GetAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        return await Query(tenantId).SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException("PM template was not found.");
    }

    // ── Write ─────────────────────────────────────────────────────────────────

    public async Task<PmTemplate> CreateAsync(Guid tenantId, PmTemplateWriteCommand command, CancellationToken ct)
    {
        Validate(command);

        var normalizedCategory = NormalizeCategory(command.Category);
        var normalizedName = command.Name.Trim();

        await EnsureAssetCategoryExistsAsync(tenantId, command.AssetCategoryId, ct);
        await EnsureTemplateNameIsUniqueAsync(tenantId, normalizedCategory, normalizedName, null, ct);

        var template = new PmTemplate
        {
            TenantId = tenantId,
            AssetCategoryId = command.AssetCategoryId,
            Category = normalizedCategory,
            Name = normalizedName,
            Code = Normalize(command.Code),
            Description = Normalize(command.Description),
            WorkType = NormalizeWorkType(command.WorkType),
            DefaultIntervalMonths = NormalizeDefaultIntervalMonths(command.DefaultIntervalMonths),
            AutoScheduleByDefault = false,
            IsActive = command.IsActive,
            Version = 1,
            IsPlatformSeed = false,
            IsSystemTemplate = false
        };

        ApplyStructure(template, command);
        dbContext.PmTemplates.Add(template);
        await SaveChangesAsync(ct);

        return await Query(tenantId).SingleAsync(x => x.Id == template.Id, ct);
    }

    public async Task<PmTemplate> UpdateAsync(Guid tenantId, Guid id, PmTemplateWriteCommand command, CancellationToken ct)
    {
        Validate(command);

        var normalizedCategory = NormalizeCategory(command.Category);
        var normalizedName = command.Name.Trim();
        var normalizedCode = Normalize(command.Code);
        var normalizedDescription = Normalize(command.Description);
        var normalizedWorkType = NormalizeWorkType(command.WorkType);
        var normalizedDefaultIntervalMonths = NormalizeDefaultIntervalMonths(command.DefaultIntervalMonths);

        await EnsureAssetCategoryExistsAsync(tenantId, command.AssetCategoryId, ct);
        await EnsureTemplateNameIsUniqueAsync(tenantId, normalizedCategory, normalizedName, id, ct);

        // Wrap entire replace operation in a transaction so partial failure rolls back cleanly.
        // BeginTransactionAsync is attempted but treated as optional — InMemory and SQLite
        // providers don't support real transactions; for those we proceed without one and
        // rely on the provider's own atomicity guarantees (sufficient for tests).
        IDbContextTransaction? tx = await TryBeginTransactionAsync(ct);
        try
        {
            var template = await Query(tenantId).SingleOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new NotFoundException("PM template was not found.");

            // Step 1 — snapshot the current version for audit history, delete old children, commit
            var snapshot = BuildVersionSnapshot(tenantId, template);
            dbContext.PmTemplateVersions.Add(snapshot);
            dbContext.PmTemplateQuestions.RemoveRange(template.Questions);
            dbContext.PmTemplateSections.RemoveRange(template.Sections);
            await dbContext.SaveChangesAsync(ct);

            // Step 2 — clear tracker, re-fetch header, apply updates
            dbContext.ChangeTracker.Clear();
            template = await dbContext.PmTemplates
                .SingleAsync(x => x.TenantId == tenantId && x.Id == id, ct);

            template.AssetCategoryId = command.AssetCategoryId;
            template.Category = normalizedCategory;
            template.Name = normalizedName;
            template.Code = normalizedCode;
            template.Description = normalizedDescription;
            template.WorkType = normalizedWorkType;
            template.DefaultIntervalMonths = normalizedDefaultIntervalMonths;
            template.IsActive = command.IsActive;
            template.Version += 1;
            template.Questions = [];
            template.Sections = [];

            ApplyStructure(template, command);
            dbContext.PmTemplateSections.AddRange(template.Sections);
            dbContext.PmTemplateQuestions.AddRange(template.Questions);

            // Step 3 — save and commit
            await SaveChangesAsync(ct);
            if (tx is not null) await tx.CommitAsync(ct);
        }
        catch
        {
            if (tx is not null) await tx.RollbackAsync(ct);
            throw;
        }
        finally
        {
            if (tx is not null) await tx.DisposeAsync();
        }

        dbContext.ChangeTracker.Clear();
        return await Query(tenantId).SingleAsync(x => x.Id == id, ct);
    }

    public async Task<PmTemplate> DuplicateAsync(Guid tenantId, Guid id, string? name, CancellationToken ct)
    {
        var source = await Query(tenantId).SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException("PM template was not found.");

        var duplicateName = string.IsNullOrWhiteSpace(name) ? $"{source.Name} Copy" : name.Trim();
        await EnsureTemplateNameIsUniqueAsync(tenantId, source.Category, duplicateName, null, ct);

        var sections = source.Sections
            .OrderBy(x => x.DisplayOrder)
            .Select(x => new PmTemplateSection
            {
                SectionName = x.SectionName,
                Description = x.Description,
                DisplayOrder = x.DisplayOrder,
                IsActive = x.IsActive
            })
            .ToList();

        var sectionsByName = sections.ToDictionary(x => x.SectionName, StringComparer.OrdinalIgnoreCase);

        var template = new PmTemplate
        {
            TenantId = tenantId,
            AssetCategoryId = source.AssetCategoryId,
            Category = source.Category,
            Name = duplicateName,
            Code = null,
            Description = source.Description,
            WorkType = source.WorkType,
            DefaultIntervalMonths = source.DefaultIntervalMonths,
            AutoScheduleByDefault = source.AutoScheduleByDefault,
            IsActive = false,
            Version = 1,
            IsPlatformSeed = false,
            IsSystemTemplate = false,
            Sections = sections,
            Questions = source.Questions
                .OrderBy(x => x.Section?.DisplayOrder ?? 0)
                .ThenBy(x => x.DisplayOrder)
                .Select(x =>
                {
                    var sectionName = Normalize(x.SectionName);
                    return new PmTemplateQuestion
                    {
                        Section = sectionName is not null && sectionsByName.TryGetValue(sectionName, out var s) ? s : null,
                        SectionName = sectionName,
                        Prompt = x.Prompt,
                        HelperText = x.HelperText,
                        ResponseType = x.ResponseType,
                        Unit = x.Unit,
                        DefaultValue = x.DefaultValue,
                        MinValue = x.MinValue,
                        MaxValue = x.MaxValue,
                        IsRequired = x.IsRequired,
                        EvidenceRequired = x.EvidenceRequired,
                        TechnicianRemarksAllowed = x.TechnicianRemarksAllowed,
                        RequiresNoteOnFail = x.RequiresNoteOnFail,
                        IsActive = x.IsActive,
                        DisplayOrder = x.DisplayOrder,
                        OptionsJson = x.OptionsJson
                    };
                })
                .ToList()
        };

        dbContext.PmTemplates.Add(template);
        await SaveChangesAsync(ct);

        return await Query(tenantId).SingleAsync(x => x.Id == template.Id, ct);
    }

    public async Task<PmTemplate> SetActiveAsync(Guid tenantId, Guid id, bool isActive, CancellationToken ct)
    {
        var template = await Query(tenantId).SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException("PM template was not found.");

        template.IsActive = isActive;
        await SaveChangesAsync(ct);
        return template;
    }

    public async Task DeleteAsync(Guid tenantId, Guid id, Guid? deletedByUserId, CancellationToken ct)
    {
        // Bypass the soft-delete filter in Query so we can resolve even already-deleted templates.
        var template = await dbContext.PmTemplates
            .Include(x => x.Sections)
            .Include(x => x.Questions)
            .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, ct)
            ?? throw new NotFoundException("PM template was not found.");

        var isReferenced =
            await dbContext.WorkOrders.AnyAsync(x => x.TenantId == tenantId && x.PmTemplateId == id, ct)
            || await dbContext.PreventiveMaintenancePlans.AnyAsync(x => x.TenantId == tenantId && x.PmTemplateId == id, ct);

        if (isReferenced)
        {
            // Soft-delete: mark inactive and record who deleted it and when.
            template.IsActive = false;
            template.DeletedAt = DateTime.UtcNow;
            template.DeletedByUserId = deletedByUserId;
            await dbContext.SaveChangesAsync(ct);
            return;
        }

        // Hard-delete: no work orders or PM plans reference this template.
        dbContext.PmTemplates.Remove(template);
        await dbContext.SaveChangesAsync(ct);
    }

    // ── Validation ────────────────────────────────────────────────────────────

    public static void Validate(PmTemplateWriteCommand command)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(command.Category))
            errors.Add("Category is required.");

        if (string.IsNullOrWhiteSpace(command.Name))
            errors.Add("Template name is required.");

        if (!IsValidWorkType(command.WorkType))
            errors.Add("Work type must be PreventiveMaintenance or BreakdownCorrective.");

        if (command.DefaultIntervalMonths.HasValue && command.DefaultIntervalMonths.Value is not (1 or 3 or 6 or 12))
            errors.Add("Default interval must be 1, 3, 6, or 12 months.");

        if (command.Checklist.Count == 0)
            errors.Add("At least one checklist question is required.");

        for (var i = 0; i < command.Checklist.Count; i++)
        {
            var q = command.Checklist[i];
            var prefix = $"Question {i + 1}";

            if (string.IsNullOrWhiteSpace(q.Question))
                errors.Add($"{prefix}: question text is required.");

            if (!string.IsNullOrWhiteSpace(q.Type) && !AllowedQuestionTypes.Contains(q.Type))
                errors.Add($"{prefix}: type '{q.Type}' is not supported.");

            var optionsRequired = !string.IsNullOrWhiteSpace(q.Type) && IsOptionsRequired(q.Type);
            if (optionsRequired && (q.Options is null || q.Options.All(string.IsNullOrWhiteSpace)))
                errors.Add($"{prefix}: dropdown, pass/fail, and condition questions require at least one option.");

            if (q.MinValue.HasValue && q.MaxValue.HasValue && q.MinValue > q.MaxValue)
                errors.Add($"{prefix}: minimum value cannot exceed maximum value.");
        }

        if (errors.Count > 0)
            throw new BusinessRuleException(string.Join(" | ", errors));
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Attempts to begin a real database transaction. Returns null for providers that do not
    /// support transactions (InMemory, some SQLite modes) so callers can proceed without one.
    /// </summary>
    private async Task<IDbContextTransaction?> TryBeginTransactionAsync(CancellationToken ct)
    {
        try
        {
            return await dbContext.Database.BeginTransactionAsync(ct);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static PmTemplateVersion BuildVersionSnapshot(Guid tenantId, PmTemplate template)
    {
        var snapshotData = new
        {
            template.Id,
            template.Name,
            template.Category,
            template.WorkType,
            template.Description,
            template.DefaultIntervalMonths,
            template.IsActive,
            template.Version,
            Questions = template.Questions.Select(q => new
            {
                q.Id,
                q.SectionName,
                q.Prompt,
                q.ResponseType,
                q.IsRequired,
                q.DisplayOrder
            })
        };

        return new PmTemplateVersion
        {
            TenantId = tenantId,
            PmTemplateId = template.Id,
            Version = template.Version,
            SnapshotJson = JsonSerializer.Serialize(snapshotData),
            CreatedAt = DateTime.UtcNow
        };
    }

    private IQueryable<PmTemplate> Query(Guid tenantId) =>
        dbContext.PmTemplates
            .Include(x => x.AssetCategory)
            .Include(x => x.Sections)
            .Include(x => x.Questions).ThenInclude(x => x.Section)
            .Where(x => x.TenantId == tenantId && x.DeletedAt == null);

    private async Task EnsureAssetCategoryExistsAsync(Guid tenantId, Guid? assetCategoryId, CancellationToken ct)
    {
        if (!assetCategoryId.HasValue) return;

        var exists = await dbContext.AssetCategories
            .AnyAsync(x => x.TenantId == tenantId && x.Id == assetCategoryId.Value, ct);

        if (!exists)
            throw new BusinessRuleException("Linked asset template was not found for this tenant.");
    }

    private async Task EnsureTemplateNameIsUniqueAsync(
        Guid tenantId, string category, string name, Guid? excludeId, CancellationToken ct)
    {
        var exists = await dbContext.PmTemplates.AnyAsync(
            x => x.TenantId == tenantId
              && x.Category == category
              && x.Name == name
              && (!excludeId.HasValue || x.Id != excludeId.Value),
            ct);

        if (exists)
            throw new ConflictException("A template with this name already exists.");
    }

    private async Task SaveChangesAsync(CancellationToken ct)
    {
        try
        {
            await dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (TryMapSaveException(ex, out var mapped))
        {
            throw mapped;
        }
    }

    private static bool TryMapSaveException(DbUpdateException ex, out Exception mapped)
    {
        if (ex.InnerException is PostgresException pg)
        {
            if (pg.SqlState == PostgresErrorCodes.UniqueViolation
                && string.Equals(pg.ConstraintName, "IX_pm_templates_TenantId_Category_Name", StringComparison.Ordinal))
            {
                mapped = new ConflictException("A template with this name already exists.");
                return true;
            }

            if (pg.SqlState == PostgresErrorCodes.ForeignKeyViolation
                && string.Equals(pg.ConstraintName, "FK_pm_templates_asset_categories_AssetCategoryId", StringComparison.Ordinal))
            {
                mapped = new BusinessRuleException("Linked asset template was not found for this tenant.");
                return true;
            }
        }

        mapped = ex;
        return false;
    }

    private static void ApplyStructure(PmTemplate template, PmTemplateWriteCommand command)
    {
        var sections = command.Checklist
            .Select(x => new { Name = Normalize(x.SectionName) ?? "General", Order = x.SectionOrder ?? x.Order })
            .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x.Min(s => s.Order))
            .ThenBy(x => x.Key)
            .Select((x, i) => new PmTemplateSection
            {
                SectionName = x.Key,
                DisplayOrder = i + 1,
                IsActive = true
            })
            .ToList();

        var sectionsByName = sections.ToDictionary(x => x.SectionName, StringComparer.OrdinalIgnoreCase);
        template.Sections = sections;
        template.Questions = command.Checklist
            .OrderBy(x => x.SectionOrder ?? x.Order)
            .ThenBy(x => x.Order)
            .Select((q, i) =>
            {
                var sectionName = Normalize(q.SectionName) ?? "General";
                return MapQuestion(q, sectionsByName[sectionName], i + 1);
            })
            .ToList();
    }

    private static PmTemplateQuestion MapQuestion(
        PmTemplateChecklistItemCommand q, PmTemplateSection section, int displayOrder) =>
        new()
        {
            Section = section,
            SectionName = section.SectionName,
            Prompt = q.Question.Trim(),
            HelperText = Normalize(q.HelperText),
            ResponseType = NormalizeType(q.Type),
            Unit = Normalize(q.Unit),
            DefaultValue = Normalize(q.DefaultValue),
            MinValue = q.MinValue,
            MaxValue = q.MaxValue,
            IsRequired = q.Required,
            EvidenceRequired = q.EvidenceRequired,
            TechnicianRemarksAllowed = q.TechnicianRemarksAllowed ?? true,
            RequiresNoteOnFail = q.RequiresNoteOnFail,
            IsActive = true,
            DisplayOrder = displayOrder,
            OptionsJson = q.Options is { Count: > 0 }
                ? JsonSerializer.Serialize(
                    q.Options.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToList())
                : null
        };

    public static string NormalizeCategory(string category) => category.Trim();

    public static string NormalizeWorkType(string? workType)
    {
        var normalized = string.IsNullOrWhiteSpace(workType) ? "PreventiveMaintenance" : workType.Trim();
        return normalized switch
        {
            "PreventiveMaintenance" => "PreventiveMaintenance",
            "BreakdownCorrective" => "BreakdownCorrective",
            _ => throw new BusinessRuleException("Work type must be PreventiveMaintenance or BreakdownCorrective.")
        };
    }

    public static int? NormalizeDefaultIntervalMonths(int? value)
    {
        if (!value.HasValue) return null;
        return value.Value is 1 or 3 or 6 or 12
            ? value.Value
            : throw new BusinessRuleException("Default interval must be 1, 3, 6, or 12 months.");
    }

    public static string NormalizeType(string type) =>
        AllowedQuestionTypes.Single(x => string.Equals(x, type.Trim(), StringComparison.OrdinalIgnoreCase));

    public static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool IsValidWorkType(string? workType) =>
        workType is "PreventiveMaintenance" or "BreakdownCorrective"
        || string.IsNullOrWhiteSpace(workType); // null/empty defaults to PreventiveMaintenance

    private static bool IsOptionsRequired(string type) =>
        type.Equals("dropdown", StringComparison.OrdinalIgnoreCase)
        || type.Equals("passfail", StringComparison.OrdinalIgnoreCase)
        || type.Equals("condition", StringComparison.OrdinalIgnoreCase);
}
