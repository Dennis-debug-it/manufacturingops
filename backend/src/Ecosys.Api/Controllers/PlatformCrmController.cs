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

// ── Contacts ─────────────────────────────────────────────────────────────────

[ApiController]
[Authorize(Policy = "PlatformOwnerOnly")]
public sealed class PlatformCrmContactsController(
    AppDbContext dbContext,
    IAuditLogService auditLogService,
    ITenantContext tenantContext) : ControllerBase
{
    [HttpGet("api/platform/crm/contacts")]
    public async Task<ActionResult<IReadOnlyCollection<CrmContactResponse>>> List(CancellationToken cancellationToken)
    {
        var contacts = await dbContext.CrmContacts.AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Select(MapExpr)
            .ToListAsync(cancellationToken);
        return Ok(contacts);
    }

    [HttpGet("api/platform/crm/leads/{leadId:guid}/contacts")]
    public async Task<ActionResult<IReadOnlyCollection<CrmContactResponse>>> ListForLead(Guid leadId, CancellationToken cancellationToken)
    {
        var contacts = await dbContext.CrmContacts.AsNoTracking()
            .Where(x => x.LeadId == leadId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(MapExpr)
            .ToListAsync(cancellationToken);
        return Ok(contacts);
    }

    [HttpPost("api/platform/crm/contacts")]
    public async Task<ActionResult<CrmContactResponse>> Create([FromBody] CreateCrmContactRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.FullName)) throw new BusinessRuleException("Contact name is required.");
        if (string.IsNullOrWhiteSpace(request.CompanyName)) throw new BusinessRuleException("Company name is required.");
        if (string.IsNullOrWhiteSpace(request.Email)) throw new BusinessRuleException("Email is required.");
        if (request.LeadId is { } leadId && !await dbContext.PlatformLeads.AnyAsync(x => x.Id == leadId, cancellationToken))
            throw new NotFoundException("Lead not found.");

        var contact = new CrmContact
        {
            FullName = request.FullName.Trim(),
            CompanyName = request.CompanyName.Trim(),
            Email = request.Email.Trim().ToLowerInvariant(),
            Phone = Trim(request.Phone),
            Role = Trim(request.Role),
            LeadId = request.LeadId,
        };
        dbContext.CrmContacts.Add(contact);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(null, tenantContext.UserId, "platform.crm.contact.created", nameof(CrmContact), contact.Id.ToString(), $"CRM contact {contact.FullName} ({contact.CompanyName}) created.", cancellationToken);
        return Ok(Map(contact));
    }

    [HttpPut("api/platform/crm/contacts/{id:guid}")]
    public async Task<ActionResult<CrmContactResponse>> Update(Guid id, [FromBody] CreateCrmContactRequest request, CancellationToken cancellationToken)
    {
        var contact = await dbContext.CrmContacts.SingleOrDefaultAsync(x => x.Id == id, cancellationToken) ?? throw new NotFoundException("Contact not found.");
        if (!string.IsNullOrWhiteSpace(request.FullName)) contact.FullName = request.FullName.Trim();
        if (!string.IsNullOrWhiteSpace(request.CompanyName)) contact.CompanyName = request.CompanyName.Trim();
        if (!string.IsNullOrWhiteSpace(request.Email)) contact.Email = request.Email.Trim().ToLowerInvariant();
        contact.Phone = Trim(request.Phone);
        contact.Role = Trim(request.Role);
        contact.LeadId = request.LeadId ?? contact.LeadId;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(Map(contact));
    }

    [HttpDelete("api/platform/crm/contacts/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var contact = await dbContext.CrmContacts.SingleOrDefaultAsync(x => x.Id == id, cancellationToken) ?? throw new NotFoundException("Contact not found.");
        dbContext.CrmContacts.Remove(contact);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(null, tenantContext.UserId, "platform.crm.contact.deleted", nameof(CrmContact), id.ToString(), $"CRM contact {contact.FullName} deleted.", cancellationToken);
        return NoContent();
    }

    private static string? Trim(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
    private static readonly System.Linq.Expressions.Expression<Func<CrmContact, CrmContactResponse>> MapExpr =
        x => new CrmContactResponse(x.Id, x.FullName, x.CompanyName, x.Email, x.Phone, x.Role, x.LeadId, x.CreatedAt);
    private static CrmContactResponse Map(CrmContact x) => new(x.Id, x.FullName, x.CompanyName, x.Email, x.Phone, x.Role, x.LeadId, x.CreatedAt);
}

// ── Activities ───────────────────────────────────────────────────────────────

[ApiController]
[Authorize(Policy = "PlatformOwnerOnly")]
public sealed class PlatformCrmActivitiesController(
    AppDbContext dbContext,
    IAuditLogService auditLogService,
    ITenantContext tenantContext) : ControllerBase
{
    private static readonly string[] AllowedTypes = ["Call", "Email", "Meeting", "Note"];

    [HttpGet("api/platform/crm/activities")]
    public async Task<ActionResult<IReadOnlyCollection<CrmActivityResponse>>> List(CancellationToken cancellationToken)
    {
        var activities = await dbContext.CrmActivities.AsNoTracking()
            .Include(x => x.Contact)
            .OrderByDescending(x => x.OccurredAt)
            .Take(500)
            .ToListAsync(cancellationToken);
        return Ok(activities.Select(Map).ToList());
    }

    [HttpGet("api/platform/crm/leads/{leadId:guid}/activities")]
    public async Task<ActionResult<IReadOnlyCollection<CrmActivityResponse>>> ListForLead(Guid leadId, CancellationToken cancellationToken)
    {
        var activities = await dbContext.CrmActivities.AsNoTracking()
            .Include(x => x.Contact)
            .Where(x => x.LeadId == leadId)
            .OrderByDescending(x => x.OccurredAt)
            .ToListAsync(cancellationToken);
        return Ok(activities.Select(Map).ToList());
    }

    [HttpPost("api/platform/crm/activities")]
    public async Task<ActionResult<CrmActivityResponse>> Create([FromBody] CreateCrmActivityRequest request, CancellationToken cancellationToken)
    {
        var type = AllowedTypes.FirstOrDefault(t => string.Equals(t, request.Type?.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? throw new BusinessRuleException("Activity type must be Call, Email, Meeting, or Note.");
        if (string.IsNullOrWhiteSpace(request.Summary)) throw new BusinessRuleException("Summary is required.");
        if (request.ContactId is null && request.LeadId is null) throw new BusinessRuleException("An activity must be linked to a contact or a lead.");

        CrmContact? contact = null;
        if (request.ContactId is { } contactId)
            contact = await dbContext.CrmContacts.SingleOrDefaultAsync(x => x.Id == contactId, cancellationToken) ?? throw new NotFoundException("Contact not found.");

        var loggedByName = await ResolveUserNameAsync(dbContext, tenantContext.UserId, cancellationToken);
        var activity = new CrmActivity
        {
            Type = type,
            Summary = request.Summary.Trim(),
            OccurredAt = request.OccurredAt is { } at ? DateTime.SpecifyKind(at, DateTimeKind.Utc) : DateTime.UtcNow,
            ContactId = request.ContactId,
            LeadId = request.LeadId ?? contact?.LeadId,
            LoggedByUserId = tenantContext.UserId,
            LoggedByName = loggedByName,
        };
        dbContext.CrmActivities.Add(activity);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(null, tenantContext.UserId, "platform.crm.activity.logged", nameof(CrmActivity), activity.Id.ToString(), $"CRM {type} logged.", cancellationToken);

        activity.Contact = contact;
        return Ok(Map(activity));
    }

    internal static async Task<string> ResolveUserNameAsync(AppDbContext db, Guid? userId, CancellationToken ct) =>
        userId is { } id
            ? await db.Users.Where(x => x.Id == id).Select(x => x.FullName).FirstOrDefaultAsync(ct) ?? "Platform Admin"
            : "Platform Admin";

    private static CrmActivityResponse Map(CrmActivity x) =>
        new(x.Id, x.Contact != null ? x.Contact.FullName : "—", x.Type, x.Summary, x.OccurredAt, x.LoggedByName);
}

// ── Tasks ────────────────────────────────────────────────────────────────────

[ApiController]
[Authorize(Policy = "PlatformOwnerOnly")]
public sealed class PlatformCrmTasksController(
    AppDbContext dbContext,
    IAuditLogService auditLogService,
    ITenantContext tenantContext) : ControllerBase
{
    private static readonly string[] AllowedPriorities = ["Low", "Medium", "High"];

    [HttpGet("api/platform/crm/tasks")]
    public async Task<ActionResult<IReadOnlyCollection<CrmTaskResponse>>> List([FromQuery] string? status, CancellationToken cancellationToken)
    {
        var query = dbContext.CrmTasks.AsNoTracking().Include(x => x.RelatedContact).AsQueryable();
        if (!string.IsNullOrWhiteSpace(status) && !string.Equals(status, "All", StringComparison.OrdinalIgnoreCase))
            query = query.Where(x => x.Status == status.Trim());

        var tasks = await query.OrderBy(x => x.Status == "Done").ThenBy(x => x.DueDate).Take(500).ToListAsync(cancellationToken);
        return Ok(tasks.Select(Map).ToList());
    }

    [HttpPost("api/platform/crm/tasks")]
    public async Task<ActionResult<CrmTaskResponse>> Create([FromBody] CreateCrmTaskRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Title)) throw new BusinessRuleException("Title is required.");
        var priority = AllowedPriorities.FirstOrDefault(p => string.Equals(p, request.Priority?.Trim(), StringComparison.OrdinalIgnoreCase)) ?? "Medium";

        var assignedTo = await PlatformCrmActivitiesController.ResolveUserNameAsync(dbContext, tenantContext.UserId, cancellationToken);
        var task = new CrmTask
        {
            Title = request.Title.Trim(),
            DueDate = request.DueDate is { } d ? DateTime.SpecifyKind(d, DateTimeKind.Utc) : null,
            Priority = priority,
            Status = "Open",
            RelatedContactId = request.RelatedContactId,
            RelatedLeadId = request.RelatedLeadId,
            AssignedToUserId = tenantContext.UserId,
            AssignedToName = assignedTo,
        };
        dbContext.CrmTasks.Add(task);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(null, tenantContext.UserId, "platform.crm.task.created", nameof(CrmTask), task.Id.ToString(), $"CRM task '{task.Title}' created.", cancellationToken);
        return Ok(Map(task));
    }

    [HttpPut("api/platform/crm/tasks/{id:guid}/complete")]
    public async Task<ActionResult<CrmTaskResponse>> Complete(Guid id, CancellationToken cancellationToken)
    {
        var task = await LoadWithContact(id, cancellationToken);
        task.Status = "Done";
        task.CompletedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(null, tenantContext.UserId, "platform.crm.task.completed", nameof(CrmTask), task.Id.ToString(), $"CRM task '{task.Title}' completed.", cancellationToken);
        return Ok(Map(task));
    }

    [HttpPut("api/platform/crm/tasks/{id:guid}")]
    public async Task<ActionResult<CrmTaskResponse>> Update(Guid id, [FromBody] UpdateCrmTaskRequest request, CancellationToken cancellationToken)
    {
        var task = await LoadWithContact(id, cancellationToken);
        if (!string.IsNullOrWhiteSpace(request.Title)) task.Title = request.Title!.Trim();
        if (request.DueDate is { } d) task.DueDate = DateTime.SpecifyKind(d, DateTimeKind.Utc);
        if (!string.IsNullOrWhiteSpace(request.Priority)) task.Priority = AllowedPriorities.FirstOrDefault(p => string.Equals(p, request.Priority!.Trim(), StringComparison.OrdinalIgnoreCase)) ?? task.Priority;
        if (!string.IsNullOrWhiteSpace(request.Status)) task.Status = request.Status!.Trim();
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(Map(task));
    }

    private async Task<CrmTask> LoadWithContact(Guid id, CancellationToken ct) =>
        await dbContext.CrmTasks.Include(x => x.RelatedContact).SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Task not found.");

    private static CrmTaskResponse Map(CrmTask x) =>
        new(x.Id, x.Title, x.RelatedContact != null ? x.RelatedContact.FullName : null, x.AssignedToName, x.DueDate, x.Priority, x.Status);
}

// ── DTOs ─────────────────────────────────────────────────────────────────────

public sealed record CrmContactResponse(Guid Id, string FullName, string CompanyName, string Email, string? Phone, string? Role, Guid? LeadId, DateTime CreatedAt);
public sealed record CreateCrmContactRequest([Required] string FullName, [Required] string CompanyName, [Required] string Email, string? Phone, string? Role, Guid? LeadId);

public sealed record CrmActivityResponse(Guid Id, string ContactName, string Type, string Summary, DateTime OccurredAt, string LoggedByName);
public sealed record CreateCrmActivityRequest([Required] string Type, [Required] string Summary, Guid? ContactId, Guid? LeadId, DateTime? OccurredAt);

public sealed record CrmTaskResponse(Guid Id, string Title, string? RelatedContactName, string AssignedToName, DateTime? DueDate, string Priority, string Status);
public sealed record CreateCrmTaskRequest([Required] string Title, DateTime? DueDate, string? Priority, Guid? RelatedContactId, Guid? RelatedLeadId);
public sealed record UpdateCrmTaskRequest(string? Title, DateTime? DueDate, string? Priority, string? Status);
