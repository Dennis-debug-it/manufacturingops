using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Infrastructure.Services.Factory;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Api.Controllers;

/// <summary>
/// The gate's other registers: the vehicle in/out log and standing passes for people who come
/// regularly. Part of Visitor Management; gate staff only, and only visitor managers issue passes.
/// </summary>
[ApiController]
[Authorize]
[Route("api/visitors")]
public sealed class VisitorGateController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IOrgScopeService orgScopeService,
    IVehicleLogService vehicleLog,
    IVisitorPassService passService,
    IAuditLogService auditLogService,
    IVisitorBadgePdfRenderer? badgeRenderer = null) : TenantAwareControllerBase(tenantContext)
{
    private bool CanManage => IsAdmin || HasPermission(PermissionNames.VisitorsManage);
    private bool CanGate => CanManage || HasPermission(PermissionNames.VisitorsGate);
    private VisitActor Actor => new(UserId, UserDisplayName, CanManage);

    /// <summary>The vehicle log: vehicles inside now, or every movement in a period, searchable by registration or driver.</summary>
    [HttpGet("vehicles")]
    public async Task<ActionResult<IReadOnlyList<VehicleMovementRow>>> Vehicles(
        [FromQuery] Guid? factoryId, [FromQuery] bool insideOnly, [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] string? q, CancellationToken cancellationToken)
    {
        EnsureGate();
        var nodes = await orgScopeService.VisibleNodesAsync(TenantId, UserId, factoryId, cancellationToken);
        var query = dbContext.VehicleMovements.AsNoTracking().Where(x => x.TenantId == TenantId && (nodes == null || nodes.Contains(x.FactoryId)));
        if (insideOnly)
        {
            query = query.Where(x => x.ExitedAt == null);
        }
        else
        {
            var end = to ?? DateTime.UtcNow.AddDays(1);
            var start = from ?? end.AddDays(-30);
            query = query.Where(x => x.EnteredAt >= start && x.EnteredAt < end);
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLower();
            var compact = new string(term.Where(char.IsLetterOrDigit).ToArray());
            query = query.Where(x => (compact.Length > 0 && x.Registration.ToLower().Contains(compact))
                || (x.DriverName != null && x.DriverName.ToLower().Contains(term))
                || (x.Description != null && x.Description.ToLower().Contains(term)));
        }

        var now = DateTime.UtcNow;
        var rows = await query.OrderByDescending(x => x.EnteredAt).Take(300)
            .Select(x => new
            {
                x.Id, x.FactoryId, Factory = x.Factory!.Name, x.Registration, x.Purpose, x.Description, x.DriverName, x.DriverPhone, x.VisitId,
                VisitNumber = x.Visit != null ? x.Visit.VisitNumber : null, VisitStatus = x.Visit != null ? x.Visit.Status : null,
                x.EnteredAt, x.EnteredByName, x.ExitedAt, x.ExitedByName, x.Notes, x.ExitNotes,
            })
            .ToListAsync(cancellationToken);
        return Ok(rows.Select(x => new VehicleMovementRow(
            x.Id, x.FactoryId, x.Factory, x.Registration, x.Purpose, x.Description, x.DriverName, x.DriverPhone, x.VisitId, x.VisitNumber,
            x.EnteredAt, x.EnteredByName, x.ExitedAt, x.ExitedByName, x.Notes, x.ExitNotes,
            (int)Math.Round(((x.ExitedAt ?? now) - x.EnteredAt).TotalMinutes),
            // Still inside although the visit it came with is over: the gate should look for it.
            x.ExitedAt == null && x.VisitStatus is VisitStatuses.Completed or VisitStatuses.Cancelled)).ToList());
    }

    [HttpPost("vehicles")]
    public async Task<ActionResult<VehicleMovementRow>> Enter([FromBody] VehicleEntryRequest request, CancellationToken cancellationToken)
    {
        EnsureGate();
        await orgScopeService.EnsureCanAccessAsync(TenantId, UserId, request.FactoryId, cancellationToken);
        var movement = await vehicleLog.EnterAsync(TenantId, new NewVehicleEntry(request.FactoryId, request.Registration ?? "", request.Purpose ?? VehiclePurposes.Visitor,
            request.DriverName, request.DriverPhone, request.Description, request.VisitId, request.Notes), Actor, cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "visitors.vehicle.entered", nameof(VehicleMovement), movement.Id.ToString(),
            $"{movement.Registration} came in ({movement.Purpose}).", cancellationToken);
        return Ok(await RowAsync(movement.Id, cancellationToken));
    }

    [HttpPost("vehicles/{id:guid}/exit")]
    public async Task<ActionResult<VehicleMovementRow>> Exit(Guid id, [FromBody] VehicleExitRequest? request, CancellationToken cancellationToken)
    {
        EnsureGate();
        var factoryId = await dbContext.VehicleMovements.AsNoTracking().Where(x => x.TenantId == TenantId && x.Id == id).Select(x => (Guid?)x.FactoryId).SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Vehicle entry not found.");
        await orgScopeService.EnsureCanAccessAsync(TenantId, UserId, factoryId, cancellationToken);
        var movement = await vehicleLog.ExitAsync(TenantId, id, request?.Notes, Actor, cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "visitors.vehicle.exited", nameof(VehicleMovement), movement.Id.ToString(),
            $"{movement.Registration} went out.", cancellationToken);
        return Ok(await RowAsync(movement.Id, cancellationToken));
    }

    /// <summary>Standing passes: active ones by default, with whether each is good right now.</summary>
    [HttpGet("passes")]
    public async Task<ActionResult<IReadOnlyList<VisitorPassRow>>> Passes([FromQuery] Guid? factoryId, [FromQuery] bool includeEnded, [FromQuery] string? q, CancellationToken cancellationToken)
    {
        EnsureGate();
        var nodes = await orgScopeService.VisibleNodesAsync(TenantId, UserId, factoryId, cancellationToken);
        var today = DateTime.UtcNow.AddHours(3).Date;
        var query = dbContext.VisitorPasses.AsNoTracking().Include(x => x.Visitor).Include(x => x.Factory).Include(x => x.Contractor).Include(x => x.Zones).ThenInclude(x => x.AccessZone)
            .Where(x => x.TenantId == TenantId && (nodes == null || nodes.Contains(x.FactoryId)));
        if (!includeEnded) query = query.Where(x => x.Status == VisitorPassStatuses.Active && x.ValidTo >= today);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLower();
            query = query.Where(x => x.PassNumber.ToLower().Contains(term) || x.Visitor!.FullName.ToLower().Contains(term)
                || (x.Visitor.Company != null && x.Visitor.Company.ToLower().Contains(term)) || (x.Contractor != null && x.Contractor.Name.ToLower().Contains(term)));
        }

        var passes = await query.OrderBy(x => x.Visitor!.FullName).Take(300).ToListAsync(cancellationToken);
        var now = DateTime.UtcNow;
        return Ok(passes.Select(x => Map(x, now)).ToList());
    }

    [HttpPost("passes")]
    public async Task<ActionResult<VisitorPassRow>> CreatePass([FromBody] CreateVisitorPassRequest request, CancellationToken cancellationToken)
    {
        EnsureGate();
        await orgScopeService.EnsureCanAccessAsync(TenantId, UserId, request.FactoryId, cancellationToken);
        var person = request.Person ?? throw new BusinessRuleException("Who is the pass for?");
        var pass = await passService.CreateAsync(TenantId, new NewVisitorPass(
            request.FactoryId,
            new VisitorDetails(person.FullName ?? "", person.Phone, person.Email, person.Company, person.IdType, person.IdNumber, person.ContractorId, person.VisitorId),
            request.HostUserId, request.ValidFrom, request.ValidTo, request.Days, ParseTime(request.DailyFrom), ParseTime(request.DailyTo), request.ZoneIds,
            request.ContractorId, request.Purpose, request.VehicleRegistration, request.Notes), Actor, cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "visitors.pass.issued", nameof(VisitorPass), pass.Id.ToString(),
            $"Standing pass {pass.PassNumber} issued, {pass.ValidFrom:dd MMM yyyy} to {pass.ValidTo:dd MMM yyyy}.", cancellationToken);
        return Ok(await PassRowAsync(pass.Id, cancellationToken));
    }

    [HttpPost("passes/{id:guid}/revoke")]
    public async Task<ActionResult<VisitorPassRow>> RevokePass(Guid id, [FromBody] RevokeVisitorPassRequest request, CancellationToken cancellationToken)
    {
        EnsureGate();
        var pass = await passService.RevokeAsync(TenantId, id, request.Reason, Actor, cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "visitors.pass.revoked", nameof(VisitorPass), pass.Id.ToString(), $"{pass.PassNumber} revoked: {pass.RevokedReason}", cancellationToken);
        return Ok(await PassRowAsync(id, cancellationToken));
    }

    /// <summary>The pass holder's place on today's visit (raised now if need be), ready for the gate checks.</summary>
    [HttpPost("passes/{id:guid}/today")]
    public async Task<ActionResult<PassTodayResponse>> Today(Guid id, CancellationToken cancellationToken)
    {
        EnsureGate();
        var factoryId = await dbContext.VisitorPasses.AsNoTracking().Where(x => x.TenantId == TenantId && x.Id == id).Select(x => (Guid?)x.FactoryId).SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Pass not found.");
        await orgScopeService.EnsureCanAccessAsync(TenantId, UserId, factoryId, cancellationToken);
        var attendee = await passService.TodayAsync(TenantId, id, Actor, cancellationToken);
        return Ok(new PassTodayResponse(attendee.VisitId, attendee.Id));
    }

    /// <summary>The pass holder's permanent badge (PDF). Its QR code works at the gate on every day the pass allows.</summary>
    [HttpGet("passes/{id:guid}/badge")]
    public async Task<IActionResult> PassBadge(Guid id, CancellationToken cancellationToken)
    {
        EnsureGate();
        var pass = await dbContext.VisitorPasses.AsNoTracking().Include(x => x.Visitor).Include(x => x.Factory).Include(x => x.Zones).ThenInclude(x => x.AccessZone)
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken) ?? throw new NotFoundException("Pass not found.");
        if (pass.Status != VisitorPassStatuses.Active) throw new BusinessRuleException($"{pass.PassNumber} is revoked.");
        var company = await dbContext.Tenants.Where(x => x.Id == TenantId).Select(x => x.CompanyName).SingleOrDefaultAsync(cancellationToken) ?? string.Empty;
        var escort = pass.Zones.Where(x => x.AccessZone!.RequiresEscort).Select(x => x.AccessZone!.Name).ToList();
        var badge = new VisitorBadge(company, pass.Factory!.Name, $"{pass.PassNumber} · {DaysLabel(pass.Days)} {pass.DailyFrom:HH\\:mm}–{pass.DailyTo:HH\\:mm}",
            $"VISITOR:{pass.BadgeToken}", pass.Visitor!.FullName, pass.Visitor.Company, pass.HostName, pass.Purpose,
            pass.ValidTo.Date.Add(pass.DailyTo.ToTimeSpan()), pass.Zones.Select(x => x.AccessZone!.Name).ToList(),
            escort.Count == 0 ? null : $"Escort required in: {string.Join(", ", escort)}");
        var pdf = (badgeRenderer ?? throw new InvalidOperationException("Badge printing is not set up.")).Render([badge]);
        return File(pdf, "application/pdf", $"pass-{pass.PassNumber}.pdf");
    }

    private void EnsureGate()
    {
        if (!CanGate) throw new ForbiddenException("This is for gate and security staff.");
    }

    private static TimeOnly? ParseTime(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null
        : TimeOnly.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, out var time) ? time
        : throw new BusinessRuleException("Enter daily hours as HH:mm, e.g. 07:00.");

    internal static string DaysLabel(string days)
    {
        var list = days.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).OrderBy(x => x).ToList();
        string[] names = ["", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"];
        if (list.SequenceEqual([1, 2, 3, 4, 5])) return "Mon–Fri";
        if (list.SequenceEqual([1, 2, 3, 4, 5, 6])) return "Mon–Sat";
        if (list.Count == 7) return "Every day";
        return string.Join(", ", list.Select(x => names[x]));
    }

    private static VisitorPassRow Map(VisitorPass x, DateTime now)
    {
        var window = VisitorPassRules.Window(x, now);
        return new VisitorPassRow(
            x.Id, x.PassNumber, x.FactoryId, x.Factory?.Name ?? "", x.VisitorId, x.Visitor!.FullName, x.Visitor.Company, x.Visitor.Phone, x.Visitor.IsBlocked,
            x.ContractorId, x.Contractor?.Name, x.HostUserId, x.HostName, x.Purpose, x.ValidFrom, x.ValidTo,
            x.Days.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList(), DaysLabel(x.Days),
            x.DailyFrom.ToString("HH:mm"), x.DailyTo.ToString("HH:mm"),
            x.Zones.Where(z => z.AccessZone is not null).Select(z => new ZoneRef(z.AccessZoneId, z.AccessZone!.Name, z.AccessZone.Level)).ToList(),
            x.VehicleRegistration, x.Notes, x.Status, x.IssuedByName, x.CreatedAt, x.RevokedByName, x.RevokedReason, window.Usable, window.Reason);
    }

    private async Task<VisitorPassRow> PassRowAsync(Guid id, CancellationToken cancellationToken)
    {
        var pass = await dbContext.VisitorPasses.AsNoTracking().Include(x => x.Visitor).Include(x => x.Factory).Include(x => x.Contractor).Include(x => x.Zones).ThenInclude(x => x.AccessZone)
            .SingleAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken);
        return Map(pass, DateTime.UtcNow);
    }

    private async Task<VehicleMovementRow> RowAsync(Guid id, CancellationToken cancellationToken) =>
        await dbContext.VehicleMovements.AsNoTracking().Where(x => x.TenantId == TenantId && x.Id == id)
            .Select(x => new VehicleMovementRow(x.Id, x.FactoryId, x.Factory!.Name, x.Registration, x.Purpose, x.Description, x.DriverName, x.DriverPhone, x.VisitId,
                x.Visit != null ? x.Visit.VisitNumber : null, x.EnteredAt, x.EnteredByName, x.ExitedAt, x.ExitedByName, x.Notes, x.ExitNotes, 0, false))
            .SingleAsync(cancellationToken);
}

public sealed record VehicleMovementRow(
    Guid Id, Guid FactoryId, string FactoryName, string Registration, string Purpose, string? Description, string? DriverName, string? DriverPhone,
    Guid? VisitId, string? VisitNumber, DateTime EnteredAt, string EnteredByName, DateTime? ExitedAt, string? ExitedByName, string? Notes, string? ExitNotes,
    int MinutesInside, bool VisitOver);

public sealed record VehicleEntryRequest(
    Guid FactoryId, string? Registration, string? Purpose = null, string? DriverName = null, string? DriverPhone = null, string? Description = null,
    Guid? VisitId = null, string? Notes = null);

public sealed record VehicleExitRequest(string? Notes = null);

public sealed record VisitorPassRow(
    Guid Id, string PassNumber, Guid FactoryId, string FactoryName, Guid VisitorId, string FullName, string? Company, string? Phone, bool IsBlocked,
    Guid? ContractorId, string? ContractorName, Guid HostUserId, string HostName, string Purpose, DateTime ValidFrom, DateTime ValidTo,
    IReadOnlyList<int> Days, string DaysLabel, string DailyFrom, string DailyTo, IReadOnlyList<ZoneRef> Zones, string? VehicleRegistration, string? Notes,
    string Status, string IssuedByName, DateTime IssuedAt, string? RevokedByName, string? RevokedReason, bool UsableNow, string? NotUsableReason);

public sealed record CreateVisitorPassRequest(
    Guid FactoryId,
    VisitorDetailsRequest? Person,
    DateTime ValidFrom,
    DateTime ValidTo,
    Guid? HostUserId = null,
    IReadOnlyList<int>? Days = null,
    string? DailyFrom = null,
    string? DailyTo = null,
    IReadOnlyList<Guid>? ZoneIds = null,
    Guid? ContractorId = null,
    string? Purpose = null,
    string? VehicleRegistration = null,
    string? Notes = null);

public sealed record RevokeVisitorPassRequest(string? Reason);

public sealed record PassTodayResponse(Guid VisitId, Guid AttendeeId);
