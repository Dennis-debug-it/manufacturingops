using System.ComponentModel.DataAnnotations;
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

/// <summary>The maintenance team: trades, skills, rates, certificates, the roster and the workload.</summary>
[ApiController]
[Authorize]
[Route("api/factory/people")]
public sealed class FactoryPeopleController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IUserAccessService userAccessService,
    IOrgScopeService orgScopeService,
    IPeopleService peopleService) : TenantAwareControllerBase(tenantContext)
{
    [HttpGet("technicians")]
    public async Task<ActionResult<IReadOnlyCollection<TeamMember>>> Technicians([FromQuery] bool includeInactive, CancellationToken cancellationToken)
    {
        EnsureCanView();
        var today = DateTime.UtcNow.Date;
        var query = dbContext.Technicians.Where(x => x.TenantId == TenantId);
        if (!includeInactive) query = query.Where(x => x.IsActive);
        var rows = await query.OrderBy(x => x.FullName).Select(x => new
        {
            x.Id, x.FullName, x.Email, x.Phone, x.Trade, x.HomeOrgNodeId, x.HourlyRate, x.OvertimeMultiplier, x.IsActive, x.UserId,
            Skills = x.Skills.OrderBy(s => s.Skill).Select(s => new TechnicianSkillDto(s.Skill, s.Level)).ToList(),
            Certificates = x.Certificates.Select(c => new { c.Id, c.CertificationDefinitionId, Name = c.CertificationDefinition!.Name, c.CertificateNumber, c.IssuedOn, c.ExpiresOn }).ToList(),
        }).ToListAsync(cancellationToken);
        var nodes = await dbContext.OrgNodes.Where(x => x.TenantId == TenantId).ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);
        return Ok(rows.Select(x => new TeamMember(
            x.Id, x.FullName, x.Email, x.Phone, x.Trade, x.HomeOrgNodeId, x.HomeOrgNodeId is { } n ? nodes.GetValueOrDefault(n) : null,
            x.HourlyRate, x.OvertimeMultiplier, x.IsActive, x.UserId != null, x.Skills,
            x.Certificates.OrderBy(c => c.ExpiresOn ?? DateTime.MaxValue)
                .Select(c => new TechnicianCertificateDto(c.Id, x.Id, x.FullName, c.CertificationDefinitionId, c.Name, c.CertificateNumber, c.IssuedOn, c.ExpiresOn, CertificateStatuses.Of(c.ExpiresOn, today))).ToList()))
            .ToList());
    }

    /// <summary>A technician's trade, home factory or section, rates and skills.</summary>
    [HttpPut("technicians/{id:guid}")]
    public async Task<ActionResult> UpdateProfile(Guid id, [FromBody] TechnicianProfileRequest request, CancellationToken cancellationToken)
    {
        EnsureCanManage();
        var technician = await dbContext.Technicians.Include(x => x.Skills).SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("The technician was not found.");
        technician.Trade = request.Trade is null ? null : Trades.Find(request.Trade) ?? throw new BusinessRuleException($"Trade must be one of: {string.Join(", ", Trades.All)}.");
        if (request.HomeOrgNodeId is { } node)
        {
            await orgScopeService.EnsureCanAccessAsync(TenantId, UserId, node, cancellationToken);
        }

        technician.HomeOrgNodeId = request.HomeOrgNodeId;
        if (request.HourlyRate is < 0 || request.OvertimeMultiplier is < 1 or > 4) throw new BusinessRuleException("Check the rates: overtime is between 1× and 4× the hourly rate.");
        technician.HourlyRate = request.HourlyRate;
        technician.OvertimeMultiplier = request.OvertimeMultiplier ?? technician.OvertimeMultiplier;

        var wanted = (request.Skills ?? []).Where(x => !string.IsNullOrWhiteSpace(x.Skill))
            .GroupBy(x => x.Skill.Trim(), StringComparer.OrdinalIgnoreCase).Select(g => g.Last() with { Skill = g.Key }).ToList();
        dbContext.TechnicianSkills.RemoveRange(technician.Skills.Where(s => !wanted.Any(w => string.Equals(w.Skill, s.Skill, StringComparison.OrdinalIgnoreCase))));
        foreach (var skill in wanted)
        {
            var existing = technician.Skills.FirstOrDefault(s => string.Equals(s.Skill, skill.Skill, StringComparison.OrdinalIgnoreCase));
            if (existing is null)
            {
                dbContext.TechnicianSkills.Add(new TechnicianSkill { TenantId = TenantId, TechnicianId = id, Skill = skill.Skill, Level = Math.Clamp(skill.Level, 1, 3) });
            }
            else
            {
                existing.Level = Math.Clamp(skill.Level, 1, 3);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpGet("certificate-types")]
    public async Task<ActionResult<IReadOnlyCollection<CertificateTypeDto>>> CertificateTypes(CancellationToken cancellationToken)
    {
        EnsureCanView();
        return Ok(await dbContext.CertificationDefinitions.Where(x => x.TenantId == TenantId && x.IsActive).OrderBy(x => x.Name)
            .Select(x => new CertificateTypeDto(x.Id, x.Name, x.Issuer, x.RenewalPeriodMonths)).ToListAsync(cancellationToken));
    }

    /// <summary>Adds the usual tea factory tickets (electrical licence, boiler operator, welding, heights, confined space, first aid) that are missing.</summary>
    [HttpPost("certificate-types/standard")]
    public async Task<ActionResult<IReadOnlyCollection<CertificateTypeDto>>> AddStandardTypes(CancellationToken cancellationToken)
    {
        EnsureCanManage();
        (string Name, string Issuer, int? Months)[] standard =
        [
            ("Electrical installation licence", "Energy and Petroleum Regulatory Authority (EPRA)", 36),
            ("Boiler operator certificate", "DOSH", 24),
            ("Welding certificate", "NITA", 36),
            ("Working at heights", "Approved trainer", 24),
            ("Confined space entry", "Approved trainer", 24),
            ("First aid", "St John Ambulance / Red Cross", 24),
            ("Forklift operator", "NITA", 36),
        ];
        var existing = (await dbContext.CertificationDefinitions.Where(x => x.TenantId == TenantId).Select(x => x.Name).ToListAsync(cancellationToken)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, issuer, months) in standard.Where(x => !existing.Contains(x.Name)))
        {
            dbContext.CertificationDefinitions.Add(new CertificationDefinition { TenantId = TenantId, Name = name, Issuer = issuer, RenewalPeriodMonths = months, IsActive = true });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return await CertificateTypes(cancellationToken);
    }

    [HttpGet("certificates")]
    public async Task<ActionResult<IReadOnlyCollection<TechnicianCertificateDto>>> Certificates([FromQuery] string? status, CancellationToken cancellationToken)
    {
        EnsureCanView();
        var today = DateTime.UtcNow.Date;
        var rows = (await dbContext.TechnicianCertificates.Where(x => x.TenantId == TenantId && x.Technician!.IsActive)
            .Select(x => new TechnicianCertificateDto(x.Id, x.TechnicianId, x.Technician!.FullName, x.CertificationDefinitionId, x.CertificationDefinition!.Name,
                x.CertificateNumber, x.IssuedOn, x.ExpiresOn, string.Empty))
            .ToListAsync(cancellationToken))
            .Select(x => x with { Status = CertificateStatuses.Of(x.ExpiresOn, today) });
        if (!string.IsNullOrWhiteSpace(status)) rows = rows.Where(x => string.Equals(x.Status, status, StringComparison.OrdinalIgnoreCase));
        return Ok(rows.OrderBy(x => x.Status == CertificateStatuses.Expired ? 0 : x.Status == CertificateStatuses.Expiring ? 1 : 2).ThenBy(x => x.ExpiresOn).ThenBy(x => x.TechnicianName).ToList());
    }

    [HttpPost("technicians/{id:guid}/certificates")]
    public async Task<ActionResult<TechnicianCertificateDto>> AddCertificate(Guid id, [FromBody] SaveTechnicianCertificateRequest request, CancellationToken cancellationToken)
    {
        EnsureCanManage();
        if (!await dbContext.Technicians.AnyAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)) throw new NotFoundException("The technician was not found.");
        var certificate = new TechnicianCertificate { TenantId = TenantId, TechnicianId = id };
        await ApplyAsync(certificate, request, cancellationToken);
        dbContext.TechnicianCertificates.Add(certificate);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(await CertificateAsync(certificate.Id, cancellationToken));
    }

    [HttpPut("certificates/{id:guid}")]
    public async Task<ActionResult<TechnicianCertificateDto>> UpdateCertificate(Guid id, [FromBody] SaveTechnicianCertificateRequest request, CancellationToken cancellationToken)
    {
        EnsureCanManage();
        var certificate = await dbContext.TechnicianCertificates.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("The certificate was not found.");
        var expiry = certificate.ExpiresOn;
        await ApplyAsync(certificate, request, cancellationToken);
        if (certificate.ExpiresOn != expiry) certificate.LastAlertDays = null;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(await CertificateAsync(id, cancellationToken));
    }

    [HttpDelete("certificates/{id:guid}")]
    public async Task<ActionResult> DeleteCertificate(Guid id, CancellationToken cancellationToken)
    {
        EnsureCanManage();
        var certificate = await dbContext.TechnicianCertificates.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("The certificate was not found.");
        dbContext.TechnicianCertificates.Remove(certificate);
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    /// <summary>The roster for a date range: each technician's shift, on-call or day off, with approved leave.</summary>
    [HttpGet("roster")]
    public async Task<ActionResult<RosterView>> Roster([FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken cancellationToken)
    {
        EnsureCanView();
        if (to < from || to.DayNumber - from.DayNumber > 62) throw new BusinessRuleException("Show up to two months at a time.");
        var settings = await dbContext.FactorySettings.AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == TenantId, cancellationToken);
        var entries = await dbContext.RosterEntries.Where(x => x.TenantId == TenantId && x.WorkDate >= from && x.WorkDate <= to)
            .Select(x => new RosterCell(x.TechnicianId, x.WorkDate, x.Kind, x.ShiftName, x.Note)).ToListAsync(cancellationToken);
        var leave = await dbContext.LeaveRequests.Where(x => x.TenantId == TenantId && x.Status == "Approved" && x.StartDate <= to && x.EndDate >= from)
            .Select(x => new { x.TechnicianId, x.UserId, x.StartDate, x.EndDate, x.LeaveTypeCode }).ToListAsync(cancellationToken);
        var technicians = await dbContext.Technicians.Where(x => x.TenantId == TenantId && x.IsActive)
            .Select(x => new { x.Id, x.UserId }).ToListAsync(cancellationToken);
        var leaveCells = leave.SelectMany(l => technicians.Where(t => l.TechnicianId == t.Id || (t.UserId != null && l.UserId == t.UserId))
            .Select(t => new RosterLeave(t.Id, l.StartDate < from ? from : l.StartDate, l.EndDate > to ? to : l.EndDate, l.LeaveTypeCode))).ToList();
        return Ok(new RosterView(ShiftCalendar.Parse(settings?.ShiftsJson).Select(x => x.Name).ToList(), entries, leaveCells));
    }

    /// <summary>Sets roster cells; a cell with kind "Clear" removes the entry.</summary>
    [HttpPut("roster")]
    public async Task<ActionResult> SaveRoster([FromBody] SaveRosterRequest request, CancellationToken cancellationToken)
    {
        EnsureCanManage();
        var cells = request.Cells ?? [];
        if (cells.Count > 2000) throw new BusinessRuleException("Too many cells at once.");
        var settings = await dbContext.FactorySettings.AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == TenantId, cancellationToken);
        var shifts = ShiftCalendar.Parse(settings?.ShiftsJson).Select(x => x.Name).ToList();
        var ids = cells.Select(x => x.TechnicianId).Distinct().ToList();
        if (await dbContext.Technicians.CountAsync(x => x.TenantId == TenantId && ids.Contains(x.Id), cancellationToken) != ids.Count) throw new BusinessRuleException("Some technicians were not found.");
        var dates = cells.Select(x => x.WorkDate).Distinct().ToList();
        var existing = await dbContext.RosterEntries.Where(x => x.TenantId == TenantId && ids.Contains(x.TechnicianId) && dates.Contains(x.WorkDate)).ToListAsync(cancellationToken);
        foreach (var cell in cells)
        {
            var entry = existing.FirstOrDefault(x => x.TechnicianId == cell.TechnicianId && x.WorkDate == cell.WorkDate);
            if (string.Equals(cell.Kind, "Clear", StringComparison.OrdinalIgnoreCase))
            {
                if (entry is not null) dbContext.RosterEntries.Remove(entry);
                continue;
            }

            var kind = RosterKinds.All.FirstOrDefault(x => string.Equals(x, cell.Kind, StringComparison.OrdinalIgnoreCase))
                ?? throw new BusinessRuleException("A roster cell is a shift, on call, off, or clear.");
            var shift = kind == RosterKinds.Shift
                ? shifts.FirstOrDefault(x => string.Equals(x, cell.ShiftName, StringComparison.OrdinalIgnoreCase)) ?? throw new BusinessRuleException($"Shift must be one of: {string.Join(", ", shifts)}.")
                : null;
            if (entry is null)
            {
                entry = new RosterEntry { TenantId = TenantId, TechnicianId = cell.TechnicianId, WorkDate = cell.WorkDate };
                dbContext.RosterEntries.Add(entry);
                existing.Add(entry);
            }

            entry.Kind = kind;
            entry.ShiftName = shift;
            entry.Note = string.IsNullOrWhiteSpace(cell.Note) ? null : cell.Note.Trim();
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    /// <summary>Copies one week's roster onto the next weeks.</summary>
    [HttpPost("roster/copy-week")]
    public async Task<ActionResult<int>> CopyWeek([FromBody] CopyRosterRequest request, CancellationToken cancellationToken)
    {
        EnsureCanManage();
        if (request.Weeks is < 1 or > 8) throw new BusinessRuleException("Copy to between 1 and 8 weeks.");
        var source = await dbContext.RosterEntries.Where(x => x.TenantId == TenantId && x.WorkDate >= request.WeekStart && x.WorkDate < request.WeekStart.AddDays(7)).ToListAsync(cancellationToken);
        var lastDay = request.WeekStart.AddDays(7 * (request.Weeks + 1));
        var target = await dbContext.RosterEntries.Where(x => x.TenantId == TenantId && x.WorkDate >= request.WeekStart.AddDays(7) && x.WorkDate < lastDay).ToListAsync(cancellationToken);
        var written = 0;
        for (var week = 1; week <= request.Weeks; week++)
        {
            foreach (var entry in source)
            {
                var date = entry.WorkDate.AddDays(7 * week);
                var copy = target.FirstOrDefault(x => x.TechnicianId == entry.TechnicianId && x.WorkDate == date);
                if (copy is null)
                {
                    copy = new RosterEntry { TenantId = TenantId, TechnicianId = entry.TechnicianId, WorkDate = date };
                    dbContext.RosterEntries.Add(copy);
                    target.Add(copy);
                }

                copy.Kind = entry.Kind;
                copy.ShiftName = entry.ShiftName;
                copy.Note = entry.Note;
                written++;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(written);
    }

    [HttpGet("workload")]
    public async Task<ActionResult<IReadOnlyList<WorkloadRow>>> Workload([FromQuery] DateOnly from, [FromQuery] DateOnly to, [FromQuery] Guid? orgNodeId, CancellationToken cancellationToken)
    {
        EnsureCanView();
        if (to < from || to.DayNumber - from.DayNumber > 366) throw new BusinessRuleException("Choose up to a year.");
        var scope = await orgScopeService.GetScopeAsync(TenantId, UserId, cancellationToken);
        IReadOnlyCollection<Guid>? nodes = scope.Nodes?.ToList();
        if (orgNodeId.HasValue)
        {
            var subtree = await orgScopeService.GetSubtreeAsync(TenantId, orgNodeId.Value, cancellationToken);
            nodes = nodes is null ? subtree.ToList() : subtree.Where(nodes.Contains).ToList();
        }

        return Ok(await peopleService.WorkloadAsync(TenantId, nodes, from, to, cancellationToken));
    }

    private async Task ApplyAsync(TechnicianCertificate certificate, SaveTechnicianCertificateRequest request, CancellationToken cancellationToken)
    {
        var type = await dbContext.CertificationDefinitions.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == request.CertificationDefinitionId, cancellationToken)
            ?? throw new BusinessRuleException("Choose the certificate type.");
        DateTime? Utc(DateTime? d) => d is { } v ? DateTime.SpecifyKind(v.Date, DateTimeKind.Utc) : null;
        certificate.CertificationDefinitionId = type.Id;
        certificate.CertificateNumber = string.IsNullOrWhiteSpace(request.CertificateNumber) ? null : request.CertificateNumber.Trim();
        certificate.IssuedOn = Utc(request.IssuedOn);
        certificate.ExpiresOn = Utc(request.ExpiresOn) ?? (certificate.IssuedOn is { } issued && type.RenewalPeriodMonths is { } months ? issued.AddMonths(months) : null);
        if (certificate.ExpiresOn is { } e && certificate.IssuedOn is { } i && e <= i) throw new BusinessRuleException("The expiry must be after the issue date.");
        certificate.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
    }

    private async Task<TechnicianCertificateDto> CertificateAsync(Guid id, CancellationToken cancellationToken)
    {
        var row = await dbContext.TechnicianCertificates.Where(x => x.Id == id)
            .Select(x => new TechnicianCertificateDto(x.Id, x.TechnicianId, x.Technician!.FullName, x.CertificationDefinitionId, x.CertificationDefinition!.Name, x.CertificateNumber, x.IssuedOn, x.ExpiresOn, string.Empty))
            .SingleAsync(cancellationToken);
        return row with { Status = CertificateStatuses.Of(row.ExpiresOn, DateTime.UtcNow.Date) };
    }

    private void EnsureCanView() => userAccessService.EnsureAdminOrAnyPermission(PermissionNames.CanViewWorkOrders);

    private void EnsureCanManage() => userAccessService.EnsureAdminOrAnyPermission(PermissionNames.CanManageSettings, PermissionNames.CanAssignWorkOrders);
}

public sealed record TechnicianSkillDto(string Skill, int Level);

public sealed record TechnicianCertificateDto(
    Guid Id, Guid TechnicianId, string TechnicianName, Guid CertificationDefinitionId, string Name, string? CertificateNumber,
    DateTime? IssuedOn, DateTime? ExpiresOn, string Status);

public sealed record TeamMember(
    Guid Id, string FullName, string Email, string? Phone, string? Trade, Guid? HomeOrgNodeId, string? HomeOrgNodeName,
    decimal? HourlyRate, decimal OvertimeMultiplier, bool IsActive, bool HasLogin,
    IReadOnlyCollection<TechnicianSkillDto> Skills, IReadOnlyCollection<TechnicianCertificateDto> Certificates);

public sealed record TechnicianProfileRequest(string? Trade, Guid? HomeOrgNodeId, decimal? HourlyRate, decimal? OvertimeMultiplier, IReadOnlyCollection<TechnicianSkillDto>? Skills);

public sealed record CertificateTypeDto(Guid Id, string Name, string? Issuer, int? RenewalPeriodMonths);

public sealed record SaveTechnicianCertificateRequest(Guid CertificationDefinitionId, [StringLength(100)] string? CertificateNumber, DateTime? IssuedOn, DateTime? ExpiresOn, [StringLength(1000)] string? Notes);

public sealed record RosterCell(Guid TechnicianId, DateOnly WorkDate, string Kind, string? ShiftName, string? Note);

public sealed record RosterLeave(Guid TechnicianId, DateOnly From, DateOnly To, string LeaveType);

public sealed record RosterView(IReadOnlyCollection<string> Shifts, IReadOnlyCollection<RosterCell> Entries, IReadOnlyCollection<RosterLeave> Leave);

public sealed record SaveRosterRequest(IReadOnlyCollection<RosterCell>? Cells);

public sealed record CopyRosterRequest(DateOnly WeekStart, int Weeks = 1);
