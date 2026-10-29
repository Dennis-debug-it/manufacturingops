using Ecosys.Domain.Entities;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Api.Controllers;

/// <summary>
/// Monthly sales targets for each person and for the team, a year at a time. Anyone with CRM
/// access can read them; admins and sales managers (<see cref="PermissionNames.CrmViewAll"/>) set them.
/// </summary>
public sealed partial class SalesCrmController
{
    [HttpGet("targets")]
    public async Task<ActionResult<SalesTargetsResponse>> GetTargets([FromQuery] int? year, CancellationToken cancellationToken)
    {
        EnsureCanView();
        var y = year ?? DateTime.UtcNow.Year;
        var targets = await dbContext.SalesTargets.AsNoTracking()
            .Where(x => x.TenantId == TenantId && x.Month.Year == y)
            .OrderBy(x => x.Month)
            .Select(x => new SalesTargetRow(x.OwnerUserId, x.Month, x.Amount))
            .ToListAsync(cancellationToken);
        var converter = await SalesCurrencyConverter.LoadAsync(dbContext, TenantId, cancellationToken);
        var owners = await SalesCrmAccess.ListEligibleOwnersAsync(dbContext, rolePermissionService, TenantId, cancellationToken);
        return Ok(new SalesTargetsResponse(y, converter.ReportingCurrency, owners, targets,
            tenantContext.IsAdmin || tenantContext.HasPermission(PermissionNames.CrmViewAll)));
    }

    /// <summary>Replaces the year's targets with the ones sent; a missing or zero amount clears that month.</summary>
    [HttpPut("targets")]
    public async Task<ActionResult<SalesTargetsResponse>> SetTargets([FromBody] SetSalesTargetsRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureTenantOperationalAccess();
        if (!(tenantContext.IsAdmin || tenantContext.HasPermission(PermissionNames.CrmViewAll)))
            throw new ForbiddenException("Only admins and sales managers set targets.");
        await licenseGuardService.EnsureTenantCanMutateAsync(TenantId, cancellationToken);
        if (request.Year is < 2000 or > 2100)
            throw new BusinessRuleException("Pick a year between 2000 and 2100.");
        if (request.Targets.Any(x => x.Amount < 0))
            throw new BusinessRuleException("Targets cannot be negative.");
        if (request.Targets.Any(x => x.Month.Year != request.Year))
            throw new BusinessRuleException("Every target must be in the year being saved.");

        var existing = await dbContext.SalesTargets.Where(x => x.TenantId == TenantId && x.Month.Year == request.Year).ToListAsync(cancellationToken);
        dbContext.SalesTargets.RemoveRange(existing);
        foreach (var row in request.Targets
            .Where(x => x.Amount > 0)
            .GroupBy(x => (x.OwnerUserId, Month: new DateTime(x.Month.Year, x.Month.Month, 1, 0, 0, 0, DateTimeKind.Utc))))
        {
            dbContext.SalesTargets.Add(new SalesTarget { TenantId = TenantId, OwnerUserId = row.Key.OwnerUserId, Month = row.Key.Month, Amount = row.Last().Amount });
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "crm.targets.updated", nameof(SalesTarget), TenantId.ToString(), $"Sales targets for {request.Year} updated.", cancellationToken);
        return await GetTargets(request.Year, cancellationToken);
    }
}

public sealed record SalesTargetRow(Guid? OwnerUserId, DateTime Month, decimal Amount);

public sealed record SalesTargetsResponse(int Year, string ReportingCurrency, IReadOnlyList<SalesOwnerOption> Owners, IReadOnlyList<SalesTargetRow> Targets, bool CanEdit);

public sealed record SetSalesTargetsRequest(int Year, IReadOnlyList<SalesTargetRow> Targets);
