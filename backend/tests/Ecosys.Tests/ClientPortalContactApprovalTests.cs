using System.Text.Json;
using Ecosys.Api.Controllers;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Tests;

/// <summary>
/// "Require Admin Approval for New Portal Contacts": when on, a contact invited by non-admin staff is created
/// inactive and pending, gets no sign-in link, and only an admin can approve it (which sends the link).
/// </summary>
public sealed class ClientPortalContactApprovalTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _clientId = Guid.NewGuid();

    [Fact]
    public async Task NonAdminInvite_IsPendingAndSendsNoLink_WhenApprovalRequired()
    {
        var (db, links) = await SetupAsync(requireApproval: true);
        var controller = CreateController(db, links, isAdmin: false);

        var contact = await CreateAsync(controller);

        Assert.False(contact.IsActive);
        Assert.True(contact.IsPendingApproval);
        Assert.Empty(links.Sent);
        await Assert.ThrowsAsync<BusinessRuleException>(() => controller.ResendLink(_clientId, contact.Id, CancellationToken.None));
    }

    [Fact]
    public async Task AdminInvite_IsActiveImmediately_WhenApprovalRequired()
    {
        var (db, links) = await SetupAsync(requireApproval: true);

        var contact = await CreateAsync(CreateController(db, links, isAdmin: true));

        Assert.True(contact.IsActive);
        Assert.False(contact.IsPendingApproval);
        Assert.Equal(["jane@client.test"], links.Sent);
    }

    [Fact]
    public async Task NonAdminInvite_IsActive_WhenApprovalNotRequired()
    {
        var (db, links) = await SetupAsync(requireApproval: false);

        var contact = await CreateAsync(CreateController(db, links, isAdmin: false));

        Assert.True(contact.IsActive);
        Assert.Single(links.Sent);
    }

    [Fact]
    public async Task OnlyAdmin_CanApprove_AndApprovalSendsLink()
    {
        var (db, links) = await SetupAsync(requireApproval: true);
        var contact = await CreateAsync(CreateController(db, links, isAdmin: false));

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            CreateController(db, links, isAdmin: false).UpdateStatus(_clientId, contact.Id, new UpdateClientPortalContactStatusRequest(true), CancellationToken.None));
        Assert.Empty(links.Sent);

        await CreateController(db, links, isAdmin: true).UpdateStatus(_clientId, contact.Id, new UpdateClientPortalContactStatusRequest(true), CancellationToken.None);

        var stored = await db.ClientPortalUsers.SingleAsync();
        Assert.True(stored.IsActive);
        Assert.False(stored.IsPendingApproval);
        Assert.Equal(["jane@client.test"], links.Sent);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task<(AppDbContext Db, RecordingPortalAuthService Links)> SetupAsync(bool requireApproval)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ecosys-portal-approval-{Guid.NewGuid():N}")
            .Options;
        var db = new AppDbContext(options, new FakeTenantContext(_tenantId, _userId, isAdmin: true));
        db.Clients.Add(new Client { Id = _clientId, TenantId = _tenantId, ClientName = "Client Co" });
        db.TenantModuleSettings.Add(new TenantModuleSetting
        {
            TenantId = _tenantId,
            ModuleKey = PortalModuleSettings.ModuleKey,
            ValueJson = JsonSerializer.Serialize(new { requireApprovalForNewContacts = requireApproval }),
        });
        await db.SaveChangesAsync();
        return (db, new RecordingPortalAuthService());
    }

    private ClientPortalContactsController CreateController(AppDbContext db, RecordingPortalAuthService links, bool isAdmin) =>
        new(db, new FakeTenantContext(_tenantId, _userId, isAdmin), new AllowAllUserAccessService(), links, new NoOpAuditLogService())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };

    private async Task<ClientPortalContactResponse> CreateAsync(ClientPortalContactsController controller)
    {
        var result = await controller.Create(_clientId, new CreateClientPortalContactRequest("Jane", "jane@client.test", null), CancellationToken.None);
        return Assert.IsType<ClientPortalContactResponse>(Assert.IsType<CreatedAtActionResult>(result.Result).Value);
    }

    private sealed class RecordingPortalAuthService : IPortalAuthService
    {
        public List<string> Sent { get; } = [];

        public Task RequestMagicLinkAsync(string email, string magicLinkBaseUrl, string? ipAddress, string? userAgent, CancellationToken cancellationToken = default)
        {
            Sent.Add(email);
            return Task.CompletedTask;
        }

        public Task<PortalMagicLinkResult> VerifyMagicLinkAsync(string token, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<string> IssueSignInLinkAsync(Guid portalUserId, string magicLinkBaseUrl, string? ipAddress, string? userAgent, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FakeTenantContext(Guid tenantId, Guid userId, bool isAdmin) : ITenantContext
    {
        public Guid? TenantId => tenantId;
        public Guid? UserId => userId;
        public Guid? SessionId { get; } = Guid.NewGuid();
        public string? Email => "staff@acme.test";
        public string? Role => isAdmin ? AppRoles.Admin : AppRoles.User;
        public string? JobTitle => null;
        public bool IsAuthenticated => true;
        public bool IsSuperAdmin => false;
        public bool IsAdmin => isAdmin;
        public bool HasRole(string role) => string.Equals(role, Role, StringComparison.OrdinalIgnoreCase);
        public bool HasPermission(string permissionName) => true;
        public Guid GetRequiredTenantId() => tenantId;
        public Guid GetRequiredUserId() => userId;
        public Guid GetRequiredSessionId() => SessionId!.Value;
    }

    private sealed class AllowAllUserAccessService : IUserAccessService
    {
        public void EnsureAdmin() { }
        public void EnsureAdminOrPermission(string permissionName) { }
        public void EnsureAdminOrAnyPermission(params string[] permissionNames) { }
        public void EnsureTenantOperationalAccess() { }
    }

    private sealed class NoOpAuditLogService : IAuditLogService
    {
        public Task LogAsync(Guid? tenantId, Guid? userId, string action, string entityName, string entityId, string? details, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LogAsync(Guid? tenantId, Guid? userId, string action, string entityName, string entityId, string? details, string severity = "Info", string? actorName = null, string? ipAddress = null, string? userAgent = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
