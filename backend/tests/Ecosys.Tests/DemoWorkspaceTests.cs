using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Options;
using Ecosys.Tests.Factory;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Ecosys.Tests;

/// <summary>
/// The demo workspace is created in production next to real tenants, through the real API and services. These run
/// the whole thing against PostgreSQL: create, sign in as each login, open it from the platform, look at the
/// screens a demo walks through, and reset it again (the reset purges with raw SQL, so it needs a real database).
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class DemoWorkspaceTests
{
    private const string PlatformEmail = "platform.owner@ecosys.test";
    private const string PlatformPassword = "ValidPlatformAdmin123!";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public Task Reset_BuildsAWorkingTeaFactory_ThatEveryLoginCanUse_AndResetsAgain() =>
        FactoryPostgres.RunAsync("demo", async connection =>
        {
            await using var app = CreateApp(connection);
            using var platform = app.CreateClient();
            await SignInAsync(platform, PlatformEmail, PlatformPassword);

            var first = await PostAsync<DemoWorkspaceResetResult>(platform, "/api/platform/demo-workspace/reset");
            Assert.True(first.Status.Exists);
            Assert.Equal(27 + 4, first.Status.Assets); // the register plus four fleet vehicles
            Assert.Equal(4, first.Status.Vehicles);
            Assert.True(first.Status.ClosedJobs >= 40, $"Expected months of history, got {first.Status.ClosedJobs} closed jobs.");
            Assert.InRange(first.Status.OpenJobs, 6, 12);
            Assert.True(first.Status.PmPlans >= 10, $"Expected PM plans, got {first.Status.PmPlans}.");
            Assert.StartsWith("Demo-", first.Password);

            // Every login signs in with the shared password, by username and workspace.
            foreach (var login in first.Status.Logins)
            {
                using var client = app.CreateClient();
                await SignInAsync(client, login.Username, first.Password, DemoWorkspace.Slug);
            }

            // What a demo walks through, as the demo's admin.
            using var demo = app.CreateClient();
            await SignInAsync(demo, "admin", first.Password, DemoWorkspace.Slug);
            foreach (var path in new[]
            {
                "/api/factory/dashboard", "/api/factory/work-orders", "/api/factory/pm/plans", "/api/condition-monitoring/overview",
                "/api/reliability/assets", "/api/reliability/repeat-failures", "/api/fleet/vehicles", "/api/factory/compliance/items",
                "/api/factory/stores", "/api/factory/stores/parts", "/api/factory/costs", "/api/factory/checks/templates",
            })
            {
                var response = await demo.GetAsync(path);
                Assert.True(response.StatusCode == HttpStatusCode.OK, $"{path} returned {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
            }

            // Opening it from the platform as the technician needs no password.
            var opened = await PostAsync<JsonElement>(platform, "/api/platform/demo-workspace/open", new { username = "technician" });
            Assert.Equal("Technician", opened.GetProperty("user").GetProperty("role").GetString(), ignoreCase: true);
            Assert.Equal(DemoWorkspace.TenantId, opened.GetProperty("tenant").GetProperty("tenantId").GetGuid());
            var refused = await platform.PostAsJsonAsync("/api/platform/demo-workspace/open", new { username = "someone-else" });
            Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);

            await AssertDataAsync(connection);

            // A second reset wipes the first one's data and builds it again from scratch.
            var second = await PostAsync<DemoWorkspaceResetResult>(platform, "/api/platform/demo-workspace/reset");
            Assert.Equal(first.Status.Assets, second.Status.Assets);
            Assert.Equal(first.Status.ClosedJobs, second.Status.ClosedJobs);
            Assert.NotEqual(first.Password, second.Password);
            using (var stale = app.CreateClient())
            {
                var oldPassword = await stale.PostAsJsonAsync("/api/auth/login", new { emailOrUsername = "admin", password = first.Password, workspace = DemoWorkspace.Slug });
                Assert.False(oldPassword.IsSuccessStatusCode);
            }

            await AssertDataAsync(connection);
        });

    [Fact]
    public Task OnlyPlatformAdmins_CanResetOrOpenTheDemo() =>
        FactoryPostgres.RunAsync("demoauth", async connection =>
        {
            await using var app = CreateApp(connection);
            using var anonymous = app.CreateClient();
            Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsync("/api/platform/demo-workspace/reset", null)).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/platform/demo-workspace/open", new { username = "admin" })).StatusCode);

            using var platform = app.CreateClient();
            await SignInAsync(platform, PlatformEmail, PlatformPassword);
            var result = await PostAsync<DemoWorkspaceResetResult>(platform, "/api/platform/demo-workspace/reset");

            // The demo's own admin is a tenant user: the platform endpoints are not theirs.
            using var demo = app.CreateClient();
            await SignInAsync(demo, "admin", result.Password, DemoWorkspace.Slug);
            Assert.Equal(HttpStatusCode.Forbidden, (await demo.PostAsync("/api/platform/demo-workspace/reset", null)).StatusCode);
        });

    /// <summary>What makes the demo safe in production and consistent with what the app itself writes.</summary>
    private static async Task AssertDataAsync(string connection)
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options);
        var tenantId = DemoWorkspace.TenantId;

        var license = await db.TenantLicenses.SingleAsync(x => x.TenantId == tenantId);
        Assert.True(license.IsPerpetual);
        Assert.Null(license.ExpiresAt);
        Assert.Null(license.NextBillingDate);
        Assert.False((await db.EmailSettings.SingleAsync(x => x.TenantId == tenantId)).IsEnabled);
        Assert.All(await db.Users.Where(x => x.TenantId == tenantId).ToListAsync(), user =>
        {
            Assert.False(user.HasEmail);
            Assert.EndsWith("@no-email.invalid", user.Email);
        });

        // Assignment rows are the source of truth; the job column only mirrors them.
        var jobs = await db.WorkOrders.Where(x => x.TenantId == tenantId).ToListAsync();
        var assignments = await db.WorkOrderTechnicianAssignments.Where(x => x.TenantId == tenantId).ToListAsync();
        Assert.All(jobs.Where(x => x.AssignedTechnicianId.HasValue), job =>
            Assert.Contains(assignments, a => a.WorkOrderId == job.Id && a.TechnicianId == job.AssignedTechnicianId));
        Assert.Equal(jobs.Count, jobs.Select(x => x.WorkOrderNumber).Distinct().Count());

        // History is spread over months, not stamped "now".
        var closed = jobs.Where(x => x.Status == WorkOrderStatuses.Closed).ToList();
        Assert.True(closed.Min(x => x.CreatedAt) < DateTime.UtcNow.AddDays(-90));
        Assert.All(closed, job => Assert.True(job.ClosedAt > job.CreatedAt));

        // Repeat failures and a worn bearing, for the reliability and condition screens.
        var ctc = await db.Assets.SingleAsync(x => x.TenantId == tenantId && x.AssetCode == "KPK-CTC-01");
        Assert.True(closed.Count(x => x.AssetId == ctc.Id && x.IsBreakdown) >= 4);
        var vibration = await db.Meters.SingleAsync(x => x.AssetId == ctc.Id && x.MeterType == MeterTypes.Vibration);
        Assert.NotEqual(ConditionLevels.Normal, vibration.ConditionState);

        // Nothing leaked into another tenant.
        Assert.Equal(0, await db.Assets.CountAsync(x => x.TenantId != tenantId && x.AssetCode.StartsWith("KPK-")));
    }

    private static WebApplicationFactory<Program> CreateApp(string connection) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.UseSetting("ConnectionStrings:DefaultConnection", connection);
            builder.UseSetting("Jwt:Issuer", "ecosys-tests");
            builder.UseSetting("Jwt:Audience", "ecosys-tests");
            builder.UseSetting("Jwt:SigningKey", "12345678901234567890123456789012");
            builder.UseSetting("PlatformAdmin:Email", PlatformEmail);
            builder.UseSetting("PlatformAdmin:Password", PlatformPassword);
            builder.UseSetting("PlatformAdmin:FullName", "Platform Owner");
            builder.UseSetting("App:PublicUrl", "https://ecosys-tests.example.com");
            builder.UseSetting($"{AuthSecurityOptions.SectionName}:FailedLoginThreshold", "50");
            builder.UseSetting($"{AuthSecurityOptions.SectionName}:LockoutMinutes", "15");
        });

    private static async Task SignInAsync(HttpClient client, string user, string password, string? workspace = null)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new { emailOrUsername = user, password, workspace });
        Assert.True(response.IsSuccessStatusCode, $"Sign-in as {user} failed: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        var token = (await response.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("token").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private static async Task<T> PostAsync<T>(HttpClient client, string path, object? body = null)
    {
        var response = body is null ? await client.PostAsync(path, null) : await client.PostAsJsonAsync(path, body);
        Assert.True(response.IsSuccessStatusCode, $"{path} returned {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }
}
