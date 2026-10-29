using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Api.Controllers;

/// <summary>Someone asking about work: from the website enquiry form or an email to the lead address.</summary>
public sealed record SalesInboundLead(
    string? CompanyName, string? ContactName, string? Email, string? Phone, string? Location, string Message, string Source, string? ServiceType = null);

/// <summary>
/// Turns enquiries into leads the same way people add them: the company is matched to an existing client
/// or prospect (by name, email domain or phone) or becomes a new prospect, the person becomes a contact,
/// an owner is chosen by the usual order (assignment rules first, see <see cref="ISalesOwnerResolver"/>),
/// and a follow-up due now puts it on the owner's list and reminder. A second enquiry from the same person
/// within a week is added to their open lead instead of opening another.
/// </summary>
public static class SalesInboundLeads
{
    public static readonly TimeSpan RepeatWindow = TimeSpan.FromDays(7);

    public static async Task<(SalesOpportunity Deal, bool IsNew)> CreateAsync(
        AppDbContext dbContext, ISalesOwnerResolver ownerResolver, IPushNotificationQueue? push, Guid tenantId, SalesInboundLead lead, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var email = string.IsNullOrWhiteSpace(lead.Email) ? null : lead.Email.Trim().ToLowerInvariant();
        var phone = string.IsNullOrWhiteSpace(lead.Phone) ? null : lead.Phone.Trim();
        var who = FirstNonBlank(lead.ContactName, email, phone) ?? "Someone";
        var message = SalesDealEmail.Truncate(lead.Message.Trim(), 1500);

        if (email is not null || ClientMatching.PhoneKey(phone) is not null)
        {
            var since = now - RepeatWindow;
            var phoneKey = ClientMatching.PhoneKey(phone);
            var recent = await dbContext.SalesOpportunities
                .Where(x => x.TenantId == tenantId && x.Stage != "Won" && x.Stage != "Lost" && x.CreatedAt >= since
                    && (x.Source == lead.Source || x.Source == "Website" || x.Source == "Email")
                    && ((email != null && x.ContactEmail == email) || (phoneKey != null && x.ContactPhone != null && x.ContactPhone.EndsWith(phoneKey))))
                .OrderByDescending(x => x.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);
            if (recent is not null)
            {
                dbContext.SalesActivities.Add(new SalesActivity
                {
                    TenantId = tenantId, OpportunityId = recent.Id, Type = SalesActivityKinds.EmailReceived, OccurredAt = now,
                    Summary = SalesDealEmail.Truncate($"Another enquiry from {who} ({lead.Source.ToLowerInvariant()}): {message}", 2000), LoggedByName = who,
                });
                recent.LastActivityAt = now;
                Notify(push, tenantId, recent, $"New message from {who}", message);
                return (recent, false);
            }
        }

        // An existing client or prospect when the details clearly match one; otherwise a new prospect.
        var matches = await ClientMatching.FindAsync(dbContext, tenantId, lead.CompanyName, email, phone, null, cancellationToken);
        var match = matches.FirstOrDefault(x => x.Reason != "Similar name");
        var pipeline = SalesPipelineCatalog.Default(await SalesPipelineCatalog.LoadAsync(dbContext, tenantId, cancellationToken));
        var currency = await dbContext.Tenants.Where(x => x.Id == tenantId).Select(x => x.Currency).FirstOrDefaultAsync(cancellationToken);
        var company = FirstNonBlank(lead.CompanyName, match?.ClientName, lead.ContactName, email, phone)!;
        var deal = new SalesOpportunity
        {
            TenantId = tenantId,
            Title = SalesDealEmail.Truncate($"Enquiry: {FirstLine(message)}", 200),
            Stage = pipeline.FirstOpen.Key,
            Probability = pipeline.FirstOpen.Probability,
            PipelineId = pipeline.Id,
            ClientId = match?.Id,
            CompanyName = match is null ? company : null,
            ContactName = lead.ContactName?.Trim(),
            ContactEmail = email,
            ContactPhone = phone,
            Location = lead.Location?.Trim(),
            Source = lead.Source,
            ServiceType = lead.ServiceType,
            Description = message,
            Currency = string.IsNullOrWhiteSpace(currency) ? "KES" : currency.Trim().ToUpperInvariant(),
            LastActivityAt = now,
        };
        await ProspectClients.LinkAsync(dbContext, deal, null, cancellationToken);
        var owner = await ownerResolver.ResolveAsync(tenantId, [], match?.Id, new SalesLeadFacts(lead.Source, lead.ServiceType, lead.Location), cancellationToken);
        deal.OwnerUserId = owner?.Id;
        deal.OwnerName = owner?.FullName;
        dbContext.SalesOpportunities.Add(deal);
        if (deal.ClientId.HasValue && !string.IsNullOrWhiteSpace(deal.ContactName))
        {
            var contact = await ClientContacts.FindOrAddAsync(dbContext, tenantId, deal.ClientId.Value, deal.ContactName, email, phone, cancellationToken);
            dbContext.SalesOpportunityContacts.Add(new SalesOpportunityContact { TenantId = tenantId, OpportunityId = deal.Id, ContactId = contact.Id });
        }
        dbContext.SalesActivities.Add(new SalesActivity
        {
            TenantId = tenantId, OpportunityId = deal.Id, Type = SalesActivityKinds.EmailReceived, OccurredAt = now,
            Summary = SalesDealEmail.Truncate($"Enquiry from {who} ({lead.Source.ToLowerInvariant()}): {message}", 2000), LoggedByName = who,
        });
        dbContext.SalesActivities.Add(new SalesActivity
        {
            TenantId = tenantId, OpportunityId = deal.Id, Type = "FollowUp", OccurredAt = now, DueAt = now,
            Summary = SalesDealEmail.Truncate($"Reply to {who} about their enquiry.", 2000), LoggedByName = "Lead capture",
        });
        Notify(push, tenantId, deal, $"New lead: {company}", message);
        return (deal, true);
    }

    private static void Notify(IPushNotificationQueue? push, Guid tenantId, SalesOpportunity deal, string title, string body)
    {
        if (push is not null && deal.OwnerUserId is { } owner)
            push.Enqueue(tenantId, [owner], new PushMessage(title, SalesDealEmail.Truncate(body, 140), $"/crm/opportunities/{deal.Id}", $"crm-lead-{deal.Id}"));
    }

    private static string FirstLine(string text)
    {
        var line = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() ?? text;
        return line.Length > 80 ? line[..80].TrimEnd() + "…" : line;
    }

    private static string? FirstNonBlank(params string?[] values) => values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))?.Trim();
}

/// <summary>
/// The CRM side of the email intake (<see cref="ISalesInboundEmailHandler"/>). A reply carrying a deal's
/// tag goes on that deal when it comes from someone at the deal's client (a contact's address, or the
/// same company domain); mail sent to the workspace's lead address becomes a lead.
/// </summary>
public sealed class SalesInboundEmailHandler(
    AppDbContext dbContext,
    ISalesOwnerResolver ownerResolver,
    IPushNotificationQueue pushNotificationQueue,
    ILicenseGuardService licenseGuardService) : ISalesInboundEmailHandler
{
    public async Task<SalesInboundEmailResult?> TryHandleAsync(Guid tenantId, SalesInboundEmail email, CancellationToken cancellationToken = default)
    {
        var snapshot = await licenseGuardService.GetSnapshotAsync(tenantId, cancellationToken);
        if (!ServiceOpsPlans.RequiredModules("SalesCrm", "/api/crm").All(m => snapshot.Modules.Contains(m, StringComparer.OrdinalIgnoreCase)))
            return null;

        var reply = await TryAddReplyAsync(tenantId, email, cancellationToken);
        if (reply is not null)
            return reply;

        var settings = await dbContext.TenantSalesSettings.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken);
        var leadAddress = settings?.LeadEmailAddress?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(leadAddress) || !email.Recipients.Contains(leadAddress))
            return null;

        var (deal, isNew) = await SalesInboundLeads.CreateAsync(dbContext, ownerResolver, pushNotificationQueue, tenantId,
            new SalesInboundLead(null, email.FromName, email.FromAddress, null, null, $"{email.Subject}\n\n{email.Body}".Trim(), "Email"), cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new SalesInboundEmailResult(
            isNew ? SalesInboundOutcomes.LeadCreated : SalesInboundOutcomes.AddedToDeal,
            isNew ? $"New lead \"{deal.Title}\" for {deal.OwnerName ?? "no owner yet"}." : $"Added to the open lead \"{deal.Title}\".",
            deal.Id, deal.ClientId);
    }

    private async Task<SalesInboundEmailResult?> TryAddReplyAsync(Guid tenantId, SalesInboundEmail email, CancellationToken cancellationToken)
    {
        var prefix = SalesDealTags.Find(email.Subject);
        if (prefix is null)
            return null;
        var ids = await dbContext.SalesOpportunities.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);
        var id = ids.Where(x => x.ToString("N").StartsWith(prefix, StringComparison.Ordinal)).Take(2).ToList();
        if (id.Count != 1)
            return null;
        var deal = await dbContext.SalesOpportunities.IgnoreQueryFilters().SingleAsync(x => x.Id == id[0], cancellationToken);

        // Only people at the deal's client: a guessed tag must not let anyone write on a deal.
        var sender = email.FromAddress.Trim().ToLowerInvariant();
        var known = new List<string?> { deal.ContactEmail };
        if (deal.ClientId.HasValue)
        {
            known.AddRange(await dbContext.ClientContacts.IgnoreQueryFilters().AsNoTracking()
                .Where(x => x.TenantId == tenantId && x.ClientId == deal.ClientId).Select(x => x.Email).ToListAsync(cancellationToken));
            known.Add(await dbContext.Clients.IgnoreQueryFilters().AsNoTracking().Where(x => x.Id == deal.ClientId).Select(x => x.Email).FirstOrDefaultAsync(cancellationToken));
        }
        var addresses = known.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!.Trim().ToLowerInvariant()).ToList();
        var domain = ClientMatching.CompanyDomain(sender);
        if (!addresses.Contains(sender) && (domain is null || !addresses.Any(x => ClientMatching.CompanyDomain(x) == domain)))
            return null;

        var now = DateTime.UtcNow;
        var who = string.IsNullOrWhiteSpace(email.FromName) ? sender : $"{email.FromName} <{sender}>";
        dbContext.SalesActivities.Add(new SalesActivity
        {
            TenantId = tenantId, OpportunityId = deal.Id, Type = SalesActivityKinds.EmailReceived, OccurredAt = email.ReceivedAt,
            Summary = SalesDealEmail.Truncate($"Email from {who}: {email.Subject}\n\n{email.Body}", 2000),
            LoggedByName = string.IsNullOrWhiteSpace(email.FromName) ? sender : email.FromName,
        });
        deal.LastActivityAt = now;
        var stopped = await SalesSequences.StopOnReplyAsync(dbContext, tenantId, deal.Id, now, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        if (deal.OwnerUserId is { } owner)
            pushNotificationQueue.Enqueue(tenantId, [owner], new PushMessage($"Reply on {deal.Title}", SalesDealEmail.Truncate(email.Body, 140), $"/crm/opportunities/{deal.Id}", $"crm-reply-{deal.Id}"));
        return new SalesInboundEmailResult(SalesInboundOutcomes.AddedToDeal,
            $"Added to the deal \"{deal.Title}\"." + (stopped > 0 ? " Its follow-up sequence stopped." : string.Empty), deal.Id, deal.ClientId);
    }
}
