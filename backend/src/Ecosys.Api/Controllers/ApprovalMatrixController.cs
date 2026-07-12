using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Api.Controllers;

/// <summary>Configures the per-tenant approval matrices (settings) and drives the
/// cross-module My Approvals inbox + decisions.</summary>
[ApiController]
[Authorize]
[Route("api")]
public sealed class ApprovalMatrixController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IUserAccessService userAccessService,
    IApprovalWorkflowService approvalWorkflowService) : TenantAwareControllerBase(tenantContext)
{
    // ── Matrix configuration (settings) ───────────────────────────────────────

    [HttpGet("settings/approval-matrices")]
    public async Task<ActionResult<IReadOnlyCollection<ApprovalMatrixResponse>>> ListMatrices(CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageSettings);

        var matrices = await dbContext.ApprovalMatrices
            .Include(x => x.Levels)
            .Where(x => x.TenantId == TenantId)
            .ToListAsync(cancellationToken);

        // Return a row for every supported document type, even if not yet configured,
        // so the UI can offer "configure" for each.
        var byType = matrices.ToDictionary(x => x.DocumentType, StringComparer.OrdinalIgnoreCase);
        var result = ApprovalDocumentTypes.All
            .Select(type => byType.TryGetValue(type, out var matrix) ? MapMatrix(matrix) : EmptyMatrix(type))
            .ToList();

        return Ok(result);
    }

    [HttpPut("settings/approval-matrices/{documentType}")]
    public async Task<ActionResult<ApprovalMatrixResponse>> UpsertMatrix(
        string documentType, [FromBody] UpsertApprovalMatrixRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageSettings);

        if (!ApprovalDocumentTypes.IsSupported(documentType))
            throw new BusinessRuleException($"'{documentType}' does not support an approval matrix.");

        ValidateLevels(request.Levels);

        var canonicalType = ApprovalDocumentTypes.All.Single(x => string.Equals(x, documentType, StringComparison.OrdinalIgnoreCase));

        // Load the matrix header only (no Levels navigation) — old levels are replaced via a
        // set-based delete below, which avoids change-tracker UPDATE/DELETE conflicts.
        var matrix = await dbContext.ApprovalMatrices
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.DocumentType == canonicalType, cancellationToken);

        if (matrix is null)
        {
            matrix = new ApprovalMatrix { TenantId = TenantId, DocumentType = canonicalType };
            dbContext.ApprovalMatrices.Add(matrix);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        else
        {
            await dbContext.ApprovalLevels
                .Where(x => x.ApprovalMatrixId == matrix.Id)
                .ExecuteDeleteAsync(cancellationToken);
        }

        matrix.Currency = string.IsNullOrWhiteSpace(request.Currency) ? "KES" : request.Currency.Trim();
        matrix.IsActive = request.IsActive;
        matrix.UpdatedAt = DateTime.UtcNow;

        var order = 1;
        var newLevels = request.Levels.OrderBy(x => x.MinAmount).Select(level => new ApprovalLevel
        {
            TenantId = TenantId,
            ApprovalMatrixId = matrix.Id,
            LevelOrder = order++,
            Name = level.Name.Trim(),
            MinAmount = level.MinAmount,
            MaxAmount = level.MaxAmount,
            ApproverType = NormalizeApproverType(level.ApproverType),
            RequiredPermission = string.IsNullOrWhiteSpace(level.RequiredPermission) ? null : level.RequiredPermission.Trim(),
            ApproverUserIdsCsv = level.ApproverUserIds is { Count: > 0 } ? string.Join(',', level.ApproverUserIds) : null,
            ReminderAfterHours = level.ReminderAfterHours
        }).ToList();

        dbContext.ApprovalLevels.AddRange(newLevels);
        await dbContext.SaveChangesAsync(cancellationToken);

        matrix.Levels = newLevels;
        return Ok(MapMatrix(matrix));
    }

    // ── My Approvals inbox + decisions ────────────────────────────────────────

    [HttpGet("approvals/pending")]
    public async Task<ActionResult<IReadOnlyCollection<ApprovalRequestResponse>>> Pending(CancellationToken cancellationToken)
    {
        var pending = await dbContext.ApprovalRequests
            .Include(x => x.Steps)
            .Where(x => x.TenantId == TenantId && x.Status == ApprovalStatuses.Pending)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        // Only surface requests whose current level this user can actually decide.
        var mine = pending.Where(request =>
        {
            var step = request.Steps.SingleOrDefault(s => s.LevelOrder == request.CurrentLevelOrder && s.Status == ApprovalStatuses.Pending);
            return step is not null && CanDecide(step);
        }).ToList();

        return Ok(mine.Select(MapRequest).ToList());
    }

    [HttpPost("approvals/{requestId:guid}/decision")]
    public async Task<IActionResult> Decide(Guid requestId, [FromBody] ApprovalDecisionRequest request, CancellationToken cancellationToken)
    {
        await approvalWorkflowService.DecideAsync(TenantId, requestId, request.Approve, request.Note, cancellationToken);
        return NoContent();
    }

    private bool CanDecide(ApprovalRequestStep step)
    {
        if (IsAdmin)
        {
            return true;
        }

        if (string.Equals(step.ApproverType, ApprovalApproverTypes.SpecificUsers, StringComparison.OrdinalIgnoreCase))
        {
            return (step.ApproverUserIdsCsv ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(x => Guid.TryParse(x, out var g) ? g : Guid.Empty)
                .Contains(UserId);
        }

        return !string.IsNullOrWhiteSpace(step.RequiredPermission) && HasPermission(step.RequiredPermission);
    }

    private static void ValidateLevels(IReadOnlyList<UpsertApprovalLevelRequest> levels)
    {
        if (levels.Count == 0)
        {
            throw new BusinessRuleException("Configure at least one approval level, or deactivate the matrix.");
        }

        var ordered = levels.OrderBy(x => x.MinAmount).ToList();
        for (var i = 0; i < ordered.Count; i++)
        {
            var level = ordered[i];
            if (string.IsNullOrWhiteSpace(level.Name))
                throw new BusinessRuleException("Each approval level needs a name.");
            if (level.MinAmount < 0)
                throw new BusinessRuleException("Level minimum amount cannot be negative.");
            if (level.MaxAmount.HasValue && level.MaxAmount.Value <= level.MinAmount)
                throw new BusinessRuleException($"Level '{level.Name}' has a maximum that is not greater than its minimum.");

            var type = NormalizeApproverType(level.ApproverType);
            if (type == ApprovalApproverTypes.Permission && string.IsNullOrWhiteSpace(level.RequiredPermission))
                throw new BusinessRuleException($"Level '{level.Name}' must specify a required permission.");
            if (type == ApprovalApproverTypes.SpecificUsers && (level.ApproverUserIds is null || level.ApproverUserIds.Count == 0))
                throw new BusinessRuleException($"Level '{level.Name}' must list at least one approver.");

            // Bands must be contiguous and non-overlapping so exactly one level matches any amount.
            if (i > 0)
            {
                var prev = ordered[i - 1];
                if (!prev.MaxAmount.HasValue)
                    throw new BusinessRuleException($"Level '{prev.Name}' is unbounded but is not the highest band.");
                if (prev.MaxAmount.Value != level.MinAmount)
                    throw new BusinessRuleException($"Amount bands must be contiguous: level '{level.Name}' should start at {prev.MaxAmount.Value:N2}.");
            }
        }
    }

    private static string NormalizeApproverType(string? value) =>
        string.Equals(value, ApprovalApproverTypes.SpecificUsers, StringComparison.OrdinalIgnoreCase)
            ? ApprovalApproverTypes.SpecificUsers
            : ApprovalApproverTypes.Permission;

    private static ApprovalMatrixResponse MapMatrix(ApprovalMatrix matrix) => new(
        matrix.DocumentType,
        matrix.Currency,
        matrix.IsActive,
        IsConfigured: matrix.Levels.Count > 0,
        matrix.Levels.OrderBy(x => x.LevelOrder).Select(level => new ApprovalLevelResponse(
            level.LevelOrder,
            level.Name,
            level.MinAmount,
            level.MaxAmount,
            level.ApproverType,
            level.RequiredPermission,
            (level.ApproverUserIdsCsv ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
            level.ReminderAfterHours)).ToList());

    private static ApprovalMatrixResponse EmptyMatrix(string documentType) =>
        new(documentType, "KES", IsActive: false, IsConfigured: false, []);

    private static ApprovalRequestResponse MapRequest(ApprovalRequest request)
    {
        var currentStep = request.Steps.SingleOrDefault(s => s.LevelOrder == request.CurrentLevelOrder);
        return new ApprovalRequestResponse(
            request.Id,
            request.DocumentType,
            request.DocumentId,
            request.DocumentNumber,
            request.Amount,
            request.Currency,
            request.Status,
            request.CurrentLevelOrder,
            request.Steps.Count,
            currentStep?.LevelName ?? string.Empty,
            request.CreatedAt,
            request.Steps.OrderBy(s => s.LevelOrder).Select(s => new ApprovalStepResponse(
                s.LevelOrder, s.LevelName, s.Status, s.DecidedByName, s.DecidedAt, s.Note)).ToList());
    }
}

public sealed record UpsertApprovalMatrixRequest(string Currency, bool IsActive, IReadOnlyList<UpsertApprovalLevelRequest> Levels);
public sealed record UpsertApprovalLevelRequest(
    string Name,
    decimal MinAmount,
    decimal? MaxAmount,
    string ApproverType,
    string? RequiredPermission,
    IReadOnlyList<Guid>? ApproverUserIds,
    int? ReminderAfterHours);

public sealed record ApprovalMatrixResponse(
    string DocumentType,
    string Currency,
    bool IsActive,
    bool IsConfigured,
    IReadOnlyList<ApprovalLevelResponse> Levels);
public sealed record ApprovalLevelResponse(
    int LevelOrder,
    string Name,
    decimal MinAmount,
    decimal? MaxAmount,
    string ApproverType,
    string? RequiredPermission,
    IReadOnlyList<string> ApproverUserIds,
    int? ReminderAfterHours);

public sealed record ApprovalDecisionRequest(bool Approve, string? Note);
public sealed record ApprovalRequestResponse(
    Guid Id,
    string DocumentType,
    Guid DocumentId,
    string DocumentNumber,
    decimal Amount,
    string Currency,
    string Status,
    int CurrentLevelOrder,
    int TotalLevels,
    string CurrentLevelName,
    DateTime CreatedAt,
    IReadOnlyList<ApprovalStepResponse> Steps);
public sealed record ApprovalStepResponse(
    int LevelOrder,
    string LevelName,
    string Status,
    string? DecidedByName,
    DateTime? DecidedAt,
    string? Note);
