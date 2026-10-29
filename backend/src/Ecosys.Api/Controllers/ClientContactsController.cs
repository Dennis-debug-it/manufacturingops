using System.ComponentModel.DataAnnotations;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Api.Controllers;

/// <summary>
/// The people at a client or prospect. Anyone who can see clients can see them; editing needs the
/// client-editing permission or the sales CRM, since sales keeps most of these up to date.
/// </summary>
[ApiController]
[Authorize]
[Route("api/clients/{clientId:guid}/contacts")]
public sealed class ClientContactsController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IUserAccessService userAccessService) : TenantAwareControllerBase(tenantContext)
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<ClientContactResponse>>> List(Guid clientId, CancellationToken cancellationToken)
    {
        userAccessService.EnsureTenantOperationalAccess();
        await EnsureClientAsync(clientId, cancellationToken);
        var contacts = await dbContext.ClientContacts.AsNoTracking()
            .Where(x => x.TenantId == TenantId && x.ClientId == clientId && x.IsActive)
            .OrderByDescending(x => x.IsPrimary).ThenBy(x => x.FullName)
            .ToListAsync(cancellationToken);
        return Ok(contacts.Select(ClientContacts.Map).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<ClientContactResponse>> Create(Guid clientId, [FromBody] UpsertClientContactRequest request, CancellationToken cancellationToken)
    {
        EnsureCanEdit();
        await EnsureClientAsync(clientId, cancellationToken);
        var contact = new ClientContact { TenantId = TenantId, ClientId = clientId };
        await ApplyAsync(contact, request, cancellationToken);
        dbContext.ClientContacts.Add(contact);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(ClientContacts.Map(contact));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ClientContactResponse>> Update(Guid clientId, Guid id, [FromBody] UpsertClientContactRequest request, CancellationToken cancellationToken)
    {
        EnsureCanEdit();
        var contact = await LoadAsync(clientId, id, cancellationToken);
        await ApplyAsync(contact, request, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(ClientContacts.Map(contact));
    }

    /// <summary>Archives the contact; deals keep their history but stop listing them.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid clientId, Guid id, CancellationToken cancellationToken)
    {
        EnsureCanEdit();
        var contact = await LoadAsync(clientId, id, cancellationToken);
        contact.IsActive = false;
        contact.IsPrimary = false;
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private void EnsureCanEdit()
    {
        userAccessService.EnsureTenantOperationalAccess();
        if (!(tenantContext.IsAdmin || tenantContext.HasPermission(PermissionNames.CanManageAssets) || SalesCrmAccess.CanManage(tenantContext)))
            throw new ForbiddenException("You do not have permission to edit client contacts.");
    }

    private async Task EnsureClientAsync(Guid clientId, CancellationToken cancellationToken)
    {
        if (!await dbContext.Clients.AnyAsync(x => x.TenantId == TenantId && x.Id == clientId, cancellationToken))
            throw new NotFoundException("Client not found.");
    }

    private async Task<ClientContact> LoadAsync(Guid clientId, Guid id, CancellationToken cancellationToken) =>
        await dbContext.ClientContacts.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.ClientId == clientId && x.Id == id && x.IsActive, cancellationToken)
        ?? throw new NotFoundException("Contact not found.");

    private async Task ApplyAsync(ClientContact contact, UpsertClientContactRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.FullName))
            throw new BusinessRuleException("Contact name is required.");
        contact.FullName = request.FullName.Trim();
        contact.JobTitle = Clean(request.JobTitle);
        contact.Email = Clean(request.Email)?.ToLowerInvariant();
        contact.Phone = Clean(request.Phone);
        contact.Role = ClientContacts.NormalizeRole(request.Role);
        contact.Notes = Clean(request.Notes);
        if (request.IsPrimary && !contact.IsPrimary)
        {
            // One primary per client.
            await dbContext.ClientContacts
                .Where(x => x.TenantId == TenantId && x.ClientId == contact.ClientId && x.IsPrimary && x.Id != contact.Id)
                .ForEachAsync(x => x.IsPrimary = false, cancellationToken);
        }
        contact.IsPrimary = request.IsPrimary;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

internal static class ClientContacts
{
    public static readonly string[] Roles = ["DecisionMaker", "Procurement", "Technical", "Finance", "Other"];

    public static string NormalizeRole(string? role) =>
        Roles.FirstOrDefault(x => string.Equals(x, role?.Trim(), StringComparison.OrdinalIgnoreCase)) ?? "Other";

    public static ClientContactResponse Map(ClientContact x) =>
        new(x.Id, x.ClientId, x.FullName, x.JobTitle, x.Email, x.Phone, x.Role, x.IsPrimary, x.Notes);

    /// <summary>
    /// The contact on a client for the given person: one with the same email, else the same name,
    /// else a new one. Changes are added to the context.
    /// </summary>
    public static async Task<ClientContact> FindOrAddAsync(
        AppDbContext dbContext, Guid tenantId, Guid clientId, string fullName, string? email, string? phone, CancellationToken cancellationToken)
    {
        var name = fullName.Trim();
        var normalizedEmail = string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();
        var existing = await dbContext.ClientContacts
            .Where(x => x.TenantId == tenantId && x.ClientId == clientId && x.IsActive)
            .ToListAsync(cancellationToken);
        var contact = existing.FirstOrDefault(x => normalizedEmail != null && x.Email == normalizedEmail)
            ?? existing.FirstOrDefault(x => string.Equals(x.FullName.Trim(), name, StringComparison.OrdinalIgnoreCase))
            ?? dbContext.ClientContacts.Local.FirstOrDefault(x => x.ClientId == clientId && string.Equals(x.FullName, name, StringComparison.OrdinalIgnoreCase));
        if (contact is null)
        {
            contact = new ClientContact
            {
                TenantId = tenantId, ClientId = clientId, FullName = name, Email = normalizedEmail, Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim(),
                IsPrimary = existing.Count == 0,
            };
            dbContext.ClientContacts.Add(contact);
        }
        else
        {
            contact.Email ??= normalizedEmail;
            contact.Phone ??= string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
        }
        return contact;
    }
}

public sealed record UpsertClientContactRequest(
    [StringLength(150)] string FullName,
    [StringLength(120)] string? JobTitle,
    [StringLength(256)] string? Email,
    [StringLength(50)] string? Phone,
    [StringLength(30)] string? Role,
    bool IsPrimary,
    [StringLength(1000)] string? Notes);

public sealed record ClientContactResponse(
    Guid Id, Guid ClientId, string FullName, string? JobTitle, string? Email, string? Phone, string Role, bool IsPrimary, string? Notes);
