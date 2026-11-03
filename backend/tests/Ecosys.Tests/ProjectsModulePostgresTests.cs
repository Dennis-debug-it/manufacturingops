using Ecosys.Api.Controllers;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Ecosys.Tests;

/// <summary>
/// The in-memory provider runs any LINQ it is given, so it can't catch a query PostgreSQL won't
/// translate. These run the Projects flows end to end on a real, freshly migrated database
/// (ECOSYS_TEST_DB_CONNECTION, default localhost:5432 postgres/postgres).
/// </summary>
public sealed partial class ProjectsModuleTests
{
    [Fact]
    [Trait("Category", "PostgreSql")]
    public async Task Postgres_RegisterWorkspaceVariationsConsultantsAndPortal_AllTranslate()
    {
        await WithPostgresAsync(async connection =>
        {
            var harness = await Harness.CreateAsync(context => PostgresContext(connection, context), seedTenants: true);

            harness.Db.ProjectTemplates.Add(new ProjectTemplate
            {
                TenantId = harness.TenantId, Name = "Install",
                ItemsJson = """[{"name":"Rough-in","weight":1,"offsetDays":5,"tasks":[{"title":"Core drilling","offsetDays":1}]},{"name":"Commission","weight":1,"offsetDays":40}]""",
            });
            harness.Db.ApprovalMatrices.Add(new ApprovalMatrix
            {
                TenantId = harness.TenantId,
                DocumentType = ApprovalDocumentTypes.ProjectVariation,
                Levels = [new ApprovalLevel { TenantId = harness.TenantId, LevelOrder = 1, Name = "Director", MinAmount = 100_000m, ApproverType = ApprovalApproverTypes.Permission, RequiredPermission = PermissionNames.ProjectsVariationsApprove }],
            });
            await harness.Db.SaveChangesAsync();
            var templateId = await harness.Db.ProjectTemplates.Select(x => x.Id).SingleAsync();

            var start = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-10);
            var project = Ok(await harness.Controller.Create(new UpsertProjectRequest(
                "Westlands HVAC", harness.ClientId, null, null, null, harness.ManagerUserId, null, 2_500_000m, "KES", start, start.AddDays(60), "Main block", templateId),
                CancellationToken.None));
            var id = project.Summary.Id;

            // Workspace reads: members, consultants, milestones, tasks (all with ordering expressions).
            var consultants = new ConsultantsController(harness.Db, harness.TenantContext, harness.Access, new NoOpAuditLogService());
            var xyz = Ok(await consultants.Create(new UpsertConsultantRequest("XYZ Engineering", "M&E", "Jane", null, null, null, null), CancellationToken.None));
            Ok(await harness.Controller.LinkConsultant(id, new LinkProjectConsultantRequest(xyz.Id, "Lead consultant"), CancellationToken.None));
            Ok(await harness.Controller.AddMember(id, new UpsertProjectMemberRequest(harness.ViewerUserId, null, ProjectMemberRoles.Supervisor, "HVAC", null, null), CancellationToken.None));
            var listed = Ok(await consultants.List("xyz", false, CancellationToken.None));
            Assert.Equal(1, Assert.Single(listed).ProjectCount);

            var milestones = Ok(await harness.Controller.ListMilestones(id, CancellationToken.None));
            Ok(await harness.Controller.SetMilestoneStatus(id, milestones.First().Id, new SetMilestoneStatusRequest("Completed"), CancellationToken.None));
            Ok(await harness.Controller.CreateTask(id, new UpsertProjectTaskRequest("Order AHU", null, null, harness.ManagerUserId, null, "High", null), CancellationToken.None));
            Assert.Equal(2, Ok(await harness.Controller.ListTasks(id, null, null, CancellationToken.None)).Count);

            // Variations: a small one is decided directly, a large one goes through the matrix.
            var small = Ok(await harness.Controller.CreateVariation(id, new UpsertProjectVariationRequest("Omission", 50_000m, "Drop grille", null, null), CancellationToken.None));
            var large = Ok(await harness.Controller.CreateVariation(id, new UpsertProjectVariationRequest("Addition", 350_000m, "Extra AHU", null, "CI-07"), CancellationToken.None));
            Ok(await harness.Controller.SubmitVariation(id, small.Id, CancellationToken.None));
            Ok(await harness.Controller.SubmitVariation(id, large.Id, CancellationToken.None));
            Assert.Equal(1, await harness.Db.ApprovalRequests.CountAsync(x => x.DocumentId == large.Id));
            Ok(await harness.Controller.DecideVariation(id, small.Id, new VariationDecisionRequest(true, null), CancellationToken.None));
            Ok(await harness.Controller.DecideVariation(id, large.Id, new VariationDecisionRequest(true, null), CancellationToken.None));

            var detail = Ok(await harness.Controller.Get(id, CancellationToken.None));
            Assert.Equal(2_800_000m, detail.Summary.RevisedValue);
            Assert.Equal(50m, detail.Summary.ProgressPercent);
            Assert.Equal(2, detail.Members.Count);
            Assert.Equal(ProjectMemberRoles.ProjectManager, detail.Members[0].Role);

            // Register, summary and members-only visibility.
            Assert.Single(Ok(await harness.Controller.List("Open", null, null, null, "westlands", CancellationToken.None)));
            var summary = Ok(await harness.Controller.Summary(CancellationToken.None));
            Assert.Equal(1, summary.OpenProjects);
            Assert.Equal(2_800_000m, summary.TotalRevisedValue);
            Ok(await harness.Controller.Create(new UpsertProjectRequest("Someone else's", harness.ClientId, null, null, null, null, null, 1m, null, null, null, null), CancellationToken.None));
            await new ProjectSettingsService(harness.Db).SaveAsync(harness.TenantId, new ProjectSettingsModel { Visibility = ProjectVisibility.MembersOnly });
            var viewer = harness.ControllerFor(harness.ViewerUserId, AppRoles.Viewer, PermissionNames.ProjectsView);
            Assert.Equal([id], Ok(await viewer.List(null, null, null, null, null, CancellationToken.None)).Select(x => x.Id));

            // Closing runs the material-issue blocker query.
            var closed = await harness.Db.ProjectStages.SingleAsync(x => x.Category == ProjectStageCategories.Closed);
            Assert.NotNull(Ok(await harness.Controller.ChangeStage(id, new ChangeProjectStageRequest(closed.Id, "Final account"), CancellationToken.None)).ClosedAt);

            // Portal.
            await new ProjectSettingsService(harness.Db).SaveAsync(harness.TenantId, new ProjectSettingsModel());
            var portal = new PortalProjectsController(harness.Db, new FakePortalContext(harness.TenantId, harness.ClientId), new ProjectSettingsService(harness.Db), null!);
            Assert.Equal(2, Ok(await portal.List(CancellationToken.None)).Count);
            Assert.Equal(2, Ok(await portal.Get(id, CancellationToken.None)).Variations.Count);
        });
    }

    [Fact]
    [Trait("Category", "PostgreSql")]
    public async Task Postgres_ConcurrentVariationDecisions_TheSecondConflicts()
    {
        await WithPostgresAsync(async connection =>
        {
            var harness = await Harness.CreateAsync(context => PostgresContext(connection, context), seedTenants: true);
            var project = await harness.CreateProjectAsync(originalValue: 1_000m);

            await using var first = PostgresContext(connection, null);
            await using var second = PostgresContext(connection, null);
            var a = await first.Projects.SingleAsync(x => x.Id == project.Summary.Id);
            var b = await second.Projects.SingleAsync(x => x.Id == project.Summary.Id);
            ProjectCommercials.ApplyApprovedTotals(a, [Variation(ProjectVariationKinds.Addition, 100m, ProjectVariationStatuses.Approved)]);
            ProjectCommercials.ApplyApprovedTotals(b, [Variation(ProjectVariationKinds.Addition, 200m, ProjectVariationStatuses.Approved)]);

            await first.SaveChangesAsync();
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
        });
    }

    private static AppDbContext PostgresContext(string connection, ITenantContext? tenantContext) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options, tenantContext);

    private static async Task WithPostgresAsync(Func<string, Task> body)
    {
        var server = Environment.GetEnvironmentVariable("ECOSYS_TEST_DB_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=ecosys_test;Username=postgres;Password=postgres";
        var databaseName = $"ecosys_projects_{Guid.NewGuid():N}";
        var database = new NpgsqlConnectionStringBuilder(server) { Database = databaseName, Pooling = false }.ConnectionString;
        var admin = new NpgsqlConnectionStringBuilder(server) { Database = "postgres", Pooling = false }.ConnectionString;

        await ExecuteAdminAsync(admin, $"CREATE DATABASE \"{databaseName}\"");
        try
        {
            await using (var setup = PostgresContext(database, null))
                await setup.Database.MigrateAsync();
            await body(database);
        }
        finally
        {
            await ExecuteAdminAsync(admin, $"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)");
        }
    }

    private static async Task ExecuteAdminAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}
