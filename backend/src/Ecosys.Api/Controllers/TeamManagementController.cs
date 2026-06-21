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
[Route("api/team-management")]
public sealed class TeamManagementController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IUserAccessService userAccessService) : TenantAwareControllerBase(tenantContext)
{
    private static readonly string[] ClosedWorkOrderStatuses = ["Completed", "Acknowledged", "Closed", "Cancelled"];

    [HttpGet("overview")]
    public async Task<ActionResult<TeamManagementOverviewResponse>> GetOverview(CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();
        return Ok(await BuildOverviewAsync(cancellationToken));
    }

    [HttpPost("certifications")]
    public async Task<ActionResult<CertificationDefinitionResponse>> CreateCertification([FromBody] UpsertCertificationDefinitionRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();
        ValidateCertification(request);

        var certification = new CertificationDefinition
        {
            TenantId = TenantId,
            Name = request.Name.Trim(),
            Issuer = NormalizeOptional(request.Issuer),
            Description = NormalizeOptional(request.Description),
            RenewalPeriodMonths = request.RenewalPeriodMonths,
            IsActive = request.IsActive
        };

        dbContext.CertificationDefinitions.Add(certification);
        await dbContext.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(GetOverview), new { id = certification.Id }, MapCertification(certification));
    }

    [HttpPut("certifications/{id:guid}")]
    public async Task<ActionResult<CertificationDefinitionResponse>> UpdateCertification(Guid id, [FromBody] UpsertCertificationDefinitionRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();
        ValidateCertification(request);

        var certification = await dbContext.CertificationDefinitions
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Certification was not found.");

        certification.Name = request.Name.Trim();
        certification.Issuer = NormalizeOptional(request.Issuer);
        certification.Description = NormalizeOptional(request.Description);
        certification.RenewalPeriodMonths = request.RenewalPeriodMonths;
        certification.IsActive = request.IsActive;

        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(MapCertification(certification));
    }

    [HttpDelete("certifications/{id:guid}")]
    public async Task<IActionResult> DeleteCertification(Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();

        var certification = await dbContext.CertificationDefinitions
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Certification was not found.");

        certification.IsActive = false;
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPut("users/{userId:guid}/profile")]
    public async Task<ActionResult<TeamManagementProfileResponse>> UpdateUserProfile(Guid userId, [FromBody] UpdateTeamManagementProfileRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();

        var user = await dbContext.Users
            .Include(x => x.BranchAssignments)
            .Include(x => x.DefaultBranch)
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == userId, cancellationToken)
            ?? throw new NotFoundException("User was not found.");

        var technician = await ResolveOrCreateTechnicianAsync(user, cancellationToken);
        technician.AvailabilityStatus = NormalizeAvailabilityInput(request.AvailabilityStatus);
        technician.AvailabilityNote = NormalizeOptional(request.AvailabilityNote);
        technician.AvailabilityUntil = request.AvailabilityUntil;
        technician.AvailabilityUpdatedAt = DateTime.UtcNow;

        var certifications = request.Certifications?
            .Where(x => x.CertificationId != Guid.Empty)
            .GroupBy(x => x.CertificationId)
            .Select(group => group.First())
            .ToList() ?? [];

        var requestedCertificationIds = certifications.Select(x => x.CertificationId).ToList();
        if (requestedCertificationIds.Count > 0)
        {
            var validCertificationIds = await dbContext.CertificationDefinitions
                .Where(x => x.TenantId == TenantId && x.IsActive && requestedCertificationIds.Contains(x.Id))
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);

            if (validCertificationIds.Count != requestedCertificationIds.Count)
            {
                throw new BusinessRuleException("One or more certifications were not found.");
            }
        }

        var currentCertifications = await dbContext.UserCertifications
            .Where(x => x.TenantId == TenantId && x.UserId == userId)
            .ToListAsync(cancellationToken);

        dbContext.UserCertifications.RemoveRange(currentCertifications.Where(x => !requestedCertificationIds.Contains(x.CertificationDefinitionId)));

        foreach (var certification in currentCertifications.Where(x => requestedCertificationIds.Contains(x.CertificationDefinitionId)))
        {
            var requested = certifications.First(x => x.CertificationId == certification.CertificationDefinitionId);
            certification.CertificateNumber = NormalizeOptional(requested.CertificateNumber);
            certification.IssuedOn = requested.IssuedOn;
            certification.ExpiresOn = requested.ExpiresOn;
            certification.Notes = NormalizeOptional(requested.Notes);
        }

        var existingCertificationIds = currentCertifications.Select(x => x.CertificationDefinitionId).ToHashSet();
        foreach (var certification in certifications.Where(x => !existingCertificationIds.Contains(x.CertificationId)))
        {
            dbContext.UserCertifications.Add(new UserCertification
            {
                TenantId = TenantId,
                UserId = userId,
                CertificationDefinitionId = certification.CertificationId,
                CertificateNumber = NormalizeOptional(certification.CertificateNumber),
                IssuedOn = certification.IssuedOn,
                ExpiresOn = certification.ExpiresOn,
                Notes = NormalizeOptional(certification.Notes)
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var overview = await BuildOverviewAsync(cancellationToken);
        var profile = overview.Profiles.SingleOrDefault(x => x.UserId == userId);
        return profile is null ? NotFound() : Ok(profile);
    }

    private async Task<TeamManagementOverviewResponse> BuildOverviewAsync(CancellationToken cancellationToken)
    {
        var users = await dbContext.Users
            .Include(x => x.BranchAssignments)
            .Include(x => x.DefaultBranch)
            .Include(x => x.Certifications)
            .ThenInclude(x => x.CertificationDefinition)
            .Where(x => x.TenantId == TenantId)
            .OrderBy(x => x.FullName)
            .ToListAsync(cancellationToken);

        var technicians = await dbContext.Technicians
            .Include(x => x.Branch)
            .Include(x => x.AssignmentGroups.Where(member => member.IsActive))
            .ThenInclude(x => x.AssignmentGroup)
            .Where(x => x.TenantId == TenantId)
            .ToListAsync(cancellationToken);

        var certificationCatalog = await dbContext.CertificationDefinitions
            .Where(x => x.TenantId == TenantId)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

        var technicianIds = technicians.Select(x => x.Id).ToList();
        var assignmentLoad = technicianIds.Count == 0
            ? []
            : await dbContext.WorkOrderTechnicianAssignments
                .Where(x => x.TenantId == TenantId && technicianIds.Contains(x.TechnicianId))
                .Join(
                    dbContext.WorkOrders.Where(x => x.TenantId == TenantId),
                    assignment => assignment.WorkOrderId,
                    workOrder => workOrder.Id,
                    (assignment, workOrder) => new
                    {
                        assignment.TechnicianId,
                        assignment.Status,
                        assignment.ArrivalAt,
                        assignment.DepartureAt,
                        WorkOrderStatus = workOrder.Status
                    })
                .ToListAsync(cancellationToken);

        var loadByTechnician = assignmentLoad
            .Where(x => !ClosedWorkOrderStatuses.Contains(x.WorkOrderStatus))
            .GroupBy(x => x.TechnicianId)
            .ToDictionary(
                group => group.Key,
                group => new AssignmentLoad(
                    group.Count(),
                    group.Count(x => string.Equals(x.Status, "Pending", StringComparison.OrdinalIgnoreCase)),
                    group.Count(x => x.ArrivalAt.HasValue && !x.DepartureAt.HasValue)));

        var technicianByUserId = technicians
            .Where(x => x.UserId.HasValue)
            .ToDictionary(x => x.UserId!.Value, x => x);

        var technicianByEmail = technicians
            .GroupBy(x => x.Email.Trim().ToLowerInvariant())
            .ToDictionary(group => group.Key, group => group.First());

        var now = DateTime.UtcNow;
        var profiles = users
            .Select(user =>
            {
                technicianByUserId.TryGetValue(user.Id, out var technician);
                technician ??= technicianByEmail.GetValueOrDefault(user.Email.Trim().ToLowerInvariant());

                var load = technician is not null && loadByTechnician.TryGetValue(technician.Id, out var currentLoad)
                    ? currentLoad
                    : AssignmentLoad.Empty;

                var assignmentGroups = technician?.AssignmentGroups
                    .Where(x => x.IsActive && x.AssignmentGroup is not null)
                    .OrderByDescending(x => x.IsLead)
                    .ThenBy(x => x.AssignmentGroup!.Name)
                    .Select(x => new TeamManagementNamedMembershipResponse(
                        x.AssignmentGroupId,
                        x.AssignmentGroup!.Name,
                        x.IsLead))
                    .ToList() ?? [];

                var certifications = user.Certifications
                    .Where(x => x.CertificationDefinition is not null && x.CertificationDefinition.IsActive)
                    .OrderBy(x => x.CertificationDefinition!.Name)
                    .Select(x => new TeamManagementCertificationAssignmentResponse(
                        x.CertificationDefinitionId,
                        x.CertificationDefinition!.Name,
                        x.CertificationDefinition.Issuer,
                        x.CertificateNumber,
                        x.IssuedOn,
                        x.ExpiresOn,
                        x.Notes,
                        ResolveCertificationStatus(x, now)))
                    .ToList();

                var readiness = BuildReadiness(user, technician, assignmentGroups.Count, certifications, load, now);

                return new TeamManagementProfileResponse(
                    user.Id,
                    technician?.Id,
                    user.FullName,
                    user.Email,
                    user.Role,
                    user.IsActive,
                    technician?.IsActive ?? user.IsActive,
                    technician?.BranchId ?? user.DefaultBranchId ?? user.BranchAssignments.OrderByDescending(x => x.IsDefault).Select(x => (Guid?)x.BranchId).FirstOrDefault(),
                    technician?.Branch?.Name ?? user.DefaultBranch?.Name,
                    ResolveAvailabilityStatus(technician, now),
                    technician?.AvailabilityNote,
                    technician?.AvailabilityUntil,
                    technician?.AvailabilityUpdatedAt,
                    assignmentGroups,
                    certifications,
                    load.ActiveWorkOrderCount,
                    load.PendingResponseCount,
                    load.OnSiteCount,
                    technician?.IsTrackingActive ?? false,
                    technician?.LastLocationAt,
                    readiness.Status,
                    readiness.Reasons);
            })
            .ToList();

        return new TeamManagementOverviewResponse(
            profiles,
            certificationCatalog.Select(MapCertification).ToList());
    }

    private async Task<Technician> ResolveOrCreateTechnicianAsync(User user, CancellationToken cancellationToken)
    {
        var technician = await dbContext.Technicians
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && (x.UserId == user.Id || x.Email.ToLower() == user.Email.ToLower()), cancellationToken);

        technician ??= new Technician
        {
            TenantId = TenantId,
            UserId = user.Id
        };

        technician.UserId = user.Id;
        technician.FullName = user.FullName;
        technician.Email = user.Email;
        technician.IsActive = user.IsActive;
        technician.BranchId = user.DefaultBranchId ?? user.BranchAssignments.OrderByDescending(x => x.IsDefault).Select(x => (Guid?)x.BranchId).FirstOrDefault();

        if (dbContext.Entry(technician).State == EntityState.Detached)
        {
            dbContext.Technicians.Add(technician);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return technician;
    }

    private static TeamReadiness BuildReadiness(
        User user,
        Technician? technician,
        int assignmentGroupCount,
        IReadOnlyCollection<TeamManagementCertificationAssignmentResponse> certifications,
        AssignmentLoad load,
        DateTime now)
    {
        var reasons = new List<string>();

        if (!user.IsActive)
        {
            reasons.Add("User account is inactive.");
        }

        if (technician is null)
        {
            reasons.Add("No workforce profile has been prepared.");
            return new TeamReadiness("Limited", reasons);
        }

        if (!technician.IsActive)
        {
            reasons.Add("Workforce profile is inactive.");
        }

        var availabilityStatus = ResolveAvailabilityStatus(technician, now);
        if (string.Equals(availabilityStatus, "Unavailable", StringComparison.OrdinalIgnoreCase))
        {
            reasons.Add(technician.AvailabilityNote?.Trim() ?? "Marked unavailable for assignment.");
        }
        else if (string.Equals(availabilityStatus, "Limited", StringComparison.OrdinalIgnoreCase))
        {
            reasons.Add(technician.AvailabilityNote?.Trim() ?? "Marked as limited availability.");
        }

        if (!technician.BranchId.HasValue)
        {
            reasons.Add("No branch assigned.");
        }

        if (assignmentGroupCount == 0)
        {
            reasons.Add("Not assigned to an assignment group.");
        }

        if (certifications.Count > 0)
        {
            if (certifications.Any(x => string.Equals(x.Status, "Expired", StringComparison.OrdinalIgnoreCase)))
            {
                reasons.Add("At least one certification has expired.");
            }
            else if (certifications.Any(x => string.Equals(x.Status, "ExpiringSoon", StringComparison.OrdinalIgnoreCase)))
            {
                reasons.Add("At least one certification is due for renewal soon.");
            }
        }

        if (load.ActiveWorkOrderCount > 0)
        {
            reasons.Add(load.ActiveWorkOrderCount == 1
                ? "Already handling 1 active work order."
                : $"Already handling {load.ActiveWorkOrderCount} active work orders.");
        }

        var status = string.Equals(availabilityStatus, "Unavailable", StringComparison.OrdinalIgnoreCase) || !user.IsActive || !technician.IsActive
            ? "Unavailable"
            : reasons.Count == 0
                ? "Ready"
                : "Limited";

        return new TeamReadiness(status, reasons);
    }

    private static string ResolveAvailabilityStatus(Technician? technician, DateTime now)
    {
        if (technician is null)
        {
            return "NotReady";
        }

        if (technician.AvailabilityUntil.HasValue && technician.AvailabilityUntil.Value <= now)
        {
            return "Available";
        }

        return string.IsNullOrWhiteSpace(technician.AvailabilityStatus)
            ? "Available"
            : technician.AvailabilityStatus.Trim();
    }

    private static string NormalizeAvailabilityInput(string? value)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? "Available" : value.Trim();
        return normalized switch
        {
            "Available" => "Available",
            "Limited" => "Limited",
            "Unavailable" => "Unavailable",
            _ => throw new BusinessRuleException("Availability must be Available, Limited, or Unavailable.")
        };
    }

    private static string ResolveCertificationStatus(UserCertification certification, DateTime now)
    {
        if (certification.ExpiresOn.HasValue && certification.ExpiresOn.Value.Date < now.Date)
        {
            return "Expired";
        }

        if (certification.ExpiresOn.HasValue && certification.ExpiresOn.Value.Date <= now.Date.AddDays(30))
        {
            return "ExpiringSoon";
        }

        return "Valid";
    }

    private static CertificationDefinitionResponse MapCertification(CertificationDefinition certification) =>
        new(certification.Id, certification.Name, certification.Issuer, certification.Description, certification.RenewalPeriodMonths, certification.IsActive, certification.CreatedAt, certification.UpdatedAt);

    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void ValidateCertification(UpsertCertificationDefinitionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new BusinessRuleException("Certification name is required.");
        }
    }

    private sealed record AssignmentLoad(int ActiveWorkOrderCount, int PendingResponseCount, int OnSiteCount)
    {
        public static AssignmentLoad Empty { get; } = new(0, 0, 0);
    }

    private sealed record TeamReadiness(string Status, IReadOnlyCollection<string> Reasons);
}

public sealed record TeamManagementOverviewResponse(
    IReadOnlyCollection<TeamManagementProfileResponse> Profiles,
    IReadOnlyCollection<CertificationDefinitionResponse> Certifications);

public sealed record TeamManagementProfileResponse(
    Guid UserId,
    Guid? TechnicianId,
    string FullName,
    string Email,
    string Role,
    bool IsUserActive,
    bool IsWorkforceActive,
    Guid? BranchId,
    string? BranchName,
    string AvailabilityStatus,
    string? AvailabilityNote,
    DateTime? AvailabilityUntil,
    DateTime? AvailabilityUpdatedAt,
    IReadOnlyCollection<TeamManagementNamedMembershipResponse> AssignmentGroups,
    IReadOnlyCollection<TeamManagementCertificationAssignmentResponse> Certifications,
    int ActiveWorkOrderCount,
    int PendingResponseCount,
    int OnSiteCount,
    bool IsTrackingActive,
    DateTime? LastLocationAt,
    string ReadinessStatus,
    IReadOnlyCollection<string> ReadinessReasons);

public sealed record TeamManagementNamedMembershipResponse(Guid Id, string Name, bool IsLead);

public sealed record TeamManagementCertificationAssignmentResponse(
    Guid CertificationId,
    string CertificationName,
    string? Issuer,
    string? CertificateNumber,
    DateTime? IssuedOn,
    DateTime? ExpiresOn,
    string? Notes,
    string Status);

public sealed record CertificationDefinitionResponse(
    Guid Id,
    string Name,
    string? Issuer,
    string? Description,
    int? RenewalPeriodMonths,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record UpsertCertificationDefinitionRequest(
    string Name,
    string? Issuer,
    string? Description,
    int? RenewalPeriodMonths,
    bool IsActive);

public sealed record UpdateTeamManagementProfileRequest(
    string? AvailabilityStatus,
    string? AvailabilityNote,
    DateTime? AvailabilityUntil,
    IReadOnlyCollection<UpdateTeamManagementProfileCertificationRequest>? Certifications);

public sealed record UpdateTeamManagementProfileCertificationRequest(
    Guid CertificationId,
    string? CertificateNumber,
    DateTime? IssuedOn,
    DateTime? ExpiresOn,
    string? Notes);
