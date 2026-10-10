using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Options;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Ecosys.Tests;

/// <summary>
/// "Start free trial": a company signs up, confirms its email, and lands in a 14-day trial workspace.
/// Runs the real API against a throwaway PostgreSQL database.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class SelfSignupTests : IAsyncLifetime
{
    private const string Password = "Trial-Signup-Pass-2026!";
    private readonly string databaseName = $"ecosys_signup_{Guid.NewGuid():N}";
    private string serverConnection = string.Empty;
    private string databaseConnection = string.Empty;

    public async Task InitializeAsync()
    {
        serverConnection = Environment.GetEnvironmentVariable("ECOSYS_TEST_DB_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=ecosys_test;Username=postgres;Password=postgres";
        databaseConnection = new NpgsqlConnectionStringBuilder(serverConnection) { Database = databaseName }.ConnectionString;
        await ExecuteAdminAsync($"CREATE DATABASE \"{databaseName}\"");
    }

    public async Task DisposeAsync()
    {
        NpgsqlConnection.ClearAllPools();
        await ExecuteAdminAsync($"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)");
    }

    [Fact]
    public async Task ConfirmedSignup_CreatesATrialWorkspace_AndSignsTheOwnerIn()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var start = await client.PostAsJsonAsync("/api/auth/signup/start", Form("amina@riftcool.test"));
        Assert.Equal(HttpStatusCode.OK, start.StatusCode);
        Assert.Equal(SelfSignupService.SentMessage, (await start.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("message").GetString());

        // Nothing exists until the email is confirmed.
        await using (var db = CreateContext())
        {
            Assert.False(await db.Users.AnyAsync(x => x.Email == "amina@riftcool.test"));
            Assert.False(await db.Tenants.AnyAsync(x => x.CompanyName == "Rift Cooling Ltd"));
        }

        var token = await ReadLinkTokenAsync("amina@riftcool.test");
        var verify = await client.PostAsJsonAsync("/api/auth/signup/verify", new { token });
        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);
        var session = await verify.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Rift Cooling Ltd", session.GetProperty("tenant").GetProperty("companyName").GetString());
        Assert.Equal("amina@riftcool.test", session.GetProperty("user").GetProperty("email").GetString());

        // Signed in straight away.
        using var me = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        me.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.GetProperty("token").GetString());
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(me)).StatusCode);

        await using (var db = CreateContext())
        {
            var tenant = await db.Tenants.SingleAsync(x => x.CompanyName == "Rift Cooling Ltd");
            Assert.True(tenant.IsActive);
            Assert.Equal(TenantStatuses.Trial, tenant.Status);
            var license = await db.TenantLicenses.Include(x => x.LicensePlan).SingleAsync(x => x.TenantId == tenant.Id);
            Assert.Equal(LicenseStatuses.Trial, license.Status);
            Assert.Equal(ServiceOpsPlans.Suite, license.LicensePlan!.PlanCode);
            Assert.InRange(license.TrialEndsAt!.Value, DateTime.UtcNow.AddDays(13.9), DateTime.UtcNow.AddDays(14.1));

            var owner = await db.Users.SingleAsync(x => x.Email == "amina@riftcool.test");
            Assert.Equal(tenant.Id, owner.TenantId);
            Assert.False(owner.MustChangePassword);

            var lead = await db.PlatformLeads.SingleAsync(x => x.Email == "amina@riftcool.test");
            Assert.Equal("Converted to Workspace", lead.Status);
            Assert.Equal(tenant.Id, lead.ConvertedTenantId);
            Assert.Equal("+254700000001", lead.Phone);
        }

        // The owner signs in later with the password chosen on the form.
        var login = await client.PostAsJsonAsync("/api/auth/login", new { emailOrUsername = "amina@riftcool.test", password = Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        // The link works once.
        var again = await client.PostAsJsonAsync("/api/auth/signup/verify", new { token });
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
    }

    [Fact]
    public async Task ExistingAccount_GetsTheSameReply_AndNoEmail()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/signup/start", Form("owner@twice.test"));
        await client.PostAsJsonAsync("/api/auth/signup/verify", new { token = await ReadLinkTokenAsync("owner@twice.test") });

        var start = await client.PostAsJsonAsync("/api/auth/signup/start", Form("owner@twice.test"));

        Assert.Equal(HttpStatusCode.OK, start.StatusCode);
        Assert.Equal(SelfSignupService.SentMessage, (await start.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("message").GetString());
        await using var db = CreateContext();
        Assert.Equal(1, await db.EmailOutboxMessages.CountAsync(x => x.RecipientEmail == "owner@twice.test" && x.EventKey == "auth.signup.verify-email"));
    }

    [Fact]
    public async Task OnlyTheNewestLinkWorks()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/signup/start", Form("twice@clicked.test"));
        var first = await ReadLinkTokenAsync("twice@clicked.test");
        await client.PostAsJsonAsync("/api/auth/signup/start", Form("twice@clicked.test"));
        var second = await ReadLinkTokenAsync("twice@clicked.test");

        Assert.NotEqual(first, second);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/signup/verify", new { token = first })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/signup/verify", new { token = second })).StatusCode);
    }

    [Fact]
    public async Task WeakPasswordOrBadEmail_IsRefused()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var weak = await client.PostAsJsonAsync("/api/auth/signup/start", Form("weak@pass.test") with { password = "short" });
        var badEmail = await client.PostAsJsonAsync("/api/auth/signup/start", Form("not-an-email"));

        Assert.Equal(HttpStatusCode.BadRequest, weak.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, badEmail.StatusCode);
    }

    [Fact]
    public async Task WithoutEmailDelivery_SignupSaysItIsUnavailable()
    {
        await using var factory = CreateFactory(builder => builder.UseSetting("Smtp:Host", string.Empty));
        using var client = factory.CreateClient();

        var start = await client.PostAsJsonAsync("/api/auth/signup/start", Form("nomail@server.test"));

        Assert.Equal(HttpStatusCode.BadRequest, start.StatusCode);
        Assert.Contains("request a demo", await start.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WhenTurnedOff_TheEndpointsAreNotFound()
    {
        await using var factory = CreateFactory(builder => builder.UseSetting($"{AuthSecurityOptions.SectionName}:EmailVerifiedSignupEnabled", "false"));
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/auth/signup/start", Form("off@switch.test"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/auth/signup/verify", new { token = "anything" })).StatusCode);
    }

    private static SignupForm Form(string email) =>
        new("Amina Otieno", email, Password, "Rift Cooling Ltd", "+254700000001", "Kenya", "HVAC");

    private sealed record SignupForm(string fullName, string email, string password, string companyName, string phone, string country, string industry);

    /// <summary>The confirmation link from the newest queued email (the token is after "#").</summary>
    private async Task<string> ReadLinkTokenAsync(string email)
    {
        await using var db = CreateContext();
        var message = await db.EmailOutboxMessages
            .Where(x => x.RecipientEmail == email && x.EventKey == "auth.signup.verify-email")
            .OrderByDescending(x => x.CreatedAt)
            .FirstAsync();
        var match = Regex.Match(message.TextBody ?? string.Empty, @"https://ecosys-tests\.example\.com/signup/verify#([A-Za-z0-9_-]+)");
        Assert.True(match.Success, "The email carries the confirmation link.");
        return match.Groups[1].Value;
    }

    private AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(databaseConnection).Options);

    private WebApplicationFactory<Program> CreateFactory(Action<IWebHostBuilder>? configure = null) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.UseSetting("ConnectionStrings:DefaultConnection", databaseConnection);
            builder.UseSetting("Jwt:Issuer", "ecosys-tests");
            builder.UseSetting("Jwt:Audience", "ecosys-tests");
            builder.UseSetting("Jwt:SigningKey", "12345678901234567890123456789012");
            builder.UseSetting("PlatformAdmin:Email", "platform.owner@ecosys.test");
            builder.UseSetting("PlatformAdmin:Password", "ValidPlatformAdmin123!");
            builder.UseSetting("PlatformAdmin:FullName", "Platform Owner");
            builder.UseSetting("App:PublicUrl", "https://ecosys-tests.example.com");
            // A host is enough for sign-up to accept the form; nothing listens there, so the queued
            // emails just stay in the outbox where the tests read them.
            builder.UseSetting("Smtp:Host", "127.0.0.1");
            builder.UseSetting("Smtp:Port", "1");
            builder.UseSetting($"{AuthSecurityOptions.SectionName}:RateLimitPermitLimit", "1000");
            configure?.Invoke(builder);
        });

    private async Task ExecuteAdminAsync(string sql)
    {
        var admin = new NpgsqlConnectionStringBuilder(serverConnection) { Database = "postgres", Pooling = false }.ConnectionString;
        await using var connection = new NpgsqlConnection(admin);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}
