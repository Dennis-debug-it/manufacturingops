using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/clients/{clientId:guid}/portal-users")]
public sealed class ClientPortalContactsController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IUserAccessService userAccessService,
    IPortalAuthService portalAuthService,
    IAuditLogService auditLogService) : TenantAwareControllerBase(tenantContext)
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
        await auditLogService.LogAsync(TenantId, UserId, "portal.contact.link-resent", nameof(ClientPortalUser), contact.Id.ToString(), $"Sign-in link resent to portal contact '{contact.Email}'.", cancellationToken);

        return Ok(Map(contact));
    }

    private Task SendSignInLinkAsync(ClientPortalUser contact, CancellationToken cancellationToken)
    {
        var origin = Request.Headers.Origin.FirstOrDefault() ?? $"{Request.Scheme}://{Request.Host.Value}";
        return portalAuthService.RequestMagicLinkAsync(contact.Email, origin, HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString(), cancellationToken);
    }

    private async Task EnsureClientExistsAsync(Guid clientId, CancellationToken cancellationToken)
    {
        var exists = await dbContext.Clients.AnyAsync(x => x.TenantId == TenantId && x.Id == clientId, cancellationToken);
        if (!exists)
        {
            throw new NotFoundException("Client was not found.");
        }
    }

    private static ClientPortalContactResponse Map(ClientPortalUser x) =>
        new(x.Id, x.FullName, x.Email, x.Phone, x.IsActive, x.IsPendingApproval, x.LastLoginAt, x.CreatedAt);
}

public sealed record ClientPortalContactResponse(Guid Id, string FullName, string Email, string? Phone, bool IsActive, bool IsPendingApproval, DateTime? LastLoginAt, DateTime CreatedAt);
public sealed record CreateClientPortalContactRequest(string FullName, string Email, string? Phone);
public sealed record UpdateClientPortalContactStatusRequest(bool IsActive);
