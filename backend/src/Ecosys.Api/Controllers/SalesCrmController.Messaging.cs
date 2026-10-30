using System.ComponentModel.DataAnnotations;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Api.Controllers;

/// <summary>
/// Talking to the client from the deal, follow-up sequences, and saved pipeline views.
/// <list type="bullet">
/// <item>Email goes out from the workspace's address with the sender's name, tagged with the deal, and is
/// logged on it; replies come back through the intake mailbox (see <see cref="SalesInboundEmailHandler"/>).</item>
/// <item>WhatsApp opens the person's own WhatsApp (the shared platform number cannot carry free-text
/// sales chat), and the message is logged as a WhatsApp activity; the workspace's WhatsApp inbox
/// conversations with the deal's contacts are listed alongside.</item>
/// </list>
/// </summary>
public sealed partial class SalesCrmController
{
    [HttpPost("opportunities/{id:guid}/email")]
    public async Task<ActionResult<SalesActivityResponse>> EmailContact(Guid id, [FromBody] SendDealEmailRequest request, CancellationToken cancellationToken)
    {
        await EnsureCanManageAsync(cancellationToken);
        if (emailOutbox is null)
            throw new BusinessRuleException("Email is not available.");
        var opportunity = await LoadAsync(id, cancellationToken);
        var activity = await SalesDealEmail.QueueAsync(dbContext, emailOutbox, opportunity, request.To, request.ToName, request.Cc,
            request.Subject, request.Body, UserId, await ResolveUserNameAsync(UserId, cancellationToken), automatic: false, null, cancellationToken,
            useIntakeMailbox: platformFeatures?.Value.EmailIntake != false);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "crm.opportunity.emailed", nameof(SalesOpportunity), id.ToString(), $"Emailed {request.To} about '{opportunity.Title}'.", cancellationToken);
        return Ok(MapActivity(activity));
    }

    /// <summary>The workspace WhatsApp inbox's messages with the deal's people (matched on phone number), newest first.</summary>
    [HttpGet("opportunities/{id:guid}/whatsapp")]
    public async Task<ActionResult<IReadOnlyList<SalesWhatsAppMessage>>> DealWhatsApp(Guid id, CancellationToken cancellationToken)
    {
        EnsureCanView();
        var opportunity = await LoadAsync(id, cancellationToken);
        var phones = new List<string?> { opportunity.ContactPhone };
        phones.AddRange(await dbContext.SalesOpportunityContacts.AsNoTracking()
            .Where(x => x.TenantId == TenantId && x.OpportunityId == id).Select(x => x.Contact!.Phone).ToListAsync(cancellationToken));
        var keys = phones.Select(ClientMatching.PhoneKey).Where(x => x is not null).Distinct().ToList();
        if (keys.Count == 0)
            return Ok(Array.Empty<SalesWhatsAppMessage>());
        var conversations = (await dbContext.WhatsAppConversations.AsNoTracking()
                .Where(x => x.TenantId == TenantId)
                .Select(x => new { x.Id, x.ContactName, x.ContactPhone })
                .ToListAsync(cancellationToken))
            .Where(x => keys.Contains(ClientMatching.PhoneKey(x.ContactPhone)))
            .ToList();
        var conversationIds = conversations.Select(x => x.Id).ToList();
        var messages = await dbContext.WhatsAppMessages.AsNoTracking()
            .Where(x => x.TenantId == TenantId && conversationIds.Contains(x.ConversationId))
            .OrderByDescending(x => x.SentAt)
            .Take(50)
            .ToListAsync(cancellationToken);
        return Ok(messages.Select(x =>
        {
            var conversation = conversations.First(c => c.Id == x.ConversationId);
            return new SalesWhatsAppMessage(x.Id, x.ConversationId, conversation.ContactName, conversation.ContactPhone, x.Direction, x.Body, x.SentAt, x.Status);
        }).ToList());
    }

    // ── Sequences ────────────────────────────────────────────────────────────

    [HttpGet("sequences")]
    public async Task<ActionResult<IReadOnlyList<SalesSequenceResponse>>> ListSequences(CancellationToken cancellationToken)
    {
        EnsureCanView();
        var sequences = await dbContext.SalesSequences.AsNoTracking().Include(x => x.Steps)
            .Where(x => x.TenantId == TenantId).OrderBy(x => x.Name).ToListAsync(cancellationToken);
        var running = await dbContext.SalesSequenceEnrollments.AsNoTracking()
            .Where(x => x.TenantId == TenantId && x.Status == SalesSequenceStatuses.Active)
            .GroupBy(x => x.SequenceId).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);
        return Ok(sequences.Select(x => new SalesSequenceResponse(
            x.Id, x.Name, x.IsActive, x.StopOnReply, running.GetValueOrDefault(x.Id),
            x.Steps.OrderBy(s => s.SortOrder).Select(s => new SalesSequenceStepDto(s.DelayDays, s.Action, s.Subject, s.Body)).ToList())).ToList());
    }

    [HttpPost("sequences")]
    public async Task<ActionResult<IReadOnlyList<SalesSequenceResponse>>> CreateSequence([FromBody] UpsertSalesSequenceRequest request, CancellationToken cancellationToken)
    {
        await EnsureCanConfigureSequencesAsync(cancellationToken);
        var sequence = new SalesSequence { TenantId = TenantId };
        ApplySequence(sequence, request);
        dbContext.SalesSequences.Add(sequence);
        await dbContext.SaveChangesAsync(cancellationToken);
        return await ListSequences(cancellationToken);
    }

    /// <summary>Changing the steps affects deals already running it from their next step on.</summary>
    [HttpPut("sequences/{sequenceId:guid}")]
    public async Task<ActionResult<IReadOnlyList<SalesSequenceResponse>>> UpdateSequence(Guid sequenceId, [FromBody] UpsertSalesSequenceRequest request, CancellationToken cancellationToken)
    {
        await EnsureCanConfigureSequencesAsync(cancellationToken);
        var sequence = await dbContext.SalesSequences.Include(x => x.Steps)
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == sequenceId, cancellationToken)
            ?? throw new NotFoundException("Sequence not found.");
        dbContext.SalesSequenceSteps.RemoveRange(sequence.Steps);
        sequence.Steps.Clear();
        ApplySequence(sequence, request);
        await dbContext.SaveChangesAsync(cancellationToken);
        return await ListSequences(cancellationToken);
    }

    [HttpDelete("sequences/{sequenceId:guid}")]
    public async Task<ActionResult<IReadOnlyList<SalesSequenceResponse>>> DeleteSequence(Guid sequenceId, CancellationToken cancellationToken)
    {
        await EnsureCanConfigureSequencesAsync(cancellationToken);
        var sequence = await dbContext.SalesSequences.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == sequenceId, cancellationToken)
            ?? throw new NotFoundException("Sequence not found.");
        if (await dbContext.SalesSequenceEnrollments.AnyAsync(x => x.TenantId == TenantId && x.SequenceId == sequenceId && x.Status == SalesSequenceStatuses.Active, cancellationToken))
            throw new BusinessRuleException($"Deals are still running {sequence.Name}. Switch it off, or stop it on those deals first.");
        dbContext.SalesSequences.Remove(sequence);
        await dbContext.SaveChangesAsync(cancellationToken);
        return await ListSequences(cancellationToken);
    }

    [HttpPost("opportunities/{id:guid}/sequence")]
    public async Task<ActionResult<SalesOpportunityDetailResponse>> StartSequence(Guid id, [FromBody] StartSalesSequenceRequest request, CancellationToken cancellationToken)
    {
        await EnsureCanManageAsync(cancellationToken);
        var opportunity = await LoadAsync(id, cancellationToken);
        var enrollment = await SalesSequences.EnrollAsync(dbContext, TenantId, opportunity, request.SequenceId, UserId, DateTime.UtcNow, cancellationToken);
        var name = await dbContext.SalesSequences.Where(x => x.Id == enrollment.SequenceId).Select(x => x.Name).FirstAsync(cancellationToken);
        await AddSystemActivityAsync(opportunity, $"Started the {name} sequence.", cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        // The first step may be due at once; run it now rather than at the next hourly run.
        if (enrollment.NextDueAt <= DateTime.UtcNow && emailOutbox is not null)
            await SalesSequences.RunDueAsync(dbContext, emailOutbox, new HashSet<Guid> { TenantId }, DateTime.UtcNow, cancellationToken,
                useIntakeMailbox: platformFeatures?.Value.EmailIntake != false);
        return Ok(await LoadDetailAsync(id, cancellationToken));
    }

    [HttpDelete("opportunities/{id:guid}/sequence")]
    public async Task<ActionResult<SalesOpportunityDetailResponse>> StopSequence(Guid id, CancellationToken cancellationToken)
    {
        await EnsureCanManageAsync(cancellationToken);
        var opportunity = await LoadAsync(id, cancellationToken);
        var who = await ResolveUserNameAsync(UserId, cancellationToken);
        if (await SalesSequences.StopAsync(dbContext, TenantId, id, $"Stopped by {who}.", DateTime.UtcNow, cancellationToken) > 0)
            await AddSystemActivityAsync(opportunity, "Stopped the follow-up sequence.", cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(await LoadDetailAsync(id, cancellationToken));
    }

    private async Task<SalesDealSequence?> DealSequenceAsync(Guid opportunityId, CancellationToken cancellationToken)
    {
        var enrollment = await dbContext.SalesSequenceEnrollments.AsNoTracking()
            .Include(x => x.Sequence).ThenInclude(x => x!.Steps)
            .Where(x => x.TenantId == TenantId && x.OpportunityId == opportunityId)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        return enrollment is null ? null : new SalesDealSequence(
            enrollment.Id, enrollment.SequenceId, enrollment.Sequence!.Name, enrollment.Status, enrollment.NextStep, enrollment.Sequence.Steps.Count,
            enrollment.NextDueAt, enrollment.StoppedReason, enrollment.FinishedAt);
    }

    /// <summary>Sequences send email as the deal owners, so setting them up is for admins and sales managers.</summary>
    private async Task EnsureCanConfigureSequencesAsync(CancellationToken cancellationToken)
    {
        await EnsureCanManageAsync(cancellationToken);
        if (!IsAdmin && !tenantContext.HasPermission(Shared.Auth.PermissionNames.CrmViewAll))
            throw new ForbiddenException("Only admins and sales managers can set up sequences.");
    }

    private static void ApplySequence(SalesSequence sequence, UpsertSalesSequenceRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new BusinessRuleException("Give the sequence a name.");
        var steps = request.Steps ?? [];
        if (steps.Count == 0) throw new BusinessRuleException("Add at least one step.");
        if (steps.Count > 20) throw new BusinessRuleException("A sequence has at most 20 steps.");
        var order = 0;
        foreach (var step in steps)
        {
            var action = SalesSequenceActions.All.FirstOrDefault(x => string.Equals(x, step.Action, StringComparison.OrdinalIgnoreCase))
                ?? throw new BusinessRuleException("A step is a task or an email.");
            if (step.DelayDays is < 0 or > 365) throw new BusinessRuleException("Wait between 0 and 365 days between steps.");
            if (string.IsNullOrWhiteSpace(step.Body)) throw new BusinessRuleException(action == SalesSequenceActions.Email ? "Write each email." : "Say what each task is.");
            if (action == SalesSequenceActions.Email && string.IsNullOrWhiteSpace(step.Subject)) throw new BusinessRuleException("Give each email a subject.");
            sequence.Steps.Add(new SalesSequenceStep
            {
                TenantId = sequence.TenantId, SequenceId = sequence.Id, SortOrder = order += 10, DelayDays = step.DelayDays, Action = action,
                Subject = action == SalesSequenceActions.Email ? step.Subject!.Trim() : null, Body = step.Body.Trim(),
            });
        }
        sequence.Name = request.Name.Trim();
        sequence.IsActive = request.IsActive;
        sequence.StopOnReply = request.StopOnReply;
    }

    // ── Saved views ──────────────────────────────────────────────────────────

    [HttpGet("views")]
    public async Task<ActionResult<IReadOnlyList<SalesSavedViewResponse>>> ListViews(CancellationToken cancellationToken)
    {
        EnsureCanView();
        return Ok(await dbContext.SalesSavedViews.AsNoTracking()
            .Where(x => x.TenantId == TenantId && (x.UserId == UserId || x.IsShared))
            .OrderBy(x => x.Name)
            .Select(x => new SalesSavedViewResponse(x.Id, x.Name, x.Query, x.IsShared, x.UserId == UserId))
            .ToListAsync(cancellationToken));
    }

    [HttpPost("views")]
    public async Task<ActionResult<IReadOnlyList<SalesSavedViewResponse>>> SaveView([FromBody] SaveSalesViewRequest request, CancellationToken cancellationToken)
    {
        EnsureCanView();
        if (string.IsNullOrWhiteSpace(request.Name)) throw new BusinessRuleException("Name the view.");
        var query = (request.Query ?? string.Empty).Trim().TrimStart('?');
        if (request.IsShared && !IsAdmin && !tenantContext.HasPermission(Shared.Auth.PermissionNames.CrmViewAll))
            throw new ForbiddenException("Only admins and sales managers can share views.");
        var name = request.Name.Trim();
        var view = await dbContext.SalesSavedViews.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.UserId == UserId && x.Name == name, cancellationToken);
        if (view is null)
        {
            view = new SalesSavedView { TenantId = TenantId, UserId = UserId, Name = name };
            dbContext.SalesSavedViews.Add(view);
        }
        view.Query = query;
        view.IsShared = request.IsShared;
        await dbContext.SaveChangesAsync(cancellationToken);
        return await ListViews(cancellationToken);
    }

    [HttpDelete("views/{viewId:guid}")]
    public async Task<ActionResult<IReadOnlyList<SalesSavedViewResponse>>> DeleteView(Guid viewId, CancellationToken cancellationToken)
    {
        EnsureCanView();
        var view = await dbContext.SalesSavedViews.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == viewId, cancellationToken)
            ?? throw new NotFoundException("View not found.");
        if (view.UserId != UserId && !IsAdmin)
            throw new ForbiddenException("Only the person who saved a view can delete it.");
        dbContext.SalesSavedViews.Remove(view);
        await dbContext.SaveChangesAsync(cancellationToken);
        return await ListViews(cancellationToken);
    }
}

public sealed record SendDealEmailRequest(
    [StringLength(256)] string To,
    [StringLength(150)] string? ToName,
    IReadOnlyList<string>? Cc,
    [StringLength(200)] string Subject,
    [StringLength(10000)] string Body);

public sealed record SalesWhatsAppMessage(Guid Id, Guid ConversationId, string ContactName, string ContactPhone, string Direction, string Body, DateTime SentAt, string Status);

public sealed record SalesSequenceStepDto(int DelayDays, [StringLength(20)] string Action, [StringLength(200)] string? Subject, [StringLength(4000)] string Body);

public sealed record SalesSequenceResponse(Guid Id, string Name, bool IsActive, bool StopOnReply, int RunningDeals, IReadOnlyList<SalesSequenceStepDto> Steps);

public sealed record UpsertSalesSequenceRequest([StringLength(100)] string Name, bool IsActive, bool StopOnReply, IReadOnlyList<SalesSequenceStepDto>? Steps);

public sealed record StartSalesSequenceRequest(Guid SequenceId);

/// <param name="NextStep">How many steps have run.</param>
public sealed record SalesDealSequence(Guid EnrollmentId, Guid SequenceId, string Name, string Status, int NextStep, int StepCount, DateTime? NextDueAt, string? StoppedReason, DateTime? FinishedAt);

public sealed record SalesSavedViewResponse(Guid Id, string Name, string Query, bool IsShared, bool IsMine);

public sealed record SaveSalesViewRequest([StringLength(80)] string Name, [StringLength(1000)] string? Query, bool IsShared);
