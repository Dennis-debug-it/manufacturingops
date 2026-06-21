using Ecosys.Api.Controllers;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ecosys.Tests;

public sealed class PlatformLeadFlowTests
{
    [Fact]
    public async Task CreatePublicLead_PersistsLead_AndReturnsSuccessMessage()
    {
        await using var dbContext = CreateDbContext();
        var controller = CreateController(dbContext);

        var result = await controller.CreatePublicLead(
            new CreatePlatformLeadRequest(
                "Acme Facilities",
                "Amina Noor",
                "amina@acme.test",
                "+254700000100",
                "Kenya",
                "Facilities",
                "50 users",
                "Need a guided rollout.",
                "Email"),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<PublicLeadSubmissionResponse>(ok.Value);
        Assert.True(response.Success);
        Assert.Contains("Thank you", response.Message);

        var storedLead = await dbContext.PlatformLeads.SingleAsync();
        Assert.Equal("Acme Facilities", storedLead.CompanyName);
        Assert.Equal("Amina Noor", storedLead.ContactPersonName);
        Assert.Equal("amina@acme.test", storedLead.Email);
        Assert.Equal("New", storedLead.Status);

        var audit = await dbContext.AuditLogs.SingleAsync();
        Assert.Equal("platform.lead.submitted", audit.Action);
        Assert.Equal(storedLead.Id.ToString(), audit.EntityId);
        Assert.Equal("Amina Noor", audit.ActorName);
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ecosys-platform-leads-{Guid.NewGuid():N}")
            .Options;

        return new AppDbContext(options);
    }

    private static PlatformLeadsController CreateController(AppDbContext dbContext)
    {
        var controller = new PlatformLeadsController(
            dbContext,
            new AuditLogService(dbContext, new HttpContextAccessor()),
            new FakeEmailTemplateService(),
            new FakeEmailSubjectRuleService(),
            new EmailOutboxService(dbContext, new EmailDeliveryLogService(dbContext)),
            new FakeTenantContext(),
            NullLogger<PlatformLeadsController>.Instance);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        return controller;
    }

    private sealed class FakeTenantContext : ITenantContext
    {
        public Guid? TenantId => null;
        public Guid? UserId => null;
        public Guid? SessionId => null;
        public string? Email => null;
        public string? Role => null;
        public string? JobTitle => null;
        public bool IsAuthenticated => false;
        public bool IsSuperAdmin => false;
        public bool IsAdmin => false;
        public bool HasRole(string role) => false;
        public bool HasPermission(string permissionName) => false;
        public Guid GetRequiredTenantId() => throw new InvalidOperationException();
        public Guid GetRequiredUserId() => throw new InvalidOperationException();
        public Guid GetRequiredSessionId() => throw new InvalidOperationException();
    }

    private sealed class FakeEmailTemplateService : IEmailTemplateService
    {
        public Task<IReadOnlyCollection<EmailTemplateDescriptor>> ListPlatformTemplatesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<EmailTemplateDescriptor>> ListTenantTemplatesAsync(Guid tenantId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<EmailTemplateDescriptor> GetPlatformTemplateAsync(string eventKey, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<EmailTemplateDescriptor> GetTenantTemplateAsync(Guid tenantId, string eventKey, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<EmailTemplateDescriptor> SavePlatformTemplateAsync(string eventKey, EmailTemplateUpdateRequest request, Guid actorUserId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<EmailTemplateDescriptor> SaveTenantTemplateAsync(Guid tenantId, string eventKey, EmailTemplateUpdateRequest request, Guid actorUserId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<EmailTemplateDescriptor> ResetPlatformTemplateAsync(string eventKey, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<EmailTemplateDescriptor> ResetTenantTemplateAsync(Guid tenantId, string eventKey, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<RenderedEmailTemplate> RenderPlatformTemplateAsync(string eventKey, IReadOnlyDictionary<string, string?> values, CancellationToken cancellationToken = default) =>
            Task.FromResult(new RenderedEmailTemplate(eventKey, "Lead", "Lead", "<p>Lead</p>", "Lead", null, null, true));

        public Task<RenderedEmailTemplate> RenderTenantTemplateAsync(Guid tenantId, string eventKey, IReadOnlyDictionary<string, string?> values, CancellationToken cancellationToken = default) =>
            Task.FromResult(new RenderedEmailTemplate(eventKey, "Tenant", "Tenant", "<p>Tenant</p>", "Tenant", null, null, true));
    }

    private sealed class FakeEmailSubjectRuleService : IEmailSubjectRuleService
    {
        public Task<EmailSubjectRuleOptions> GetSettingsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(EmailSubjectRuleOptions.Default);

        public Task<string> BuildFinalSubjectAsync(Guid? tenantId, string eventKey, string templateSubject, string? tenantNameOverride = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(templateSubject);
    }
}
