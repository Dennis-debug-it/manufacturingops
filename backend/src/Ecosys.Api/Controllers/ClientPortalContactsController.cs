using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ecosys.Shared.Options;

namespace Ecosys.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/clients/{clientId:guid}/portal-users")]
public sealed class ClientPortalContactsController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IUserAccessService userAccessService,
    IPortalAuthService portalAuthService,
    IAuditLogService auditLogService,
    IOptions<PublicAppOptions> publicAppOptions) : TenantAwareControllerBase(tenantContext)
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<ClientPortalContactResponse>>> List(Guid clientId, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageAssets);
        await EnsureClientExistsAsync(clientId, cancellationToken);

        var contacts = await dbContext.ClientPortalUsers
            .Where(x => x.TenantId == TenantId && x.ClientId == clientId)
            .OrderBy(x => x.FullName)
            .ToListAsync(cancellationToken);

        return Ok(contacts.Select(Map).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<ClientPortalContactResponse>> Create(Guid clientId, [FromBody] CreateClientPortalContactRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageAssets);
        await EnsureClientExistsAsync(clientId, cancellationToken);

        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            throw new BusinessRuleException("Contact name is required.");
        }

        var normalizedEmail = request.Email?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            throw new BusinessRuleException("Contact email is required.");
        }

        var emailInUse = await dbContext.ClientPortalUsers.IgnoreQueryFilters().AnyAsync(x => x.Email.ToLower() == normalizedEmail, cancellationToken);
        if (emailInUse)
        {
            throw new BusinessRuleException("A portal contact with this email already exists.");
        }

        var settings = await PortalModuleSettings.LoadAsync(dbContext, TenantId, cancellationToken);
        var needsApproval = settings.RequireApprovalForNewContacts && !tenantContext.IsAdmin;

        var contact = new ClientPortalUser
        {
            TenantId = TenantId,
            ClientId = clientId,
            FullName = request.FullName.Trim(),
            Email = normalizedEmail,
            Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim(),
            IsActive = !needsApproval,
            IsPendingApproval = needsApproval,
            InvitedByUserId = UserId,
        };

        dbContext.ClientPortalUsers.Add(contact);
        await dbContext.SaveChangesAsync(cancellationToken);

        if (needsApproval)
        {
            await auditLogService.LogAsync(TenantId, UserId, "portal.contact.pending-approval", nameof(ClientPortalUser), contact.Id.ToString(), $"Portal contact '{contact.Email}' created and awaiting admin approval.", cancellationToken);
            return CreatedAtAction(nameof(List), new { clientId }, Map(contact));
        }

        await auditLogService.LogAsync(TenantId, UserId, "portal.contact.created", nameof(ClientPortalUser), contact.Id.ToString(), $"Portal contact '{contact.Email}' created.", cancellationToken);
        await SendSignInLinkAsync(contact, cancellationToken);

        return CreatedAtAction(nameof(List), new { clientId }, Map(contact));
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<ActionResult<ClientPortalContactResponse>> UpdateStatus(Guid clientId, Guid id, [FromBody] UpdateClientPortalContactStatusRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageAssets);
        var contact = await dbContext.ClientPortalUsers
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.ClientId == clientId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Portal contact was not found.");

        var approving = request.IsActive && contact.IsPendingApproval;
        if (approving && !tenantContext.IsAdmin)
        {
            throw new ForbiddenException("Only an administrator can approve a new portal contact.");
        }

        contact.IsActive = request.IsActive;
        if (approving)
        {
            contact.IsPendingApproval = false;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        if (approving)
        {
            await auditLogService.LogAsync(TenantId, UserId, "portal.contact.approved", nameof(ClientPortalUser), contact.Id.ToString(), $"Portal contact '{contact.Email}' approved.", cancellationToken);
            await SendSignInLinkAsync(contact, cancellationToken);
        }
        else
        {
            await auditLogService.LogAsync(TenantId, UserId, contact.IsActive ? "portal.contact.activated" : "portal.contact.deactivated", nameof(ClientPortalUser), contact.Id.ToString(), $"Portal contact '{contact.Email}' {(contact.IsActive ? "activated" : "deactivated")}.", cancellationToken);
        }

        return Ok(Map(contact));
    }

    [HttpPost("{id:guid}/resend-link")]
    public async Task<ActionResult<ClientPortalContactResponse>> ResendLink(Guid clientId, Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageAssets);
        var contact = await dbContext.ClientPortalUsers
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.ClientId == clientId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Portal contact was not found.");

        if (contact.IsPendingApproval)
        {
            throw new BusinessRuleException("This contact is awaiting admin approval. A sign-in link is sent once they are approved.");
        }

        await SendSignInLinkAsync(contact, cancellationToken);

        return Ok(Map(contact));
    }

    /// <summary>
    /// The contact's permanent portal link, to share by WhatsApp/SMS. It never expires and gives full
    /// access as this contact, so it is admin-only and audited; it stays the same until reset.
    /// </summary>
    [HttpPost("{id:guid}/access-link")]
    public Task<ActionResult<PortalAccessLinkResponse>> GetAccessLink(Guid clientId, Guid id, CancellationToken cancellationToken) =>
        AccessLinkAsync(clientId, id, reset: false, cancellationToken);

    /// <summary>Replaces the permanent link; the previous one stops working immediately.</summary>
    [HttpPost("{id:guid}/access-link/reset")]
    public Task<ActionResult<PortalAccessLinkResponse>> ResetAccessLink(Guid clientId, Guid id, CancellationToken cancellationToken) =>
        AccessLinkAsync(clientId, id, reset: true, cancellationToken);

    private async Task<ActionResult<PortalAccessLinkResponse>> AccessLinkAsync(Guid clientId, Guid id, bool reset, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();
        var contact = await dbContext.ClientPortalUsers
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.ClientId == clientId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Portal contact was not found.");

        var link = await portalAuthService.GetAccessLinkAsync(contact.Id, PortalBaseUrl(), UserId, reset, cancellationToken);
        return Ok(new PortalAccessLinkResponse(link, $"{PortalBaseUrl().TrimEnd('/')}/portal/login"));
    }

    /// <summary>Staff-initiated, so it's an invitation carrying the permanent portal link, not a 30-minute login link.</summary>
    private Task SendSignInLinkAsync(ClientPortalUser contact, CancellationToken cancellationToken) =>
        portalAuthService.SendInvitationAsync(contact.Id, PortalBaseUrl(), UserId,
            HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString(), cancellationToken);

    /// <summary>The configured public URL; the request's Origin only as a fallback for local setups without one.</summary>
    private string PortalBaseUrl() =>
        Uri.TryCreate(publicAppOptions.Value.PublicUrl?.Trim(), UriKind.Absolute, out var configured)
            ? configured.GetLeftPart(UriPartial.Authority)
            : Request.Headers.Origin.FirstOrDefault() ?? $"{Request.Scheme}://{Request.Host.Value}";

    private async Task EnsureClientExistsAsync(Guid clientId, CancellationToken cancellationToken)
    {
        var exists = await dbContext.Clients.AnyAsync(x => x.TenantId == TenantId && x.Id == clientId, cancellationToken);
        if (!exists)
        {
            throw new NotFoundException("Client was not found.");
        }
    }

    private static ClientPortalContactResponse Map(ClientPortalUser x) =>
        new(x.Id, x.FullName, x.Email, x.Phone, x.IsActive, x.IsPendingApproval, x.LastLoginAt, x.CreatedAt,
            x.ReceivesWhatsApp, x.WhatsAppNumber, x.WhatsAppConsentAt, x.WhatsAppConsentMethod);
}

/// <param name="ReceivesWhatsApp">Gets the client WhatsApp messages (consent recorded; see ClientWhatsAppConsentController).</param>
public sealed record ClientPortalContactResponse(Guid Id, string FullName, string Email, string? Phone, bool IsActive, bool IsPendingApproval, DateTime? LastLoginAt, DateTime CreatedAt,
    bool ReceivesWhatsApp = false, string? WhatsAppNumber = null, DateTime? WhatsAppConsentAt = null, string? WhatsAppConsentMethod = null);
public sealed record CreateClientPortalContactRequest(string FullName, string Email, string? Phone);
public sealed record UpdateClientPortalContactStatusRequest(bool IsActive);

public sealed record PortalAccessLinkResponse(string Link, string PortalLoginUrl);
