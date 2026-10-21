using System.Security.Claims;
using Ecosys.Api.Controllers;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Security;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Tests;

public sealed partial class ProjectsModuleTests
{
    // ── Calculations ──────────────────────────────────────────────────────────

    [Fact]
    public void Progress_IsCompletedWeightOverTotalWeight()
    {
        var milestones = new[]
        {
            Milestone(weight: 1, status: ProjectMilestoneStatuses.Completed),
            Milestone(weight: 2, status: ProjectMilestoneStatuses.Verified),
            Milestone(weight: 1, status: ProjectMilestoneStatuses.InProgress),
        };

        Assert.Equal(75m, ProjectCommercials.ComputeProgress(milestones, requireVerification: false));
        Assert.Equal(50m, ProjectCommercials.ComputeProgress(milestones, requireVerification: true));
        Assert.Equal(0m, ProjectCommercials.ComputeProgress([], requireVerification: false));
        Assert.Equal(0m, ProjectCommercials.ComputeProgress([Milestone(weight: 0, status: ProjectMilestoneStatuses.Completed)], false));
    }

    [Fact]
    public void PlannedProgress_CountsWeightWhosePlannedDateHasPassed()
    {
        var today = new DateOnly(2026, 10, 21);
        var milestones = new[]
        {
            Milestone(weight: 1, planned: today.AddDays(-5)),
            Milestone(weight: 1, planned: today),
            Milestone(weight: 2, planned: today.AddDays(10)),
            Milestone(weight: 0, planned: today.AddDays(-1)),
        };

        Assert.Equal(50m, ProjectCommercials.ComputePlannedProgress(milestones, today));
        Assert.True(ProjectCommercials.IsBehindSchedule(25m, 50m));
        Assert.False(ProjectCommercials.IsBehindSchedule(50m, 50m));
    }

    [Fact]
    public void ApprovedTotals_CountOnlyApprovedVariations_AndBumpTheVersion()
    {
        var project = new Project { OriginalValue = 2_500_000m };
        var variations = new[]
        {
            Variation(ProjectVariationKinds.Addition, 350_000m, ProjectVariationStatuses.Approved),
            Variation(ProjectVariationKinds.Omission, 100_000m, ProjectVariationStatuses.Approved),
            Variation(ProjectVariationKinds.Addition, 999_000m, ProjectVariationStatuses.Submitted),
            Variation(ProjectVariationKinds.Addition, 888_000m, ProjectVariationStatuses.Rejected),
            Variation(ProjectVariationKinds.Addition, 777_000m, ProjectVariationStatuses.Void),
        };

        ProjectCommercials.ApplyApprovedTotals(project, variations);

        Assert.Equal(350_000m, project.ApprovedAdditions);
        Assert.Equal(100_000m, project.ApprovedOmissions);
        Assert.Equal(2_750_000m, project.RevisedValue);
        Assert.Equal(1, project.ValueVersion);

        // Recomputing with nothing changed leaves the version alone.
        ProjectCommercials.ApplyApprovedTotals(project, variations);
        Assert.Equal(1, project.ValueVersion);
    }

    [Fact]
    public void ApprovedTotals_RefuseToTakeTheValueBelowZero()
    {
        var project = new Project { OriginalValue = 100m };
        Assert.Throws<BusinessRuleException>(() => ProjectCommercials.ApplyApprovedTotals(project,
            [Variation(ProjectVariationKinds.Omission, 150m, ProjectVariationStatuses.Approved)]));
    }

    [Fact]
    public async Task ConcurrentTotalsUpdates_SecondSaveConflicts()
    {
        var databaseName = $"ecosys-projects-concurrency-{Guid.NewGuid():N}";
        var projectId = Guid.NewGuid();
        await using (var seed = new AppDbContext(InMemory(databaseName)))
        {
            seed.Projects.Add(new Project { Id = projectId, TenantId = Guid.NewGuid(), ProjectNumber = "PRJ-1", Name = "Chiller", OriginalValue = 1000m });
            await seed.SaveChangesAsync();
        }

        await using var first = new AppDbContext(InMemory(databaseName));
        await using var second = new AppDbContext(InMemory(databaseName));
        var a = await first.Projects.SingleAsync(x => x.Id == projectId);
        var b = await second.Projects.SingleAsync(x => x.Id == projectId);

        ProjectCommercials.ApplyApprovedTotals(a, [Variation(ProjectVariationKinds.Addition, 100m, ProjectVariationStatuses.Approved)]);
        ProjectCommercials.ApplyApprovedTotals(b, [Variation(ProjectVariationKinds.Addition, 200m, ProjectVariationStatuses.Approved)]);

        await first.SaveChangesAsync();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
    }

    // ── Licensing ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("operations", "Core", "operations")]
    [InlineData("operations", "Core,projects", "operations,projects")]
    [InlineData("suite", "Core,Projects", "operations,finance,projects")]
    [InlineData("finance", "projects", "finance,projects")]
    [InlineData("legacy-custom", "Core,finance,projects", "finance,projects")]
    [InlineData("Trial", "Core", "operations,finance")]
    [InlineData("Trial", "Core,projects", "operations,finance,projects")]
    public void ProjectsIsAnAddOnThatNoPlanIncludesByDefault(string code, string included, string expected)
    {
        Assert.Equal(expected.Split(','), ServiceOpsPlans.Modules(new LicensePlan { PlanCode = code, ModulesIncluded = included }));
    }

    [Theory]
    [InlineData("Projects", "/api/projects", "projects")]
    [InlineData("Projects", "/api/projects/123/milestones", "projects")]
    [InlineData("Projects", "/api/projects/123/work-orders", "projects,operations")]
    [InlineData("Projects", "/api/projects/123/material-issues", "projects,operations")]
    [InlineData("ProjectSettings", "/api/project-settings/stages", "projects")]
    [InlineData("Consultants", "/api/consultants", "projects")]
    [InlineData("PortalProjects", "/api/portal/projects", "projects")]
    public void ProjectRoutes_RequireTheProjectsModule(string controller, string path, string expected)
    {
        Assert.True(ServiceOpsPlans.IsClassified(controller));
        Assert.Equal(expected.Split(','), ServiceOpsPlans.RequiredModules(controller, path));
    }

    [Theory]
    [InlineData("suite", "Core", "Projects", "/api/projects", false)]
    [InlineData("suite", "Core,projects", "Projects", "/api/projects", true)]
    [InlineData("finance", "Core,projects", "Projects", "/api/projects", true)]
    [InlineData("finance", "Core,projects", "Projects", "/api/projects/123/work-orders", false)]
    [InlineData("operations", "Core,projects", "Projects", "/api/projects/123/work-orders", true)]
    public async Task LicenseMiddleware_EnforcesTheProjectsModule(string planCode, string modules, string controller, string path, bool allowed)
    {
        var tenantId = Guid.NewGuid();
        var http = new DefaultHttpContext();
        http.Request.Path = path;
        http.Request.Method = "GET";
        http.User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(TenantClaimTypes.TenantId, tenantId.ToString()),
            new Claim(TenantClaimTypes.UserId, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, AppRoles.Admin)
        ], "test"));
        http.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(new ControllerActionDescriptor { ControllerName = controller }), "test"));
        var tenantContext = new HttpTenantContext(new HttpContextAccessor { HttpContext = http });
        await using var db = new AppDbContext(InMemory(Guid.NewGuid().ToString()), tenantContext);
        db.Tenants.Add(new Tenant { Id = tenantId, Name = "Plan test", CompanyName = "Plan test", Slug = "plan-test", Country = "Kenya", IsActive = true });
        var plan = new LicensePlan { PlanCode = planCode, DisplayName = "Plan test", ModulesIncluded = modules };
        db.LicensePlans.Add(plan);
        db.TenantLicenses.Add(new TenantLicense { TenantId = tenantId, LicensePlanId = plan.Id, LicensePlan = plan, Status = "Active" });
        await db.SaveChangesAsync();

        var reached = false;
        var middleware = new TenantLicenseEnforcementMiddleware(_ => { reached = true; return Task.CompletedTask; });
        if (allowed)
            await middleware.InvokeAsync(http, tenantContext, new LicenseGuardService(db), db);
        else
            Assert.StartsWith("license_module_", (await Assert.ThrowsAsync<LicenseException>(() => middleware.InvokeAsync(http, tenantContext, new LicenseGuardService(db), db))).ErrorCode);
        Assert.Equal(allowed, reached);
    }

    // ── Capabilities ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData(AppRoles.Manager, "projects.view,projects.manage,projects.variations.submit,projects.variations.approve,projects.close,projects.muster.mark,projects.financials.view,projects.financials.manage")]
    [InlineData(AppRoles.Viewer, "projects.view")]
    [InlineData(AppRoles.Dispatcher, "projects.view")]
    [InlineData(AppRoles.FinanceUser, "projects.view,projects.financials.view,projects.financials.manage")]
    [InlineData(AppRoles.Technician, "projects.muster.mark")]
    public async Task RoleDefaults_GrantProjectCapabilities(string role, string expected)
    {
        await using var db = new AppDbContext(InMemory(Guid.NewGuid().ToString()));
        var tenantId = Guid.NewGuid();
        var user = new User { TenantId = tenantId, FullName = "Role test", Email = $"{Guid.NewGuid():N}@acme.test", Role = role, IsActive = true };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var permissions = await new DynamicAccessService(db, new RolePermissionService(db)).ResolveLegacyClaimsAsync(tenantId, user.Id);

        Assert.Equal(expected.Split(',', StringSplitOptions.RemoveEmptyEntries), permissions.Capabilities);
    }

    [Fact]
    public async Task ExplicitDeny_RemovesARoleDefaultProjectCapability()
    {
        await using var db = new AppDbContext(InMemory(Guid.NewGuid().ToString()));
        var tenantId = Guid.NewGuid();
        var user = new User { TenantId = tenantId, FullName = "Manager", Email = "manager@acme.test", Role = AppRoles.Manager, IsActive = true };
        db.Users.Add(user);
        db.UserPermissionOverrides.Add(new UserPermissionOverride { TenantId = tenantId, UserId = user.Id, Capability = PermissionNames.ProjectsClose, IsAllowed = false });
        await db.SaveChangesAsync();

        var permissions = await new DynamicAccessService(db, new RolePermissionService(db)).ResolveLegacyClaimsAsync(tenantId, user.Id);

        Assert.Contains(PermissionNames.ProjectsManage, permissions.Capabilities);
        Assert.DoesNotContain(PermissionNames.ProjectsClose, permissions.Capabilities);
    }

    // ── Project lifecycle ─────────────────────────────────────────────────────

    [Fact]
    public async Task CreatingAProject_SeedsDefaults_NumbersIt_AndAddsTheManagerToTheTeam()
    {
        var harness = await Harness.CreateAsync();
        var detail = await harness.CreateProjectAsync(originalValue: 2_500_000m);

        Assert.Equal("PRJ-001", detail.Summary.ProjectNumber);
        Assert.Equal(ProjectStageCategories.Planning, detail.Summary.StageCategory);
        Assert.Equal("General Project", detail.Summary.ProjectTypeName);
        Assert.Equal(2_500_000m, detail.Summary.RevisedValue);
        var manager = Assert.Single(detail.Members);
        Assert.Equal(ProjectMemberRoles.ProjectManager, manager.Role);
        Assert.Equal(harness.ManagerUserId, manager.UserId);
        Assert.Equal(7, await harness.Db.ProjectStages.CountAsync());
    }

    [Fact]
    public async Task CreatingAProject_RejectsASiteOfAnotherClient()
    {
        var harness = await Harness.CreateAsync();
        await Assert.ThrowsAsync<BusinessRuleException>(() => harness.Controller.Create(new UpsertProjectRequest(
            "Wrong site", harness.ClientId, harness.OtherClientSiteId, null, null, null, null, 1000m, null, null, null, null), CancellationToken.None));
    }

    [Fact]
    public async Task Template_IsCopiedIntoMilestonesAndTasks_RelativeToThePlannedStart()
    {
        var harness = await Harness.CreateAsync();
        var template = new ProjectTemplate
        {
            TenantId = harness.TenantId,
            Name = "HVAC installation",
            ItemsJson = """[{"name":"Mobilisation","weight":1,"offsetDays":0,"tasks":[{"title":"Site survey","offsetDays":2}]},{"name":"Commissioning","weight":3,"offsetDays":30}]""",
        };
        harness.Db.ProjectTemplates.Add(template);
        await harness.Db.SaveChangesAsync();

        var start = new DateOnly(2026, 11, 1);
        var detail = Ok(await harness.Controller.Create(new UpsertProjectRequest(
            "Westlands HVAC", harness.ClientId, null, null, null, harness.ManagerUserId, null, 2_500_000m, "KES", start, start.AddDays(60), null, template.Id),
            CancellationToken.None));

        var milestones = Ok(await harness.Controller.ListMilestones(detail.Summary.Id, CancellationToken.None));
        Assert.Equal(["Mobilisation", "Commissioning"], milestones.Select(x => x.Name));
        Assert.Equal(start.AddDays(30), milestones.ElementAt(1).PlannedDate);
        var task = Assert.Single(Ok(await harness.Controller.ListTasks(detail.Summary.Id, null, null, CancellationToken.None)));
        Assert.Equal("Site survey", task.Title);
        Assert.Equal(start.AddDays(2), task.DueDate);
        Assert.Equal("Mobilisation", task.MilestoneName);
    }

    [Fact]
    public async Task MilestoneStatus_DrivesTheCachedProgress()
    {
        var harness = await Harness.CreateAsync();
        var project = await harness.CreateProjectAsync();
        var first = Ok(await harness.Controller.CreateMilestone(project.Summary.Id, new UpsertProjectMilestoneRequest("Install", 1, null, null, null), CancellationToken.None));
        Ok(await harness.Controller.CreateMilestone(project.Summary.Id, new UpsertProjectMilestoneRequest("Commission", 3, null, null, null), CancellationToken.None));

        Ok(await harness.Controller.SetMilestoneStatus(project.Summary.Id, first.Id, new SetMilestoneStatusRequest("Completed"), CancellationToken.None));
        Assert.Equal(25m, Ok(await harness.Controller.Get(project.Summary.Id, CancellationToken.None)).Summary.ProgressPercent);

        Ok(await harness.Controller.SetMilestoneStatus(project.Summary.Id, first.Id, new SetMilestoneStatusRequest("NotStarted"), CancellationToken.None));
        Assert.Equal(0m, Ok(await harness.Controller.Get(project.Summary.Id, CancellationToken.None)).Summary.ProgressPercent);
    }

    [Fact]
    public async Task ProgressOverride_NeedsAReason_AndCanBeRemoved()
    {
        var harness = await Harness.CreateAsync();
        var project = await harness.CreateProjectAsync();

        await Assert.ThrowsAsync<BusinessRuleException>(() => harness.Controller.OverrideProgress(project.Summary.Id, new ProgressOverrideRequest(60, null), CancellationToken.None));

        var overridden = Ok(await harness.Controller.OverrideProgress(project.Summary.Id, new ProgressOverrideRequest(60, "Site walk with consultant"), CancellationToken.None));
        Assert.Equal(60m, overridden.Summary.ProgressPercent);
        Assert.True(overridden.Summary.ProgressOverridden);

        var cleared = Ok(await harness.Controller.OverrideProgress(project.Summary.Id, new ProgressOverrideRequest(null, null), CancellationToken.None));
        Assert.Equal(0m, cleared.Summary.ProgressPercent);
        Assert.False(cleared.Summary.ProgressOverridden);
    }

    // ── Variations ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Variations_ChangeTheRevisedValueOnlyWhenApproved()
    {
        var harness = await Harness.CreateAsync();
        var project = await harness.CreateProjectAsync(originalValue: 2_500_000m);
        var id = project.Summary.Id;

        var addition = Ok(await harness.Controller.CreateVariation(id, new UpsertProjectVariationRequest("Addition", 350_000m, "Extra AHU", null, "CI-07"), CancellationToken.None));
        var omission = Ok(await harness.Controller.CreateVariation(id, new UpsertProjectVariationRequest("Omission", 100_000m, "Drop VRF unit", null, null), CancellationToken.None));
        var rejected = Ok(await harness.Controller.CreateVariation(id, new UpsertProjectVariationRequest("Addition", 50_000m, "Gold-plated grilles", null, null), CancellationToken.None));
        Assert.Equal("VO-001", addition.VariationNumber);

        foreach (var v in new[] { addition, omission, rejected })
            Ok(await harness.Controller.SubmitVariation(id, v.Id, CancellationToken.None));

        var pending = Ok(await harness.Controller.Get(id, CancellationToken.None));
        Assert.Equal(2_500_000m, pending.Summary.RevisedValue);
        Assert.Equal(3, pending.PendingVariationsCount);
        Assert.Equal(300_000m, pending.PendingVariationsValue);

        Ok(await harness.Controller.DecideVariation(id, addition.Id, new VariationDecisionRequest(true, null), CancellationToken.None));
        Ok(await harness.Controller.DecideVariation(id, omission.Id, new VariationDecisionRequest(true, null), CancellationToken.None));
        Ok(await harness.Controller.DecideVariation(id, rejected.Id, new VariationDecisionRequest(false, "Out of scope"), CancellationToken.None));

        var decided = Ok(await harness.Controller.Get(id, CancellationToken.None));
        Assert.Equal(2_750_000m, decided.Summary.RevisedValue);
        Assert.Equal(350_000m, decided.ApprovedAdditions);
        Assert.Equal(100_000m, decided.ApprovedOmissions);
        Assert.Equal(0, decided.PendingVariationsCount);

        // An approved variation is never edited: void it (with a reason) and the value reverts.
        await Assert.ThrowsAsync<BusinessRuleException>(() => harness.Controller.UpdateVariation(id, addition.Id,
            new UpsertProjectVariationRequest("Addition", 1m, "Edited", null, null), CancellationToken.None));
        await Assert.ThrowsAsync<BusinessRuleException>(() => harness.Controller.VoidVariation(id, addition.Id, new VoidVariationRequest(null), CancellationToken.None));
        Ok(await harness.Controller.VoidVariation(id, addition.Id, new VoidVariationRequest("Raised against the wrong project"), CancellationToken.None));
        Assert.Equal(2_400_000m, Ok(await harness.Controller.Get(id, CancellationToken.None)).Summary.RevisedValue);
    }

    [Fact]
    public async Task Omission_CannotBeSubmittedPastZero()
    {
        var harness = await Harness.CreateAsync();
        var project = await harness.CreateProjectAsync(originalValue: 1_000m);
        var omission = Ok(await harness.Controller.CreateVariation(project.Summary.Id, new UpsertProjectVariationRequest("Omission", 1_500m, "Too big", null, null), CancellationToken.None));
        await Assert.ThrowsAsync<BusinessRuleException>(() => harness.Controller.SubmitVariation(project.Summary.Id, omission.Id, CancellationToken.None));
    }

    [Fact]
    public async Task ApprovalMatrix_DecidesTheVariation_AndRecomputesTheTotals()
    {
        var harness = await Harness.CreateAsync();
        harness.Db.ApprovalMatrices.Add(new ApprovalMatrix
        {
            TenantId = harness.TenantId,
            DocumentType = ApprovalDocumentTypes.ProjectVariation,
            Levels = [new ApprovalLevel { TenantId = harness.TenantId, LevelOrder = 1, Name = "Director", MinAmount = 0, ApproverType = ApprovalApproverTypes.Permission, RequiredPermission = PermissionNames.ProjectsVariationsApprove }],
        });
        await harness.Db.SaveChangesAsync();
        var project = await harness.CreateProjectAsync(originalValue: 1_000_000m);
        var variation = Ok(await harness.Controller.CreateVariation(project.Summary.Id, new UpsertProjectVariationRequest("Addition", 200_000m, "Extra ducting", null, null), CancellationToken.None));
        Ok(await harness.Controller.SubmitVariation(project.Summary.Id, variation.Id, CancellationToken.None));

        var request = await harness.Db.ApprovalRequests.SingleAsync(x => x.DocumentId == variation.Id);
        Assert.Equal(ApprovalStatuses.Pending, request.Status);

        var decided = Ok(await harness.Controller.DecideVariation(project.Summary.Id, variation.Id, new VariationDecisionRequest(true, null), CancellationToken.None));

        Assert.Equal(ProjectVariationStatuses.Approved, decided.Status);
        Assert.Equal(1_200_000m, Ok(await harness.Controller.Get(project.Summary.Id, CancellationToken.None)).Summary.RevisedValue);
    }

    [Fact]
    public async Task DirectApproval_NeedsTheApprovePermission()
    {
        var harness = await Harness.CreateAsync();
        var project = await harness.CreateProjectAsync();
        var variation = Ok(await harness.Controller.CreateVariation(project.Summary.Id, new UpsertProjectVariationRequest("Addition", 10m, "Small", null, null), CancellationToken.None));
        Ok(await harness.Controller.SubmitVariation(project.Summary.Id, variation.Id, CancellationToken.None));

        var submitterOnly = harness.ControllerFor(harness.ManagerUserId, AppRoles.Manager, PermissionNames.ProjectsView, PermissionNames.ProjectsVariationsSubmit);
        await Assert.ThrowsAsync<ForbiddenException>(() => submitterOnly.DecideVariation(project.Summary.Id, variation.Id, new VariationDecisionRequest(true, null), CancellationToken.None));
    }

    // ── Stages, closure, tenant and membership boundaries ─────────────────────

    [Fact]
    public async Task Closing_IsBlockedWhileAVariationAwaitsADecision_AndAClosedProjectIsReadOnly()
    {
        var harness = await Harness.CreateAsync();
        var project = await harness.CreateProjectAsync();
        var closed = await harness.Db.ProjectStages.SingleAsync(x => x.Category == ProjectStageCategories.Closed);
        var active = await harness.Db.ProjectStages.SingleAsync(x => x.Category == ProjectStageCategories.Active);
        var variation = Ok(await harness.Controller.CreateVariation(project.Summary.Id, new UpsertProjectVariationRequest("Addition", 10m, "Small", null, null), CancellationToken.None));
        Ok(await harness.Controller.SubmitVariation(project.Summary.Id, variation.Id, CancellationToken.None));

        await Assert.ThrowsAsync<BusinessRuleException>(() => harness.Controller.ChangeStage(project.Summary.Id, new ChangeProjectStageRequest(closed.Id, null), CancellationToken.None));

        Ok(await harness.Controller.DecideVariation(project.Summary.Id, variation.Id, new VariationDecisionRequest(true, null), CancellationToken.None));
        var afterClose = Ok(await harness.Controller.ChangeStage(project.Summary.Id, new ChangeProjectStageRequest(closed.Id, "Final account agreed"), CancellationToken.None));
        Assert.NotNull(afterClose.ClosedAt);

        await Assert.ThrowsAsync<BusinessRuleException>(() => harness.Controller.CreateMilestone(project.Summary.Id, new UpsertProjectMilestoneRequest("Late", 1, null, null, null), CancellationToken.None));
        await Assert.ThrowsAsync<BusinessRuleException>(() => harness.Controller.ChangeStage(project.Summary.Id, new ChangeProjectStageRequest(active.Id, null), CancellationToken.None));
        var reopened = Ok(await harness.Controller.ChangeStage(project.Summary.Id, new ChangeProjectStageRequest(active.Id, "Snag found after handover"), CancellationToken.None));
        Assert.Null(reopened.ClosedAt);
    }

    [Fact]
    public async Task AnotherTenantsProjectIsInvisible_AndItsRecordsCannotBeLinked()
    {
        var harness = await Harness.CreateAsync();
        var project = await harness.CreateProjectAsync();
        var otherTenantProject = new Project
        {
            TenantId = harness.OtherTenantId, ProjectNumber = "PRJ-900", Name = "Not yours", ClientId = harness.OtherTenantClientId,
            ProjectTypeId = Guid.NewGuid(), StageId = Guid.NewGuid(),
        };
        var otherTenantWorkOrder = new WorkOrder
        {
            TenantId = harness.OtherTenantId, ClientId = harness.OtherTenantClientId, WorkOrderNumber = "WO-900", Title = "Not yours",
            Status = "Open", Priority = "Medium", WorkType = "CorrectiveMaintenance",
        };
        harness.Db.Projects.Add(otherTenantProject);
        harness.Db.WorkOrders.Add(otherTenantWorkOrder);
        await harness.Db.SaveChangesAsync();

        await Assert.ThrowsAsync<NotFoundException>(() => harness.Controller.Get(otherTenantProject.Id, CancellationToken.None));
        Assert.DoesNotContain(Ok(await harness.Controller.List(null, null, null, null, null, CancellationToken.None)), x => x.Id == otherTenantProject.Id);
        await Assert.ThrowsAsync<NotFoundException>(() => harness.Controller.LinkWorkOrder(project.Summary.Id, new LinkRecordRequest(otherTenantWorkOrder.Id), CancellationToken.None));
    }

    [Fact]
    public async Task LinkingAWorkOrder_RequiresTheSameClient()
    {
        var harness = await Harness.CreateAsync();
        var project = await harness.CreateProjectAsync();
        var sameClient = harness.AddWorkOrder(harness.ClientId);
        var otherClient = harness.AddWorkOrder(harness.OtherClientId);
        await harness.Db.SaveChangesAsync();

        Assert.IsType<NoContentResult>(await harness.Controller.LinkWorkOrder(project.Summary.Id, new LinkRecordRequest(sameClient.Id), CancellationToken.None));
        await Assert.ThrowsAsync<BusinessRuleException>(() => harness.Controller.LinkWorkOrder(project.Summary.Id, new LinkRecordRequest(otherClient.Id), CancellationToken.None));
        Assert.Single(Ok(await harness.Controller.ListWorkOrders(project.Summary.Id, CancellationToken.None)));
    }

    [Fact]
    public async Task MembersOnlyVisibility_HidesProjectsAUserIsNotOn()
    {
        var harness = await Harness.CreateAsync();
        var mine = await harness.CreateProjectAsync(name: "Mine");
        var notMine = await harness.CreateProjectAsync(name: "Not mine");
        await new ProjectSettingsService(harness.Db).SaveAsync(harness.TenantId, new ProjectSettingsModel { Visibility = ProjectVisibility.MembersOnly });
        harness.Db.ProjectMembers.Add(new ProjectMember { TenantId = harness.TenantId, ProjectId = mine.Summary.Id, UserId = harness.ViewerUserId, Role = ProjectMemberRoles.Member });
        await harness.Db.SaveChangesAsync();

        var viewer = harness.ControllerFor(harness.ViewerUserId, AppRoles.Viewer, PermissionNames.ProjectsView);

        Assert.Equal([mine.Summary.Id], Ok(await viewer.List(null, null, null, null, null, CancellationToken.None)).Select(x => x.Id));
        await Assert.ThrowsAsync<NotFoundException>(() => viewer.Get(notMine.Summary.Id, CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenException>(() => viewer.Create(new UpsertProjectRequest(
            "Nope", harness.ClientId, null, null, null, null, null, 1m, null, null, null, null), CancellationToken.None));
    }

    [Fact]
    public async Task WithoutProjectsView_NothingIsReadable()
    {
        var harness = await Harness.CreateAsync();
        await harness.CreateProjectAsync();
        var technician = harness.ControllerFor(harness.ViewerUserId, AppRoles.Technician);
        await Assert.ThrowsAsync<ForbiddenException>(() => technician.List(null, null, null, null, null, CancellationToken.None));
    }

    // ── Client portal ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Portal_ShowsOnlyTheClientsVisibleProjects_AndOnlyApprovedVariationsAndSharedDocuments()
    {
        var harness = await Harness.CreateAsync();
        var visible = await harness.CreateProjectAsync(name: "Visible");
        var hidden = await harness.CreateProjectAsync(name: "Hidden");
        var otherClient = Ok(await harness.Controller.Create(new UpsertProjectRequest(
            "Other client", harness.OtherClientId, null, null, null, null, null, 1m, null, null, null, null), CancellationToken.None));
        (await harness.Db.Projects.SingleAsync(x => x.Id == hidden.Summary.Id)).VisibleInClientPortal = false;

        var approved = Ok(await harness.Controller.CreateVariation(visible.Summary.Id, new UpsertProjectVariationRequest("Addition", 100m, "Approved one", null, null), CancellationToken.None));
        Ok(await harness.Controller.CreateVariation(visible.Summary.Id, new UpsertProjectVariationRequest("Addition", 999m, "Still a draft", null, null), CancellationToken.None));
        Ok(await harness.Controller.SubmitVariation(visible.Summary.Id, approved.Id, CancellationToken.None));
        Ok(await harness.Controller.DecideVariation(visible.Summary.Id, approved.Id, new VariationDecisionRequest(true, null), CancellationToken.None));
        harness.Db.Attachments.AddRange(
            new Attachment { TenantId = harness.TenantId, EntityType = "Project", EntityId = visible.Summary.Id, FileName = "handover.pdf", MimeType = "application/pdf", StoragePath = "a", PublicUrl = "a", SharedWithClient = true },
            new Attachment { TenantId = harness.TenantId, EntityType = "Project", EntityId = visible.Summary.Id, FileName = "internal-costing.xlsx", MimeType = "application/vnd.ms-excel", StoragePath = "b", PublicUrl = "b" });
        await harness.Db.SaveChangesAsync();

        var portal = new PortalProjectsController(harness.Db, new FakePortalContext(harness.TenantId, harness.ClientId), new ProjectSettingsService(harness.Db), null!);

        var list = Ok(await portal.List(CancellationToken.None));
        Assert.Equal([visible.Summary.Id], list.Select(x => x.Id));
        Assert.DoesNotContain(list, x => x.Id == otherClient.Summary.Id);
        await Assert.ThrowsAsync<NotFoundException>(() => portal.Get(hidden.Summary.Id, CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => portal.Get(otherClient.Summary.Id, CancellationToken.None));

        var detail = Ok(await portal.Get(visible.Summary.Id, CancellationToken.None));
        Assert.Equal("Approved one", Assert.Single(detail.Variations).Title);
        Assert.Equal("handover.pdf", Assert.Single(detail.Documents).FileName);
        Assert.Equal(1_000_100m, detail.Summary.ContractValue);
    }

    [Fact]
    public async Task Portal_IsClosedWhenTheTenantTurnsThePageOff()
    {
        var harness = await Harness.CreateAsync();
        await harness.CreateProjectAsync();
        await new ProjectSettingsService(harness.Db).SaveAsync(harness.TenantId, new ProjectSettingsModel { ClientPortal = false });
        var portal = new PortalProjectsController(harness.Db, new FakePortalContext(harness.TenantId, harness.ClientId), new ProjectSettingsService(harness.Db), null!);
        await Assert.ThrowsAsync<ForbiddenException>(() => portal.List(CancellationToken.None));
    }

    // ── Consultant directory ─────────────────────────────────────────────────

    [Fact]
    public async Task Consultants_AreUniquePerTenant_AndDeactivatedRatherThanDeletedOnceUsed()
    {
        var harness = await Harness.CreateAsync();
        var consultants = new ConsultantsController(harness.Db, harness.TenantContext, harness.Access, new NoOpAuditLogService());
        var xyz = Ok(await consultants.Create(new UpsertConsultantRequest("XYZ Engineering", "M&E", "Jane Wanjiru", "+254700000000", "jane@xyz.test", null, null), CancellationToken.None));
        var unused = Ok(await consultants.Create(new UpsertConsultantRequest("Never Used QS", "QS", null, null, null, null, null), CancellationToken.None));
        await Assert.ThrowsAsync<BusinessRuleException>(() => consultants.Create(new UpsertConsultantRequest("xyz engineering", null, null, null, null, null, null), CancellationToken.None));

        var project = await harness.CreateProjectAsync();
        Ok(await harness.Controller.LinkConsultant(project.Summary.Id, new LinkProjectConsultantRequest(xyz.Id, "Lead consultant"), CancellationToken.None));

        await consultants.Delete(xyz.Id, CancellationToken.None);
        await consultants.Delete(unused.Id, CancellationToken.None);

        Assert.False((await harness.Db.Consultants.SingleAsync(x => x.Id == xyz.Id)).IsActive);
        Assert.False(await harness.Db.Consultants.AnyAsync(x => x.Id == unused.Id));
        Assert.Equal("XYZ Engineering", Assert.Single(Ok(await harness.Controller.Get(project.Summary.Id, CancellationToken.None)).Consultants).FirmName);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static T Ok<T>(ActionResult<T> result) =>
        result.Value ?? (T)Assert.IsType<OkObjectResult>(result.Result).Value!;

    private static DbContextOptions<AppDbContext> InMemory(string name) =>
        new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(name).Options;

    private static ProjectMilestone Milestone(decimal weight = 1, string status = ProjectMilestoneStatuses.NotStarted, DateOnly? planned = null) =>
        new() { Name = "M", Weight = weight, Status = status, PlannedDate = planned };

    private static ProjectVariation Variation(string kind, decimal amount, string status) =>
        new() { Kind = kind, Amount = amount, Status = status, Title = "V" };

    private sealed class Harness
    {
        public AppDbContext Db { get; private set; } = null!;
        public FakeTenantContext TenantContext { get; private set; } = null!;
        public IProjectAccessService Access { get; private set; } = null!;
        public ProjectsController Controller { get; private set; } = null!;
        public Guid TenantId { get; private set; }
        public Guid OtherTenantId { get; private set; }
        public Guid ManagerUserId { get; private set; }
        public Guid ViewerUserId { get; private set; }
        public Guid ClientId { get; private set; }
        public Guid OtherClientId { get; private set; }
        public Guid OtherClientSiteId { get; private set; }
        public Guid OtherTenantClientId { get; private set; }
        private readonly FakeNumbering numbering = new();
        private int workOrderCount;

        public static Task<Harness> CreateAsync()
        {
            var databaseName = $"ecosys-projects-{Guid.NewGuid():N}";
            return CreateAsync(context => new AppDbContext(InMemory(databaseName), context), seedTenants: false);
        }

        /// <summary>
        /// Builds the harness on any provider. With <paramref name="seedTenants"/> the tenant rows the
        /// foreign keys need are created too (a real PostgreSQL schema enforces them; in-memory doesn't).
        /// </summary>
        public static async Task<Harness> CreateAsync(Func<ITenantContext?, AppDbContext> createContext, bool seedTenants)
        {
            var tenantId = Guid.NewGuid();
            var otherTenantId = Guid.NewGuid();
            var admin = new User { TenantId = tenantId, FullName = "Admin", Email = "admin@acme.test", Role = AppRoles.Admin, IsActive = true };
            var manager = new User { TenantId = tenantId, FullName = "John Kamau", Email = "john@acme.test", Role = AppRoles.Manager, IsActive = true };
            var viewer = new User { TenantId = tenantId, FullName = "Viewer", Email = "viewer@acme.test", Role = AppRoles.Viewer, IsActive = true };
            var client = new Client { TenantId = tenantId, ClientName = "ABC Properties Ltd", IsActive = true };
            var otherClient = new Client { TenantId = tenantId, ClientName = "Other Client", IsActive = true };
            var otherClientSite = new Site { TenantId = tenantId, ClientId = otherClient.Id, SiteCode = "S-2", SiteName = "Other site" };
            var otherTenantClient = new Client { TenantId = otherTenantId, ClientName = "Other tenant client", IsActive = true };

            await using (var seed = createContext(null))
            {
                if (seedTenants)
                    seed.Tenants.AddRange(TestTenant(tenantId), TestTenant(otherTenantId));
                seed.AddRange(admin, manager, viewer, client, otherClient, otherClientSite, otherTenantClient);
                await seed.SaveChangesAsync();
            }

            var tenantContext = new FakeTenantContext(tenantId, admin.Id, AppRoles.Admin, isAdmin: true);
            var harness = new Harness
            {
                Db = createContext(tenantContext),
                TenantContext = tenantContext,
                TenantId = tenantId,
                OtherTenantId = otherTenantId,
                ManagerUserId = manager.Id,
                ViewerUserId = viewer.Id,
                ClientId = client.Id,
                OtherClientId = otherClient.Id,
                OtherClientSiteId = otherClientSite.Id,
                OtherTenantClientId = otherTenantClient.Id,
            };
            (harness.Access, harness.Controller) = harness.Build(tenantContext);
            return harness;
        }

        private static Tenant TestTenant(Guid id) => new()
        {
            Id = id, Name = "Tenant", Slug = $"t-{id:N}"[..14], CompanyName = "Tenant Co", Email = $"{id:N}@tenant.test", Country = "Kenya",
            Status = "Active", LicenseStatus = "Active", PrimaryColor = "#0F4C81", SecondaryColor = "#F4B942", IsActive = true,
        };

        private (IProjectAccessService Access, ProjectsController Controller) Build(FakeTenantContext context)
        {
            var settings = new ProjectSettingsService(Db);
            var access = new ProjectAccessService(Db, context, settings);
            var audit = new NoOpAuditLogService();
            var service = new ProjectService(Db, numbering, new ApprovalWorkflowService(Db, context, audit), new ProjectSetupService(Db), settings, access, audit);
            return (access, new ProjectsController(Db, context, access, service, settings, new NoOpProjectNotifications(), audit));
        }

        /// <summary>A controller acting as another user of the same tenant, holding only the given capabilities.</summary>
        public ProjectsController ControllerFor(Guid userId, string role, params string[] permissions) =>
            Build(new FakeTenantContext(TenantId, userId, role, isAdmin: false, permissions)).Controller;

        public async Task<ProjectDetailResponse> CreateProjectAsync(decimal originalValue = 1_000_000m, string name = "Westlands HVAC Installation") =>
            Ok(await Controller.Create(new UpsertProjectRequest(
                name, ClientId, null, null, null, ManagerUserId, null, originalValue, "KES", null, null, null), CancellationToken.None));

        public WorkOrder AddWorkOrder(Guid clientId)
        {
            var workOrder = new WorkOrder
            {
                TenantId = TenantId, ClientId = clientId, WorkOrderNumber = $"WO-{++workOrderCount}", Title = "Commissioning visit",
                Status = "Open", Priority = "Medium", WorkType = "NewInstallation",
            };
            Db.WorkOrders.Add(workOrder);
            return workOrder;
        }
    }

    private sealed class FakeTenantContext(Guid tenantId, Guid userId, string role, bool isAdmin, params string[] permissions) : ITenantContext
    {
        public Guid? TenantId => tenantId;
        public Guid? UserId => userId;
        public Guid? SessionId => Guid.Empty;
        public string? Email => "user@acme.test";
        public string? Role => role;
        public string? JobTitle => null;
        public bool IsAuthenticated => true;
        public bool IsSuperAdmin => false;
        public bool IsAdmin => isAdmin;
        public bool HasRole(string value) => string.Equals(value, role, StringComparison.OrdinalIgnoreCase);
        public bool HasPermission(string permissionName) => isAdmin || permissions.Contains(permissionName, StringComparer.OrdinalIgnoreCase);
        public Guid GetRequiredTenantId() => tenantId;
        public Guid GetRequiredUserId() => userId;
        public Guid GetRequiredSessionId() => Guid.Empty;
    }

    private sealed class FakePortalContext(Guid tenantId, Guid clientId) : IPortalContext
    {
        public Guid? TenantId => tenantId;
        public Guid? PortalUserId => Guid.Empty;
        public Guid? ClientId => clientId;
        public string? Email => "client@abc.test";
        public string? FullName => "Client";
        public bool IsAuthenticated => true;
        public Guid GetRequiredTenantId() => tenantId;
        public Guid GetRequiredPortalUserId() => Guid.Empty;
        public Guid GetRequiredClientId() => clientId;
    }

    private sealed class FakeNumbering : IDocumentNumberingService
    {
        private readonly Dictionary<string, int> next = new();

        public Task<string> GenerateAsync(Guid tenantId, Guid? branchId, string documentType, CancellationToken cancellationToken = default)
        {
            var n = next.GetValueOrDefault(documentType) + 1;
            next[documentType] = n;
            var prefix = documentType == DocumentTypes.ProjectVariation ? "VO" : documentType == DocumentTypes.Project ? "PRJ" : "DOC";
            return Task.FromResult($"{prefix}-{n:000}");
        }

        public Task<NumberingSetting> UpsertAsync(Guid tenantId, Guid? branchId, string documentType, string prefix, long nextNumber, int paddingLength, string resetFrequency, bool includeYear, bool includeMonth, bool isActive, CancellationToken cancellationToken = default, string? suffix = null, string yearFormat = "YYYY", string separator = "-", bool isLocked = false) =>
            throw new NotSupportedException();
    }

    internal sealed class NoOpProjectNotifications : IProjectNotificationService
    {
        public List<(string Key, Guid ProjectId, IReadOnlyCollection<Guid> UserIds)> Sent { get; } = [];
        public Task ValuationCertifiedAsync(Guid tenantId, Project project, ProjectValuation valuation, CancellationToken cancellationToken = default)
        {
            Sent.Add(("project.valuation-certified", project.Id, []));
            return Task.CompletedTask;
        }
        public Task NotifyAsync(Guid tenantId, string key, Project project, IReadOnlyCollection<Guid> userIds, IReadOnlyDictionary<string, string?> values,
            string pushTitle, string pushBody, string pushTag, CancellationToken cancellationToken = default)
        {
            Sent.Add((key, project.Id, userIds));
            return Task.CompletedTask;
        }
        public Task<IReadOnlyList<Guid>> ManagerAndRolesAsync(Guid tenantId, Project project, IReadOnlyCollection<string> roles, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Guid>>(project.ProjectManagerUserId is { } pm ? [pm] : []);
        public Task MemberAddedAsync(Guid tenantId, Project project, Guid userId, string role, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task TaskAssignedAsync(Guid tenantId, Project project, ProjectTask task, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task VariationSubmittedAsync(Guid tenantId, Project project, ProjectVariation variation, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task VariationDecidedAsync(Guid tenantId, Project project, ProjectVariation variation, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class NoOpAuditLogService : IAuditLogService
    {
        public Task LogAsync(Guid? tenantId, Guid? userId, string action, string entityName, string entityId, string? details, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task LogAsync(Guid? tenantId, Guid? userId, string action, string entityName, string entityId, string? details, string severity = "Info", string? actorName = null, string? ipAddress = null, string? userAgent = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
