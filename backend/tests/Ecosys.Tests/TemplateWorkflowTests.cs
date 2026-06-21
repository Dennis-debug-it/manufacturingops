using Ecosys.Api.Controllers;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Tests;

public sealed class TemplateWorkflowTests
{
    [Fact]
    public async Task StarterPackSeeder_CreatesThreeSystemStarterPacks()
    {
        await using var dbContext = CreateDbContext();
        var service = new StarterPackService(dbContext);

        await service.EnsureSystemStarterPacksSeededAsync(CancellationToken.None);

        var packs = await dbContext.SystemTemplatePacks
            .Include(x => x.AssetCategories)
            .Include(x => x.ChecklistTemplates)
            .OrderBy(x => x.Name)
            .ToListAsync();

        Assert.Equal(3, packs.Count);
        Assert.Contains(packs, item => item.Name == "Generator Contractor Starter Pack");
        Assert.Contains(packs, item => item.Name == "HVAC Contractor Starter Pack");
        Assert.Contains(packs, item => item.Name == "Solar Contractor Starter Pack");
        Assert.All(packs, item =>
        {
            Assert.True(item.IsSystem);
            Assert.NotEmpty(item.AssetCategories);
            Assert.NotEmpty(item.ChecklistTemplates);
        });
    }

    [Fact]
    public async Task InstallingStarterPack_CopiesEditableTenantTemplates_AndSystemTemplatesRemainProtected()
    {
        await using var dbContext = CreateDbContext();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await SeedTenantAsync(dbContext, tenantId, userId);

        var service = new StarterPackService(dbContext);
        await service.EnsureSystemStarterPacksSeededAsync(CancellationToken.None);

        var starterPack = await dbContext.SystemTemplatePacks.SingleAsync(x => x.Code == "generator-contractor-starter");
        await service.InstallStarterPackAsync(tenantId, starterPack.Id, userId, CancellationToken.None);

        var installation = await dbContext.TenantTemplatePackInstallations.SingleAsync(x => x.TenantId == tenantId && x.SourceTemplatePackId == starterPack.Id);
        Assert.Equal("Installed", installation.Status);

        var copiedCategory = await dbContext.AssetCategories
            .Include(x => x.Fields)
            .SingleAsync(x => x.TenantId == tenantId && x.SourceAssetCategoryId != null && x.Name == "Diesel Generator");
        Assert.False(copiedCategory.IsSystemTemplate);
        Assert.NotEmpty(copiedCategory.Fields);
        Assert.Contains(copiedCategory.Fields, field => field.FieldLabel == "Make");

        var copiedChecklist = await dbContext.PmTemplates
            .Include(x => x.Questions)
            .SingleAsync(x => x.TenantId == tenantId && x.SourceTemplateId != null && x.Name == "Generator Preventive Maintenance");
        Assert.False(copiedChecklist.IsSystemTemplate);
        Assert.Contains(copiedChecklist.Questions, item => item.Prompt == "Check battery voltage");

        var templatesController = CreateTemplatesController(dbContext, tenantId, userId);
        var updateResult = await templatesController.Update(
            copiedChecklist.Id,
            new UpsertTemplateRequest(
                "Generator Preventive Maintenance - Edited",
                "Generator",
                "Tenant-edited checklist",
                null,
                [
                    new UpsertTemplateChecklistItemRequest("Safety and Pre-Checks", 1, "Confirm work permit/access approval", null, "checkbox", null, null, null, null, true, false, true, false, 1, null)
                ],
                true,
                "generator-pm-edited",
                "PreventiveMaintenance",
                1),
            CancellationToken.None);

        var updatedTemplate = Assert.IsType<TemplateResponse>(Assert.IsType<OkObjectResult>(updateResult.Result).Value);
        Assert.Equal("Generator Preventive Maintenance - Edited", updatedTemplate.Name);

        var systemTemplate = await dbContext.PmTemplates.SingleAsync(x => x.TenantId == null && x.Name == "Generator Preventive Maintenance");
        await Assert.ThrowsAsync<NotFoundException>(() =>
            templatesController.Update(
                systemTemplate.Id,
                new UpsertTemplateRequest(
                    "Should not edit system template",
                    "Generator",
                    "Blocked",
                    null,
                    [
                        new UpsertTemplateChecklistItemRequest("General", 1, "Prompt", null, "text", null, null, null, null, true, false, true, false, 1, null)
                    ],
                    true),
                CancellationToken.None));
    }

    [Fact]
    public async Task TemplateController_CreateEditDuplicateActivateDeactivate_UsesTenantScopedBackendData()
    {
        await using var dbContext = CreateDbContext();
        var tenantId = Guid.NewGuid();
        var controller = CreateTemplatesController(dbContext, tenantId, Guid.NewGuid());

        var create = await controller.Create(
            TemplateRequest(
                "Generator Monthly PM",
                "Generators",
                [
                    Item("Visual", 2, "Check enclosure", "passfail", 2, options: ["Pass", "Fail"], evidenceRequired: true),
                    Item("Readings", 1, "Battery voltage", "number", 1, unit: "V", min: 12m, max: 14m)
                ]),
            CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(create.Result);
        var template = Assert.IsType<TemplateResponse>(created.Value);
        Assert.Equal(2, template.Checklist.Count);
        Assert.Equal("Readings", template.Checklist.First().SectionName);
        Assert.Equal(1, template.Version);

        var update = await controller.Update(
            template.Id,
            TemplateRequest(
                "Generator Monthly PM",
                "Generators",
                [
                    Item("Visual", 1, "Check enclosure and labels", "passfail", 1, options: ["Pass", "Fail"], evidenceRequired: true)
                ],
                isActive: true),
            CancellationToken.None);
        var updated = Assert.IsType<OkObjectResult>(update.Result);
        var updatedTemplate = Assert.IsType<TemplateResponse>(updated.Value);
        Assert.Equal(2, updatedTemplate.Version);
        Assert.Equal("Check enclosure and labels", Assert.Single(updatedTemplate.Checklist).Question);

        var duplicate = await controller.Duplicate(template.Id, new DuplicateTemplateRequest("Generator Monthly PM Copy"), CancellationToken.None);
        var duplicated = Assert.IsType<CreatedAtActionResult>(duplicate.Result);
        var copiedTemplate = Assert.IsType<TemplateResponse>(duplicated.Value);
        Assert.False(copiedTemplate.IsActive);
        Assert.Equal("Generator Monthly PM Copy", copiedTemplate.Name);

        var deactivate = await controller.Deactivate(template.Id, CancellationToken.None);
        Assert.False(Assert.IsType<TemplateResponse>(Assert.IsType<OkObjectResult>(deactivate.Result).Value).IsActive);

        var activate = await controller.Activate(template.Id, CancellationToken.None);
        Assert.True(Assert.IsType<TemplateResponse>(Assert.IsType<OkObjectResult>(activate.Result).Value).IsActive);

        dbContext.PmTemplates.Add(new PmTemplate { TenantId = Guid.NewGuid(), Category = "Generators", Name = "Other tenant", IsActive = true });
        await dbContext.SaveChangesAsync();
        var list = await controller.GetAll(CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(list.Result);
        var templates = Assert.IsAssignableFrom<IReadOnlyCollection<TemplateResponse>>(ok.Value);
        Assert.DoesNotContain(templates, item => item.Name == "Other tenant");
    }

    [Fact]
    public async Task TemplateController_CreateWithoutOptionalFields_SetsTenantIdServerSide()
    {
        await using var dbContext = CreateDbContext();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await SeedTenantAsync(dbContext, tenantId, userId);

        var controller = CreateTemplatesController(dbContext, tenantId, userId);
        var create = await controller.Create(
            new UpsertTemplateRequest(
                "Generator Monthly PM",
                "Generators",
                null,
                null,
                [
                    new UpsertTemplateChecklistItemRequest("Visual", 1, "Check enclosure", null, "passfail", null, null, null, null, true, false, true, false, 1, ["Pass", "Fail"])
                ],
                true,
                null,
                "PreventiveMaintenance",
                null),
            CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(create.Result);
        var response = Assert.IsType<TemplateResponse>(created.Value);
        var persisted = await dbContext.PmTemplates.SingleAsync(x => x.Id == response.Id);

        Assert.Equal(tenantId, persisted.TenantId);
        Assert.Null(persisted.Description);
        Assert.Null(persisted.Code);
        Assert.Null(persisted.AssetCategoryId);
        Assert.True(persisted.CreatedAt > DateTime.MinValue);
    }

    [Fact]
    public async Task TemplateController_RejectsMissingName()
    {
        await using var dbContext = CreateDbContext();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await SeedTenantAsync(dbContext, tenantId, userId);

        var controller = CreateTemplatesController(dbContext, tenantId, userId);

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            controller.Create(
                new UpsertTemplateRequest(
                    "   ",
                    "Generators",
                    null,
                    null,
                    [
                        new UpsertTemplateChecklistItemRequest("Visual", 1, "Check enclosure", null, "passfail", null, null, null, null, true, false, true, false, 1, ["Pass", "Fail"])
                    ],
                    true),
                CancellationToken.None));

        Assert.Equal("Template name is required.", exception.Message);
    }

    [Fact]
    public async Task TemplateController_RejectsInvalidAssetCategory()
    {
        await using var dbContext = CreateDbContext();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await SeedTenantAsync(dbContext, tenantId, userId);

        var controller = CreateTemplatesController(dbContext, tenantId, userId);
        var invalidAssetCategoryId = Guid.NewGuid();

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            controller.Create(
                new UpsertTemplateRequest(
                    "Generator Monthly PM",
                    "Generators",
                    null,
                    invalidAssetCategoryId,
                    [
                        new UpsertTemplateChecklistItemRequest("Visual", 1, "Check enclosure", null, "passfail", null, null, null, null, true, false, true, false, 1, ["Pass", "Fail"])
                    ],
                    true),
                CancellationToken.None));

        Assert.Equal("Linked asset template was not found for this tenant.", exception.Message);
    }

    [Fact]
    public async Task TemplateController_RejectsDuplicateNamePerTenant()
    {
        await using var dbContext = CreateDbContext();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await SeedTenantAsync(dbContext, tenantId, userId);

        var controller = CreateTemplatesController(dbContext, tenantId, userId);
        var request = new UpsertTemplateRequest(
            "Generator Monthly PM",
            "Generators",
            "Template description",
            null,
            [
                new UpsertTemplateChecklistItemRequest("Visual", 1, "Check enclosure", null, "passfail", null, null, null, null, true, false, true, false, 1, ["Pass", "Fail"])
            ],
            true);

        await controller.Create(request, CancellationToken.None);

        var exception = await Assert.ThrowsAsync<ConflictException>(() => controller.Create(request, CancellationToken.None));
        Assert.Equal("A template with this name already exists.", exception.Message);
    }

    [Fact]
    public async Task AttachTemplate_CopiesOrderedVersionedSnapshot_AndCompletionValidationHonorsRequiredEvidenceAndRange()
    {
        await using var dbContext = CreateDbContext();
        var tenantId = Guid.NewGuid();
        var workOrder = new WorkOrder
        {
            TenantId = tenantId,
            ClientId = Guid.NewGuid(),
            WorkOrderNumber = "WO-TPL-001",
            Title = "PM",
            Priority = "Medium",
            Status = "Open",
            IsPreventiveMaintenance = true
        };
        var template = new PmTemplate
        {
            TenantId = tenantId,
            Category = "Generators",
            Name = "Generator PM",
            Version = 3,
            IsActive = true,
            Sections =
            [
                new PmTemplateSection { SectionName = "Readings", DisplayOrder = 1 },
                new PmTemplateSection { SectionName = "Visual", DisplayOrder = 2 }
            ]
        };
        template.Questions =
        [
            new PmTemplateQuestion { Section = template.Sections.ElementAt(1), SectionName = "Visual", Prompt = "Photo evidence", ResponseType = "passfail", DisplayOrder = 2, IsRequired = true, EvidenceRequired = true, OptionsJson = "[\"Pass\",\"Fail\"]" },
            new PmTemplateQuestion { Section = template.Sections.ElementAt(0), SectionName = "Readings", Prompt = "Battery voltage", ResponseType = "number", Unit = "V", MinValue = 12m, MaxValue = 14m, DisplayOrder = 1, IsRequired = true }
        ];

        dbContext.AddRange(workOrder, template);
        await dbContext.SaveChangesAsync();

        var service = new PmWorkOrderChecklistService(dbContext, new AuditLogService(dbContext, new Microsoft.AspNetCore.Http.HttpContextAccessor()));
        var copied = await service.AttachPmTemplateToWorkOrderAsync(workOrder.Id, template.Id, tenantId, Guid.NewGuid());
        Assert.Equal(2, copied);

        var snapshot = await dbContext.WorkOrders.Include(x => x.ChecklistItems).SingleAsync(x => x.Id == workOrder.Id);
        Assert.Equal("Generator PM", snapshot.PmTemplateNameSnapshot);
        Assert.Equal(3, snapshot.PmTemplateVersionSnapshot);
        Assert.Equal(["Battery voltage", "Photo evidence"], snapshot.ChecklistItems.OrderBy(x => x.SectionOrder).ThenBy(x => x.SortOrder).Select(x => x.QuestionText).ToArray());

        template.Name = "Edited future template";
        template.Version = 4;
        await dbContext.SaveChangesAsync();
        Assert.Equal("Generator PM", snapshot.PmTemplateNameSnapshot);
        Assert.Equal(3, snapshot.PmTemplateVersionSnapshot);

        var voltage = snapshot.ChecklistItems.Single(x => x.QuestionText == "Battery voltage");
        var rangeError = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.UpdateChecklistItemAsync(workOrder.Id, voltage.Id, tenantId, Guid.NewGuid(), "15", null, true));
        Assert.Contains("at most", rangeError.Message, StringComparison.OrdinalIgnoreCase);

        await service.UpdateChecklistItemAsync(workOrder.Id, voltage.Id, tenantId, Guid.NewGuid(), "13", "Nominal", true);

        var evidenceItem = snapshot.ChecklistItems.Single(x => x.QuestionText == "Photo evidence");
        await service.UpdateChecklistItemAsync(workOrder.Id, evidenceItem.Id, tenantId, Guid.NewGuid(), "Pass", "Clean", true);

        var missingEvidence = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.EnsureRequiredChecklistCompletedAsync(workOrder.Id, tenantId));
        Assert.Contains("evidence", missingEvidence.Message, StringComparison.OrdinalIgnoreCase);

        var evidenceAttachment = new Attachment
        {
            TenantId = tenantId,
            EntityType = "WorkOrderChecklistItem",
            EntityId = snapshot.ChecklistItems.Single(x => x.QuestionText == "Photo evidence").Id,
            FileName = "evidence.jpg",
            MimeType = "image/jpeg",
            StoragePath = "tenant/evidence.jpg",
            PublicUrl = "/api/attachments/evidence/download",
            UploadedByUserId = Guid.NewGuid()
        };
        dbContext.Attachments.Add(evidenceAttachment);
        evidenceItem.AttachmentId = evidenceAttachment.Id;
        await dbContext.SaveChangesAsync();

        await service.EnsureRequiredChecklistCompletedAsync(workOrder.Id, tenantId);
    }


    // ── Idempotency & partial-install repair tests ─────────────────────────────

    [Fact]
    public async Task InstallingStarterPack_SecondCall_IsIdempotentAndReturnsSuccess()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await SeedTenantAsync(dbContext, tenantId, userId);

        var service = new StarterPackService(dbContext);
        await service.EnsureSystemStarterPacksSeededAsync(CancellationToken.None);

        var pack = await dbContext.SystemTemplatePacks.SingleAsync(x => x.Code == "generator-contractor-starter");

        // Act — install twice
        await service.InstallStarterPackAsync(tenantId, pack.Id, userId, CancellationToken.None);
        var secondResult = await service.InstallStarterPackAsync(tenantId, pack.Id, userId, CancellationToken.None);

        // Assert — installation is still marked installed, no duplicate records
        Assert.True(secondResult.IsInstalled);

        var categoryCount = await dbContext.AssetCategories
            .CountAsync(x => x.TenantId == tenantId && x.SourceAssetCategoryId != null);
        var templateCount = await dbContext.PmTemplates
            .CountAsync(x => x.TenantId == tenantId && x.SourceTemplateId != null);
        var installationCount = await dbContext.TenantTemplatePackInstallations
            .CountAsync(x => x.TenantId == tenantId && x.SourceTemplatePackId == pack.Id);

        // Exactly the categories and checklists from the generator pack — no duplicates.
        Assert.Equal(6, categoryCount);
        Assert.Equal(2, templateCount);
        Assert.Equal(1, installationCount);
    }

    [Fact]
    public async Task InstallingStarterPack_WithPartialExistingRecords_CompletesInstallSuccessfully()
    {
        // Arrange — simulate a partial install: some categories already copied but
        // no installation record (mimics the observed production state).
        await using var dbContext = CreateDbContext();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await SeedTenantAsync(dbContext, tenantId, userId);

        var service = new StarterPackService(dbContext);
        await service.EnsureSystemStarterPacksSeededAsync(CancellationToken.None);

        var pack = await dbContext.SystemTemplatePacks
            .Include(x => x.AssetCategories.Where(c => c.TenantId == null && c.IsSystemTemplate))
            .SingleAsync(x => x.Code == "generator-contractor-starter");

        // Manually insert a subset of category copies — no installation record, no checklists.
        var partialCategories = pack.AssetCategories.Take(3).ToList();
        foreach (var src in partialCategories)
        {
            dbContext.AssetCategories.Add(new AssetCategory
            {
                TenantId = tenantId,
                TemplatePackId = null,
                SourceAssetCategoryId = src.Id,
                Name = src.Name,
                Code = src.Code,
                ContractorType = src.ContractorType,
                Icon = src.Icon,
                IsActive = true,
                IsSystemTemplate = false,
                DisplayOrder = src.DisplayOrder
            });
        }
        await dbContext.SaveChangesAsync();

        // Act — full install attempt should complete the missing records
        var result = await service.InstallStarterPackAsync(tenantId, pack.Id, userId, CancellationToken.None);

        // Assert — all categories and checklists now exist exactly once
        Assert.True(result.IsInstalled);

        var categoryCount = await dbContext.AssetCategories
            .CountAsync(x => x.TenantId == tenantId && x.SourceAssetCategoryId != null);
        var templateCount = await dbContext.PmTemplates
            .CountAsync(x => x.TenantId == tenantId && x.SourceTemplateId != null);

        Assert.Equal(6, categoryCount);  // 3 pre-existing + 3 newly inserted = 6 total, no duplicates
        Assert.Equal(2, templateCount);
    }

    [Fact]
    public async Task InstallingStarterPack_TenantCopiesHaveNullTemplatePackId_NoIndexCollisionWithSystemTemplates()
    {
        // Ensures the fix: tenant copies must NOT carry the system TemplatePackId.
        // If they did, the (TemplatePackId, Code) unique index in PostgreSQL would reject them
        // because system templates already occupy every (packId, code) slot.
        await using var dbContext = CreateDbContext();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await SeedTenantAsync(dbContext, tenantId, userId);

        var service = new StarterPackService(dbContext);
        await service.EnsureSystemStarterPacksSeededAsync(CancellationToken.None);

        var pack = await dbContext.SystemTemplatePacks.SingleAsync(x => x.Code == "hvac-contractor-starter");
        await service.InstallStarterPackAsync(tenantId, pack.Id, userId, CancellationToken.None);

        // All tenant copies must have TemplatePackId = null
        var categoriesWithPackId = await dbContext.AssetCategories
            .Where(x => x.TenantId == tenantId && x.TemplatePackId != null)
            .CountAsync();
        var templatesWithPackId = await dbContext.PmTemplates
            .Where(x => x.TenantId == tenantId && x.TemplatePackId != null)
            .CountAsync();

        Assert.Equal(0, categoriesWithPackId);
        Assert.Equal(0, templatesWithPackId);
    }

    [Fact]
    public async Task InstallingStarterPack_AllThreePacks_NoDuplicateRecordsAndAllMarkedInstalled()
    {
        // Regression: installing all three packs for the same tenant must not produce
        // any duplicate records and all must be marked installed.
        await using var dbContext = CreateDbContext();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await SeedTenantAsync(dbContext, tenantId, userId);

        var service = new StarterPackService(dbContext);
        await service.EnsureSystemStarterPacksSeededAsync(CancellationToken.None);

        var packs = await dbContext.SystemTemplatePacks.ToListAsync();
        foreach (var pack in packs)
        {
            await service.InstallStarterPackAsync(tenantId, pack.Id, userId, CancellationToken.None);
        }

        var installations = await dbContext.TenantTemplatePackInstallations
            .Where(x => x.TenantId == tenantId)
            .ToListAsync();
        Assert.Equal(3, installations.Count);
        Assert.All(installations, i => Assert.Equal("Installed", i.Status));

        // No tenant category should appear more than once per source
        var allCategorySourceIds = await dbContext.AssetCategories
            .Where(x => x.TenantId == tenantId && x.SourceAssetCategoryId != null)
            .Select(x => x.SourceAssetCategoryId)
            .ToListAsync();
        var duplicateCategories = allCategorySourceIds.GroupBy(x => x).Count(g => g.Count() > 1);
        Assert.Equal(0, duplicateCategories);

        var allTemplateSourceIds = await dbContext.PmTemplates
            .Where(x => x.TenantId == tenantId && x.SourceTemplateId != null)
            .Select(x => x.SourceTemplateId)
            .ToListAsync();
        var duplicateTemplates = allTemplateSourceIds.GroupBy(x => x).Count(g => g.Count() > 1);
        Assert.Equal(0, duplicateTemplates);
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ecosys-template-workflow-{Guid.NewGuid():N}")
            .Options;

        return new AppDbContext(options);
    }

    private static PmTemplatesController CreateTemplatesController(AppDbContext dbContext, Guid tenantId, Guid userId) =>
        new(new FakeTenantContext(tenantId, userId), new NoOpUserAccessService(), new PmTemplateService(dbContext));

    private static async Task SeedTenantAsync(AppDbContext dbContext, Guid tenantId, Guid userId)
    {
        var tenant = new Tenant
        {
            Id = tenantId,
            Name = "Tenant",
            Slug = $"tenant-{Guid.NewGuid():N}".Substring(0, 12),
            CompanyName = "Tenant Company",
            Email = "tenant@example.com",
            Country = "Kenya",
            Status = "Active",
            LicenseStatus = "Active",
            PrimaryColor = "#0F4C81",
            SecondaryColor = "#F4B942",
            IsActive = true
        };

        var user = new User
        {
            Id = userId,
            TenantId = tenantId,
            FullName = "Tenant Admin",
            Email = $"tenant-admin-{Guid.NewGuid():N}@example.com",
            PasswordHash = "hash",
            Role = AppRoles.Admin,
            IsActive = true,
            HasAllBranchAccess = true
        };

        dbContext.AddRange(tenant, user);
        await dbContext.SaveChangesAsync();
    }

    private static UpsertTemplateRequest TemplateRequest(string name, string category, IReadOnlyCollection<UpsertTemplateChecklistItemRequest> items, bool isActive = true) =>
        new(name, category, "Template description", null, items, isActive);

    private static UpsertTemplateChecklistItemRequest Item(
        string sectionName,
        int sectionOrder,
        string question,
        string type,
        int order,
        string? unit = null,
        decimal? min = null,
        decimal? max = null,
        IReadOnlyCollection<string>? options = null,
        bool evidenceRequired = false) =>
        new(sectionName, sectionOrder, question, "Helper", type, unit, min, max, null, true, evidenceRequired, true, false, order, options);

    private sealed class FakeTenantContext(Guid tenantId, Guid userId) : ITenantContext
    {
        public Guid? TenantId => tenantId;
        public Guid? UserId => userId;
        public Guid? SessionId => Guid.NewGuid();
        public string? Email => "admin@tenant.test";
        public string? Role => AppRoles.Admin;
        public string? JobTitle => "Admin";
        public bool IsAuthenticated => true;
        public bool IsSuperAdmin => false;
        public bool IsAdmin => true;
        public bool HasRole(string role) => string.Equals(role, AppRoles.Admin, StringComparison.OrdinalIgnoreCase);
        public bool HasPermission(string permissionName) => true;
        public Guid GetRequiredTenantId() => tenantId;
        public Guid GetRequiredUserId() => userId;
        public Guid GetRequiredSessionId() => SessionId!.Value;
    }

    private sealed class NoOpUserAccessService : IUserAccessService
    {
        public void EnsureAdmin() { }
        public void EnsureAdminOrPermission(string permissionName) { }
        public void EnsureAdminOrAnyPermission(params string[] permissionNames) { }
        public void EnsureTenantOperationalAccess() { }
    }
}
