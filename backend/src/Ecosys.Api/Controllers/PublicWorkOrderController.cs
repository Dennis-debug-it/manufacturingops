using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Api.Controllers;

[ApiController]
public sealed class PublicWorkOrderController(AppDbContext dbContext) : ControllerBase
{
    [AllowAnonymous]
    [EnableRateLimiting("PublicEndpoints")]
    [HttpGet("api/public/workorders/{token}")]
    public async Task<ActionResult<PublicWorkOrderTrackingResponse>> GetByToken(string token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new NotFoundException("This tracking link is invalid.");
        }

        var workOrder = await dbContext.WorkOrders
            .Where(x => x.TrackingToken == token.Trim())
            .Select(x => new
            {
                x.WorkOrderNumber,
                x.Title,
                x.Status,
                x.ScheduledStartAt,
                x.ScheduledEndAt,
                x.ArrivalAt,
                x.CompletedAt,
                x.UpdatedAt,
                x.CreatedAt,
                TechnicianName = x.AssignedTechnician != null ? x.AssignedTechnician.FullName : null,
                CompanyName = x.Tenant != null ? x.Tenant.CompanyName : null
            })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("This tracking link is invalid or has expired.");

        return Ok(new PublicWorkOrderTrackingResponse(
            workOrder.WorkOrderNumber,
            workOrder.Title,
            workOrder.Status,
            workOrder.CompanyName,
            workOrder.TechnicianName,
            workOrder.ScheduledStartAt,
            workOrder.ScheduledEndAt,
            workOrder.ArrivalAt,
            workOrder.CompletedAt,
            workOrder.UpdatedAt ?? workOrder.CreatedAt));
    }
}

public sealed record PublicWorkOrderTrackingResponse(
    string WorkOrderNumber,
    string Title,
    string Status,
    string? CompanyName,
    string? TechnicianName,
    DateTime? ScheduledStartAt,
    DateTime? ScheduledEndAt,
    DateTime? ArrivalAt,
    DateTime? CompletedAt,
    DateTime LastUpdatedAt);
