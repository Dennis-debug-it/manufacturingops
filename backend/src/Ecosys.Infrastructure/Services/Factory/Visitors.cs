using System.Security.Cryptography;
using System.Text;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Errors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Ecosys.Infrastructure.Services.Factory;

/// <summary>A person on a new visit. <paramref name="VisitorId"/> picks a returning visitor; otherwise one is matched by ID or phone and name, or created.</summary>
public sealed record VisitorDetails(
    string FullName,
    string? Phone = null,
    string? Email = null,
    string? Company = null,
    string? IdType = null,
    string? IdNumber = null,
    Guid? ContractorId = null,
    Guid? VisitorId = null);

public sealed record NewVisit(
    Guid FactoryId,
    string Purpose,
    IReadOnlyList<VisitorDetails> Visitors,
    Guid? HostUserId = null,
    DateTime? ExpectedFrom = null,
    DateTime? ExpectedTo = null,
    IReadOnlyList<Guid>? ZoneIds = null,
    Guid? WorkOrderId = null,
    Guid? WorkPermitId = null,
    Guid? ContractorId = null,
    string? VehicleRegistration = null,
    string? Notes = null,
    bool WalkIn = false);

/// <summary>Who is acting. <paramref name="CanManage"/>: may approve any visit and override a failed gate check.</summary>
public sealed record VisitActor(Guid UserId, string Name, bool CanManage = false);

/// <summary>What the gate confirmed for one person.</summary>
/// <param name="RecordInduction">The induction was given at the gate now; it is recorded as a training record.</param>
public sealed record GateConfirmation(
    bool IdVerified = false,
    bool PpeConfirmed = false,
    bool RecordInduction = false,
    Guid? EscortUserId = null,
    string? OverrideReason = null);

/// <summary>A gate check that is not met. An overridable one can be waived by a supervisor with a reason.</summary>
public sealed record GateIssue(string Code, string Message, bool Overridable);

/// <summary>What a person needs at the gate before they may go in.</summary>
public sealed record GateRequirements(bool NeedsInduction, bool HasCurrentInduction, IReadOnlyList<string> Ppe, bool NeedsEscort, bool NeedsPermit);

public sealed record GateCheckResult(GateRequirements Requirements, IReadOnlyList<GateIssue> Issues)
{
    public bool CanEnter => Issues.Count == 0;
    public bool Overridable => Issues.Count > 0 && Issues.All(x => x.Overridable);
}

public interface IVisitorService
{
    /// <summary>
    /// Raises a visit. It is approved straight away when the host (or a visitor manager) raises it ahead;
    /// a walk-in raised at the gate, or a visit raised for someone else, waits for the host.
    /// </summary>
    Task<Visit> CreateAsync(Guid tenantId, NewVisit request, VisitActor actor, CancellationToken cancellationToken = default);

    /// <summary>The host (or a visitor manager) approves or turns down a waiting visit.</summary>
    Task<Visit> DecideAsync(Guid tenantId, Guid visitId, bool approve, string? note, VisitActor actor, CancellationToken cancellationToken = default);

    Task<Visit> CancelAsync(Guid tenantId, Guid visitId, string? reason, VisitActor actor, CancellationToken cancellationToken = default);

    /// <summary>The gate checks for one person, given what the gate has confirmed so far.</summary>
    Task<GateCheckResult> CheckAsync(Guid tenantId, Guid attendeeId, GateConfirmation confirmation, DateTime utcNow, CancellationToken cancellationToken = default);

    /// <summary>Lets a person in. Fails with every unmet check unless the remaining ones are overridable and a supervisor gave a reason.</summary>
    Task<VisitAttendee> CheckInAsync(Guid tenantId, Guid attendeeId, GateConfirmation confirmation, VisitActor actor, CancellationToken cancellationToken = default);

    /// <summary>Records a person leaving; the visit completes when nobody on it is left inside.</summary>
    Task<VisitAttendee> CheckOutAsync(Guid tenantId, Guid attendeeId, VisitActor actor, CancellationToken cancellationToken = default);

    Task<VisitAttendee?> FindBadgeAsync(Guid tenantId, string token, CancellationToken cancellationToken = default);

    /// <summary>Marks a person as accounted for at the assembly point (or clears it).</summary>
    Task MarkAccountedAsync(Guid tenantId, Guid attendeeId, bool accounted, VisitActor actor, CancellationToken cancellationToken = default);

    /// <summary>Starts a new roll call: clears every accounted-for mark at these factories.</summary>
    Task<int> ResetRollCallAsync(Guid tenantId, IReadOnlyCollection<Guid> factoryIds, CancellationToken cancellationToken = default);

    Task<Visitor> SetBlockedAsync(Guid tenantId, Guid visitorId, bool blocked, string? reason, VisitActor actor, CancellationToken cancellationToken = default);

    /// <summary>Removes the personal details of visitors not seen within the retention period. Blocked visitors are kept.</summary>
    Task<int> AnonymiseAsync(Guid tenantId, DateTime utcNow, CancellationToken cancellationToken = default);

    /// <summary>Finds a returning visitor (by ID, or phone and name) or adds a new one, updating their details. Not saved.</summary>
    Task<Visitor> FindOrAddVisitorAsync(Guid tenantId, VisitorDetails person, Guid? contractorId, CancellationToken cancellationToken = default);

    /// <summary>Adds the usual zones of the factory's industry to a factory that has none yet.</summary>
    Task<int> SeedZonesAsync(Guid tenantId, Guid factoryId, CancellationToken cancellationToken = default);
}

internal sealed class VisitorService(
    AppDbContext dbContext,
    IFactorySettingsService settingsService,
    IContractorService contractorService,
    INotificationDispatcher? notificationDispatcher = null,
    ILogger<VisitorService>? logger = null) : IVisitorService
{
    public const string ApprovalKey = "visitor.approval-requested";
    public const string ArrivedKey = "visitor.arrived";
    public const string InductionCategory = "Induction";
    public const string InductionCourse = "Site safety induction";

    /// <summary>The gate may let someone in this long before the visit's expected start.</summary>
    public static readonly TimeSpan EarlyArrival = TimeSpan.FromHours(2);
    public const int MaxPeoplePerVisit = 30;

    private static readonly string[] FinishedJob = [WorkOrderStatuses.Completed, WorkOrderStatuses.Verified, WorkOrderStatuses.Closed, WorkOrderStatuses.Cancelled, WorkOrderStatuses.Rejected];

    public async Task<Visit> CreateAsync(Guid tenantId, NewVisit request, VisitActor actor, CancellationToken cancellationToken = default)
    {
        var factory = await dbContext.OrgNodes.AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == request.FactoryId && x.IsActive, cancellationToken);
        if (factory is null || factory.Level != OrgNodeLevels.Factory) throw new BusinessRuleException("Choose the factory being visited.");
        var purpose = VisitPurposes.Find(request.Purpose) ?? throw new BusinessRuleException($"Purpose must be one of: {string.Join(", ", VisitPurposes.All)}.");
        var people = request.Visitors.Where(x => !string.IsNullOrWhiteSpace(x.FullName) || x.VisitorId is not null).ToList();
        if (people.Count == 0) throw new BusinessRuleException("Add at least one visitor.");
        if (people.Count > MaxPeoplePerVisit) throw new BusinessRuleException($"A visit can have at most {MaxPeoplePerVisit} people; split a larger group.");

        var hostId = request.HostUserId ?? actor.UserId;
        var host = await dbContext.Users.AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == hostId && x.IsActive, cancellationToken)
            ?? throw new BusinessRuleException("Choose the host: the staff member the visitors are coming to see.");

        var now = DateTime.UtcNow;
        var from = request.WalkIn ? now : request.ExpectedFrom ?? now;
        var to = request.ExpectedTo ?? from.AddHours(4);
        if (to <= from) throw new BusinessRuleException("The visit must end after it starts.");
        if (to - from > TimeSpan.FromDays(31)) throw new BusinessRuleException("A visit can last at most a month; give a regular contractor a new visit each month.");

        var zoneIds = (request.ZoneIds ?? []).Distinct().ToList();
        var zones = await dbContext.AccessZones.AsNoTracking().Where(x => x.TenantId == tenantId && zoneIds.Contains(x.Id) && x.IsActive).ToListAsync(cancellationToken);
        if (zones.Count != zoneIds.Count || zones.Any(x => x.FactoryId != factory.Id))
            throw new BusinessRuleException("Choose zones of the factory being visited.");

        WorkOrder? job = null;
        if (request.WorkOrderId is { } jobId)
            job = await dbContext.WorkOrders.AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == jobId, cancellationToken)
                ?? throw new BusinessRuleException("That job was not found.");
        if (request.WorkPermitId is { } permitId)
        {
            var permit = await dbContext.WorkPermits.AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == permitId, cancellationToken)
                ?? throw new BusinessRuleException("That permit was not found.");
            if (job is not null && permit.WorkOrderId != job.Id) throw new BusinessRuleException($"Permit {permit.PermitNumber} is for another job.");
            // A permit names its job, so choosing the permit is enough.
            job ??= await dbContext.WorkOrders.AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == permit.WorkOrderId, cancellationToken);
        }

        var contractorId = request.ContractorId ?? job?.ContractorId;
        if (contractorId is { } cid && !await dbContext.Contractors.AnyAsync(x => x.TenantId == tenantId && x.Id == cid, cancellationToken))
            throw new BusinessRuleException("That contractor was not found.");

        var hostRaisedIt = hostId == actor.UserId;
        var settings = await settingsService.GetOrCreateAsync(tenantId, cancellationToken);
        var approved = request.WalkIn
            ? hostRaisedIt || !settings.WalkInsNeedHostApproval
            : hostRaisedIt || actor.CanManage;

        var visit = new Visit
        {
            TenantId = tenantId,
            VisitNumber = await NextVisitNumberAsync(dbContext, tenantId, cancellationToken),
            FactoryId = factory.Id,
            Purpose = purpose,
            HostUserId = host.Id,
            HostName = host.FullName,
            ExpectedFrom = from,
            ExpectedTo = to,
            Source = request.WalkIn ? VisitSources.WalkIn : VisitSources.PreRegistered,
            Status = approved ? VisitStatuses.Approved : VisitStatuses.PendingApproval,
            WorkOrderId = job?.Id,
            WorkPermitId = request.WorkPermitId,
            ContractorId = contractorId,
            VehicleRegistration = NormaliseRegistration(request.VehicleRegistration),
            Notes = Trim(request.Notes, 1000),
            CreatedByName = actor.Name,
        };
        if (approved)
        {
            visit.DecidedByUserId = actor.UserId;
            visit.DecidedByName = actor.Name;
            visit.DecidedAt = now;
        }

        dbContext.Visits.Add(visit);
        foreach (var zone in zones)
            dbContext.VisitZones.Add(new VisitZone { TenantId = tenantId, VisitId = visit.Id, AccessZoneId = zone.Id });

        var seen = new HashSet<Guid>();
        var n = 0;
        foreach (var person in people)
        {
            var visitor = await ResolveVisitorAsync(tenantId, person, contractorId, cancellationToken);
            if (!seen.Add(visitor.Id)) continue;
            n++;
            dbContext.VisitAttendees.Add(new VisitAttendee
            {
                TenantId = tenantId,
                VisitId = visit.Id,
                VisitorId = visitor.Id,
                BadgeNumber = $"{visit.VisitNumber}-{n}",
                BadgeToken = NewBadgeToken(),
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        if (!approved)
        {
            var names = string.Join(", ", people.Select(x => x.FullName.Trim()).Where(x => x.Length > 0));
            await NotifyAsync(new NotificationRequest(tenantId, ApprovalKey, [host.Id],
                request.WalkIn ? $"Visitor at the gate: {names}" : $"Visit to approve: {names}",
                $"{purpose} at {factory.Name}, {Local(from):dd MMM HH:mm}. Raised by {actor.Name}.",
                $"/visitors?visit={visit.Id}", nameof(Visit), visit.Id,
                new Dictionary<string, string?> { ["Visitors"] = names, ["Factory"] = factory.Name, ["Purpose"] = purpose, ["VisitNumber"] = visit.VisitNumber, ["RaisedBy"] = actor.Name },
                NotificationSeverities.Warning), cancellationToken);
        }

        return visit;
    }

    public async Task<Visit> DecideAsync(Guid tenantId, Guid visitId, bool approve, string? note, VisitActor actor, CancellationToken cancellationToken = default)
    {
        var visit = await LoadVisitAsync(tenantId, visitId, cancellationToken);
        if (visit.HostUserId != actor.UserId && !actor.CanManage) throw new ForbiddenException("Only the host or a visitor manager can decide this visit.");
        if (visit.Status != VisitStatuses.PendingApproval) throw new BusinessRuleException($"Visit {visit.VisitNumber} is not waiting for approval.");
        if (!approve && string.IsNullOrWhiteSpace(note)) throw new BusinessRuleException("Say why the visit is turned down; the gate will see it.");
        visit.Status = approve ? VisitStatuses.Approved : VisitStatuses.Rejected;
        visit.DecidedByUserId = actor.UserId;
        visit.DecidedByName = actor.Name;
        visit.DecidedAt = DateTime.UtcNow;
        visit.DecisionNote = Trim(note, 500);
        await dbContext.SaveChangesAsync(cancellationToken);
        return visit;
    }

    public async Task<Visit> CancelAsync(Guid tenantId, Guid visitId, string? reason, VisitActor actor, CancellationToken cancellationToken = default)
    {
        var visit = await LoadVisitAsync(tenantId, visitId, cancellationToken);
        if (visit.Status is not (VisitStatuses.PendingApproval or VisitStatuses.Approved))
            throw new BusinessRuleException(visit.Status == VisitStatuses.OnSite ? "Someone on this visit is still inside; check them out first." : $"Visit {visit.VisitNumber} is already {visit.Status.ToLowerInvariant()}.");
        visit.Status = VisitStatuses.Cancelled;
        visit.DecidedByUserId = actor.UserId;
        visit.DecidedByName = actor.Name;
        visit.DecidedAt = DateTime.UtcNow;
        visit.DecisionNote = Trim(reason, 500);
        await dbContext.SaveChangesAsync(cancellationToken);
        return visit;
    }

    public async Task<GateCheckResult> CheckAsync(Guid tenantId, Guid attendeeId, GateConfirmation confirmation, DateTime utcNow, CancellationToken cancellationToken = default)
    {
        var attendee = await LoadAttendeeAsync(tenantId, attendeeId, cancellationToken);
        return await EvaluateAsync(attendee, confirmation, utcNow, cancellationToken);
    }

    public async Task<VisitAttendee> CheckInAsync(Guid tenantId, Guid attendeeId, GateConfirmation confirmation, VisitActor actor, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var attendee = await LoadAttendeeAsync(tenantId, attendeeId, cancellationToken);
        var result = await EvaluateAsync(attendee, confirmation, now, cancellationToken);
        var visit = attendee.Visit!;
        var visitor = attendee.Visitor!;

        if (!result.CanEnter)
        {
            var hard = result.Issues.Where(x => !x.Overridable).ToList();
            if (hard.Count > 0) throw new BusinessRuleException(string.Join(" ", hard.Select(x => x.Message)));
            if (!actor.CanManage) throw new BusinessRuleException(string.Join(" ", result.Issues.Select(x => x.Message)) + " A supervisor can let them in with a reason.");
            if (string.IsNullOrWhiteSpace(confirmation.OverrideReason)) throw new BusinessRuleException("Give the reason for letting them in anyway.");
            attendee.OverrideReason = Trim($"{string.Join("; ", result.Issues.Select(x => x.Code))}: {confirmation.OverrideReason}", 500);
            attendee.OverriddenByName = actor.Name;
        }

        if (result.Requirements.NeedsInduction && !result.Requirements.HasCurrentInduction && confirmation.RecordInduction)
        {
            dbContext.TrainingRecords.Add(new TrainingRecord
            {
                TenantId = tenantId,
                PersonName = visitor.FullName,
                Company = visitor.Company,
                IsContractor = visitor.ContractorId is not null || visit.ContractorId is not null,
                Course = InductionCourse,
                Category = InductionCategory,
                CompletedOn = now.Date,
                ExpiresOn = now.Date.AddYears(1),
                Trainer = actor.Name,
                Notes = $"Given at the gate for visit {visit.VisitNumber}.",
            });
        }

        if (confirmation.EscortUserId is { } escortId)
        {
            var escort = await dbContext.Users.AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == escortId && x.IsActive, cancellationToken)
                ?? throw new BusinessRuleException("The escort must be an active staff member.");
            attendee.EscortUserId = escort.Id;
            attendee.EscortName = escort.FullName;
        }

        attendee.IdVerified = confirmation.IdVerified;
        attendee.PpeConfirmed = confirmation.PpeConfirmed || result.Requirements.Ppe.Count == 0;
        attendee.InductionConfirmed = !result.Requirements.NeedsInduction || result.Requirements.HasCurrentInduction || confirmation.RecordInduction;
        attendee.CheckedInAt = now;
        attendee.CheckedInByName = actor.Name;
        attendee.AccountedForAt = null;
        attendee.AccountedForByName = null;
        visitor.LastVisitAt = now;
        visit.Status = VisitStatuses.OnSite;
        if (visit.VehicleRegistration is { } registration
            && !await dbContext.VehicleMovements.AnyAsync(x => x.TenantId == tenantId && x.FactoryId == visit.FactoryId && x.Registration == registration && x.ExitedAt == null, cancellationToken))
        {
            // The vehicle came in with them: open its entry in the vehicle log.
            dbContext.VehicleMovements.Add(new VehicleMovement
            {
                TenantId = tenantId, FactoryId = visit.FactoryId, Registration = registration, VisitId = visit.Id,
                Purpose = visit.Purpose == VisitPurposes.ContractorWork ? VehiclePurposes.Contractor : visit.Purpose == VisitPurposes.Delivery ? VehiclePurposes.Delivery : VehiclePurposes.Visitor,
                DriverName = visitor.FullName, DriverPhone = visitor.Phone, EnteredAt = now, EnteredByName = actor.Name,
            });
        }
        await dbContext.SaveChangesAsync(cancellationToken);

        var factory = await dbContext.OrgNodes.AsNoTracking().Where(x => x.Id == visit.FactoryId).Select(x => x.Name).SingleAsync(cancellationToken);
        await NotifyAsync(new NotificationRequest(tenantId, ArrivedKey, [visit.HostUserId],
            $"{visitor.FullName} has arrived",
            $"{visitor.FullName}{(visitor.Company is null ? "" : $" ({visitor.Company})")} is at {factory} for your {visit.Purpose.ToLowerInvariant()} ({visit.VisitNumber}).",
            $"/visitors?visit={visit.Id}", nameof(Visit), visit.Id,
            new Dictionary<string, string?> { ["Visitor"] = visitor.FullName, ["Company"] = visitor.Company, ["Factory"] = factory, ["Purpose"] = visit.Purpose, ["VisitNumber"] = visit.VisitNumber }),
            cancellationToken);
        return attendee;
    }

    public async Task<VisitAttendee> CheckOutAsync(Guid tenantId, Guid attendeeId, VisitActor actor, CancellationToken cancellationToken = default)
    {
        var attendee = await LoadAttendeeAsync(tenantId, attendeeId, cancellationToken);
        if (attendee.CheckedInAt is null) throw new BusinessRuleException($"{attendee.Visitor!.FullName} has not come in.");
        if (attendee.CheckedOutAt is not null) throw new BusinessRuleException($"{attendee.Visitor!.FullName} already left at {Local(attendee.CheckedOutAt.Value):HH:mm}.");
        // A badge scanned out at the gate with no signal is synced later, dated when it was scanned.
        var now = OccurredAt.UtcNow;
        if (now < attendee.CheckedInAt) now = attendee.CheckedInAt.Value;
        attendee.CheckedOutAt = now;
        attendee.CheckedOutByName = actor.Name;
        attendee.Visitor!.LastVisitAt = now;

        var visit = attendee.Visit!;
        var others = await dbContext.VisitAttendees.Where(x => x.VisitId == visit.Id && x.Id != attendee.Id).ToListAsync(cancellationToken);
        var anyoneInside = others.Any(x => x.CheckedInAt != null && x.CheckedOutAt == null);
        var anyoneExpected = others.Any(x => x.CheckedInAt == null) && now <= visit.ExpectedTo;
        if (!anyoneInside)
        {
            visit.Status = anyoneExpected ? VisitStatuses.Approved : VisitStatuses.Completed;
            visit.CompletedAt = anyoneExpected ? null : now;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return attendee;
    }

    public async Task<VisitAttendee?> FindBadgeAsync(Guid tenantId, string token, CancellationToken cancellationToken = default)
    {
        var value = token.Trim();
        // A scanned badge carries "VISITOR:<token>"; a typed or linked one may be a whole link.
        if (value.StartsWith("VISITOR:", StringComparison.OrdinalIgnoreCase)) value = value["VISITOR:".Length..];
        var slash = value.LastIndexOf('/');
        if (slash >= 0) value = value[(slash + 1)..];
        if (value.Length is < 10 or > 64) return null;
        return await dbContext.VisitAttendees
            .Include(x => x.Visit).ThenInclude(x => x!.Zones).ThenInclude(x => x.AccessZone)
            .Include(x => x.Visit).ThenInclude(x => x!.Factory)
            .Include(x => x.Visitor)
            .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.BadgeToken == value, cancellationToken);
    }

    public async Task MarkAccountedAsync(Guid tenantId, Guid attendeeId, bool accounted, VisitActor actor, CancellationToken cancellationToken = default)
    {
        var attendee = await dbContext.VisitAttendees.SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == attendeeId, cancellationToken)
            ?? throw new NotFoundException("Visitor not found.");
        if (attendee.CheckedInAt is null || attendee.CheckedOutAt is not null) throw new BusinessRuleException("Only someone on site can be marked for the roll call.");
        attendee.AccountedForAt = accounted ? DateTime.UtcNow : null;
        attendee.AccountedForByName = accounted ? actor.Name : null;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> ResetRollCallAsync(Guid tenantId, IReadOnlyCollection<Guid> factoryIds, CancellationToken cancellationToken = default)
    {
        var marked = await dbContext.VisitAttendees
            .Where(x => x.TenantId == tenantId && x.AccountedForAt != null && x.CheckedOutAt == null && factoryIds.Contains(x.Visit!.FactoryId))
            .ToListAsync(cancellationToken);
        foreach (var attendee in marked)
        {
            attendee.AccountedForAt = null;
            attendee.AccountedForByName = null;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return marked.Count;
    }

    public async Task<Visitor> SetBlockedAsync(Guid tenantId, Guid visitorId, bool blocked, string? reason, VisitActor actor, CancellationToken cancellationToken = default)
    {
        var visitor = await dbContext.Visitors.SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == visitorId, cancellationToken)
            ?? throw new NotFoundException("Visitor not found.");
        if (blocked && string.IsNullOrWhiteSpace(reason)) throw new BusinessRuleException("Say why the visitor is blocked; the gate will see it.");
        visitor.IsBlocked = blocked;
        visitor.BlockedReason = blocked ? Trim(reason, 500) : null;
        visitor.BlockedByName = blocked ? actor.Name : null;
        await dbContext.SaveChangesAsync(cancellationToken);
        return visitor;
    }

    public async Task<int> AnonymiseAsync(Guid tenantId, DateTime utcNow, CancellationToken cancellationToken = default)
    {
        var settings = await settingsService.GetOrCreateAsync(tenantId, cancellationToken);
        var cutoff = utcNow.AddMonths(-Math.Max(1, settings.VisitorRetentionMonths));
        var due = await dbContext.Visitors.IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId && x.AnonymisedAt == null && !x.IsBlocked && (x.LastVisitAt ?? x.CreatedAt) < cutoff)
            .Where(x => !dbContext.VisitAttendees.IgnoreQueryFilters().Any(a => a.VisitorId == x.Id
                && (a.Visit!.Status == VisitStatuses.OnSite || ((a.Visit.Status == VisitStatuses.Approved || a.Visit.Status == VisitStatuses.PendingApproval) && a.Visit.ExpectedTo > utcNow))))
            .ToListAsync(cancellationToken);
        foreach (var visitor in due)
        {
            visitor.FullName = "Visitor (details removed)";
            visitor.Phone = null;
            visitor.Email = null;
            visitor.IdType = null;
            visitor.IdNumberMasked = null;
            visitor.IdNumberHash = null;
            visitor.AnonymisedAt = utcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return due.Count;
    }

    public async Task<int> SeedZonesAsync(Guid tenantId, Guid factoryId, CancellationToken cancellationToken = default)
    {
        if (!await dbContext.OrgNodes.AnyAsync(x => x.TenantId == tenantId && x.Id == factoryId && x.Level == OrgNodeLevels.Factory, cancellationToken))
            throw new BusinessRuleException("Choose a factory.");
        if (await dbContext.AccessZones.AnyAsync(x => x.TenantId == tenantId && x.FactoryId == factoryId, cancellationToken))
            throw new BusinessRuleException("This factory already has zones; add or edit them one by one.");
        var industry = await IndustryTemplates.ForFactoryAsync(dbContext, tenantId, factoryId, cancellationToken);
        var order = 0;
        foreach (var zone in industry.Zones)
        {
            dbContext.AccessZones.Add(new AccessZone
            {
                TenantId = tenantId, FactoryId = factoryId, Name = zone.Name, Level = zone.Level, RequiresEscort = zone.Escort,
                RequiresInduction = zone.Induction, RequiresPermit = zone.Permit, RequiredPpe = zone.Ppe, DisplayOrder = ++order,
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return industry.Zones.Count;
    }

    private async Task<GateCheckResult> EvaluateAsync(VisitAttendee attendee, GateConfirmation confirmation, DateTime utcNow, CancellationToken cancellationToken)
    {
        var visit = attendee.Visit!;
        var visitor = attendee.Visitor!;
        if (visit.Status is VisitStatuses.Rejected or VisitStatuses.Cancelled or VisitStatuses.Completed)
            throw new BusinessRuleException($"Visit {visit.VisitNumber} is {visit.Status.ToLowerInvariant()}" + (visit.DecisionNote is null ? "." : $": {visit.DecisionNote}"));
        if (attendee.CheckedOutAt is not null) throw new BusinessRuleException($"{visitor.FullName} already came and left on this visit; raise a new visit.");
        if (attendee.CheckedInAt is not null) throw new BusinessRuleException($"{visitor.FullName} is already on site.");

        var issues = new List<GateIssue>();
        if (visit.Status == VisitStatuses.PendingApproval)
            issues.Add(new GateIssue("approval", $"Waiting for {visit.HostName} to approve the visit.", false));
        if (utcNow < visit.ExpectedFrom - EarlyArrival || utcNow > visit.ExpectedTo)
            issues.Add(new GateIssue("schedule", $"The visit is expected {Local(visit.ExpectedFrom):dd MMM HH:mm} to {Local(visit.ExpectedTo):dd MMM HH:mm}.", true));
        if (visitor.IsBlocked)
            issues.Add(new GateIssue("blocked", $"{visitor.FullName} is blocked: {visitor.BlockedReason}", true));

        var contractorId = visit.ContractorId ?? visitor.ContractorId;
        if (contractorId is { } cid && await dbContext.Contractors.AsNoTracking().SingleOrDefaultAsync(x => x.Id == cid, cancellationToken) is { } contractor)
            issues.AddRange((await contractorService.WorkBlockersAsync(contractor, utcNow, cancellationToken)).Select(x => new GateIssue("contractor", x, false)));

        if (visit.WorkOrderId is { } jobId && await dbContext.WorkOrders.AsNoTracking().Where(x => x.Id == jobId).Select(x => new { x.WorkOrderNumber, x.Status }).SingleOrDefaultAsync(cancellationToken) is { } job
            && FinishedJob.Contains(job.Status))
            issues.Add(new GateIssue("work_order", $"Job {job.WorkOrderNumber} is {job.Status.ToLowerInvariant()}.", true));

        var zones = visit.Zones.Select(x => x.AccessZone!).Where(x => x.IsActive).ToList();
        var inductionZones = zones.Where(x => x.RequiresInduction).Select(x => x.Name).ToList();
        var escortZones = zones.Where(x => x.RequiresEscort).Select(x => x.Name).ToList();
        var permitZones = zones.Where(x => x.RequiresPermit).Select(x => x.Name).ToList();
        var ppe = zones.SelectMany(x => (x.RequiredPpe ?? "").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        var hasInduction = inductionZones.Count > 0 && await HasCurrentInductionAsync(visit.TenantId, visitor, utcNow, cancellationToken);
        if (inductionZones.Count > 0 && !hasInduction && !confirmation.RecordInduction)
            issues.Add(new GateIssue("induction", $"{visitor.FullName} has no current safety induction, needed for {string.Join(", ", inductionZones)}. Give it at the gate and tick it.", false));
        if (ppe.Count > 0 && !confirmation.PpeConfirmed)
            issues.Add(new GateIssue("ppe", $"Confirm {visitor.FullName} has: {string.Join(", ", ppe)}.", true));
        if (escortZones.Count > 0 && confirmation.EscortUserId is null)
            issues.Add(new GateIssue("escort", $"{string.Join(", ", escortZones)} {(escortZones.Count == 1 ? "needs" : "need")} an escort; name who will escort {visitor.FullName}.", false));
        if (permitZones.Count > 0)
        {
            var permit = visit.WorkPermitId is { } pid ? await dbContext.WorkPermits.AsNoTracking().SingleOrDefaultAsync(x => x.Id == pid, cancellationToken) : null;
            if (permit is null)
                issues.Add(new GateIssue("permit", $"{string.Join(", ", permitZones)} {(permitZones.Count == 1 ? "needs" : "need")} a permit to work on the visit.", false));
            else if (permit.Status != PermitStatuses.Approved)
                issues.Add(new GateIssue("permit", $"Permit {permit.PermitNumber} is {permit.Status.ToLowerInvariant()}, not approved.", false));
            else if (utcNow < permit.ValidFrom || utcNow > permit.ValidTo)
                issues.Add(new GateIssue("permit", $"Permit {permit.PermitNumber} is valid {Local(permit.ValidFrom):dd MMM HH:mm} to {Local(permit.ValidTo):dd MMM HH:mm}, not now.", false));
        }

        return new GateCheckResult(new GateRequirements(inductionZones.Count > 0, hasInduction, ppe, escortZones.Count > 0, permitZones.Count > 0), issues);
    }

    /// <summary>A current induction training record in the person's name (and company, when both have one).</summary>
    private async Task<bool> HasCurrentInductionAsync(Guid tenantId, Visitor visitor, DateTime utcNow, CancellationToken cancellationToken)
    {
        var name = visitor.FullName.Trim().ToLower();
        var records = await dbContext.TrainingRecords.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.Category == InductionCategory && x.PersonName.ToLower() == name && (x.ExpiresOn == null || x.ExpiresOn >= utcNow.Date))
            .Select(x => x.Company)
            .ToListAsync(cancellationToken);
        return records.Any(company => company is null || visitor.Company is null || string.Equals(company.Trim(), visitor.Company.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public Task<Visitor> FindOrAddVisitorAsync(Guid tenantId, VisitorDetails person, Guid? contractorId, CancellationToken cancellationToken = default) =>
        ResolveVisitorAsync(tenantId, person, contractorId, cancellationToken);

    /// <summary>The next visit number. Counted across every visit of the tenant, whatever raised it.</summary>
    internal static async Task<string> NextVisitNumberAsync(AppDbContext dbContext, Guid tenantId, CancellationToken cancellationToken)
    {
        var count = await dbContext.Visits.CountAsync(x => x.TenantId == tenantId, cancellationToken)
            + dbContext.ChangeTracker.Entries<Visit>().Count(x => x.State == EntityState.Added && x.Entity.TenantId == tenantId);
        return $"VIS-{count + 1:000000}";
    }

    private async Task<Visitor> ResolveVisitorAsync(Guid tenantId, VisitorDetails person, Guid? contractorId, CancellationToken cancellationToken)
    {
        Visitor? visitor = null;
        if (person.VisitorId is { } id)
            visitor = await dbContext.Visitors.SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id && x.AnonymisedAt == null, cancellationToken)
                ?? throw new BusinessRuleException("That visitor was not found.");

        var name = person.FullName?.Trim() ?? string.Empty;
        if (visitor is null && name.Length < 2) throw new BusinessRuleException("Enter each visitor's full name.");
        var idType = string.IsNullOrWhiteSpace(person.IdType) ? null : Trim(person.IdType, 30);
        var idHash = HashId(tenantId, person.IdNumber);
        var phone = NormalisePhone(person.Phone);

        if (visitor is null && idHash is not null)
            visitor = await dbContext.Visitors.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.IdNumberHash == idHash, cancellationToken);
        if (visitor is null && phone is not null)
        {
            var lower = name.ToLower();
            visitor = await dbContext.Visitors.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Phone == phone && x.FullName.ToLower() == lower, cancellationToken);
        }

        if (visitor is null)
        {
            visitor = new Visitor { TenantId = tenantId, FullName = Trim(name, 200)! };
            dbContext.Visitors.Add(visitor);
        }
        else if (name.Length >= 2)
        {
            visitor.FullName = Trim(name, 200)!;
        }

        visitor.Phone = phone ?? visitor.Phone;
        visitor.Email = Trim(person.Email, 200) ?? visitor.Email;
        visitor.Company = Trim(person.Company, 200) ?? visitor.Company;
        visitor.ContractorId = person.ContractorId ?? contractorId ?? visitor.ContractorId;
        if (idHash is not null)
        {
            visitor.IdType = idType ?? visitor.IdType ?? "National ID";
            visitor.IdNumberHash = idHash;
            visitor.IdNumberMasked = MaskId(person.IdNumber);
        }

        return visitor;
    }

    private async Task<Visit> LoadVisitAsync(Guid tenantId, Guid visitId, CancellationToken cancellationToken) =>
        await dbContext.Visits.SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == visitId, cancellationToken)
        ?? throw new NotFoundException("Visit not found.");

    private async Task<VisitAttendee> LoadAttendeeAsync(Guid tenantId, Guid attendeeId, CancellationToken cancellationToken) =>
        await dbContext.VisitAttendees
            .Include(x => x.Visit).ThenInclude(x => x!.Zones).ThenInclude(x => x.AccessZone)
            .Include(x => x.Visitor)
            .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == attendeeId, cancellationToken)
        ?? throw new NotFoundException("Visitor not found on any visit.");

    private async Task NotifyAsync(NotificationRequest request, CancellationToken cancellationToken)
    {
        if (notificationDispatcher is null) return;
        try
        {
            await notificationDispatcher.NotifyAsync(request, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger?.LogWarning(ex, "Visitor notification {Key} failed.", request.Key);
        }
    }

    /// <summary>Visit times are shown in East Africa Time (UTC+3), the factories' zone.</summary>
    internal static DateTime Local(DateTime utc) => utc.AddHours(3);

    internal static string NewBadgeToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(16)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    internal static string? NormaliseId(string? idNumber)
    {
        if (string.IsNullOrWhiteSpace(idNumber)) return null;
        var value = new string(idNumber.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        return value.Length >= 4 ? value : throw new BusinessRuleException("An ID number needs at least four letters or digits.");
    }

    internal static string? HashId(Guid tenantId, string? idNumber) =>
        NormaliseId(idNumber) is { } id ? Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{tenantId:N}:{id}"))).ToLowerInvariant() : null;

    internal static string? MaskId(string? idNumber) => NormaliseId(idNumber) is { } id ? $"••••{id[^4..]}" : null;

    internal static string? NormalisePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return null;
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.Length < 9) throw new BusinessRuleException("Enter a full phone number.");
        // Kenyan numbers: 0712 345 678 and +254 712 345 678 are the same person.
        if (digits.StartsWith('0') && digits.Length == 10) digits = "254" + digits[1..];
        return "+" + digits;
    }

    internal static string? NormaliseRegistration(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var compact = new string(value.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        return compact.Length is >= 3 and <= 20 ? compact : throw new BusinessRuleException("Enter the vehicle's registration, e.g. KCA 123A.");
    }

    internal static string? Trim(string? value, int max) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().Length > max ? value.Trim()[..max] : value.Trim();
}

/// <summary>How visitor identity details are stored: ID numbers only as a masked suffix and a per-tenant hash.</summary>
public static class VisitorIdentity
{
    public static string? Hash(Guid tenantId, string? idNumber) => VisitorService.HashId(tenantId, idNumber);

    public static string? Mask(string? idNumber) => VisitorService.MaskId(idNumber);

    public static string? NormalisePhone(string? phone) => VisitorService.NormalisePhone(phone);
}

/// <summary>Removes visitors' personal details once a day after each tenant's retention period.</summary>
public sealed class VisitorRetentionWorker(IServiceScopeFactory serviceScopeFactory, ILogger<VisitorRetentionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(24));
        do
        {
            try
            {
                using var scope = serviceScopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var service = scope.ServiceProvider.GetRequiredService<IVisitorService>();
                var tenants = await db.Visitors.IgnoreQueryFilters().Where(x => x.AnonymisedAt == null).Select(x => x.TenantId).Distinct().ToListAsync(stoppingToken);
                foreach (var tenantId in tenants)
                {
                    var removed = await service.AnonymiseAsync(tenantId, DateTime.UtcNow, stoppingToken);
                    if (removed > 0) logger.LogInformation("Removed the personal details of {Count} visitors of tenant {TenantId}.", removed, tenantId);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Visitor retention cycle failed.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
