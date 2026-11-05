using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Errors;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Tests;

/// <summary>
/// Unit tests for PmTemplateService business logic.
/// Uses EF InMemory so every test gets a clean, isolated database.
/// </summary>
public sealed class PmTemplateServiceTests
{
    // ── Validation ────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_MissingCategory_ThrowsWithCategoryError()
    {
        var cmd = ValidCommand() with { Category = "   " };
        var ex = Assert.Throws<BusinessRuleException>(() => PmTemplateService.Validate(cmd));
        Assert.Contains("Category is required", ex.Message);
    }

    [Fact]
    public void Validate_MissingName_ThrowsWithNameError()
    {
        var cmd = ValidCommand() with { Name = "" };
        var ex = Assert.Throws<BusinessRuleException>(() => PmTemplateService.Validate(cmd));
        Assert.Contains("Template name is required", ex.Message);
    }

    [Fact]
    public void Validate_EmptyChecklist_ThrowsWithChecklistError()
    {
        var cmd = ValidCommand() with { Checklist = [] };
        var ex = Assert.Throws<BusinessRuleException>(() => PmTemplateService.Validate(cmd));
        Assert.Contains("At least one checklist question", ex.Message);
    }

    [Fact]
    public void Validate_InvalidWorkType_ThrowsWithWorkTypeError()
    {
        var cmd = ValidCommand() with { WorkType = "NotAWorkType" };
        var ex = Assert.Throws<BusinessRuleException>(() => PmTemplateService.Validate(cmd));
        Assert.Contains("Work type", ex.Message);
    }

    [Fact]
    public void Validate_InvalidIntervalMonths_ThrowsWithIntervalError()
    {
        var cmd = ValidCommand() with { DefaultIntervalMonths = 7 };
        var ex = Assert.Throws<BusinessRuleException>(() => PmTemplateService.Validate(cmd));
        Assert.Contains("interval", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_QuestionWithInvertedMinMax_ThrowsPerQuestion()
    {
        var cmd = ValidCommand() with
        {
            Checklist =
            [
                new PmTemplateChecklistItemCommand("General", 1, "Reading", null, "number", "V", 100m, 10m, null, true, false, true, false, 1, null)
            ]
        };
        var ex = Assert.Throws<BusinessRuleException>(() => PmTemplateService.Validate(cmd));
        Assert.Contains("minimum value cannot exceed maximum", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_DropdownWithoutOptions_ThrowsOptionsError()
    {
        var cmd = ValidCommand() with
        {
            Checklist =
            [
                new PmTemplateChecklistItemCommand("General", 1, "Pick one", null, "dropdown", null, null, null, null, false, false, true, false, 1, null)
            ]
        };
        var ex = Assert.Throws<BusinessRuleException>(() => PmTemplateService.Validate(cmd));
        Assert.Contains("require at least one option", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_CollectsMultipleErrors_InOneException()
    {
        var cmd = new PmTemplateWriteCommand("", "", null, null, [], false);
        var ex = Assert.Throws<BusinessRuleException>(() => PmTemplateService.Validate(cmd));
        // At least three errors joined by " | "
        Assert.Contains(" | ", ex.Message);
        Assert.Contains("Category", ex.Message);
        Assert.Contains("Template name", ex.Message);
        Assert.Contains("At least one", ex.Message);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(6)]
    [InlineData(12)]
    public void Validate_ValidIntervalMonths_DoesNotThrow(int months)
    {
        var cmd = ValidCommand() with { DefaultIntervalMonths = months };
        PmTemplateService.Validate(cmd); // must not throw
    }

    // ── Normalizers ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData("text", "text")]
    [InlineData("TEXT", "text")]
    [InlineData("PassFail", "passfail")]
    [InlineData("YESNO", "yesno")]
    [InlineData("MultilineText", "multilinetext")]
    public void NormalizeType_NormalisesToLowercase(string input, string expected)
    {
        Assert.Equal(expected, PmTemplateService.NormalizeType(input));
    }

    [Fact]
    public void NormalizeType_UnknownType_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => PmTemplateService.NormalizeType("unknown"));
    }

    [Fact]
    public void NormalizeWorkType_NullOrEmpty_DefaultsToPreventiveMaintenance()
    {
        Assert.Equal("PreventiveMaintenance", PmTemplateService.NormalizeWorkType(null));
        Assert.Equal("PreventiveMaintenance", PmTemplateService.NormalizeWorkType(""));
        Assert.Equal("PreventiveMaintenance", PmTemplateService.NormalizeWorkType("   "));
    }

    [Theory]
    [InlineData("PreventiveMaintenance")]
    [InlineData("BreakdownCorrective")]
    public void NormalizeWorkType_ValidTypes_ReturnsSameValue(string type)
    {
        Assert.Equal(type, PmTemplateService.NormalizeWorkType(type));
    }

    [Fact]
    public void NormalizeWorkType_InvalidType_Throws()
    {
        Assert.Throws<BusinessRuleException>(() => PmTemplateService.NormalizeWorkType("Installation"));
    }

    [Theory]
    [InlineData("RunningCheck")]
    [InlineData("Inspection")]
    public void NormalizeWorkType_AcceptsFactoryChecklistTypes(string workType)
    {
        Assert.Equal(workType, PmTemplateService.NormalizeWorkType(workType));
    }

    [Fact]
    public void NormalizeDefaultIntervalMonths_Null_ReturnsNull()
    {
        Assert.Null(PmTemplateService.NormalizeDefaultIntervalMonths(null));
    }

    [Fact]
    public void NormalizeDefaultIntervalMonths_InvalidValue_Throws()
    {
        Assert.Throws<BusinessRuleException>(() => PmTemplateService.NormalizeDefaultIntervalMonths(2));
        Assert.Throws<BusinessRuleException>(() => PmTemplateService.NormalizeDefaultIntervalMonths(0));
        Assert.Throws<BusinessRuleException>(() => PmTemplateService.NormalizeDefaultIntervalMonths(24));
    }

    // ── Create ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_ValidCommand_PersistsTemplateWithSectionsAndQuestions()
    {
        await using var db = CreateDb();
        var svc = new PmTemplateService(db);
        var tenantId = Guid.NewGuid();

        var result = await svc.CreateAsync(tenantId, ValidCommand(), CancellationToken.None);

        Assert.Equal(tenantId, result.TenantId);
        Assert.Equal(1, result.Version);
        Assert.Single(result.Sections);
        Assert.Equal(2, result.Questions.Count);
        // Questions are ordered by DisplayOrder
        Assert.Equal("Visual check", result.Questions.First().Prompt);
    }

    [Fact]
    public async Task Create_TrimsNameAndCategory()
    {
        await using var db = CreateDb();
        var svc = new PmTemplateService(db);
        var cmd = ValidCommand() with { Name = "  Generator PM  ", Category = "  Generators  " };

        var result = await svc.CreateAsync(Guid.NewGuid(), cmd, CancellationToken.None);

        Assert.Equal("Generator PM", result.Name);
        Assert.Equal("Generators", result.Category);
    }

    [Fact]
    public async Task Create_SetsVersion1AndIsPlatformSeedFalse()
    {
        await using var db = CreateDb();
        var svc = new PmTemplateService(db);

        var result = await svc.CreateAsync(Guid.NewGuid(), ValidCommand(), CancellationToken.None);

        Assert.Equal(1, result.Version);
        Assert.False(result.IsPlatformSeed);
        Assert.False(result.IsSystemTemplate);
    }

    [Fact]
    public async Task Create_DuplicateNameSameCategory_ThrowsConflict()
    {
        await using var db = CreateDb();
        var svc = new PmTemplateService(db);
        var tenantId = Guid.NewGuid();

        await svc.CreateAsync(tenantId, ValidCommand(), CancellationToken.None);

        await Assert.ThrowsAsync<ConflictException>(() =>
            svc.CreateAsync(tenantId, ValidCommand(), CancellationToken.None));
    }

    [Fact]
    public async Task Create_SameNameDifferentTenant_Succeeds()
    {
        await using var db = CreateDb();
        var svc = new PmTemplateService(db);

        await svc.CreateAsync(Guid.NewGuid(), ValidCommand(), CancellationToken.None);
        var second = await svc.CreateAsync(Guid.NewGuid(), ValidCommand(), CancellationToken.None);

        Assert.NotNull(second);
    }

    // ── Update ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Update_IncrementsVersionAndReplacesQuestions()
    {
        await using var db = CreateDb();
        var svc = new PmTemplateService(db);
        var tenantId = Guid.NewGuid();

        var created = await svc.CreateAsync(tenantId, ValidCommand(), CancellationToken.None);
        Assert.Equal(1, created.Version);

        var updated = await svc.UpdateAsync(tenantId, created.Id,
            ValidCommand() with
            {
                Checklist =
                [
                    new PmTemplateChecklistItemCommand("Inspection", 1, "New item only", null, "checkbox", null, null, null, null, true, false, true, false, 1, null)
                ]
            },
            CancellationToken.None);

        Assert.Equal(2, updated.Version);
        Assert.Single(updated.Questions);
        Assert.Equal("New item only", updated.Questions.Single().Prompt);
    }

    [Fact]
    public async Task Update_CapturesVersionSnapshot()
    {
        await using var db = CreateDb();
        var svc = new PmTemplateService(db);
        var tenantId = Guid.NewGuid();

        var created = await svc.CreateAsync(tenantId, ValidCommand(), CancellationToken.None);
        await svc.UpdateAsync(tenantId, created.Id, ValidCommand() with { Name = "Generator PM v2" }, CancellationToken.None);

        var snapshots = await db.PmTemplateVersions
            .Where(x => x.PmTemplateId == created.Id)
            .ToListAsync();

        Assert.Single(snapshots);
        Assert.Equal(1, snapshots[0].Version);
        Assert.Contains("Generator PM", snapshots[0].SnapshotJson);
    }

    [Fact]
    public async Task Update_NonExistentId_ThrowsNotFound()
    {
        await using var db = CreateDb();
        var svc = new PmTemplateService(db);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            svc.UpdateAsync(Guid.NewGuid(), Guid.NewGuid(), ValidCommand(), CancellationToken.None));
    }

    // ── Delete ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_UnreferencedTemplate_HardDeletes()
    {
        await using var db = CreateDb();
        var svc = new PmTemplateService(db);
        var tenantId = Guid.NewGuid();

        var created = await svc.CreateAsync(tenantId, ValidCommand(), CancellationToken.None);
        await svc.DeleteAsync(tenantId, created.Id, Guid.NewGuid(), CancellationToken.None);

        var exists = await db.PmTemplates.AnyAsync(x => x.Id == created.Id);
        Assert.False(exists);
    }

    [Fact]
    public async Task Delete_ReferencedTemplate_SoftDeletesSetsDeletedAt()
    {
        await using var db = CreateDb();
        var svc = new PmTemplateService(db);
        var tenantId = Guid.NewGuid();
        var deletedById = Guid.NewGuid();

        var created = await svc.CreateAsync(tenantId, ValidCommand(), CancellationToken.None);

        // Add a work order referencing this template so hard-delete is blocked.
        db.WorkOrders.Add(new Ecosys.Domain.Entities.WorkOrder
        {
            TenantId = tenantId,
            PmTemplateId = created.Id,
            WorkOrderNumber = "WO-DEL-001",
            Title = "Referenced WO",
            Priority = "Medium",
            Status = "Open"
        });
        await db.SaveChangesAsync();

        await svc.DeleteAsync(tenantId, created.Id, deletedById, CancellationToken.None);

        var persisted = await db.PmTemplates.SingleAsync(x => x.Id == created.Id);
        Assert.False(persisted.IsActive);
        Assert.NotNull(persisted.DeletedAt);
        Assert.Equal(deletedById, persisted.DeletedByUserId);
    }

    [Fact]
    public async Task Delete_NonExistentId_ThrowsNotFound()
    {
        await using var db = CreateDb();
        var svc = new PmTemplateService(db);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            svc.DeleteAsync(Guid.NewGuid(), Guid.NewGuid(), null, CancellationToken.None));
    }

    // ── Duplicate ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Duplicate_CreatesInactiveCopyWithDefaultCopyName()
    {
        await using var db = CreateDb();
        var svc = new PmTemplateService(db);
        var tenantId = Guid.NewGuid();

        var created = await svc.CreateAsync(tenantId, ValidCommand(), CancellationToken.None);
        var copy = await svc.DuplicateAsync(tenantId, created.Id, null, CancellationToken.None);

        Assert.Equal("Generator PM Copy", copy.Name);
        Assert.False(copy.IsActive);
        Assert.Equal(1, copy.Version);
        Assert.Equal(created.Questions.Count, copy.Questions.Count);
    }

    [Fact]
    public async Task Duplicate_WithCustomName_UsesProvidedName()
    {
        await using var db = CreateDb();
        var svc = new PmTemplateService(db);
        var tenantId = Guid.NewGuid();

        var created = await svc.CreateAsync(tenantId, ValidCommand(), CancellationToken.None);
        var copy = await svc.DuplicateAsync(tenantId, created.Id, "Custom Copy Name", CancellationToken.None);

        Assert.Equal("Custom Copy Name", copy.Name);
    }

    // ── SetActive ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task SetActive_TogglesIsActive()
    {
        await using var db = CreateDb();
        var svc = new PmTemplateService(db);
        var tenantId = Guid.NewGuid();

        var created = await svc.CreateAsync(tenantId, ValidCommand(), CancellationToken.None);
        Assert.True(created.IsActive);

        var deactivated = await svc.SetActiveAsync(tenantId, created.Id, false, CancellationToken.None);
        Assert.False(deactivated.IsActive);

        var reactivated = await svc.SetActiveAsync(tenantId, created.Id, true, CancellationToken.None);
        Assert.True(reactivated.IsActive);
    }

    // ── List / Get ────────────────────────────────────────────────────────────

    [Fact]
    public async Task List_ReturnsOnlyTemplatesForRequestedTenant()
    {
        await using var db = CreateDb();
        var svc = new PmTemplateService(db);
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        await svc.CreateAsync(tenantA, ValidCommand(), CancellationToken.None);
        await svc.CreateAsync(tenantB, ValidCommand(), CancellationToken.None);

        var result = await svc.ListAsync(tenantA, CancellationToken.None);

        Assert.Single(result);
        Assert.All(result, t => Assert.Equal(tenantA, t.TenantId));
    }

    [Fact]
    public async Task List_ExcludesSoftDeletedTemplates()
    {
        await using var db = CreateDb();
        var svc = new PmTemplateService(db);
        var tenantId = Guid.NewGuid();

        var active = await svc.CreateAsync(tenantId, ValidCommand(), CancellationToken.None);
        var deleted = await svc.CreateAsync(
            tenantId,
            ValidCommand() with { Name = "Generator PM 2" },
            CancellationToken.None);

        db.WorkOrders.Add(new Ecosys.Domain.Entities.WorkOrder
        {
            TenantId = tenantId,
            PmTemplateId = deleted.Id,
            WorkOrderNumber = "WO-DEL-002",
            Title = "Referenced WO",
            Priority = "Medium",
            Status = "Open"
        });
        await db.SaveChangesAsync();

        await svc.DeleteAsync(tenantId, deleted.Id, Guid.NewGuid(), CancellationToken.None);

        var result = await svc.ListAsync(tenantId, CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(active.Id, result.Single().Id);
    }

    [Fact]
    public async Task Get_WrongTenant_ThrowsNotFound()
    {
        await using var db = CreateDb();
        var svc = new PmTemplateService(db);
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        var created = await svc.CreateAsync(tenantA, ValidCommand(), CancellationToken.None);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            svc.GetAsync(tenantB, created.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Get_SoftDeletedTemplate_ThrowsNotFound()
    {
        await using var db = CreateDb();
        var svc = new PmTemplateService(db);
        var tenantId = Guid.NewGuid();

        var created = await svc.CreateAsync(tenantId, ValidCommand(), CancellationToken.None);
        db.WorkOrders.Add(new Ecosys.Domain.Entities.WorkOrder
        {
            TenantId = tenantId,
            PmTemplateId = created.Id,
            WorkOrderNumber = "WO-DEL-003",
            Title = "Referenced WO",
            Priority = "Medium",
            Status = "Open"
        });
        await db.SaveChangesAsync();

        await svc.DeleteAsync(tenantId, created.Id, Guid.NewGuid(), CancellationToken.None);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            svc.GetAsync(tenantId, created.Id, CancellationToken.None));
    }

    // ── Section ordering ──────────────────────────────────────────────────────

    [Fact]
    public async Task Create_MultipleSections_OrderedByFirstItemAppearance()
    {
        await using var db = CreateDb();
        var svc = new PmTemplateService(db);
        var tenantId = Guid.NewGuid();

        var cmd = ValidCommand() with
        {
            Checklist =
            [
                new PmTemplateChecklistItemCommand("Safety", 2, "Wear PPE", null, "checkbox", null, null, null, null, true, false, true, false, 2, null),
                new PmTemplateChecklistItemCommand("Readings", 1, "Battery V", null, "number", "V", null, null, null, true, false, true, false, 1, null),
                new PmTemplateChecklistItemCommand("Safety", 2, "Lock out", null, "checkbox", null, null, null, null, true, false, true, false, 3, null),
            ]
        };

        var result = await svc.CreateAsync(tenantId, cmd, CancellationToken.None);

        var sections = result.Sections.OrderBy(s => s.DisplayOrder).ToList();
        Assert.Equal("Readings", sections[0].SectionName);
        Assert.Equal("Safety", sections[1].SectionName);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static AppDbContext CreateDb()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"pm-template-svc-{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(opts);
    }

    private static PmTemplateWriteCommand ValidCommand() =>
        new(
            Name: "Generator PM",
            Category: "Generators",
            Description: "Standard generator PM checklist",
            AssetCategoryId: null,
            Checklist:
            [
                new PmTemplateChecklistItemCommand("General", 1, "Visual check", null, "checkbox", null, null, null, null, true, false, true, false, 1, null),
                new PmTemplateChecklistItemCommand("General", 1, "Oil level reading", null, "number", "L", 0m, 10m, null, true, false, true, false, 2, null),
            ],
            IsActive: true,
            WorkType: "PreventiveMaintenance",
            DefaultIntervalMonths: 3);
}
