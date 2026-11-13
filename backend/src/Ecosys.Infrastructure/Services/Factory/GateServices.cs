using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Errors;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Infrastructure.Services.Factory;

public sealed record NewVehicleEntry(
    Guid FactoryId,
    string Registration,
    string Purpose,
    string? DriverName = null,
    string? DriverPhone = null,
    string? Description = null,
    Guid? VisitId = null,
    string? Notes = null);

public interface IVehicleLogService
{
    /// <summary>Records a vehicle coming in. A vehicle already inside the same factory cannot come in again until it goes out.</summary>
    Task<VehicleMovement> EnterAsync(Guid tenantId, NewVehicleEntry entry, VisitActor actor, CancellationToken cancellationToken = default);

    /// <summary>Records a vehicle going out, dated when the gate saw it (<see cref="OccurredAt"/>).</summary>
    Task<VehicleMovement> ExitAsync(Guid tenantId, Guid movementId, string? notes, VisitActor actor, CancellationToken cancellationToken = default);
}

internal sealed class VehicleLogService(AppDbContext dbContext) : IVehicleLogService
{
    public async Task<VehicleMovement> EnterAsync(Guid tenantId, NewVehicleEntry entry, VisitActor actor, CancellationToken cancellationToken = default)
    {
        var registration = VisitorService.NormaliseRegistration(entry.Registration) ?? throw new BusinessRuleException("Enter the vehicle's registration, e.g. KCA 123A.");
        var purpose = VehiclePurposes.Find(entry.Purpose) ?? throw new BusinessRuleException($"Purpose must be one of: {string.Join(", ", VehiclePurposes.All)}.");
        if (!await dbContext.OrgNodes.AnyAsync(x => x.TenantId == tenantId && x.Id == entry.FactoryId && x.Level == OrgNodeLevels.Factory, cancellationToken))
            throw new BusinessRuleException("Choose the factory gate.");
        var inside = await dbContext.VehicleMovements.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.FactoryId == entry.FactoryId && x.Registration == registration && x.ExitedAt == null)
            .Select(x => (DateTime?)x.EnteredAt).FirstOrDefaultAsync(cancellationToken);
        if (inside is { } since) throw new ConflictException($"{registration} is already inside since {VisitorService.Local(since):dd MMM HH:mm}; record it going out first.");
        if (entry.VisitId is { } visitId && !await dbContext.Visits.AnyAsync(x => x.TenantId == tenantId && x.Id == visitId && x.FactoryId == entry.FactoryId, cancellationToken))
            throw new BusinessRuleException("That visit is not at this factory.");

        var movement = new VehicleMovement
        {
            TenantId = tenantId,
            FactoryId = entry.FactoryId,
            Registration = registration,
            Purpose = purpose,
            DriverName = VisitorService.Trim(entry.DriverName, 200),
            DriverPhone = VisitorIdentity.NormalisePhone(entry.DriverPhone),
            Description = VisitorService.Trim(entry.Description, 200),
            VisitId = entry.VisitId,
            Notes = VisitorService.Trim(entry.Notes, 1000),
            EnteredAt = OccurredAt.UtcNow,
            EnteredByName = actor.Name,
        };
        dbContext.VehicleMovements.Add(movement);
        await dbContext.SaveChangesAsync(cancellationToken);
        return movement;
    }

    public async Task<VehicleMovement> ExitAsync(Guid tenantId, Guid movementId, string? notes, VisitActor actor, CancellationToken cancellationToken = default)
    {
        var movement = await dbContext.VehicleMovements.SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == movementId, cancellationToken)
            ?? throw new NotFoundException("Vehicle entry not found.");
        if (movement.ExitedAt is not null) throw new BusinessRuleException($"{movement.Registration} already went out at {VisitorService.Local(movement.ExitedAt.Value):HH:mm}.");
        var at = OccurredAt.UtcNow;
        movement.ExitedAt = at < movement.EnteredAt ? movement.EnteredAt : at;
        movement.ExitedByName = actor.Name;
        movement.ExitNotes = VisitorService.Trim(notes, 1000);
        await dbContext.SaveChangesAsync(cancellationToken);
        return movement;
    }
}

public sealed record NewVisitorPass(
    Guid FactoryId,
    VisitorDetails Person,
    Guid? HostUserId,
    DateTime ValidFrom,
    DateTime ValidTo,
    IReadOnlyList<int>? Days = null,
    TimeOnly? DailyFrom = null,
    TimeOnly? DailyTo = null,
    IReadOnlyList<Guid>? ZoneIds = null,
    Guid? ContractorId = null,
    string? Purpose = null,
    string? VehicleRegistration = null,
    string? Notes = null);

/// <summary>Whether a pass can be used at a moment, and the day's visit window if so.</summary>
public sealed record PassWindow(bool Usable, string? Reason, DateTime From, DateTime To);

public interface IVisitorPassService
{
    Task<VisitorPass> CreateAsync(Guid tenantId, NewVisitorPass request, VisitActor actor, CancellationToken cancellationToken = default);

    Task<VisitorPass> RevokeAsync(Guid tenantId, Guid passId, string? reason, VisitActor actor, CancellationToken cancellationToken = default);

    /// <summary>
    /// The person's place on today's visit for this pass: the open visit raised from it today, or a new one,
    /// approved, with the pass's zones, contractor and hours. Fails when the pass is revoked, expired or not
    /// good for today.
    /// </summary>
    Task<VisitAttendee> TodayAsync(Guid tenantId, Guid passId, VisitActor actor, CancellationToken cancellationToken = default);

    Task<VisitorPass?> FindByTokenAsync(Guid tenantId, string token, CancellationToken cancellationToken = default);
}

internal sealed class VisitorPassService(AppDbContext dbContext, IVisitorService visitorService) : IVisitorPassService
{
    public const int MaxDays = 366;

    public async Task<VisitorPass> CreateAsync(Guid tenantId, NewVisitorPass request, VisitActor actor, CancellationToken cancellationToken = default)
    {
        if (!actor.CanManage) throw new ForbiddenException("Only visitor managers can issue standing passes.");
        if (!await dbContext.OrgNodes.AnyAsync(x => x.TenantId == tenantId && x.Id == request.FactoryId && x.Level == OrgNodeLevels.Factory && x.IsActive, cancellationToken))
            throw new BusinessRuleException("Choose the factory the pass is for.");
        var from = request.ValidFrom.Date;
        var to = request.ValidTo.Date;
        if (to < from) throw new BusinessRuleException("The pass must end on or after its first day.");
        if ((to - from).TotalDays >= MaxDays) throw new BusinessRuleException("A pass can last at most a year; issue a new one to renew it.");
        var days = (request.Days ?? [1, 2, 3, 4, 5]).Distinct().OrderBy(x => x).ToList();
        if (days.Count == 0 || days.Any(x => x is < 1 or > 7)) throw new BusinessRuleException("Choose the days of the week the pass is good for.");
        var dailyFrom = request.DailyFrom ?? new TimeOnly(7, 0);
        var dailyTo = request.DailyTo ?? new TimeOnly(17, 0);
        if (dailyTo <= dailyFrom) throw new BusinessRuleException("The daily hours must end after they start (night work needs a visit per night).");
        var purpose = request.Purpose is null ? VisitPurposes.ContractorWork : VisitPurposes.Find(request.Purpose) ?? throw new BusinessRuleException($"Purpose must be one of: {string.Join(", ", VisitPurposes.All)}.");

        var hostId = request.HostUserId ?? actor.UserId;
        var host = await dbContext.Users.AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == hostId && x.IsActive, cancellationToken)
            ?? throw new BusinessRuleException("Choose the host responsible for the pass holder.");
        var zoneIds = (request.ZoneIds ?? []).Distinct().ToList();
        var zones = await dbContext.AccessZones.AsNoTracking().Where(x => x.TenantId == tenantId && zoneIds.Contains(x.Id) && x.IsActive).ToListAsync(cancellationToken);
        if (zones.Count != zoneIds.Count || zones.Any(x => x.FactoryId != request.FactoryId)) throw new BusinessRuleException("Choose zones of the pass's factory.");
        if (request.ContractorId is { } cid && !await dbContext.Contractors.AnyAsync(x => x.TenantId == tenantId && x.Id == cid, cancellationToken))
            throw new BusinessRuleException("That contractor was not found.");

        var visitor = await visitorService.FindOrAddVisitorAsync(tenantId, request.Person, request.ContractorId, cancellationToken);
        if (visitor.IsBlocked) throw new BusinessRuleException($"{visitor.FullName} is blocked: {visitor.BlockedReason}");
        if (dbContext.Entry(visitor).State != EntityState.Added
            && await dbContext.VisitorPasses.AnyAsync(x => x.TenantId == tenantId && x.VisitorId == visitor.Id && x.FactoryId == request.FactoryId && x.Status == VisitorPassStatuses.Active && x.ValidTo >= from, cancellationToken))
            throw new ConflictException($"{visitor.FullName} already has a standing pass for this factory; revoke it first.");

        var count = await dbContext.VisitorPasses.CountAsync(x => x.TenantId == tenantId, cancellationToken);
        var pass = new VisitorPass
        {
            TenantId = tenantId,
            PassNumber = $"PASS-{count + 1:00000}",
            VisitorId = visitor.Id,
            FactoryId = request.FactoryId,
            HostUserId = host.Id,
            HostName = host.FullName,
            ContractorId = request.ContractorId ?? visitor.ContractorId,
            Purpose = purpose,
            ValidFrom = DateTime.SpecifyKind(from, DateTimeKind.Utc),
            ValidTo = DateTime.SpecifyKind(to, DateTimeKind.Utc),
            Days = string.Join(",", days),
            DailyFrom = dailyFrom,
            DailyTo = dailyTo,
            BadgeToken = VisitorService.NewBadgeToken(),
            VehicleRegistration = VisitorService.NormaliseRegistration(request.VehicleRegistration),
            Notes = VisitorService.Trim(request.Notes, 1000),
            IssuedByName = actor.Name,
        };
        dbContext.VisitorPasses.Add(pass);
        foreach (var zone in zones)
            dbContext.VisitorPassZones.Add(new VisitorPassZone { TenantId = tenantId, PassId = pass.Id, AccessZoneId = zone.Id });
        await dbContext.SaveChangesAsync(cancellationToken);
        return pass;
    }

    public async Task<VisitorPass> RevokeAsync(Guid tenantId, Guid passId, string? reason, VisitActor actor, CancellationToken cancellationToken = default)
    {
        if (!actor.CanManage) throw new ForbiddenException("Only visitor managers can revoke standing passes.");
        var pass = await dbContext.VisitorPasses.SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == passId, cancellationToken)
            ?? throw new NotFoundException("Pass not found.");
        if (pass.Status == VisitorPassStatuses.Revoked) throw new BusinessRuleException($"{pass.PassNumber} is already revoked.");
        if (string.IsNullOrWhiteSpace(reason)) throw new BusinessRuleException("Say why the pass is revoked; the gate will see it.");
        pass.Status = VisitorPassStatuses.Revoked;
        pass.RevokedAt = DateTime.UtcNow;
        pass.RevokedByName = actor.Name;
        pass.RevokedReason = VisitorService.Trim(reason, 500);
        await dbContext.SaveChangesAsync(cancellationToken);
        return pass;
    }

    public async Task<VisitAttendee> TodayAsync(Guid tenantId, Guid passId, VisitActor actor, CancellationToken cancellationToken = default)
    {
        var pass = await dbContext.VisitorPasses.Include(x => x.Zones).Include(x => x.Visitor)
            .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == passId, cancellationToken)
            ?? throw new NotFoundException("Pass not found.");
        var now = DateTime.UtcNow;
        var window = Window(pass, now);
        if (!window.Usable) throw new BusinessRuleException($"{pass.PassNumber} ({pass.Visitor!.FullName}): {window.Reason}");

        // Today's open visit from this pass, if the holder already came today and has not finished.
        var existing = await dbContext.VisitAttendees.Include(x => x.Visit).Include(x => x.Visitor)
            .Where(x => x.TenantId == tenantId && x.Visit!.PassId == pass.Id && x.Visit.ExpectedFrom == window.From
                && (x.Visit.Status == VisitStatuses.Approved || x.Visit.Status == VisitStatuses.OnSite) && x.CheckedOutAt == null)
            .FirstOrDefaultAsync(cancellationToken);
        if (existing is not null) return existing;

        var visit = new Visit
        {
            TenantId = tenantId,
            VisitNumber = await VisitorService.NextVisitNumberAsync(dbContext, tenantId, cancellationToken),
            FactoryId = pass.FactoryId,
            Purpose = pass.Purpose,
            HostUserId = pass.HostUserId,
            HostName = pass.HostName,
            ExpectedFrom = window.From,
            ExpectedTo = window.To,
            Status = VisitStatuses.Approved,
            Source = VisitSources.Pass,
            PassId = pass.Id,
            ContractorId = pass.ContractorId,
            VehicleRegistration = pass.VehicleRegistration,
            Notes = $"Standing pass {pass.PassNumber}.",
            CreatedByName = actor.Name,
            DecidedByName = pass.IssuedByName,
            DecidedAt = now,
        };
        dbContext.Visits.Add(visit);
        foreach (var zone in pass.Zones)
            dbContext.VisitZones.Add(new VisitZone { TenantId = tenantId, VisitId = visit.Id, AccessZoneId = zone.AccessZoneId });
        var attendee = new VisitAttendee
        {
            TenantId = tenantId,
            VisitId = visit.Id,
            VisitorId = pass.VisitorId,
            BadgeNumber = $"{visit.VisitNumber}-1",
            BadgeToken = VisitorService.NewBadgeToken(),
            Visit = visit,
            Visitor = pass.Visitor,
        };
        dbContext.VisitAttendees.Add(attendee);
        await dbContext.SaveChangesAsync(cancellationToken);
        return attendee;
    }

    public async Task<VisitorPass?> FindByTokenAsync(Guid tenantId, string token, CancellationToken cancellationToken = default)
    {
        var value = token.Trim();
        if (value.StartsWith("VISITOR:", StringComparison.OrdinalIgnoreCase)) value = value["VISITOR:".Length..];
        var slash = value.LastIndexOf('/');
        if (slash >= 0) value = value[(slash + 1)..];
        if (value.Length is < 10 or > 64) return null;
        return await dbContext.VisitorPasses.SingleOrDefaultAsync(x => x.TenantId == tenantId && x.BadgeToken == value, cancellationToken);
    }

    /// <summary>Whether the pass is good at <paramref name="utcNow"/>, and today's window in UTC (factory time is UTC+3).</summary>
    internal static PassWindow Window(VisitorPass pass, DateTime utcNow)
    {
        var local = VisitorService.Local(utcNow);
        var today = local.Date;
        var from = DateTime.SpecifyKind(today.Add(pass.DailyFrom.ToTimeSpan()).AddHours(-3), DateTimeKind.Utc);
        var to = DateTime.SpecifyKind(today.Add(pass.DailyTo.ToTimeSpan()).AddHours(-3), DateTimeKind.Utc);
        var isoDay = local.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)local.DayOfWeek;
        string? reason = null;
        if (pass.Status == VisitorPassStatuses.Revoked) reason = $"revoked by {pass.RevokedByName}: {pass.RevokedReason}";
        else if (today < pass.ValidFrom.Date) reason = $"starts on {pass.ValidFrom:dd MMM yyyy}";
        else if (today > pass.ValidTo.Date) reason = $"expired on {pass.ValidTo:dd MMM yyyy}";
        else if (!pass.Days.Split(',').Contains(isoDay.ToString())) reason = $"not good on {local:dddd}s";
        else if (utcNow < from.AddHours(-1) || utcNow > to) reason = $"good {pass.DailyFrom:HH\\:mm}–{pass.DailyTo:HH\\:mm} only";
        return new PassWindow(reason is null, reason, from, to);
    }
}

/// <summary>When a standing pass can be used.</summary>
public static class VisitorPassRules
{
    public static PassWindow Window(VisitorPass pass, DateTime utcNow) => VisitorPassService.Window(pass, utcNow);
}

/// <summary>Small text helpers shared by the gate and patrol screens.</summary>
public static class GateText
{
    /// <summary>Trimmed, cut to <paramref name="max"/>, null when blank.</summary>
    public static string? Trim(string? value, int max) => VisitorService.Trim(value, max);

    /// <summary>A random, URL-safe token for a QR label or badge.</summary>
    public static string NewToken() => VisitorService.NewBadgeToken();
}
