using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Errors;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Infrastructure.Services;

/// <summary>
/// Follow-up sequences: enrolling a deal, running steps as they come due (hourly, from the sales
/// reminder worker) and stopping. A task step adds a follow-up for the owner; an email step emails the
/// deal's contact as the owner, or becomes a task when the deal has no email address. Closing the deal,
/// or (when the sequence says so) the client replying, stops it and closes the follow-ups it left open.
/// </summary>
public static class SalesSequences
{
    public static async Task<SalesSequenceEnrollment> EnrollAsync(
        AppDbContext dbContext, Guid tenantId, SalesOpportunity deal, Guid sequenceId, Guid? userId, DateTime now, CancellationToken cancellationToken)
    {
        if (deal.Stage is "Won" or "Lost")
            throw new BusinessRuleException("This deal is closed.");
        var sequence = await dbContext.SalesSequences.Include(x => x.Steps)
            .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == sequenceId, cancellationToken)
            ?? throw new NotFoundException("Sequence not found.");
        if (!sequence.IsActive)
            throw new BusinessRuleException($"The {sequence.Name} sequence is switched off.");
        var steps = sequence.Steps.OrderBy(x => x.SortOrder).ToList();
        if (steps.Count == 0)
            throw new BusinessRuleException($"The {sequence.Name} sequence has no steps.");
        if (await dbContext.SalesSequenceEnrollments.AnyAsync(x => x.TenantId == tenantId && x.OpportunityId == deal.Id && x.Status == SalesSequenceStatuses.Active, cancellationToken))
            throw new BusinessRuleException("This deal is already in a sequence. Stop it first.");

        var enrollment = new SalesSequenceEnrollment
        {
            TenantId = tenantId, SequenceId = sequence.Id, OpportunityId = deal.Id, NextStep = 0,
            NextDueAt = now.AddDays(steps[0].DelayDays), EnrolledByUserId = userId,
        };
        dbContext.SalesSequenceEnrollments.Add(enrollment);
        return enrollment;
    }

    /// <summary>Stops a deal's running sequence and closes the follow-ups it left open. Returns how many were stopped.</summary>
    public static async Task<int> StopAsync(AppDbContext dbContext, Guid tenantId, Guid opportunityId, string reason, DateTime now, CancellationToken cancellationToken, bool onlyStopOnReply = false)
    {
        var running = await dbContext.SalesSequenceEnrollments.IgnoreQueryFilters()
            .Include(x => x.Sequence)
            .Where(x => x.TenantId == tenantId && x.OpportunityId == opportunityId && x.Status == SalesSequenceStatuses.Active)
            .ToListAsync(cancellationToken);
        running = running.Where(x => !onlyStopOnReply || x.Sequence?.StopOnReply != false).ToList();
        foreach (var enrollment in running)
        {
            enrollment.Status = SalesSequenceStatuses.Stopped;
            enrollment.StoppedReason = reason;
            enrollment.FinishedAt = now;
            enrollment.NextDueAt = null;
            var open = await dbContext.SalesActivities.IgnoreQueryFilters()
                .Where(x => x.SequenceEnrollmentId == enrollment.Id && x.DueAt != null && x.CompletedAt == null)
                .ToListAsync(cancellationToken);
            foreach (var task in open)
                task.CompletedAt = now;
        }
        return running.Count;
    }

    public static Task<int> StopOnReplyAsync(AppDbContext dbContext, Guid tenantId, Guid opportunityId, DateTime now, CancellationToken cancellationToken) =>
        StopAsync(dbContext, tenantId, opportunityId, "The client replied.", now, cancellationToken, onlyStopOnReply: true);

    /// <summary>Runs every step that has come due for these tenants. Returns how many steps ran.</summary>
    public static async Task<int> RunDueAsync(AppDbContext dbContext, IEmailOutboxService outbox, IReadOnlySet<Guid> tenants, DateTime now, CancellationToken cancellationToken)
    {
        var due = await dbContext.SalesSequenceEnrollments.IgnoreQueryFilters()
            .Include(x => x.Sequence).ThenInclude(x => x!.Steps)
            .Include(x => x.Opportunity)
            .Where(x => x.Status == SalesSequenceStatuses.Active && x.NextDueAt != null && x.NextDueAt <= now)
            .OrderBy(x => x.NextDueAt)
            .Take(500)
            .ToListAsync(cancellationToken);
        var ran = 0;
        foreach (var enrollment in due.Where(x => tenants.Contains(x.TenantId)))
        {
            var deal = enrollment.Opportunity!;
            if (deal.Stage is "Won" or "Lost")
            {
                await StopAsync(dbContext, enrollment.TenantId, deal.Id, $"The deal was {deal.Stage.ToLowerInvariant()}.", now, cancellationToken);
                continue;
            }
            var steps = enrollment.Sequence!.Steps.OrderBy(x => x.SortOrder).ToList();
            if (enrollment.NextStep >= steps.Count)
            {
                Finish(enrollment, now);
                continue;
            }

            var step = steps[enrollment.NextStep];
            await RunStepAsync(dbContext, outbox, enrollment, step, deal, now, cancellationToken);
            ran++;
            enrollment.NextStep++;
            if (enrollment.NextStep >= steps.Count)
                Finish(enrollment, now);
            else
                enrollment.NextDueAt = now.AddDays(steps[enrollment.NextStep].DelayDays);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        return ran;
    }

    private static void Finish(SalesSequenceEnrollment enrollment, DateTime now)
    {
        enrollment.Status = SalesSequenceStatuses.Completed;
        enrollment.FinishedAt = now;
        enrollment.NextDueAt = null;
    }

    private static async Task RunStepAsync(AppDbContext dbContext, IEmailOutboxService outbox, SalesSequenceEnrollment enrollment, SalesSequenceStep step,
        SalesOpportunity deal, DateTime now, CancellationToken cancellationToken)
    {
        var contact = await PrimaryContactAsync(dbContext, deal, cancellationToken);
        var body = Fill(step.Body, deal, contact.Name);
        var sequenceName = enrollment.Sequence!.Name;
        if (step.Action == SalesSequenceActions.Email && contact.Email is not null)
        {
            await SalesDealEmail.QueueAsync(dbContext, outbox, deal, contact.Email, contact.Name, null,
                Fill(step.Subject ?? deal.Title, deal, contact.Name), body, deal.OwnerUserId, deal.OwnerName, automatic: true, enrollment.Id, cancellationToken);
            return;
        }

        var summary = step.Action == SalesSequenceActions.Email
            ? $"Email {contact.Name ?? "the client"} ({sequenceName}; no email address on the deal): {Fill(step.Subject ?? string.Empty, deal, contact.Name)}\n{body}"
            : $"{body} ({sequenceName})";
        dbContext.SalesActivities.Add(new SalesActivity
        {
            TenantId = deal.TenantId, OpportunityId = deal.Id, Type = "FollowUp", OccurredAt = now, DueAt = now,
            Summary = SalesDealEmail.Truncate(summary, 2000), LoggedByName = sequenceName, SequenceEnrollmentId = enrollment.Id,
        });
        deal.LastActivityAt = now;
    }

    /// <summary>Who a sequence writes to: the deal's own contact details, else its main linked contact.</summary>
    public static async Task<(string? Name, string? Email)> PrimaryContactAsync(AppDbContext dbContext, SalesOpportunity deal, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(deal.ContactEmail))
            return (deal.ContactName, deal.ContactEmail.Trim());
        var linked = await dbContext.SalesOpportunityContacts.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.OpportunityId == deal.Id && x.Contact!.IsActive && x.Contact.Email != null)
            .Select(x => new { x.Contact!.FullName, x.Contact.Email, x.Contact.IsPrimary })
            .OrderByDescending(x => x.IsPrimary)
            .FirstOrDefaultAsync(cancellationToken);
        return linked is null ? (deal.ContactName, null) : (linked.FullName, linked.Email);
    }

    public static string Fill(string template, SalesOpportunity deal, string? contactName) =>
        template
            .Replace("{{ContactName}}", FirstName(contactName) ?? "there", StringComparison.OrdinalIgnoreCase)
            .Replace("{{CompanyName}}", deal.CompanyName ?? deal.Client?.ClientName ?? "your company", StringComparison.OrdinalIgnoreCase)
            .Replace("{{DealTitle}}", deal.Title, StringComparison.OrdinalIgnoreCase)
            .Replace("{{OwnerName}}", deal.OwnerName ?? string.Empty, StringComparison.OrdinalIgnoreCase);

    private static string? FirstName(string? name) =>
        string.IsNullOrWhiteSpace(name) ? null : name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
}
