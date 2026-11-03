using System.Security.Cryptography;
using System.Text;
using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Api.Controllers;

[ApiController]
public sealed class CsatController(AppDbContext dbContext, IPortalContext portalContext) : ControllerBase
{
    private const string InvalidLinkMessage = "This survey link is invalid, expired, or has already been used.";

    // ── Anonymous email-link flow (single-use hashed token) ───────────────────

    [AllowAnonymous]
    [EnableRateLimiting("PublicEndpoints")]
    [HttpGet("api/public/csat/{token}")]
    public async Task<ActionResult<CsatSurveyInfoResponse>> GetSurvey(string token, CancellationToken cancellationToken)
    {
        var survey = await FindByTokenAsync(token, cancellationToken)
            ?? throw new NotFoundException(InvalidLinkMessage);

        var details = await dbContext.WorkOrders
            .Where(x => x.Id == survey.WorkOrderId)
            .Select(x => new { x.WorkOrderNumber, TenantName = x.Tenant!.CompanyName })
            .SingleOrDefaultAsync(cancellationToken);

        return Ok(new CsatSurveyInfoResponse(
            details?.TenantName ?? "Service Provider",
            details?.WorkOrderNumber ?? string.Empty,
            survey.RespondedAt.HasValue));
    }

    [AllowAnonymous]
    [EnableRateLimiting("PublicEndpoints")]
    [HttpPost("api/public/csat/{token}")]
    public async Task<ActionResult<CsatSubmitResponse>> Submit(string token, [FromBody] CsatSubmitRequest request, CancellationToken cancellationToken)
    {
        ValidateScore(request.Score);

        var survey = await FindByTokenAsync(token, cancellationToken)
            ?? throw new NotFoundException(InvalidLinkMessage);

        if (survey.RespondedAt.HasValue)
        {
            throw new BusinessRuleException("This survey has already been submitted. Thank you!");
        }

        survey.Score = request.Score;
        survey.Comment = NormalizeComment(request.Comment);
        survey.RespondedAt = DateTime.UtcNow;
        survey.Channel = "Email";
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new CsatSubmitResponse("Thank you for your feedback!"));
    }

    // ── Portal flow (authenticated portal client, no token needed) ────────────

    [Authorize(Policy = "PortalAccess")]
    [HttpGet("api/portal/csat/pending")]
    public async Task<ActionResult<IReadOnlyCollection<PortalPendingCsatResponse>>> PendingForPortal(CancellationToken cancellationToken)
    {
        var tenantId = portalContext.GetRequiredTenantId();
        var clientId = portalContext.GetRequiredClientId();
        var now = DateTime.UtcNow;

        var pending = await dbContext.CsatSurveys
            .Where(x => x.TenantId == tenantId && x.ClientId == clientId && x.RespondedAt == null && x.ExpiresAt > now)
            .Join(dbContext.WorkOrders, s => s.WorkOrderId, w => w.Id, (s, w) => new PortalPendingCsatResponse(w.Id, w.WorkOrderNumber, w.Title, s.SentAt))
            .ToListAsync(cancellationToken);

        return Ok(pending);
    }

    [Authorize(Policy = "PortalAccess")]
    [HttpPost("api/portal/csat/{workOrderId:guid}")]
    public async Task<ActionResult<CsatSubmitResponse>> SubmitFromPortal(Guid workOrderId, [FromBody] CsatSubmitRequest request, CancellationToken cancellationToken)
    {
        ValidateScore(request.Score);

        var tenantId = portalContext.GetRequiredTenantId();
        var clientId = portalContext.GetRequiredClientId();

        var survey = await dbContext.CsatSurveys
            .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.ClientId == clientId && x.WorkOrderId == workOrderId, cancellationToken)
            ?? throw new NotFoundException("No survey is open for this work order.");

        if (survey.RespondedAt.HasValue)
        {
            throw new BusinessRuleException("This work order has already been rated. Thank you!");
        }

        survey.Score = request.Score;
        survey.Comment = NormalizeComment(request.Comment);
        survey.RespondedAt = DateTime.UtcNow;
        survey.Channel = "Portal";
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new CsatSubmitResponse("Thank you for your feedback!"));
    }

    private async Task<Domain.Entities.CsatSurvey?> FindByTokenAsync(string token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token.Trim())));
        var now = DateTime.UtcNow;
        return await dbContext.CsatSurveys
            .SingleOrDefaultAsync(x => x.TokenHash == tokenHash && x.ExpiresAt > now, cancellationToken);
    }

    private static void ValidateScore(int score)
    {
        if (score is < 1 or > 5)
        {
            throw new BusinessRuleException("Score must be between 1 and 5.");
        }
    }

    private static string? NormalizeComment(string? comment)
    {
        var trimmed = comment?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        return trimmed.Length > 2000 ? trimmed[..2000] : trimmed;
    }
}

public sealed record CsatSurveyInfoResponse(string CompanyName, string WorkOrderNumber, bool AlreadySubmitted);
public sealed record CsatSubmitRequest(int Score, string? Comment);
public sealed record CsatSubmitResponse(string Message);
public sealed record PortalPendingCsatResponse(Guid WorkOrderId, string WorkOrderNumber, string Title, DateTime? SentAt);
